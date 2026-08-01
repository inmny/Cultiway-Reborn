using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Const;
using Cultiway.Core.Performance;
using Cultiway.Patch;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.CollectiveProjects;

/// <summary>
/// 管理集体工程的注册、规划、去重、成员认领、执行状态、验收和世界级历史限流。
/// </summary>
public static class CollectiveProjectService
{
    private const double AssignmentStartGrace = 1d;
    private const double FinishedRecordLifetime = TimeScales.SecPerYear;
    private const double ProjectSweepInterval = 0.25d;
    private const int ProjectSweepBudget = 32;

    private static readonly object Sync = new();
    private static readonly Dictionary<string, ICollectiveProjectOwnerAdapter> OwnerAdapters =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<string, CollectiveProjectDefinitionAsset> Definitions =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<string, ICollectiveProjectExecutor> Executors =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<string, PlannerRuntime> Planners =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<long, ProjectRecord> Projects = new();
    private static readonly Dictionary<string, long> ProjectsByDeduplicationKey =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<long, long> ActorAssignments = new();
    private static readonly List<CompletionRecord> CompletionHistory = new();
    private static readonly Queue<long> ProjectSweepQueue = new();

    private static long _nextProjectId;
    private static long _nextExecutionToken;
    private static double _nextHistoryPrune;
    private static double _nextProjectSweep;
    private static double _nextTerminalPrune;
    private static bool _initialized;

    /// <summary>绑定通用更新系统与可注册的世界清理回调。</summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        PatchMapBox.RegisterActionOnClearWorld(ClearWorldState);
        ModClass.I.GeneralLogicSystems.Add(new UpdateSystem());
    }

    /// <summary>注册一种工程发起者适配器。</summary>
    public static void RegisterOwnerAdapter(ICollectiveProjectOwnerAdapter adapter)
    {
        if (adapter == null) throw new ArgumentNullException(nameof(adapter));
        if (string.IsNullOrWhiteSpace(adapter.Id)) throw new ArgumentException("工程发起者适配器缺少 ID");
        lock (Sync)
        {
            if (OwnerAdapters.ContainsKey(adapter.Id))
                throw new InvalidOperationException($"工程发起者适配器重复注册: {adapter.Id}");
            OwnerAdapters.Add(adapter.Id, adapter);
        }
    }

    /// <summary>注册一类工程定义。</summary>
    public static void RegisterDefinition(CollectiveProjectDefinitionAsset definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (string.IsNullOrWhiteSpace(definition.id)) throw new ArgumentException("工程定义缺少 ID");
        if (string.IsNullOrWhiteSpace(definition.ExecutorId))
            throw new ArgumentException($"工程定义 {definition.id} 缺少执行器 ID");
        lock (Sync)
        {
            if (Definitions.ContainsKey(definition.id))
                throw new InvalidOperationException($"工程定义重复注册: {definition.id}");
            Definitions.Add(definition.id, definition);
        }
    }

    /// <summary>注册一种角色工程执行器。</summary>
    public static void RegisterExecutor(ICollectiveProjectExecutor executor)
    {
        if (executor == null) throw new ArgumentNullException(nameof(executor));
        if (string.IsNullOrWhiteSpace(executor.Id)) throw new ArgumentException("工程执行器缺少 ID");
        lock (Sync)
        {
            if (Executors.ContainsKey(executor.Id))
                throw new InvalidOperationException($"工程执行器重复注册: {executor.Id}");
            Executors.Add(executor.Id, executor);
        }
    }

    /// <summary>注册一个按固定节奏为特定发起者生成提案的规划器。</summary>
    public static void RegisterPlanner(ICollectiveProjectPlanner planner)
    {
        if (planner == null) throw new ArgumentNullException(nameof(planner));
        if (string.IsNullOrWhiteSpace(planner.Id)) throw new ArgumentException("工程规划器缺少 ID");
        if (string.IsNullOrWhiteSpace(planner.OwnerProviderId))
            throw new ArgumentException($"工程规划器 {planner.Id} 缺少发起者适配器 ID");
        lock (Sync)
        {
            if (Planners.ContainsKey(planner.Id))
                throw new InvalidOperationException($"工程规划器重复注册: {planner.Id}");
            Planners.Add(planner.Id, new PlannerRuntime(planner));
        }
    }

    /// <summary>解析一个工程所有者；具体类型由对应适配器负责。</summary>
    public static bool TryResolveOwner(CollectiveProjectOwnerKey key, out NanoObject owner)
    {
        ICollectiveProjectOwnerAdapter adapter;
        lock (Sync)
        {
            OwnerAdapters.TryGetValue(key.ProviderId, out adapter);
        }
        if (adapter != null && adapter.TryResolve(key.OwnerId, out owner) && IsAlive(owner)) return true;
        owner = null;
        return false;
    }

    /// <summary>判断角色是否存在当前即可执行的应急或常规工程。</summary>
    public static bool HasAssignableProject(ActorExtend actor, bool emergencyOnly)
    {
        return TrySelectCandidate(actor, emergencyOnly, out _, out _, false);
    }

    /// <summary>为自然工作选择认领最佳常规工程，并返回执行器声明的角色工作 ID。</summary>
    public static bool TryAssignRoutineJob(ActorExtend actor, out string jobId)
    {
        jobId = null;
        if (!TrySelectCandidate(actor, false, out Candidate candidate, out ICollectiveProjectExecutor executor, true))
            return false;

        CollectiveProjectView project = candidate.Project;
        if (!executor.TryPrepare(actor, in project))
        {
            ReleaseAssignment(actor);
            return false;
        }

        jobId = executor.ResolveRoutineJobId(in project);
        if (!string.IsNullOrEmpty(jobId)) return true;
        ReleaseAssignment(actor);
        return false;
    }

    /// <summary>由应急决策为角色认领并准备一个最高优先级的应急工程。</summary>
    public static bool TryAcquireEmergencyProject(ActorExtend actor)
    {
        if (!TrySelectCandidate(actor, true, out Candidate candidate, out ICollectiveProjectExecutor executor, true))
            return false;
        CollectiveProjectView project = candidate.Project;
        if (executor.TryPrepare(actor, in project)) return true;
        ReleaseAssignment(actor);
        return false;
    }

    /// <summary>重新校验并准备角色已经认领的工程目标。</summary>
    public static bool TryPrepareAssignedProject(ActorExtend actor)
    {
        if (!TryGetAssigned(actor, out CollectiveProjectView project, out ICollectiveProjectExecutor executor))
            return false;
        if (executor.TryPrepare(actor, in project)) return true;
        ReleaseAssignment(actor);
        return false;
    }

    /// <summary>要求已认领项目的执行器提交实际行动。</summary>
    public static bool TryExecuteAssignedProject(ActorExtend actor)
    {
        if (!TryGetAssigned(actor, out CollectiveProjectView project, out ICollectiveProjectExecutor executor))
            return false;
        if (executor.TryExecute(actor, in project)) return true;
        ReleaseAssignment(actor);
        return false;
    }

    /// <summary>取得角色当前认领的项目快照。</summary>
    public static bool TryGetAssignedProject(ActorExtend actor, out CollectiveProjectView project)
    {
        return TryGetAssigned(actor, out project, out _);
    }

    /// <summary>释放角色尚未提交的项目认领。</summary>
    public static void ReleaseAssignment(ActorExtend actor)
    {
        if (actor == null) return;
        ReleaseAssignment(actor.Base.getID(), actor);
    }

    /// <summary>
    /// 把已认领项目切换为执行中，并生成用于拒绝迟到完成事件的唯一令牌。
    /// </summary>
    public static bool TryBeginExecution(
        long projectId,
        long actorId,
        double timeoutSeconds,
        out long executionToken)
    {
        var releases = new List<AssignmentRelease>();
        lock (Sync)
        {
            executionToken = 0;
            if (!Projects.TryGetValue(projectId, out ProjectRecord project) ||
                project.State != CollectiveProjectState.Claimed ||
                !project.ClaimedActorIds.Contains(actorId)) return false;

            if (Definitions.TryGetValue(project.DefinitionId, out CollectiveProjectDefinitionAsset definition) &&
                Executors.TryGetValue(definition.ExecutorId, out ICollectiveProjectExecutor executor))
            {
                CollectiveProjectView view = project.ToView();
                for (int i = 0; i < project.ClaimedActorIds.Count; i++)
                {
                    long claimedActorId = project.ClaimedActorIds[i];
                    if (claimedActorId != actorId)
                        releases.Add(new AssignmentRelease(claimedActorId, view, executor));
                }
            }
            for (int i = 0; i < project.ClaimedActorIds.Count; i++)
                ActorAssignments.Remove(project.ClaimedActorIds[i]);
            project.State = CollectiveProjectState.Executing;
            project.ExecutingActorId = actorId;
            project.ExecutionToken = ++_nextExecutionToken;
            project.ExecutionExpiresAt = SimulationTime.Now + Math.Max(0.5d, timeoutSeconds);
            project.ClaimedActorIds.Clear();
            executionToken = project.ExecutionToken;
        }
        NotifyAssignmentReleases(releases);
        return true;
    }

    /// <summary>确认执行完成，并在指定延迟后依据工程定义验收世界状态。</summary>
    public static bool TryBeginVerification(
        long projectId,
        long actorId,
        long executionToken,
        double delaySeconds)
    {
        lock (Sync)
        {
            if (!Projects.TryGetValue(projectId, out ProjectRecord project) ||
                project.State != CollectiveProjectState.Executing ||
                project.ExecutingActorId != actorId ||
                project.ExecutionToken != executionToken) return false;
            project.State = CollectiveProjectState.Verifying;
            project.VerifyAt = SimulationTime.Now + Math.Max(0d, delaySeconds);
            project.ExecutionExpiresAt = 0d;
            return true;
        }
    }

    /// <summary>报告一次执行失败，并选择重新发布或终止工程。</summary>
    public static bool TryFailExecution(long projectId, long executionToken, bool retry)
    {
        ExecutionRelease release;
        lock (Sync)
        {
            if (!Projects.TryGetValue(projectId, out ProjectRecord project) ||
                project.ExecutionToken != executionToken ||
                project.State is not (CollectiveProjectState.Executing or CollectiveProjectState.Verifying))
                return false;
            release = CreateExecutionRelease(project);
            ResetAfterExecution(project, retry ? CollectiveProjectState.Planned : CollectiveProjectState.Failed);
        }
        NotifyExecutionRelease(in release);
        return true;
    }

    /// <summary>取得指定运行时项目的当前快照。</summary>
    public static bool TryGetProject(long projectId, out CollectiveProjectView project)
    {
        lock (Sync)
        {
            if (Projects.TryGetValue(projectId, out ProjectRecord record))
            {
                project = record.ToView();
                return true;
            }
        }
        project = default;
        return false;
    }

    /// <summary>驱动规划器与项目生命周期；由通用逻辑系统在主模拟线程调用。</summary>
    internal static void Update()
    {
        if (!Config.game_loaded || World.world == null) return;
        double now = SimulationTime.Now;
        RunPlanners(now);
        SweepProjects(now);
        PruneHistory(now);
    }

    /// <summary>清除当前世界项目、认领和历史，同时保留模块注册。</summary>
    public static void ClearWorldState()
    {
        ICollectiveProjectExecutor[] executors;
        lock (Sync)
        {
            Projects.Clear();
            ProjectsByDeduplicationKey.Clear();
            ActorAssignments.Clear();
            CompletionHistory.Clear();
            ProjectSweepQueue.Clear();
            _nextProjectId = 0;
            _nextExecutionToken = 0;
            _nextHistoryPrune = 0d;
            _nextProjectSweep = 0d;
            _nextTerminalPrune = 0d;
            foreach (PlannerRuntime runtime in Planners.Values) runtime.Reset();
            executors = Executors.Values.ToArray();
        }
        for (int i = 0; i < executors.Length; i++) executors[i].ClearWorldState();
    }

    /// <summary>依次给到期规划器补充发起者队列，并在单帧预算内生成提案。</summary>
    private static void RunPlanners(double now)
    {
        PlannerRuntime[] runtimes;
        lock (Sync)
        {
            runtimes = Planners.Values.ToArray();
        }

        for (int i = 0; i < runtimes.Length; i++)
        {
            PlannerRuntime runtime = runtimes[i];
            ICollectiveProjectOwnerAdapter adapter;
            lock (Sync)
            {
                OwnerAdapters.TryGetValue(runtime.Planner.OwnerProviderId, out adapter);
            }
            if (adapter == null) continue;

            runtime.FillQueueIfDue(adapter, now);
            int budget = Math.Max(1, runtime.Planner.OwnersPerUpdate);
            for (int handled = 0; handled < budget && runtime.TryDequeue(out CollectiveProjectOwnerKey key);
                 handled++)
            {
                if (!adapter.TryResolve(key.OwnerId, out NanoObject owner) || !IsAlive(owner))
                {
                    Reconcile(runtime.Planner.Id, key, Array.Empty<CollectiveProjectProposal>(), now);
                    continue;
                }

                var proposals = new List<CollectiveProjectProposal>();
                try
                {
                    var context = new CollectiveProjectOwnerContext(key, owner, adapter);
                    runtime.Planner.CollectProposals(in context, proposals);
                }
                catch (Exception exception)
                {
                    ModClass.LogError($"工程规划器 {runtime.Planner.Id} 执行失败: {exception}");
                    continue;
                }
                Reconcile(runtime.Planner.Id, key, proposals, now);
            }
        }
    }

    /// <summary>用本轮提案更新项目，并撤销规划器已不再返回的待执行项目。</summary>
    private static void Reconcile(
        string plannerId,
        CollectiveProjectOwnerKey owner,
        IReadOnlyList<CollectiveProjectProposal> proposals,
        double now)
    {
        var activeKeys = new HashSet<string>(StringComparer.Ordinal);
        var releases = new List<AssignmentRelease>();
        lock (Sync)
        {
            for (int i = 0; i < proposals.Count; i++)
            {
                CollectiveProjectProposal proposal = proposals[i];
                if (proposal == null || proposal.Owner != owner ||
                    !Definitions.TryGetValue(proposal.DefinitionId ?? string.Empty, out CollectiveProjectDefinitionAsset definition) ||
                    !Executors.ContainsKey(definition.ExecutorId)) continue;

                string deduplicationKey = BuildDeduplicationKey(proposal);
                activeKeys.Add(deduplicationKey);
                ProjectRecord existing = null;
                if (ProjectsByDeduplicationKey.TryGetValue(deduplicationKey, out long existingId) &&
                    Projects.TryGetValue(existingId, out ProjectRecord resolved) && !IsTerminal(resolved.State))
                    existing = resolved;
                if (!PassesHistoryPolicies(proposal, definition, now, existing?.Id ?? 0L)) continue;

                if (existing != null)
                {
                    if (existing.State == CollectiveProjectState.Planned)
                    {
                        existing.Payload = proposal.Payload;
                        existing.TargetTileId = proposal.TargetTileId;
                        existing.Priority = proposal.Priority == 0f
                            ? definition.DefaultPriority
                            : proposal.Priority;
                        existing.Urgency = proposal.Urgency;
                        existing.HistoryTag = ResolveHistoryTag(proposal);
                        existing.ConflictingHistoryTags =
                            proposal.ConflictingHistoryTags ?? Array.Empty<string>();
                        existing.ConflictWindowSeconds = Math.Max(0d, proposal.ConflictWindowSeconds);
                        existing.ConflictRadius = Math.Max(0f, proposal.ConflictRadius);
                    }
                    existing.LastProposedAt = now;
                    continue;
                }

                var record = new ProjectRecord
                {
                    Id = ++_nextProjectId,
                    DefinitionId = definition.id,
                    PlannerId = plannerId,
                    DeduplicationKey = deduplicationKey,
                    Owner = owner,
                    TargetTileId = proposal.TargetTileId,
                    Payload = proposal.Payload,
                    Urgency = proposal.Urgency,
                    Priority = proposal.Priority == 0f ? definition.DefaultPriority : proposal.Priority,
                    State = CollectiveProjectState.Planned,
                    CreatedAt = now,
                    LastProposedAt = now,
                    HistoryTag = ResolveHistoryTag(proposal),
                    ConflictingHistoryTags = proposal.ConflictingHistoryTags ?? Array.Empty<string>(),
                    ConflictWindowSeconds = Math.Max(0d, proposal.ConflictWindowSeconds),
                    ConflictRadius = Math.Max(0f, proposal.ConflictRadius)
                };
                Projects.Add(record.Id, record);
                ProjectsByDeduplicationKey[deduplicationKey] = record.Id;
            }

            foreach (ProjectRecord project in Projects.Values)
            {
                if (project.PlannerId != plannerId || project.Owner != owner ||
                    IsTerminal(project.State) || activeKeys.Contains(project.DeduplicationKey) ||
                    project.State is CollectiveProjectState.Executing or CollectiveProjectState.Verifying) continue;
                CancelRecord(project, CollectiveProjectState.Cancelled, now, releases);
            }
        }
        NotifyAssignmentReleases(releases);
    }

    /// <summary>挑选最佳工程；commit 为 true 时以同步方式占用执行槽位。</summary>
    private static bool TrySelectCandidate(
        ActorExtend actor,
        bool emergencyOnly,
        out Candidate selected,
        out ICollectiveProjectExecutor selectedExecutor,
        bool commit)
    {
        selected = default;
        selectedExecutor = null;
        if (actor == null || actor.Base.isRekt()) return false;
        long actorId = actor.Base.getID();
        bool preemptRoutineAssignment = false;

        lock (Sync)
        {
            if (ActorAssignments.TryGetValue(actorId, out long assignedId) &&
                Projects.TryGetValue(assignedId, out ProjectRecord assigned) &&
                Definitions.TryGetValue(assigned.DefinitionId, out CollectiveProjectDefinitionAsset assignedDefinition) &&
                Executors.TryGetValue(assignedDefinition.ExecutorId, out selectedExecutor))
            {
                bool assignedIsEmergency = assigned.Urgency == CollectiveProjectUrgency.Emergency;
                if (emergencyOnly == assignedIsEmergency)
                {
                    selected = new Candidate(assigned.ToView(), 0f);
                    return true;
                }
                if (!emergencyOnly) return false;
                preemptRoutineAssignment = true;
            }
        }

        ProjectRecord[] records;
        lock (Sync)
        {
            records = Projects.Values
                .Where(project => CanOffer(project, emergencyOnly))
                .ToArray();
        }

        bool found = false;
        Candidate best = default;
        ICollectiveProjectExecutor bestExecutor = null;
        for (int i = 0; i < records.Length; i++)
        {
            ProjectRecord record = records[i];
            if (!TryResolveCandidate(actor, record, out Candidate candidate, out ICollectiveProjectExecutor executor))
                continue;
            if (!found || IsBetter(candidate, best))
            {
                found = true;
                best = candidate;
                bestExecutor = executor;
            }
        }
        if (!found) return false;
        if (!commit)
        {
            selected = best;
            selectedExecutor = bestExecutor;
            return true;
        }

        // 应急任务只在已经确认存在可执行候选后，才释放角色原有的常规工程准备态。
        if (preemptRoutineAssignment) ReleaseAssignment(actor);

        lock (Sync)
        {
            if (ActorAssignments.ContainsKey(actorId) ||
                !Projects.TryGetValue(best.Project.ProjectId, out ProjectRecord project) ||
                !CanOffer(project, emergencyOnly)) return false;
            project.ClaimedActorIds.Add(actorId);
            project.State = CollectiveProjectState.Claimed;
            project.ClaimedAt = SimulationTime.Now;
            ActorAssignments[actorId] = project.Id;
            selected = new Candidate(project.ToView(), best.Score);
            selectedExecutor = bestExecutor;
            return true;
        }
    }

    /// <summary>校验发起者成员关系与执行器能力，并计算跨组织统一排序分数。</summary>
    private static bool TryResolveCandidate(
        ActorExtend actor,
        ProjectRecord record,
        out Candidate candidate,
        out ICollectiveProjectExecutor executor)
    {
        candidate = default;
        executor = null;
        ICollectiveProjectOwnerAdapter adapter;
        CollectiveProjectDefinitionAsset definition;
        lock (Sync)
        {
            if (!OwnerAdapters.TryGetValue(record.Owner.ProviderId, out adapter) ||
                !Definitions.TryGetValue(record.DefinitionId, out definition) ||
                !Executors.TryGetValue(definition.ExecutorId, out executor)) return false;
        }
        if (!adapter.TryResolve(record.Owner.OwnerId, out NanoObject owner) || !IsAlive(owner) ||
            !adapter.IsMember(owner, actor.Base)) return false;

        CollectiveProjectView view = record.ToView();
        if (!executor.CanExecute(actor, in view)) return false;
        float affinity = Mathf.Max(0f, adapter.ResolveMemberAffinity(owner, actor.Base));
        float distance = ResolveDistance(actor.Base, view.TargetTileId);
        float age = Mathf.Max(0f, (float)(SimulationTime.Now - view.CreatedAt));
        float score = view.Priority * 1000f + affinity * 100f +
                      executor.ScoreExecutor(actor, in view) - distance * 0.02f + age * 0.001f;
        candidate = new Candidate(view, score);
        return true;
    }

    /// <summary>按紧急度、综合分数和项目 ID 形成确定性排序。</summary>
    private static bool IsBetter(in Candidate candidate, in Candidate current)
    {
        if (candidate.Project.Urgency != current.Project.Urgency)
            return candidate.Project.Urgency > current.Project.Urgency;
        if (!Mathf.Approximately(candidate.Score, current.Score)) return candidate.Score > current.Score;
        return candidate.Project.ProjectId < current.Project.ProjectId;
    }

    /// <summary>解析角色当前认领项目及其执行器。</summary>
    private static bool TryGetAssigned(
        ActorExtend actor,
        out CollectiveProjectView project,
        out ICollectiveProjectExecutor executor)
    {
        project = default;
        executor = null;
        if (actor == null) return false;
        lock (Sync)
        {
            if (!ActorAssignments.TryGetValue(actor.Base.getID(), out long projectId) ||
                !Projects.TryGetValue(projectId, out ProjectRecord record) ||
                !Definitions.TryGetValue(record.DefinitionId, out CollectiveProjectDefinitionAsset definition) ||
                !Executors.TryGetValue(definition.ExecutorId, out executor)) return false;
            project = record.ToView();
            return true;
        }
    }

    /// <summary>释放指定角色 ID 的认领，并通知执行器清理准备态。</summary>
    private static void ReleaseAssignment(long actorId, ActorExtend knownActor = null)
    {
        CollectiveProjectView view = default;
        ICollectiveProjectExecutor executor = null;
        bool released = false;
        lock (Sync)
        {
            if (!ActorAssignments.TryGetValue(actorId, out long projectId) ||
                !Projects.TryGetValue(projectId, out ProjectRecord project)) return;
            ActorAssignments.Remove(actorId);
            project.ClaimedActorIds.Remove(actorId);
            if (project.State == CollectiveProjectState.Claimed && project.ClaimedActorIds.Count == 0)
                project.State = CollectiveProjectState.Planned;
            if (Definitions.TryGetValue(project.DefinitionId, out CollectiveProjectDefinitionAsset definition))
                Executors.TryGetValue(definition.ExecutorId, out executor);
            view = project.ToView();
            released = true;
        }
        if (!released || executor == null) return;
        ActorExtend actor = knownActor ?? ResolveActor(actorId)?.GetExtend();
        executor.OnAssignmentReleased(actorId, actor, in view);
    }

    /// <summary>周期检查所有者、目标、认领、执行超时和验收结果。</summary>
    private static void SweepProjects(double now)
    {
        ProjectRecord[] records = CollectProjectSweepBatch(now);

        for (int i = 0; i < records.Length; i++)
        {
            ProjectRecord snapshot = records[i];
            if (IsTerminal(snapshot.State)) continue;
            if (!TryResolveOwner(snapshot.Owner, out NanoObject owner))
            {
                SetTerminal(snapshot.Id, CollectiveProjectState.Expired, now);
                continue;
            }

            CollectiveProjectDefinitionAsset definition;
            ICollectiveProjectExecutor executor;
            ICollectiveProjectOwnerAdapter adapter;
            lock (Sync)
            {
                Definitions.TryGetValue(snapshot.DefinitionId, out definition);
                OwnerAdapters.TryGetValue(snapshot.Owner.ProviderId, out adapter);
                executor = definition != null && Executors.TryGetValue(definition.ExecutorId, out var value)
                    ? value
                    : null;
            }
            if (definition == null || executor == null || adapter == null)
            {
                SetTerminal(snapshot.Id, CollectiveProjectState.Expired, now);
                continue;
            }

            CollectiveProjectView view = snapshot.ToView();
            if (snapshot.State is CollectiveProjectState.Planned or CollectiveProjectState.Claimed)
            {
                bool valid = SafeEvaluate(definition.Validate, in view, true, definition.id, "校验");
                if (!valid || !PassesHistoryPolicies(snapshot, definition, now))
                {
                    SetTerminal(snapshot.Id, CollectiveProjectState.Cancelled, now);
                    continue;
                }
            }

            if (snapshot.State == CollectiveProjectState.Claimed &&
                now - snapshot.ClaimedAt >= AssignmentStartGrace)
            {
                long[] actorIds = snapshot.ClaimedActorIds.ToArray();
                for (int actorIndex = 0; actorIndex < actorIds.Length; actorIndex++)
                {
                    long actorId = actorIds[actorIndex];
                    Actor actor = ResolveActor(actorId);
                    ActorExtend actorExtend = actor?.GetExtend();
                    if (actor.isRekt() || !adapter.IsMember(owner, actor) ||
                        !executor.IsAssignmentActive(actorExtend, in view))
                    {
                        ReleaseAssignment(actorId, actorExtend);
                    }
                }
            }

            if (snapshot.State == CollectiveProjectState.Executing && now >= snapshot.ExecutionExpiresAt)
            {
                ExecutionRelease timeoutRelease = default;
                lock (Sync)
                {
                    if (Projects.TryGetValue(snapshot.Id, out ProjectRecord current) &&
                        current.State == CollectiveProjectState.Executing &&
                        current.ExecutionToken == snapshot.ExecutionToken)
                    {
                        timeoutRelease = CreateExecutionRelease(current);
                        ResetAfterExecution(current, CollectiveProjectState.Planned);
                    }
                }
                NotifyExecutionRelease(in timeoutRelease);
                continue;
            }

            if (snapshot.State != CollectiveProjectState.Verifying || now < snapshot.VerifyAt) continue;
            bool completed = SafeEvaluate(definition.Verify, in view, false, definition.id, "验收");
            if (completed)
            {
                CompleteProject(snapshot.Id, now);
                continue;
            }

            bool retry = SafeEvaluate(definition.Validate, in view, false, definition.id, "重试校验");
            ExecutionRelease verificationRelease = default;
            lock (Sync)
            {
                if (Projects.TryGetValue(snapshot.Id, out ProjectRecord current) &&
                    current.State == CollectiveProjectState.Verifying)
                {
                    verificationRelease = CreateExecutionRelease(current);
                    ResetAfterExecution(current,
                        retry ? CollectiveProjectState.Planned : CollectiveProjectState.Failed);
                }
            }
            NotifyExecutionRelease(in verificationRelease);
        }

        if (now < _nextTerminalPrune) return;
        _nextTerminalPrune = now + TimeScales.SecPerYear;
        lock (Sync)
        {
            long[] expiredRecords = Projects.Values
                .Where(project => IsTerminal(project.State) && now - project.FinishedAt > FinishedRecordLifetime)
                .Select(project => project.Id)
                .ToArray();
            for (int i = 0; i < expiredRecords.Length; i++) Projects.Remove(expiredRecords[i]);
        }
    }

    /// <summary>按稳定项目 ID 分批取得本轮生命周期扫描对象，避免发起者较多时单帧集中预检。</summary>
    private static ProjectRecord[] CollectProjectSweepBatch(double now)
    {
        lock (Sync)
        {
            if (ProjectSweepQueue.Count == 0)
            {
                if (now < _nextProjectSweep) return Array.Empty<ProjectRecord>();
                foreach (long projectId in Projects.Keys.OrderBy(id => id))
                    ProjectSweepQueue.Enqueue(projectId);
                _nextProjectSweep = now + ProjectSweepInterval;
            }

            var result = new List<ProjectRecord>(Math.Min(ProjectSweepBudget, ProjectSweepQueue.Count));
            while (result.Count < ProjectSweepBudget && ProjectSweepQueue.Count > 0)
            {
                long projectId = ProjectSweepQueue.Dequeue();
                if (Projects.TryGetValue(projectId, out ProjectRecord project)) result.Add(project);
            }
            return result.ToArray();
        }
    }

    /// <summary>完成工程、记录历史并释放去重槽位。</summary>
    private static void CompleteProject(long projectId, double now)
    {
        ExecutionRelease release;
        lock (Sync)
        {
            if (!Projects.TryGetValue(projectId, out ProjectRecord project) ||
                project.State != CollectiveProjectState.Verifying) return;
            release = CreateExecutionRelease(project);
            project.State = CollectiveProjectState.Completed;
            project.FinishedAt = now;
            ProjectsByDeduplicationKey.Remove(project.DeduplicationKey);
            CompletionHistory.Add(new CompletionRecord(
                project.Owner,
                project.DefinitionId,
                project.HistoryTag,
                project.TargetTileId,
                now,
                ResolveHistoryRetention(project)));
        }
        NotifyExecutionRelease(in release);
    }

    /// <summary>把一个项目切换为终态并清理其认领和去重记录。</summary>
    private static void SetTerminal(long projectId, CollectiveProjectState state, double now)
    {
        var releases = new List<AssignmentRelease>();
        var executionReleases = new List<ExecutionRelease>();
        lock (Sync)
        {
            if (!Projects.TryGetValue(projectId, out ProjectRecord project) || IsTerminal(project.State)) return;
            CancelRecord(project, state, now, releases, executionReleases);
        }
        NotifyAssignmentReleases(releases);
        NotifyExecutionReleases(executionReleases);
    }

    /// <summary>在锁内终止项目，不调用外部执行器。</summary>
    private static void CancelRecord(
        ProjectRecord project,
        CollectiveProjectState state,
        double now,
        ICollection<AssignmentRelease> releases,
        ICollection<ExecutionRelease> executionReleases = null)
    {
        ICollectiveProjectExecutor executor = null;
        if (Definitions.TryGetValue(project.DefinitionId, out CollectiveProjectDefinitionAsset definition))
            Executors.TryGetValue(definition.ExecutorId, out executor);
        CollectiveProjectView view = project.ToView();
        for (int i = 0; i < project.ClaimedActorIds.Count; i++)
        {
            long actorId = project.ClaimedActorIds[i];
            if (executor != null) releases?.Add(new AssignmentRelease(actorId, view, executor));
            ActorAssignments.Remove(actorId);
        }
        project.ClaimedActorIds.Clear();
        if (executor != null && project.ExecutingActorId != 0)
        {
            executionReleases?.Add(new ExecutionRelease(
                project.ExecutingActorId,
                project.ExecutionToken,
                view,
                executor));
        }
        project.State = state;
        project.FinishedAt = now;
        ProjectsByDeduplicationKey.Remove(project.DeduplicationKey);
    }

    /// <summary>退出工程服务锁后通知执行器清理角色准备态。</summary>
    private static void NotifyAssignmentReleases(IReadOnlyList<AssignmentRelease> releases)
    {
        if (releases == null) return;
        for (int i = 0; i < releases.Count; i++)
        {
            AssignmentRelease release = releases[i];
            Actor actor = ResolveActor(release.ActorId);
            CollectiveProjectView project = release.Project;
            release.Executor.OnAssignmentReleased(release.ActorId, actor?.GetExtend(), in project);
        }
    }

    /// <summary>在锁内为一次仍有效的执行令牌创建清理通知。</summary>
    private static ExecutionRelease CreateExecutionRelease(ProjectRecord project)
    {
        if (project == null || project.ExecutingActorId == 0 ||
            !Definitions.TryGetValue(project.DefinitionId, out CollectiveProjectDefinitionAsset definition) ||
            !Executors.TryGetValue(definition.ExecutorId, out ICollectiveProjectExecutor executor))
            return default;
        return new ExecutionRelease(
            project.ExecutingActorId,
            project.ExecutionToken,
            project.ToView(),
            executor);
    }

    /// <summary>退出工程服务锁后发送单次执行令牌清理通知。</summary>
    private static void NotifyExecutionRelease(in ExecutionRelease release)
    {
        if (release.Executor == null) return;
        CollectiveProjectView project = release.Project;
        release.Executor.OnExecutionReleased(
            release.ActorId,
            release.ExecutionToken,
            in project);
    }

    /// <summary>批量发送终止项目时收集的执行令牌清理通知。</summary>
    private static void NotifyExecutionReleases(IReadOnlyList<ExecutionRelease> releases)
    {
        if (releases == null) return;
        for (int i = 0; i < releases.Count; i++)
        {
            ExecutionRelease release = releases[i];
            NotifyExecutionRelease(in release);
        }
    }

    /// <summary>执行失败后清理令牌并回到指定状态。</summary>
    private static void ResetAfterExecution(ProjectRecord project, CollectiveProjectState state)
    {
        project.State = state;
        project.ExecutingActorId = 0;
        project.ExecutionToken = ++_nextExecutionToken;
        project.ExecutionExpiresAt = 0d;
        project.VerifyAt = 0d;
        if (IsTerminal(state))
        {
            project.FinishedAt = SimulationTime.Now;
            ProjectsByDeduplicationKey.Remove(project.DeduplicationKey);
        }
    }

    /// <summary>检查提案是否仍有完成额度且未触发反向工程锁。</summary>
    private static bool PassesHistoryPolicies(
        CollectiveProjectProposal proposal,
        CollectiveProjectDefinitionAsset definition,
        double now,
        long ignoredProjectId = 0L)
    {
        return PassesHistoryPolicies(
            proposal.Owner,
            proposal.TargetTileId,
            ResolveHistoryTag(proposal),
            proposal.ConflictingHistoryTags,
            proposal.ConflictWindowSeconds,
            proposal.ConflictRadius,
            definition,
            now,
            ignoredProjectId);
    }

    /// <summary>检查一个已发布项目的历史策略。</summary>
    private static bool PassesHistoryPolicies(
        ProjectRecord project,
        CollectiveProjectDefinitionAsset definition,
        double now)
    {
        return PassesHistoryPolicies(
            project.Owner,
            project.TargetTileId,
            project.HistoryTag,
            project.ConflictingHistoryTags,
            project.ConflictWindowSeconds,
            project.ConflictRadius,
            definition,
            now,
            project.Id);
    }

    /// <summary>执行额度与指定历史标签冲突的公共检查。</summary>
    private static bool PassesHistoryPolicies(
        CollectiveProjectOwnerKey owner,
        int targetTileId,
        string historyTag,
        IReadOnlyList<string> conflictingTags,
        double conflictWindow,
        float conflictRadius,
        CollectiveProjectDefinitionAsset definition,
        double now,
        long ignoredProjectId)
    {
        lock (Sync)
        {
            CollectiveProjectRatePolicy rate = definition.RatePolicy;
            if (rate != null && !string.IsNullOrEmpty(rate.BudgetGroup) &&
                rate.MaxCompletions > 0 && rate.WindowSeconds > 0d)
            {
                int completed = 0;
                for (int i = 0; i < CompletionHistory.Count; i++)
                {
                    CompletionRecord record = CompletionHistory[i];
                    if (record.Owner != owner || now - record.CompletedAt > rate.WindowSeconds ||
                        !Definitions.TryGetValue(record.DefinitionId,
                            out CollectiveProjectDefinitionAsset completedDefinition) ||
                        completedDefinition.RatePolicy?.BudgetGroup != rate.BudgetGroup) continue;
                    completed++;
                    if (completed >= rate.MaxCompletions) return false;
                }

                foreach (ProjectRecord active in Projects.Values)
                {
                    if (active.Id == ignoredProjectId || active.Owner != owner || IsTerminal(active.State) ||
                        !Definitions.TryGetValue(active.DefinitionId,
                            out CollectiveProjectDefinitionAsset activeDefinition) ||
                        activeDefinition.RatePolicy?.BudgetGroup != rate.BudgetGroup) continue;
                    completed++;
                    if (completed >= rate.MaxCompletions) return false;
                }
            }

            if (conflictingTags == null || conflictingTags.Count == 0 || conflictWindow <= 0d) return true;
            for (int i = 0; i < CompletionHistory.Count; i++)
            {
                CompletionRecord record = CompletionHistory[i];
                if (record.Owner != owner || now - record.CompletedAt > conflictWindow ||
                    !conflictingTags.Contains(record.HistoryTag) ||
                    !WithinConflictRadius(targetTileId, record.TargetTileId, conflictRadius)) continue;
                return false;
            }
            return true;
        }
    }

    /// <summary>按当前所有定义和活跃项目需要的最长窗口裁剪完成历史。</summary>
    private static void PruneHistory(double now)
    {
        if (now < _nextHistoryPrune) return;
        _nextHistoryPrune = now + TimeScales.SecPerYear;
        lock (Sync)
        {
            CompletionHistory.RemoveAll(record => now - record.CompletedAt > record.RetentionSeconds);
        }
    }

    /// <summary>冻结完成记录自身所需的最长额度或反向冲突保留期。</summary>
    private static double ResolveHistoryRetention(ProjectRecord project)
    {
        double rateWindow = Definitions.TryGetValue(
            project.DefinitionId,
            out CollectiveProjectDefinitionAsset definition)
            ? definition.RatePolicy?.WindowSeconds ?? 0d
            : 0d;
        return Math.Max(FinishedRecordLifetime, Math.Max(rateWindow, project.ConflictWindowSeconds));
    }

    /// <summary>保护定义委托异常不破坏整个模拟更新。</summary>
    private static bool SafeEvaluate(
        Func<CollectiveProjectView, bool> evaluator,
        in CollectiveProjectView project,
        bool defaultValue,
        string definitionId,
        string phase)
    {
        if (evaluator == null) return defaultValue;
        try
        {
            return evaluator(project);
        }
        catch (Exception exception)
        {
            ModClass.LogError($"工程 {definitionId} {phase}失败: {exception}");
            return false;
        }
    }

    /// <summary>检查项目当前状态与槽位是否仍可供指定调度通道认领。</summary>
    private static bool CanOffer(ProjectRecord project, bool emergencyOnly)
    {
        if (project.State is not (CollectiveProjectState.Planned or CollectiveProjectState.Claimed)) return false;
        if (emergencyOnly != (project.Urgency == CollectiveProjectUrgency.Emergency)) return false;
        if (!Definitions.TryGetValue(project.DefinitionId, out CollectiveProjectDefinitionAsset definition))
            return false;
        return project.ClaimedActorIds.Count < Math.Max(1, definition.WorkerSlots);
    }

    /// <summary>构造跨规划轮次稳定的项目去重键。</summary>
    private static string BuildDeduplicationKey(CollectiveProjectProposal proposal)
    {
        string localKey = string.IsNullOrEmpty(proposal.DeduplicationKey)
            ? proposal.TargetTileId.ToString()
            : proposal.DeduplicationKey;
        return $"{proposal.Owner}|{proposal.DefinitionId}|{localKey}";
    }

    /// <summary>缺少显式历史标签时使用工程定义 ID。</summary>
    private static string ResolveHistoryTag(CollectiveProjectProposal proposal)
    {
        return string.IsNullOrEmpty(proposal.HistoryTag) ? proposal.DefinitionId : proposal.HistoryTag;
    }

    /// <summary>判断两个历史目标是否落在同一冲突影响范围。</summary>
    private static bool WithinConflictRadius(int leftTileId, int rightTileId, float radius)
    {
        if (leftTileId < 0 || rightTileId < 0) return true;
        WorldTile left = ResolveTile(leftTileId);
        WorldTile right = ResolveTile(rightTileId);
        if (left == null || right == null) return false;
        if (radius <= 0f) return leftTileId == rightTileId;
        return Toolbox.SquaredDistVec2(left.pos, right.pos) <= radius * radius;
    }

    /// <summary>计算角色到工程落点的直线距离。</summary>
    private static float ResolveDistance(Actor actor, int targetTileId)
    {
        WorldTile target = ResolveTile(targetTileId);
        return actor == null || target == null
            ? 0f
            : Toolbox.DistVec2Float(actor.current_position, target.posV3);
    }

    /// <summary>按世界内稳定 ID 解析地块。</summary>
    private static WorldTile ResolveTile(int tileId)
    {
        WorldTile[] tiles = World.world?.tiles_list;
        return tiles != null && tileId >= 0 && tileId < tiles.Length ? tiles[tileId] : null;
    }

    /// <summary>按世界内稳定 ID 解析角色。</summary>
    private static Actor ResolveActor(long actorId)
    {
        return World.world?.units?.get(actorId);
    }

    /// <summary>统一判断 NanoObject 是否仍属于当前世界。</summary>
    private static bool IsAlive(NanoObject owner)
    {
        return owner != null && owner.exists && owner.isAlive();
    }

    /// <summary>判断项目是否已经离开活动生命周期。</summary>
    private static bool IsTerminal(CollectiveProjectState state)
    {
        return state is CollectiveProjectState.Completed or CollectiveProjectState.Failed or
            CollectiveProjectState.Cancelled or CollectiveProjectState.Expired;
    }

    /// <summary>通用逻辑系统入口。</summary>
    private sealed class UpdateSystem : BaseSystem
    {
        protected override void OnUpdateGroup()
        {
            base.OnUpdateGroup();
            Update();
        }
    }

    /// <summary>规划器运行队列，避免一个周期内同时处理全部发起者。</summary>
    private sealed class PlannerRuntime
    {
        private readonly Queue<CollectiveProjectOwnerKey> _owners = new();

        public PlannerRuntime(ICollectiveProjectPlanner planner)
        {
            Planner = planner;
        }

        public ICollectiveProjectPlanner Planner { get; }
        public double NextRunAt { get; private set; }

        public void FillQueueIfDue(ICollectiveProjectOwnerAdapter adapter, double now)
        {
            if (_owners.Count > 0 || now < NextRunAt) return;
            foreach (NanoObject owner in adapter.EnumerateOwners())
            {
                if (!IsAlive(owner)) continue;
                _owners.Enqueue(new CollectiveProjectOwnerKey(adapter.Id, owner.getID()));
            }
            NextRunAt = now + Math.Max(0.1d, Planner.IntervalSeconds);
        }

        public bool TryDequeue(out CollectiveProjectOwnerKey owner)
        {
            if (_owners.Count == 0)
            {
                owner = default;
                return false;
            }
            owner = _owners.Dequeue();
            return true;
        }

        public void Reset()
        {
            _owners.Clear();
            NextRunAt = 0d;
        }
    }

    /// <summary>生命周期服务内部维护的可变工程记录。</summary>
    private sealed class ProjectRecord
    {
        public long Id;
        public string DefinitionId;
        public string PlannerId;
        public string DeduplicationKey;
        public CollectiveProjectOwnerKey Owner;
        public int TargetTileId;
        public object Payload;
        public CollectiveProjectUrgency Urgency;
        public float Priority;
        public CollectiveProjectState State;
        public double CreatedAt;
        public double LastProposedAt;
        public double ClaimedAt;
        public double FinishedAt;
        public readonly List<long> ClaimedActorIds = new();
        public long ExecutingActorId;
        public long ExecutionToken;
        public double ExecutionExpiresAt;
        public double VerifyAt;
        public string HistoryTag;
        public string[] ConflictingHistoryTags = Array.Empty<string>();
        public double ConflictWindowSeconds;
        public float ConflictRadius;

        public CollectiveProjectView ToView()
        {
            return new CollectiveProjectView(
                Id,
                DefinitionId,
                PlannerId,
                Owner,
                TargetTileId,
                Payload,
                Urgency,
                Priority,
                State,
                ClaimedActorIds.Count > 0 ? ClaimedActorIds[0] : ExecutingActorId,
                CreatedAt,
                HistoryTag);
        }
    }

    /// <summary>一个已完成项目对后续额度与反向工程锁的影响。</summary>
    private readonly struct CompletionRecord
    {
        public CompletionRecord(
            CollectiveProjectOwnerKey owner,
            string definitionId,
            string historyTag,
            int targetTileId,
            double completedAt,
            double retentionSeconds)
        {
            Owner = owner;
            DefinitionId = definitionId;
            HistoryTag = historyTag;
            TargetTileId = targetTileId;
            CompletedAt = completedAt;
            RetentionSeconds = Math.Max(0d, retentionSeconds);
        }

        public CollectiveProjectOwnerKey Owner { get; }
        public string DefinitionId { get; }
        public string HistoryTag { get; }
        public int TargetTileId { get; }
        public double CompletedAt { get; }
        public double RetentionSeconds { get; }
    }

    /// <summary>一个已经通过成员与执行器校验的候选项目。</summary>
    private readonly struct Candidate
    {
        public Candidate(CollectiveProjectView project, float score)
        {
            Project = project;
            Score = score;
        }

        public CollectiveProjectView Project { get; }
        public float Score { get; }
    }

    /// <summary>终止项目时延迟到锁外发送的一次执行器清理通知。</summary>
    private readonly struct AssignmentRelease
    {
        public AssignmentRelease(
            long actorId,
            CollectiveProjectView project,
            ICollectiveProjectExecutor executor)
        {
            ActorId = actorId;
            Project = project;
            Executor = executor;
        }

        public long ActorId { get; }
        public CollectiveProjectView Project { get; }
        public ICollectiveProjectExecutor Executor { get; }
    }

    /// <summary>执行或验收周期结束时延迟到锁外发送的令牌清理通知。</summary>
    private readonly struct ExecutionRelease
    {
        public ExecutionRelease(
            long actorId,
            long executionToken,
            CollectiveProjectView project,
            ICollectiveProjectExecutor executor)
        {
            ActorId = actorId;
            ExecutionToken = executionToken;
            Project = project;
            Executor = executor;
        }

        public long ActorId { get; }
        public long ExecutionToken { get; }
        public CollectiveProjectView Project { get; }
        public ICollectiveProjectExecutor Executor { get; }
    }
}
