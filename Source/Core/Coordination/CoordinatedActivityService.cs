using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Core.Pathfinding;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS.Systems;
using UnityEngine;
using ai;

namespace Cultiway.Core.Coordination;

/// <summary>
/// 管理瞬时多人协调行动的席位、邀请、阶段、位置订单和运行期索引。
/// 长期组织事实仍由各领域的 <see cref="ICoordinationGroupProvider"/> 持有。
/// </summary>
public static class CoordinatedActivityService
{
    private const int UpdateBudget = 64;
    private const int MaximumPathFailures = 6;

    private static readonly Dictionary<string, ICoordinationGroupProvider> GroupProviders =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<string, CoordinatedActivityDefinitionAsset> Definitions =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<long, ActivityRecord> Activities = new();
    private static readonly Dictionary<ActivityKey, long> ActivitiesByKey = new();
    private static readonly Dictionary<long, ParticipantAssignment> ActorAssignments = new();
    private static readonly Dictionary<long, List<Invitation>> Invitations = new();
    private static readonly Queue<long> DirtyActivities = new();
    private static readonly HashSet<long> DirtyActivityIds = new();
    private static readonly Queue<long> HeartbeatActivities = new();
    private static readonly HashSet<long> TaskSwitchSubscriptions = new();

    private static long nextActivityId;
    private static string routineJobId;
    private static string routineTaskId;
    private static bool initialized;

    /// <summary>行动完成、失败或取消后触发；回调只包含不可变结果。</summary>
    public static event Action<CoordinatedActivityResult> ActivityEnded;

    /// <summary>注册世界清理和有界主线程更新入口。</summary>
    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        ModClass.I.GeneralLogicSystems.Add(new UpdateSystem());
        ActorActivityPresentationRegistry.Register(TryResolvePresentation, 100);
    }

    /// <summary>配置自愿参与者被自然工作选择器接纳后使用的通用工作。</summary>
    public static void ConfigureRoutineJob(string jobId, string taskId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new ArgumentException("协调行动通用工作缺少 ID", nameof(jobId));
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("协调行动通用任务缺少 ID", nameof(taskId));
        routineJobId = jobId;
        routineTaskId = taskId;
    }

    /// <summary>注册一种长期群组来源。</summary>
    public static void RegisterGroupProvider(ICoordinationGroupProvider provider)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        if (string.IsNullOrWhiteSpace(provider.Id))
            throw new ArgumentException("协调群组提供者缺少 ID", nameof(provider));
        if (GroupProviders.ContainsKey(provider.Id))
            throw new InvalidOperationException($"协调群组提供者重复注册: {provider.Id}");
        GroupProviders.Add(provider.Id, provider);
    }

    /// <summary>注册一种协调行动定义。</summary>
    public static void RegisterDefinition(CoordinatedActivityDefinitionAsset definition)
    {
        ValidateDefinition(definition);
        if (Definitions.ContainsKey(definition.id))
            throw new InvalidOperationException($"协调行动定义重复注册: {definition.id}");
        Definitions.Add(definition.id, definition);
    }

    /// <summary>
    /// 为一个扁平群组启动行动；同群组同定义已经存在活动实例时不会重复创建。
    /// </summary>
    public static bool TryStart(
        CoordinatedActivityDefinitionAsset definition,
        in CoordinationGroupKey group,
        ICoordinatedActivitySession session,
        IReadOnlyList<CoordinationInitialParticipant> initialParticipants,
        out long activityId)
    {
        activityId = 0;
        ValidateDefinition(definition);
        if (session == null) throw new ArgumentNullException(nameof(session));
        if (!Definitions.TryGetValue(definition.id, out CoordinatedActivityDefinitionAsset registered) ||
            !ReferenceEquals(registered, definition))
            throw new InvalidOperationException($"协调行动定义尚未注册: {definition.id}");
        if (!GroupProviders.TryGetValue(group.ProviderId, out ICoordinationGroupProvider provider) ||
            !provider.IsValid(group))
            return false;

        var activityKey = new ActivityKey(definition.id, group);
        if (ActivitiesByKey.TryGetValue(activityKey, out long existingId) &&
            Activities.ContainsKey(existingId))
        {
            activityId = existingId;
            return false;
        }
        if (initialParticipants != null &&
            !CanAssignInitialParticipants(definition, group, provider, initialParticipants))
            return false;

        double now = CurrentTime;
        var record = new ActivityRecord(
            ++nextActivityId,
            definition,
            group,
            provider,
            session,
            now);
        Activities.Add(record.Id, record);
        ActivitiesByKey.Add(activityKey, record.Id);
        HeartbeatActivities.Enqueue(record.Id);

        if (initialParticipants != null)
        {
            for (var i = 0; i < initialParticipants.Count; i++)
            {
                CoordinationInitialParticipant participant = initialParticipants[i];
                if (!TryAssignParticipant(
                        record,
                        participant.Actor,
                        participant.RoleId,
                        CoordinationParticipationMode.Forced))
                {
                    EndActivity(record, CoordinatedActivityEndReason.SourceInvalid);
                    return false;
                }
            }
        }

        activityId = record.Id;
        InvokeStageChanged(record);
        MarkDirty(record.Id);
        return true;
    }

    /// <summary>判断指定群组与定义是否已经存在活动实例。</summary>
    public static bool TryGetActivityId(
        string definitionId,
        in CoordinationGroupKey group,
        out long activityId)
    {
        return ActivitiesByKey.TryGetValue(new ActivityKey(definitionId, group), out activityId) &&
               Activities.ContainsKey(activityId);
    }

    /// <summary>显式取消一个活动实例。</summary>
    public static bool Cancel(long activityId)
    {
        if (!Activities.TryGetValue(activityId, out ActivityRecord record)) return false;
        EndActivity(record, CoordinatedActivityEndReason.Cancelled);
        return true;
    }

    /// <summary>取消指定群组与定义的活动实例。</summary>
    public static bool Cancel(string definitionId, in CoordinationGroupKey group)
    {
        return TryGetActivityId(definitionId, group, out long activityId) && Cancel(activityId);
    }

    /// <summary>让已经承担职责的角色在自然换工时继续执行当前协调行动。</summary>
    public static bool TryContinueAssignedJob(Actor actor, ref string jobId)
    {
        if (actor.isRekt() || string.IsNullOrEmpty(routineJobId) || string.IsNullOrEmpty(routineTaskId))
            return false;
        long actorId = actor.getID();
        if (ActorAssignments.TryGetValue(actorId, out ParticipantAssignment assignment))
        {
            if (Activities.TryGetValue(assignment.ActivityId, out ActivityRecord assignedActivity) &&
                assignedActivity.Participants.TryGetValue(actorId, out ParticipantRecord participant))
            {
                if (participant.Role.ParticipationMode == CoordinationParticipationMode.Voluntary &&
                    !CanAcceptVoluntaryParticipation(actor))
                {
                    if (participant.Role.ParticipantLifetime ==
                        CoordinationParticipantLifetime.ExecutionBound)
                        RemoveParticipant(assignedActivity, actorId);
                    else
                        DeactivateParticipant(assignedActivity, participant, actor);
                    return false;
                }
                if (participant.Role.ParticipantLifetime ==
                    CoordinationParticipantLifetime.ExecutionBound &&
                    participant.ExecutionActivated &&
                    !IsExecutingParticipant(participant, actor))
                {
                    RemoveParticipant(assignedActivity, actorId);
                    return false;
                }
                if (RoleAllowsTask(participant.Role, routineTaskId))
                {
                    jobId = routineJobId;
                    return true;
                }
                return false;
            }
            ActorAssignments.Remove(actorId);
        }
        return false;
    }

    /// <summary>在角色自然选择工作时接纳最高优先级的自愿邀请。</summary>
    public static bool TryAcceptVoluntaryJob(Actor actor, ref string jobId)
    {
        if (actor.isRekt() || string.IsNullOrEmpty(routineJobId) ||
            string.IsNullOrEmpty(routineTaskId) || !CanAcceptVoluntaryParticipation(actor))
            return false;
        long actorId = actor.getID();
        if (ActorAssignments.ContainsKey(actorId)) return false;
        if (!Invitations.TryGetValue(actorId, out List<Invitation> invitations) ||
            invitations.Count == 0)
            return false;

        Invitation selected = default;
        ActivityRecord selectedRecord = null;
        for (var i = invitations.Count - 1; i >= 0; i--)
        {
            Invitation invitation = invitations[i];
            if (!Activities.TryGetValue(invitation.ActivityId, out ActivityRecord candidate) ||
                !CanAcceptInvitation(candidate, actor, invitation.RoleId))
            {
                invitations.RemoveAt(i);
                if (candidate != null &&
                    !HasInvitationForActivity(invitations, candidate.Id))
                    candidate.InvitedActorIds.Remove(actorId);
                continue;
            }

            if (selectedRecord != null &&
                (candidate.Definition.Priority < selectedRecord.Definition.Priority ||
                 candidate.Definition.Priority == selectedRecord.Definition.Priority &&
                 invitation.Score <= selected.Score))
                continue;
            selected = invitation;
            selectedRecord = candidate;
        }

        if (invitations.Count == 0) Invitations.Remove(actorId);
        if (selectedRecord == null ||
            !TryAssignParticipant(
                selectedRecord,
                actor,
                selected.RoleId,
                CoordinationParticipationMode.Voluntary))
            return false;

        RemoveInvitationsForActor(actorId);
        jobId = routineJobId;
        return true;
    }

    /// <summary>
    /// 推进角色当前协调行动，包括稳定位置订单、到场状态和领域成员逻辑。
    /// </summary>
    public static CoordinationParticipantResult TickParticipant(Actor actor)
    {
        if (actor.isRekt() ||
            !ActorAssignments.TryGetValue(actor.getID(), out ParticipantAssignment assignment) ||
            !Activities.TryGetValue(assignment.ActivityId, out ActivityRecord record) ||
            !record.Participants.TryGetValue(actor.getID(), out ParticipantRecord participant))
            return CoordinationParticipantResult.Leave;

        if (!record.Provider.Contains(record.Group, actor) ||
            !record.Session.IsParticipantValid(
                CreateView(record),
                CreateParticipantView(participant),
                actor))
        {
            RemoveParticipant(record, actor.getID());
            return CoordinationParticipantResult.Leave;
        }

        if (participant.AwaitingTaskSwitch || !IsExecutingParticipant(participant, actor))
        {
            bool release = participant.Role.ParticipantLifetime ==
                           CoordinationParticipantLifetime.ExecutionBound &&
                           participant.ExecutionActivated;
            DeactivateParticipant(record, participant, actor);
            if (release) RemoveParticipant(record, actor.getID());
            return CoordinationParticipantResult.Leave;
        }
        participant.ExecutionActivated = true;

        bool placementReady = TickPlacement(actor, participant);
        bool ready = placementReady && participant.DomainReady;
        SetParticipantReady(record, participant, ready);

        CoordinatedActivityView view = CreateView(record);
        CoordinationParticipantView participantView = CreateParticipantView(participant);
        var context = new CoordinationParticipantContext(
            view,
            participantView,
            actor,
            placementReady,
            CurrentTime);
        CoordinationParticipantResult result = record.Session.TickParticipant(context);
        switch (result)
        {
            case CoordinationParticipantResult.Leave:
                RemoveParticipant(record, actor.getID());
                break;
            case CoordinationParticipantResult.FailActivity:
                EndActivity(record, CoordinatedActivityEndReason.SessionFailed);
                break;
        }
        return result;
    }

    /// <summary>角色当前工作结束时，按席位生命周期释放关系或暂停长期职责。</summary>
    public static void NotifyJobEnded(Actor actor)
    {
        if (actor == null ||
            !ActorAssignments.TryGetValue(actor.getID(), out ParticipantAssignment assignment) ||
            !Activities.TryGetValue(assignment.ActivityId, out ActivityRecord record) ||
            !record.Participants.TryGetValue(actor.getID(), out ParticipantRecord participant))
            return;
        if (participant.Role.ParticipantLifetime == CoordinationParticipantLifetime.ExecutionBound &&
            participant.ExecutionActivated)
            RemoveParticipant(record, actor.getID());
        else
            DeactivateParticipant(record, participant, actor);
    }

    /// <summary>返回角色协调行动的任务栏文本键与开始时间。</summary>
    public static bool TryGetPresentation(
        Actor actor,
        out string localeKey,
        out double startedAt)
    {
        localeKey = null;
        startedAt = 0d;
        if (actor == null ||
            !ActorAssignments.TryGetValue(actor.getID(), out ParticipantAssignment assignment) ||
            !Activities.TryGetValue(assignment.ActivityId, out ActivityRecord record) ||
            !record.Participants.TryGetValue(actor.getID(), out ParticipantRecord participant))
            return false;

        CoordinatedActivityView view = CreateView(record);
        localeKey = record.Session.ResolvePresentationLocaleKey(
            view,
            CreateParticipantView(participant));
        startedAt = record.CreatedAt;
        return !string.IsNullOrEmpty(localeKey);
    }

    /// <summary>把协调上下文适配到统一角色活动展示注册表。</summary>
    private static bool TryResolvePresentation(
        Actor actor,
        out ActorActivityPresentationSegment segment)
    {
        if (!TryGetPresentation(actor, out string localeKey, out double startedAt))
        {
            segment = default;
            return false;
        }
        segment = new ActorActivityPresentationSegment(localeKey, null, startedAt);
        return true;
    }

    /// <summary>复制当前全部活动的不可变诊断快照。</summary>
    public static IReadOnlyList<CoordinatedActivityDebugSnapshot> GetDebugSnapshots()
    {
        double now = CurrentTime;
        var snapshots = new CoordinatedActivityDebugSnapshot[Activities.Count];
        var index = 0;
        foreach (ActivityRecord record in Activities.Values.OrderBy(item => item.Id))
        {
            var ready = 0;
            var blocked = 0;
            var maximumPathFailures = 0;
            foreach (ParticipantRecord participant in record.Participants.Values)
            {
                if (participant.Ready) ready++;
                if (participant.PathFailures >= MaximumPathFailures) blocked++;
                maximumPathFailures = Math.Max(maximumPathFailures, participant.PathFailures);
            }
            snapshots[index++] = new CoordinatedActivityDebugSnapshot(
                record.Id,
                record.Definition.id,
                record.Group,
                record.Stage,
                record.Participants.Count,
                ready,
                record.InvitedActorIds.Count,
                blocked,
                maximumPathFailures,
                Math.Max(0d, now - record.StageStartedAt));
        }
        return snapshots;
    }

    /// <summary>主线程有界推进脏行动与到期心跳。</summary>
    private static void Update()
    {
        var processed = 0;
        int dirtyCount = DirtyActivities.Count;
        while (processed < UpdateBudget && dirtyCount-- > 0)
        {
            long activityId = DirtyActivities.Dequeue();
            DirtyActivityIds.Remove(activityId);
            if (Activities.TryGetValue(activityId, out ActivityRecord record))
                Process(record, force: true);
            processed++;
        }

        int heartbeatCount = HeartbeatActivities.Count;
        while (processed < UpdateBudget && heartbeatCount-- > 0)
        {
            long activityId = HeartbeatActivities.Dequeue();
            if (!Activities.TryGetValue(activityId, out ActivityRecord record)) continue;
            HeartbeatActivities.Enqueue(activityId);
            Process(record, force: false);
            processed++;
        }
    }

    /// <summary>验证来源、席位和阶段条件后推进一次行动心跳。</summary>
    private static void Process(ActivityRecord record, bool force)
    {
        double now = CurrentTime;
        if (!force && now < record.NextUpdateAt) return;
        record.NextUpdateAt = now + Math.Max(0.05f, record.Definition.HeartbeatSeconds);

        if (!record.Provider.IsValid(record.Group))
        {
            EndActivity(record, CoordinatedActivityEndReason.SourceInvalid);
            return;
        }

        ValidateParticipants(record);
        if (!Activities.ContainsKey(record.Id)) return;

        if (record.Stage is CoordinatedActivityStage.Recruiting or
            CoordinatedActivityStage.Assembling)
        {
            if (record.HasEnteredRunning)
                ReconcileLateJoinCandidates(record);
            else
                ReconcileCandidates(record);
        }
        else if (record.Stage == CoordinatedActivityStage.Running)
        {
            ReconcileLateJoinCandidates(record);
        }

        if (!ValidateRunningRequirements(record)) return;
        if (record.Stage == CoordinatedActivityStage.Running &&
            record.Definition.RunningTimeoutSeconds > 0f &&
            ResolveRunningDuration(record, now) >= record.Definition.RunningTimeoutSeconds)
        {
            EndActivity(record, CoordinatedActivityEndReason.RunningTimedOut);
            return;
        }

        var controller = new ActivityController(record);
        CoordinationSessionResult sessionResult = record.Session.Update(
            new CoordinationUpdateContext(controller, now));
        if (!Activities.ContainsKey(record.Id)) return;
        if (!ValidateRunningRequirements(record)) return;
        if (sessionResult == CoordinationSessionResult.Complete)
        {
            EndActivity(record, CoordinatedActivityEndReason.Completed);
            return;
        }
        if (sessionResult == CoordinationSessionResult.Fail)
        {
            EndActivity(record, CoordinatedActivityEndReason.SessionFailed);
            return;
        }

        switch (record.Stage)
        {
            case CoordinatedActivityStage.Recruiting:
                if (HasMinimumAssignments(record))
                {
                    ChangeStage(record, CoordinatedActivityStage.Assembling);
                }
                else if (now - record.StageStartedAt >= record.Definition.RecruitmentTimeoutSeconds)
                {
                    EndActivity(record, CoordinatedActivityEndReason.RecruitmentTimedOut);
                }
                break;
            case CoordinatedActivityStage.Assembling:
                if (HasReadyParticipants(record))
                {
                    ChangeStage(record, CoordinatedActivityStage.Running);
                }
                else if (now - record.StageStartedAt >= record.Definition.AssemblyTimeoutSeconds)
                {
                    RemoveAbsentParticipants(record);
                    if (HasMinimumAssignments(record) && HasReadyParticipants(record))
                        ChangeStage(record, CoordinatedActivityStage.Running);
                    else
                        EndActivity(record, CoordinatedActivityEndReason.AssemblyTimedOut);
                }
                break;
        }
    }

    /// <summary>在会话更新前后统一验证执行阶段的必要席位和到场要求。</summary>
    private static bool ValidateRunningRequirements(ActivityRecord record)
    {
        if (record.Stage != CoordinatedActivityStage.Running) return true;
        if (!HasMinimumAssignments(record))
        {
            EndActivity(record, CoordinatedActivityEndReason.RequiredParticipantLost);
            return false;
        }
        if (HasReadyParticipants(record)) return true;
        switch (record.Definition.RunningReadinessPolicy)
        {
            case CoordinationRunningReadinessPolicy.Reassemble:
                ChangeStage(record, CoordinatedActivityStage.Assembling);
                return false;
            case CoordinationRunningReadinessPolicy.Fail:
                EndActivity(record, CoordinatedActivityEndReason.RequiredReadinessLost);
                return false;
            default:
                return true;
        }
    }

    /// <summary>移除已经死亡、离开群组或不再符合会话条件的参与者。</summary>
    private static void ValidateParticipants(ActivityRecord record)
    {
        CoordinatedActivityView view = CreateView(record);
        using var invalid = new ListPool<long>();
        foreach (ParticipantRecord participant in record.Participants.Values)
        {
            Actor actor = ResolveActor(participant.ActorId);
            if (actor.isRekt() ||
                !record.Provider.Contains(record.Group, actor) ||
                !record.Session.IsParticipantValid(view, CreateParticipantView(participant), actor))
            {
                invalid.Add(participant.ActorId);
                continue;
            }

            if (participant.AwaitingTaskSwitch || !IsExecutingParticipant(participant, actor))
            {
                bool release = participant.Role.ParticipantLifetime ==
                               CoordinationParticipantLifetime.ExecutionBound &&
                               participant.ExecutionActivated;
                DeactivateParticipant(record, participant, actor);
                if (release) invalid.Add(participant.ActorId);
                continue;
            }

            participant.ExecutionActivated = true;
            SetParticipantReady(
                record,
                participant,
                IsPlacementReady(actor, participant) && participant.DomainReady);
        }
        for (var i = 0; i < invalid.Count; i++) RemoveParticipant(record, invalid[i]);
    }

    /// <summary>按全部非满席位收集并处理候选。</summary>
    private static void ReconcileCandidates(ActivityRecord record)
    {
        for (var i = 0; i < record.Definition.Roles.Length; i++)
        {
            CoordinationRoleDefinition role = record.Definition.Roles[i];
            if (role == null) continue;
            if (IsRoleFull(record, role))
            {
                RemoveInvitationsForRole(record, role.Id);
                continue;
            }
            ReconcileRoleCandidates(record, role);
        }
    }

    /// <summary>执行阶段只为显式允许迟到加入的席位补充成员。</summary>
    private static void ReconcileLateJoinCandidates(ActivityRecord record)
    {
        for (var i = 0; i < record.Definition.Roles.Length; i++)
        {
            CoordinationRoleDefinition role = record.Definition.Roles[i];
            if (role == null || !role.AllowLateJoin) continue;
            if (IsRoleFull(record, role))
            {
                RemoveInvitationsForRole(record, role.Id);
                continue;
            }
            ReconcileRoleCandidates(record, role);
        }
    }

    /// <summary>根据席位参加策略创建邀请或直接分配候选。</summary>
    private static void ReconcileRoleCandidates(ActivityRecord record, CoordinationRoleDefinition role)
    {
        using var candidates = new ListPool<CoordinationCandidate>();
        record.Session.CollectCandidates(CreateView(record), role, candidates);
        for (var i = candidates.Count - 1; i >= 0; i--)
        {
            Actor actor = candidates[i].Actor;
            if (actor.isRekt() ||
                record.Participants.ContainsKey(actor.getID()) ||
                !record.Provider.Contains(record.Group, actor) ||
                role.ParticipationMode != CoordinationParticipationMode.Forced &&
                ActorAssignments.ContainsKey(actor.getID()))
                candidates.RemoveAt(i);
        }
        if (role.ParticipationMode == CoordinationParticipationMode.Voluntary)
        {
            using var candidateIds = new ListPool<long>();
            for (var i = 0; i < candidates.Count; i++) candidateIds.Add(candidates[i].Actor.getID());
            RemoveStaleInvitationsForRole(record, role.Id, candidateIds);
        }
        candidates.Sort((left, right) =>
        {
            int score = right.Score.CompareTo(left.Score);
            if (score != 0) return score;
            return left.Actor.getID().CompareTo(right.Actor.getID());
        });

        for (var i = 0; i < candidates.Count && !IsRoleFull(record, role); i++)
        {
            CoordinationCandidate candidate = candidates[i];
            Actor actor = candidate.Actor;
            switch (role.ParticipationMode)
            {
                case CoordinationParticipationMode.Voluntary:
                    AddInvitation(record, actor.getID(), role.Id, candidate.Score);
                    break;
                case CoordinationParticipationMode.Duty:
                    if (!ActorAssignments.ContainsKey(actor.getID()))
                        TryAssignParticipant(record, actor, role.Id, role.ParticipationMode);
                    break;
                case CoordinationParticipationMode.Forced:
                    TryAssignParticipant(record, actor, role.Id, role.ParticipationMode);
                    break;
            }
        }
    }

    /// <summary>在修改任一旧活动前，原子验证全部初始成员、席位容量与抢占资格。</summary>
    private static bool CanAssignInitialParticipants(
        CoordinatedActivityDefinitionAsset definition,
        in CoordinationGroupKey group,
        ICoordinationGroupProvider provider,
        IReadOnlyList<CoordinationInitialParticipant> participants)
    {
        var actorIds = new HashSet<long>();
        var roleCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < participants.Count; i++)
        {
            CoordinationInitialParticipant participant = participants[i];
            Actor actor = participant.Actor;
            CoordinationRoleDefinition role = definition.GetRole(participant.RoleId);
            if (actor.isRekt() || role == null || !provider.Contains(group, actor) ||
                !actorIds.Add(actor.getID()))
                return false;

            roleCounts.TryGetValue(role.Id, out int count);
            count++;
            if (role.MaximumCount > 0 && count > role.MaximumCount) return false;
            roleCounts[role.Id] = count;

            if (!ActorAssignments.TryGetValue(actor.getID(), out ParticipantAssignment assignment) ||
                !Activities.TryGetValue(assignment.ActivityId, out ActivityRecord existing))
                continue;
            if (!existing.Definition.Preemptible || definition.Priority <= existing.Definition.Priority)
                return false;
        }
        return true;
    }

    /// <summary>把角色分配到一个席位，并执行单前台行动冲突规则。</summary>
    private static bool TryAssignParticipant(
        ActivityRecord record,
        Actor actor,
        string roleId,
        CoordinationParticipationMode mode)
    {
        if (actor.isRekt() || !record.Provider.Contains(record.Group, actor)) return false;
        CoordinationRoleDefinition role = record.Definition.GetRole(roleId);
        if (role == null || IsRoleFull(record, role)) return false;
        long actorId = actor.getID();
        EnsureTaskSwitchSubscription(actor);
        bool preempted = false;

        if (ActorAssignments.TryGetValue(actorId, out ParticipantAssignment current))
        {
            if (current.ActivityId == record.Id)
            {
                return record.Participants.TryGetValue(actorId, out ParticipantRecord existingRole) &&
                       existingRole.Role.Id == roleId;
            }
            if (!Activities.TryGetValue(current.ActivityId, out ActivityRecord existing))
            {
                ActorAssignments.Remove(actorId);
            }
            else
            {
                bool canPreempt = mode == CoordinationParticipationMode.Forced &&
                                  existing.Definition.Preemptible &&
                                  record.Definition.Priority > existing.Definition.Priority;
                if (!canPreempt) return false;
                RemoveParticipant(existing, actorId);
                preempted = true;
                if (existing.Stage == CoordinatedActivityStage.Running &&
                    HasUnrecoverableRequiredRoleLoss(existing))
                    EndActivity(existing, CoordinatedActivityEndReason.Preempted);
            }
        }

        var participant = new ParticipantRecord(actorId, role);
        participant.AwaitingTaskSwitch = preempted &&
                                         role.ParticipantLifetime ==
                                         CoordinationParticipantLifetime.ExecutionBound &&
                                         IsExecutingParticipant(participant, actor);
        participant.ExecutionActivated = !participant.AwaitingTaskSwitch &&
                                         IsExecutingParticipant(participant, actor);
        record.Participants.Add(actorId, participant);
        ActorAssignments[actorId] = new ParticipantAssignment(record.Id);
        RemoveInvitationsForActor(actorId);
        MarkDirty(record.Id);
        return true;
    }

    /// <summary>判断一条自愿邀请在被选择时是否仍然有效。</summary>
    private static bool CanAcceptInvitation(ActivityRecord record, Actor actor, string roleId)
    {
        if (!CanAcceptVoluntaryParticipation(actor)) return false;
        if (record.Stage != CoordinatedActivityStage.Recruiting &&
            record.Stage != CoordinatedActivityStage.Assembling &&
            record.Stage != CoordinatedActivityStage.Running)
            return false;
        CoordinationRoleDefinition role = record.Definition.GetRole(roleId);
        if (role == null || role.ParticipationMode != CoordinationParticipationMode.Voluntary)
            return false;
        if (!RoleAllowsTask(role, routineTaskId)) return false;
        if (record.HasEnteredRunning && !role.AllowLateJoin) return false;
        if (IsRoleFull(record, role) || !record.Provider.Contains(record.Group, actor)) return false;
        return record.Session.IsParticipantValid(
            CreateView(record),
            new CoordinationParticipantView(
                actor.getID(),
                roleId,
                false,
                role.ParticipantLifetime,
                0,
                0),
            actor);
    }

    /// <summary>自愿行动只接纳当前没有战斗职责或即时战斗状态的角色。</summary>
    private static bool CanAcceptVoluntaryParticipation(Actor actor)
    {
        if (actor.isRekt() || actor.has_attack_target || actor.ai.task?.in_combat == true)
            return false;
        return actor.city == null ||
               !actor.city.hasAttackZoneOrder() && !actor.city.isInDanger();
    }

    /// <summary>判断一个席位是否允许由指定 AI 任务实际执行。</summary>
    private static bool RoleAllowsTask(CoordinationRoleDefinition role, string taskId)
    {
        if (role?.ExecutionTaskIds == null || string.IsNullOrEmpty(taskId)) return false;
        for (var i = 0; i < role.ExecutionTaskIds.Length; i++)
        {
            if (string.Equals(role.ExecutionTaskIds[i], taskId, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary>判断角色当前任务是否正在履行其协调席位。</summary>
    private static bool IsExecutingParticipant(ParticipantRecord participant, Actor actor)
    {
        return !actor.isRekt() && RoleAllowsTask(participant.Role, actor.ai.task?.id);
    }

    /// <summary>为角色安装一次任务切换监听，使执行席位在切离声明任务时同步失效。</summary>
    private static void EnsureTaskSwitchSubscription(Actor actor)
    {
        long actorId = actor.getID();
        if (!TaskSwitchSubscriptions.Add(actorId)) return;
        actor.ai.subscribeToTaskSwitch(() => HandleTaskSwitch(actor));
    }

    /// <summary>在原版 AI 完成任务切换后更新席位激活状态并撤销旧位置订单。</summary>
    private static void HandleTaskSwitch(Actor actor)
    {
        if (actor.isRekt() ||
            !ActorAssignments.TryGetValue(actor.getID(), out ParticipantAssignment assignment) ||
            !Activities.TryGetValue(assignment.ActivityId, out ActivityRecord record) ||
            !record.Participants.TryGetValue(actor.getID(), out ParticipantRecord participant))
            return;

        participant.AwaitingTaskSwitch = false;
        if (IsExecutingParticipant(participant, actor))
        {
            participant.ExecutionActivated = true;
            return;
        }

        bool release = participant.Role.ParticipantLifetime ==
                       CoordinationParticipantLifetime.ExecutionBound &&
                       participant.ExecutionActivated;
        DeactivateParticipant(record, participant, actor);
        if (release) RemoveParticipant(record, actor.getID());
    }

    /// <summary>更新参与者到场状态，并在发生变化时立即推进活动。</summary>
    private static void SetParticipantReady(
        ActivityRecord record,
        ParticipantRecord participant,
        bool ready)
    {
        if (participant.Ready == ready) return;
        participant.Ready = ready;
        MarkDirty(record.Id);
    }

    /// <summary>暂停一个长期席位或准备释放短期席位，统一撤销到场状态和旧位置订单。</summary>
    private static void DeactivateParticipant(
        ActivityRecord record,
        ParticipantRecord participant,
        Actor actor)
    {
        SetParticipantReady(record, participant, false);
        ReleaseParticipantMovement(actor, participant);
    }

    /// <summary>添加或更新一个角色对同一行动席位的邀请。</summary>
    private static void AddInvitation(
        ActivityRecord record,
        long actorId,
        string roleId,
        float score)
    {
        if (!Invitations.TryGetValue(actorId, out List<Invitation> list))
        {
            list = new List<Invitation>();
            Invitations.Add(actorId, list);
        }
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].ActivityId != record.Id || list[i].RoleId != roleId) continue;
            list[i] = new Invitation(record.Id, roleId, score);
            record.InvitedActorIds.Add(actorId);
            return;
        }
        list.Add(new Invitation(record.Id, roleId, score));
        record.InvitedActorIds.Add(actorId);
    }

    /// <summary>移除角色收到的全部协调邀请。</summary>
    private static void RemoveInvitationsForActor(long actorId)
    {
        if (Invitations.TryGetValue(actorId, out List<Invitation> invitations))
        {
            for (var i = 0; i < invitations.Count; i++)
            {
                if (Activities.TryGetValue(invitations[i].ActivityId, out ActivityRecord activity))
                    activity.InvitedActorIds.Remove(actorId);
            }
        }
        Invitations.Remove(actorId);
    }

    /// <summary>判断角色的邀请列表中是否仍存在指定行动的其他席位。</summary>
    private static bool HasInvitationForActivity(IReadOnlyList<Invitation> invitations, long activityId)
    {
        for (var i = 0; i < invitations.Count; i++)
        {
            if (invitations[i].ActivityId == activityId) return true;
        }
        return false;
    }

    /// <summary>移除一个行动席位已经不再对应当前候选集的邀请。</summary>
    private static void RemoveStaleInvitationsForRole(
        ActivityRecord record,
        string roleId,
        IReadOnlyList<long> candidateIds)
    {
        using var invitedActorIds = new ListPool<long>(record.InvitedActorIds);
        for (var i = 0; i < invitedActorIds.Count; i++)
        {
            long actorId = invitedActorIds[i];
            if (candidateIds.Contains(actorId)) continue;
            RemoveInvitationForRole(record, actorId, roleId);
        }
    }

    /// <summary>移除一个行动席位对指定角色的邀请，并维护双向局部索引。</summary>
    private static void RemoveInvitationForRole(
        ActivityRecord record,
        long actorId,
        string roleId)
    {
        if (!Invitations.TryGetValue(actorId, out List<Invitation> invitations))
        {
            record.InvitedActorIds.Remove(actorId);
            return;
        }
        invitations.RemoveAll(item => item.ActivityId == record.Id && item.RoleId == roleId);
        if (!HasInvitationForActivity(invitations, record.Id))
            record.InvitedActorIds.Remove(actorId);
        if (invitations.Count == 0) Invitations.Remove(actorId);
    }

    /// <summary>在席位已经满员时移除该席位仍未接受的全部邀请。</summary>
    private static void RemoveInvitationsForRole(ActivityRecord record, string roleId)
    {
        using var invitedActorIds = new ListPool<long>(record.InvitedActorIds);
        for (var i = 0; i < invitedActorIds.Count; i++)
            RemoveInvitationForRole(record, invitedActorIds[i], roleId);
    }

    /// <summary>移除某行动产生的全部尚未接受邀请。</summary>
    private static void RemoveInvitationsForActivity(ActivityRecord record)
    {
        foreach (long actorId in record.InvitedActorIds)
        {
            if (!Invitations.TryGetValue(actorId, out List<Invitation> invitations)) continue;
            invitations.RemoveAll(item => item.ActivityId == record.Id);
            if (invitations.Count == 0) Invitations.Remove(actorId);
        }
        record.InvitedActorIds.Clear();
    }

    /// <summary>释放参与者的前台索引和位置订单。</summary>
    private static bool RemoveParticipant(ActivityRecord record, long actorId)
    {
        if (!record.Participants.TryGetValue(actorId, out ParticipantRecord participant)) return false;
        ReleaseParticipantMovement(ResolveActor(actorId), participant);
        record.Participants.Remove(actorId);
        if (ActorAssignments.TryGetValue(actorId, out ParticipantAssignment assignment) &&
            assignment.ActivityId == record.Id)
            ActorAssignments.Remove(actorId);
        MarkDirty(record.Id);
        return true;
    }

    /// <summary>在集合截止后释放所有没有实际到场的成员。</summary>
    private static void RemoveAbsentParticipants(ActivityRecord record)
    {
        using var absent = new ListPool<long>();
        foreach (ParticipantRecord participant in record.Participants.Values)
        {
            if (!participant.Ready) absent.Add(participant.ActorId);
        }
        for (var i = 0; i < absent.Count; i++) RemoveParticipant(record, absent[i]);
    }

    /// <summary>解析并稳定执行单个参与者的位置订单。</summary>
    private static bool TickPlacement(Actor actor, ParticipantRecord participant)
    {
        CoordinationPlacementOrder order = participant.Order;
        if (order.AnchorKind == CoordinationAnchorKind.None)
        {
            ReleaseParticipantMovement(actor, participant);
            return true;
        }
        bool inCombat = actor.ai.task?.in_combat == true || actor.has_attack_target;
        if (order.SuspendWhileInCombat && inCombat)
        {
            ReleaseParticipantMovement(actor, participant);
            return false;
        }

        WorldTile target = ResolveOrderTile(order);
        if (target == null)
        {
            ReleaseParticipantMovement(actor, participant);
            return false;
        }
        float radius = Mathf.Max(0.5f, order.ArrivalRadius);
        bool arrived = Toolbox.SquaredDistVec2Float(actor.current_position, target.posV3) <=
                       radius * radius;
        if (arrived)
        {
            if (order.HoldPosition && participant.LastIssuedOrderRevision == participant.OrderRevision)
                ReleaseParticipantMovement(actor, participant);
            else if (!actor.is_moving && !actor.isUsingPath())
                ClearParticipantMovementOwnership(participant);
            participant.LastResolvedTileId = target.tile_id;
            participant.PathFailures = 0;
            participant.NextPathRetryAt = 0d;
            return true;
        }

        if (actor.is_inside_building) actor.exitBuilding();
        bool orderChanged = participant.LastIssuedOrderRevision != participant.OrderRevision;
        bool targetMoved = participant.LastResolvedTileId < 0 ||
                           ResolveTile(participant.LastResolvedTileId) is not { } previous ||
                           Toolbox.SquaredDistVec2Float(previous.posV3, target.posV3) >=
                           order.RepathDistance * order.RepathDistance;
        if (orderChanged || targetMoved)
        {
            participant.PathFailures = 0;
            participant.NextPathRetryAt = 0d;
        }
        if (participant.PathFailures >= MaximumPathFailures ||
            CurrentTime < participant.NextPathRetryAt)
            return false;

        bool shouldIssue = orderChanged ||
                           targetMoved ||
                           !actor.is_moving && !actor.isUsingPath();
        if (!shouldIssue) return false;

        if ((orderChanged || targetMoved) && participant.OwnedPathSubmissionToken > 0)
            ReleaseParticipantMovement(actor, participant);
        ExecuteEvent result = actor.goTo(target);
        participant.LastIssuedOrderRevision = participant.OrderRevision;
        participant.LastResolvedTileId = target.tile_id;
        participant.OwnedMovementTargetTileId = target.tile_id;
        participant.OwnedMovementOrderRevision = participant.OrderRevision;
        participant.OwnedPathSubmissionToken = result != ExecuteEvent.False &&
                                               PathFinder.Instance.TryGetCurrentSubmissionToken(
                                                   actor,
                                                   out long submissionToken)
            ? submissionToken
            : 0;
        if (result == ExecuteEvent.False)
        {
            participant.PathFailures = Math.Min(MaximumPathFailures, participant.PathFailures + 1);
            participant.NextPathRetryAt = CurrentTime +
                                          Math.Min(4d, 0.25d * (1 << (participant.PathFailures - 1)));
            ReleaseParticipantMovement(actor, participant);
        }
        else
        {
            participant.PathFailures = 0;
            participant.NextPathRetryAt = CurrentTime + 0.25d;
        }
        return false;
    }

    /// <summary>只撤销仍由当前参与者位置订单持有的路径。</summary>
    private static void ReleaseParticipantMovement(Actor actor, ParticipantRecord participant)
    {
        if (!actor.isRekt() &&
            participant.OwnedMovementTargetTileId >= 0 &&
            participant.OwnedMovementOrderRevision == participant.LastIssuedOrderRevision &&
            actor.tile_target?.tile_id == participant.OwnedMovementTargetTileId &&
            PathFinder.Instance.CancelOwned(actor, participant.OwnedPathSubmissionToken))
        {
            actor.stopMovement();
        }
        ClearParticipantMovementOwnership(participant);
    }

    /// <summary>清除位置订单的路径所有权标记，不影响其他系统已经接管的移动。</summary>
    private static void ClearParticipantMovementOwnership(ParticipantRecord participant)
    {
        participant.OwnedMovementTargetTileId = -1;
        participant.OwnedMovementOrderRevision = -1;
        participant.OwnedPathSubmissionToken = 0;
    }

    /// <summary>无副作用地验证角色当前是否满足位置订单。</summary>
    private static bool IsPlacementReady(Actor actor, ParticipantRecord participant)
    {
        CoordinationPlacementOrder order = participant.Order;
        if (order.AnchorKind == CoordinationAnchorKind.None) return true;
        if (order.SuspendWhileInCombat &&
            (actor.ai.task?.in_combat == true || actor.has_attack_target))
            return false;
        WorldTile target = ResolveOrderTile(order);
        if (target == null) return false;
        float radius = Mathf.Max(0.5f, order.ArrivalRadius);
        return Toolbox.SquaredDistVec2Float(actor.current_position, target.posV3) <= radius * radius;
    }

    /// <summary>把固定或动态锚点解析为当前目标地块。</summary>
    private static WorldTile ResolveOrderTile(in CoordinationPlacementOrder order)
    {
        WorldTile anchor = order.AnchorKind switch
        {
            CoordinationAnchorKind.Tile => ResolveTile(order.TileId),
            CoordinationAnchorKind.Actor => ResolveActor(order.ActorId)?.current_tile,
            _ => null
        };
        if (anchor == null) return null;
        WorldTile target = World.world.GetTile(anchor.x + order.Offset.x, anchor.y + order.Offset.y);
        return target != null && target.isSameIsland(anchor) ? target : anchor;
    }

    /// <summary>判断所有席位是否已经达到最小分配数量。</summary>
    private static bool HasMinimumAssignments(ActivityRecord record)
    {
        for (var i = 0; i < record.Definition.Roles.Length; i++)
        {
            CoordinationRoleDefinition role = record.Definition.Roles[i];
            if (role != null && CountRole(record, role.Id, readyOnly: false) < role.MinimumCount)
                return false;
        }
        return true;
    }

    /// <summary>判断执行中的必要席位已经不足且该席位不允许迟到补充。</summary>
    private static bool HasUnrecoverableRequiredRoleLoss(ActivityRecord record)
    {
        for (var i = 0; i < record.Definition.Roles.Length; i++)
        {
            CoordinationRoleDefinition role = record.Definition.Roles[i];
            if (role != null &&
                !role.AllowLateJoin &&
                CountRole(record, role.Id, readyOnly: false) < role.MinimumCount)
                return true;
        }
        return false;
    }

    /// <summary>判断席位要求、总人数与总比例是否都已满足。</summary>
    private static bool HasReadyParticipants(ActivityRecord record)
    {
        var ready = 0;
        foreach (ParticipantRecord participant in record.Participants.Values)
        {
            if (participant.Ready) ready++;
        }
        if (ready < record.Definition.MinimumReadyCount) return false;
        float requiredRatio = Mathf.Clamp01(record.Definition.MinimumReadyRatio);
        if (record.Participants.Count > 0 && ready / (float)record.Participants.Count < requiredRatio)
            return false;
        for (var i = 0; i < record.Definition.Roles.Length; i++)
        {
            CoordinationRoleDefinition role = record.Definition.Roles[i];
            if (role != null && CountRole(record, role.Id, readyOnly: true) < role.MinimumReadyCount)
                return false;
        }
        return true;
    }

    /// <summary>统计指定席位的已分配或已到场人数。</summary>
    private static int CountRole(ActivityRecord record, string roleId, bool readyOnly)
    {
        var count = 0;
        foreach (ParticipantRecord participant in record.Participants.Values)
        {
            if (participant.Role.Id == roleId && (!readyOnly || participant.Ready)) count++;
        }
        return count;
    }

    /// <summary>判断席位是否已经达到最大人数。</summary>
    private static bool IsRoleFull(ActivityRecord record, CoordinationRoleDefinition role)
    {
        return role.MaximumCount > 0 && CountRole(record, role.Id, readyOnly: false) >= role.MaximumCount;
    }

    /// <summary>切换阶段并立即通知领域会话更新订单。</summary>
    private static void ChangeStage(ActivityRecord record, CoordinatedActivityStage stage)
    {
        if (record.Stage == stage) return;
        double now = CurrentTime;
        AccumulateRunningDuration(record, now);
        record.Stage = stage;
        record.StageStartedAt = now;
        if (stage == CoordinatedActivityStage.Running)
        {
            record.HasEnteredRunning = true;
            for (var i = 0; i < record.Definition.Roles.Length; i++)
            {
                CoordinationRoleDefinition role = record.Definition.Roles[i];
                if (role != null && !role.AllowLateJoin)
                    RemoveInvitationsForRole(record, role.Id);
            }
        }
        InvokeStageChanged(record);
        MarkDirty(record.Id);
    }

    /// <summary>返回活动跨多次重新集合累计消耗的执行时长。</summary>
    private static double ResolveRunningDuration(ActivityRecord record, double now)
    {
        return record.AccumulatedRunningDuration +
               (record.Stage == CoordinatedActivityStage.Running
                   ? Math.Max(0d, now - record.StageStartedAt)
                   : 0d);
    }

    /// <summary>离开执行阶段时保存本段时长，避免重新集合重置总执行超时。</summary>
    private static void AccumulateRunningDuration(ActivityRecord record, double now)
    {
        if (record.Stage != CoordinatedActivityStage.Running) return;
        record.AccumulatedRunningDuration += Math.Max(0d, now - record.StageStartedAt);
    }

    /// <summary>通知领域会话进入一个可执行阶段。</summary>
    private static void InvokeStageChanged(ActivityRecord record)
    {
        record.Session.OnStageChanged(
            new CoordinationUpdateContext(new ActivityController(record), CurrentTime));
    }

    /// <summary>进入释放阶段，清理全部运行时索引并发布最终结果。</summary>
    private static void EndActivity(ActivityRecord record, CoordinatedActivityEndReason reason)
    {
        if (!Activities.ContainsKey(record.Id)) return;
        AccumulateRunningDuration(record, CurrentTime);
        record.Stage = CoordinatedActivityStage.Releasing;
        record.StageStartedAt = CurrentTime;

        using var actorIds = new ListPool<long>(record.Participants.Keys);
        for (var i = 0; i < actorIds.Count; i++) RemoveParticipant(record, actorIds[i]);
        RemoveInvitationsForActivity(record);
        Activities.Remove(record.Id);
        ActivitiesByKey.Remove(new ActivityKey(record.Definition.id, record.Group));
        DirtyActivityIds.Remove(record.Id);

        record.Stage = reason switch
        {
            CoordinatedActivityEndReason.Completed => CoordinatedActivityStage.Completed,
            CoordinatedActivityEndReason.Cancelled or CoordinatedActivityEndReason.Preempted =>
                CoordinatedActivityStage.Cancelled,
            _ => CoordinatedActivityStage.Failed
        };
        var result = new CoordinatedActivityResult(
            record.Id,
            record.Definition.id,
            record.Group,
            reason,
            CurrentTime);
        record.Session.OnEnded(result);
        ActivityEnded?.Invoke(result);
    }

    /// <summary>把活动加入高优先级脏队列，并保证同一活动只排队一次。</summary>
    private static void MarkDirty(long activityId)
    {
        if (!Activities.ContainsKey(activityId) || !DirtyActivityIds.Add(activityId)) return;
        DirtyActivities.Enqueue(activityId);
    }

    /// <summary>为领域会话构造不暴露可变记录的行动视图。</summary>
    private static CoordinatedActivityView CreateView(ActivityRecord record)
    {
        var participants = new CoordinationParticipantView[record.Participants.Count];
        var index = 0;
        foreach (ParticipantRecord participant in record.Participants.Values)
            participants[index++] = CreateParticipantView(participant);
        return new CoordinatedActivityView(
            record.Id,
            record.Definition,
            record.Group,
            record.Stage,
            record.CreatedAt,
            record.StageStartedAt,
            participants);
    }

    /// <summary>把可变参与者记录冻结为视图。</summary>
    private static CoordinationParticipantView CreateParticipantView(ParticipantRecord participant)
    {
        return new CoordinationParticipantView(
            participant.ActorId,
            participant.Role.Id,
            participant.Ready,
            participant.Role.ParticipantLifetime,
            participant.OrderRevision,
            participant.PathFailures);
    }

    /// <summary>验证定义 ID、席位唯一性和人数范围。</summary>
    private static void ValidateDefinition(CoordinatedActivityDefinitionAsset definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (string.IsNullOrWhiteSpace(definition.id))
            throw new ArgumentException("协调行动定义缺少 ID", nameof(definition));
        if (definition.Roles == null || definition.Roles.Length == 0)
            throw new ArgumentException($"协调行动 {definition.id} 没有席位定义", nameof(definition));
        var roleIds = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < definition.Roles.Length; i++)
        {
            CoordinationRoleDefinition role = definition.Roles[i];
            if (role == null || string.IsNullOrWhiteSpace(role.Id))
                throw new ArgumentException($"协调行动 {definition.id} 存在无标识席位", nameof(definition));
            if (!roleIds.Add(role.Id))
                throw new ArgumentException($"协调行动 {definition.id} 存在重复席位 {role.Id}", nameof(definition));
            if (role.MinimumCount < 0 || role.MinimumReadyCount < 0 ||
                role.MaximumCount > 0 && role.MinimumCount > role.MaximumCount ||
                role.MaximumCount > 0 && role.MinimumReadyCount > role.MaximumCount)
                throw new ArgumentException($"协调行动 {definition.id} 的席位 {role.Id} 人数范围无效", nameof(definition));
            if (role.ExecutionTaskIds == null || role.ExecutionTaskIds.Length == 0)
                throw new ArgumentException($"协调行动 {definition.id} 的席位 {role.Id} 没有执行任务", nameof(definition));
            var taskIds = new HashSet<string>(StringComparer.Ordinal);
            for (var taskIndex = 0; taskIndex < role.ExecutionTaskIds.Length; taskIndex++)
            {
                string taskId = role.ExecutionTaskIds[taskIndex];
                if (string.IsNullOrWhiteSpace(taskId) || !taskIds.Add(taskId))
                    throw new ArgumentException(
                        $"协调行动 {definition.id} 的席位 {role.Id} 存在无效执行任务",
                        nameof(definition));
            }
        }
    }

    /// <summary>按世界稳定 ID 解析角色。</summary>
    private static Actor ResolveActor(long actorId)
    {
        return World.world?.units?.get(actorId);
    }

    /// <summary>按世界稳定 ID 解析地块。</summary>
    private static WorldTile ResolveTile(int tileId)
    {
        WorldTile[] tiles = World.world?.tiles_list;
        return tiles != null && tileId >= 0 && tileId < tiles.Length ? tiles[tileId] : null;
    }

    /// <summary>清除当前世界的全部瞬时活动，不触发依赖旧世界对象的领域回调。</summary>
    private static void ClearWorldState()
    {
        Activities.Clear();
        ActivitiesByKey.Clear();
        ActorAssignments.Clear();
        Invitations.Clear();
        DirtyActivities.Clear();
        DirtyActivityIds.Clear();
        HeartbeatActivities.Clear();
        TaskSwitchSubscriptions.Clear();
        nextActivityId = 0;
    }

    /// <summary>读取当前世界模拟时间。</summary>
    private static double CurrentTime => World.world?.getCurWorldTime() ?? 0d;

    /// <summary>协调行动的内部可变记录。</summary>
    private sealed class ActivityRecord
    {
        /// <summary>创建一个处于招募阶段的记录。</summary>
        internal ActivityRecord(
            long id,
            CoordinatedActivityDefinitionAsset definition,
            CoordinationGroupKey group,
            ICoordinationGroupProvider provider,
            ICoordinatedActivitySession session,
            double now)
        {
            Id = id;
            Definition = definition;
            Group = group;
            Provider = provider;
            Session = session;
            Stage = CoordinatedActivityStage.Recruiting;
            CreatedAt = now;
            StageStartedAt = now;
        }

        internal long Id { get; }
        internal CoordinatedActivityDefinitionAsset Definition { get; }
        internal CoordinationGroupKey Group { get; }
        internal ICoordinationGroupProvider Provider { get; }
        internal ICoordinatedActivitySession Session { get; }
        internal Dictionary<long, ParticipantRecord> Participants { get; } = new();
        internal HashSet<long> InvitedActorIds { get; } = new();
        internal CoordinatedActivityStage Stage { get; set; }
        internal double CreatedAt { get; }
        internal double StageStartedAt { get; set; }
        internal double NextUpdateAt { get; set; }
        internal double AccumulatedRunningDuration { get; set; }
        internal bool HasEnteredRunning { get; set; }
    }

    /// <summary>参与者的内部可变记录。</summary>
    private sealed class ParticipantRecord
    {
        /// <summary>创建一个尚未获得位置订单的参与者。</summary>
        internal ParticipantRecord(long actorId, CoordinationRoleDefinition role)
        {
            ActorId = actorId;
            Role = role;
            DomainReady = true;
            Order = CoordinationPlacementOrder.None;
            LastResolvedTileId = -1;
            LastIssuedOrderRevision = -1;
            OwnedMovementTargetTileId = -1;
            OwnedMovementOrderRevision = -1;
        }

        internal long ActorId { get; }
        internal CoordinationRoleDefinition Role { get; }
        internal bool Ready { get; set; }
        internal bool DomainReady { get; set; }
        internal bool ExecutionActivated { get; set; }
        internal bool AwaitingTaskSwitch { get; set; }
        internal CoordinationPlacementOrder Order { get; set; }
        internal int OrderRevision { get; set; }
        internal int LastIssuedOrderRevision { get; set; }
        internal int LastResolvedTileId { get; set; }
        internal int PathFailures { get; set; }
        internal double NextPathRetryAt { get; set; }
        internal int OwnedMovementTargetTileId { get; set; }
        internal int OwnedMovementOrderRevision { get; set; }
        internal long OwnedPathSubmissionToken { get; set; }
    }

    /// <summary>角色到活动的单前台索引。</summary>
    private readonly struct ParticipantAssignment
    {
        /// <summary>创建参与索引。</summary>
        internal ParticipantAssignment(long activityId)
        {
            ActivityId = activityId;
        }

        internal long ActivityId { get; }
    }

    /// <summary>尚未被角色接受的自愿邀请。</summary>
    private readonly struct Invitation
    {
        /// <summary>创建席位邀请。</summary>
        internal Invitation(long activityId, string roleId, float score)
        {
            ActivityId = activityId;
            RoleId = roleId;
            Score = score;
        }

        internal long ActivityId { get; }
        internal string RoleId { get; }
        internal float Score { get; }
    }

    /// <summary>同群组同定义的活动去重键。</summary>
    private readonly struct ActivityKey : IEquatable<ActivityKey>
    {
        /// <summary>创建活动去重键。</summary>
        internal ActivityKey(string definitionId, CoordinationGroupKey group)
        {
            DefinitionId = definitionId ?? string.Empty;
            Group = group;
        }

        private string DefinitionId { get; }
        private CoordinationGroupKey Group { get; }

        /// <inheritdoc />
        public bool Equals(ActivityKey other)
        {
            return DefinitionId == other.DefinitionId && Group.Equals(other.Group);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is ActivityKey other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(DefinitionId, Group);
        }
    }

    /// <summary>只在单次会话回调期间操作所属记录的控制器。</summary>
    private sealed class ActivityController : ICoordinatedActivityController
    {
        private readonly ActivityRecord record;

        /// <summary>绑定一个活动记录。</summary>
        internal ActivityController(ActivityRecord record)
        {
            this.record = record;
        }

        /// <inheritdoc />
        public CoordinatedActivityView View => CreateView(record);

        /// <inheritdoc />
        public bool MeetsReadinessRequirements => HasReadyParticipants(record);

        /// <inheritdoc />
        public bool SetPlacement(long actorId, in CoordinationPlacementOrder order)
        {
            if (!record.Participants.TryGetValue(actorId, out ParticipantRecord participant)) return false;
            if (participant.Order.Equals(order)) return true;
            ReleaseParticipantMovement(ResolveActor(actorId), participant);
            participant.Order = order;
            unchecked
            {
                participant.OrderRevision++;
            }
            SetParticipantReady(record, participant, false);
            participant.PathFailures = 0;
            participant.NextPathRetryAt = 0d;
            MarkDirty(record.Id);
            return true;
        }

        /// <inheritdoc />
        public bool SetDomainReady(long actorId, bool ready)
        {
            if (!record.Participants.TryGetValue(actorId, out ParticipantRecord participant)) return false;
            if (participant.DomainReady == ready) return true;
            participant.DomainReady = ready;
            SetParticipantReady(record, participant, false);
            MarkDirty(record.Id);
            return true;
        }

        /// <inheritdoc />
        public bool RemoveParticipant(long actorId)
        {
            return CoordinatedActivityService.RemoveParticipant(record, actorId);
        }
    }

    /// <summary>通用协调服务的系统更新入口。</summary>
    private sealed class UpdateSystem : BaseSystem, IWorldStateClearable
    {
        void IWorldStateClearable.ClearWorldState()
        {
            CoordinatedActivityService.ClearWorldState();
        }

        /// <inheritdoc />
        protected override void OnUpdateGroup()
        {
            base.OnUpdateGroup();
            Update();
        }
    }
}
