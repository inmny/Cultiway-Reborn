using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using ai.behaviours;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.ControlledTasks;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.AIGC;

internal sealed class CultibookRequestRecord
{
    public string RequestId { get; set; }
    public long OrderId { get; set; }
    public long ActorId { get; set; }
    public long WorldSessionId { get; set; }
    public CultibookRequestKind Kind { get; set; }
    public string OriginalCultibookId { get; set; }
    public long StartedTimestamp { get; set; }
    public long DeadlineTimestamp { get; set; }
    public CultibookRequestState State { get; internal set; }
    public string ErrorReasonLocaleKey { get; internal set; }
    public string GeneratorError { get; internal set; }
    public bool UsedFallback { get; internal set; }
    internal Dictionary<int, Entity> SkillHandles { get; set; }
    internal CancellationTokenSource Cancellation { get; set; }
}

internal static class CultibookRequestService
{
    private const double TimeoutSeconds = 45d;

    private static readonly Dictionary<string, CultibookRequestRecord> Requests =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<long, string> ActiveRequestByActor = new();
    private static readonly List<string> ScratchRequestIds = new();
    private static readonly List<string> ScratchCancellationIds = new();
    private static bool initialized;
    private static long worldSessionId = 1;

    internal static long WorldSessionId => worldSessionId;

    internal static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        ModClass.I.GeneralLogicSystems.Add(new UpdateSystem());
    }

    internal static ControlledTaskAvailability EvaluateCreate(Actor actor)
    {
        ControlledTaskAvailability common = EvaluateCommon(actor);
        if (!common.Enabled) return common;
        ActorExtend extend = actor.GetExtend();
        if (!extend.TryGetComponent(out Xian xian) || xian.CurrLevel < XianLevels.Yuanying)
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.RequiresYuanying");
        if (extend.GetMainCultibook() != null)
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.AlreadyHasMainCultibook");
        if (HasPendingActor(actor.getID()))
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.CultibookRequestPending");
        return ControlledTaskAvailability.Available;
    }

    internal static ControlledTaskAvailability EvaluateImprove(Actor actor)
    {
        ControlledTaskAvailability common = EvaluateCommon(actor);
        if (!common.Enabled) return common;
        ActorExtend extend = actor.GetExtend();
        if (!extend.TryGetComponent(out Xian xian) || xian.CurrLevel < XianLevels.Yuanying)
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.RequiresYuanying");
        CultibookAsset main = extend.GetMainCultibook();
        if (main == null)
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.RequiresMainCultibook");
        if (extend.GetMainCultibookMastery() < 100f)
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.RequiresFullCultibookMastery");
        if (main.Level.Stage >= 3 && main.Level.Level >= 8)
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.CultibookAtMaximum");
        if (HasPendingActor(actor.getID()))
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.CultibookRequestPending");
        return ControlledTaskAvailability.Available;
    }

    internal static bool TryStartCreate(Actor actor, long orderId,
        out CultibookRequestRecord request, out string reasonLocaleKey)
    {
        ControlledTaskAvailability availability = EvaluateCreate(actor);
        if (!availability.Enabled)
        {
            request = null;
            reasonLocaleKey = availability.ReasonLocaleKey;
            return false;
        }
        return TryStart(actor, orderId, CultibookRequestKind.Create, null, out request,
            out reasonLocaleKey);
    }

    internal static bool TryStartImprove(Actor actor, long orderId,
        out CultibookRequestRecord request, out string reasonLocaleKey)
    {
        ControlledTaskAvailability availability = EvaluateImprove(actor);
        if (!availability.Enabled)
        {
            request = null;
            reasonLocaleKey = availability.ReasonLocaleKey;
            return false;
        }
        CultibookAsset original = actor.GetExtend().GetMainCultibook();
        return TryStart(actor, orderId, CultibookRequestKind.Improve, original, out request,
            out reasonLocaleKey);
    }

    internal static bool TryGetActive(long actorId, CultibookRequestKind kind,
        out CultibookRequestRecord request)
    {
        if (ActiveRequestByActor.TryGetValue(actorId, out string requestId) &&
            Requests.TryGetValue(requestId, out request) && request.Kind == kind)
        {
            if (request.State == CultibookRequestState.Cancelled)
            {
                RemoveTerminal(requestId);
                request = null;
                return false;
            }
            return true;
        }
        request = null;
        return false;
    }

    internal static bool IsCurrentOwner(CultibookRequestRecord request)
    {
        if (request == null || request.State != CultibookRequestState.Pending) return false;
        Actor actor = World.world?.units?.get(request.ActorId);
        if (actor == null || actor.isRekt() || !actor.isAlive()) return false;
        BehaviourTaskActor expectedTask = request.Kind == CultibookRequestKind.Create
            ? ActorTasks.CreateCultibook
            : ActorTasks.ImproveCultibook;
        if (!ReferenceEquals(actor.ai?.task, expectedTask)) return false;
        if (request.OrderId <= 0) return true;
        return ControlledTaskOrderService.TryGetActiveOrderId(request.ActorId, out long orderId) &&
               orderId == request.OrderId;
    }

    internal static bool TryMatchPending(string requestId, CultibookRequestKind kind,
        long actorId, long orderId, long eventWorldSessionId, out CultibookRequestRecord request)
    {
        if (eventWorldSessionId == worldSessionId && !string.IsNullOrEmpty(requestId) &&
            Requests.TryGetValue(requestId, out request) && request.State == CultibookRequestState.Pending &&
            request.Kind == kind && request.ActorId == actorId && request.OrderId == orderId &&
            request.WorldSessionId == eventWorldSessionId && IsCurrentOwner(request))
            return true;
        request = null;
        return false;
    }

    internal static void MarkSucceeded(CultibookRequestRecord request, bool usedFallback, string generatorError)
    {
        if (request?.State != CultibookRequestState.Pending) return;
        request.UsedFallback = usedFallback;
        request.GeneratorError = generatorError ?? string.Empty;
        request.State = CultibookRequestState.Succeeded;
        request.Cancellation.Dispose();
    }

    internal static void MarkFailed(CultibookRequestRecord request, string reasonLocaleKey,
        bool usedFallback = false, string generatorError = null)
    {
        if (request?.State != CultibookRequestState.Pending) return;
        request.UsedFallback = usedFallback;
        request.GeneratorError = generatorError ?? string.Empty;
        request.ErrorReasonLocaleKey = string.IsNullOrEmpty(reasonLocaleKey)
            ? "Cultiway.ControlledTask.Reason.CultibookCommitFailed"
            : reasonLocaleKey;
        request.State = CultibookRequestState.Failed;
        request.Cancellation.Cancel();
        request.Cancellation.Dispose();
    }

    internal static void RemoveTerminal(string requestId)
    {
        if (string.IsNullOrEmpty(requestId) || !Requests.TryGetValue(requestId, out var request) ||
            request.State == CultibookRequestState.Pending) return;
        Requests.Remove(requestId);
        if (ActiveRequestByActor.TryGetValue(request.ActorId, out string activeId) && activeId == requestId)
            ActiveRequestByActor.Remove(request.ActorId);
    }

    internal static void CancelActorRequests(long actorId)
    {
        if (ActiveRequestByActor.TryGetValue(actorId, out string requestId) &&
            Requests.TryGetValue(requestId, out CultibookRequestRecord request) &&
            request.State == CultibookRequestState.Pending)
        {
            Cancel(request, CultibookRequestState.Cancelled,
                "Cultiway.ControlledTask.Reason.CultibookRequestCancelled");
            RemoveTerminal(requestId);
        }
    }

    internal static void CancelOrder(long orderId)
    {
        if (orderId <= 0) return;
        ScratchCancellationIds.Clear();
        foreach (CultibookRequestRecord request in Requests.Values)
        {
            if (request.OrderId != orderId || request.State != CultibookRequestState.Pending) continue;
            Cancel(request, CultibookRequestState.Cancelled,
                "Cultiway.ControlledTask.Reason.CultibookRequestCancelled");
            ScratchCancellationIds.Add(request.RequestId);
        }
        for (int i = 0; i < ScratchCancellationIds.Count; i++) RemoveTerminal(ScratchCancellationIds[i]);
        ScratchCancellationIds.Clear();
    }

    private static bool TryStart(Actor actor, long orderId, CultibookRequestKind kind,
        CultibookAsset original, out CultibookRequestRecord request, out string reasonLocaleKey)
    {
        request = null;
        reasonLocaleKey = string.Empty;
        if (ActiveRequestByActor.TryGetValue(actor.getID(), out string previousRequestId))
            RemoveTerminal(previousRequestId);
        long now = Stopwatch.GetTimestamp();
        var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
        request = new CultibookRequestRecord
        {
            RequestId = Guid.NewGuid().ToString("N"),
            OrderId = orderId,
            ActorId = actor.getID(),
            WorldSessionId = worldSessionId,
            Kind = kind,
            OriginalCultibookId = original?.id,
            StartedTimestamp = now,
            DeadlineTimestamp = now + (long)(TimeoutSeconds * Stopwatch.Frequency),
            State = CultibookRequestState.Pending,
            ErrorReasonLocaleKey = string.Empty,
            GeneratorError = string.Empty,
            Cancellation = cancellation,
        };
        Requests.Add(request.RequestId, request);
        ActiveRequestByActor.Add(request.ActorId, request.RequestId);

        try
        {
            CultibookPromptSnapshot snapshot = BuildSnapshot(actor.GetExtend(), original);
            request.SkillHandles = BuildSkillHandles(actor.GetExtend(), snapshot);
            if (kind == CultibookRequestKind.Create)
                CultibookGenerator.Instance.RequestGeneration(snapshot, request.RequestId,
                    request.ActorId, orderId, worldSessionId, cancellation.Token);
            else
                CultibookGenerator.Instance.RequestImprovement(snapshot, request.RequestId,
                    request.ActorId, orderId, worldSessionId, cancellation.Token);
            return true;
        }
        catch (Exception exception)
        {
            ModClass.LogError($"[CultibookRequest] startup failed actor={actor.getID()}: {exception}");
            MarkFailed(request, "Cultiway.ControlledTask.Reason.CultibookRequestStartFailed");
            reasonLocaleKey = request.ErrorReasonLocaleKey;
            return false;
        }
    }

    private static ControlledTaskAvailability EvaluateCommon(Actor actor)
    {
        if (actor == null || actor.isRekt())
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.ActorLost");
        if (!actor.hasHouse() || !actor.hasCity() || !actor.hasLanguage() || !actor.hasCulture() ||
            !actor.city.hasBookSlots())
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.RequiresWritingPlace");
        return ControlledTaskAvailability.Available;
    }

    private static bool HasPendingActor(long actorId)
    {
        return ActiveRequestByActor.TryGetValue(actorId, out string requestId) &&
               Requests.TryGetValue(requestId, out CultibookRequestRecord request) &&
               request.State == CultibookRequestState.Pending;
    }

    private static CultibookPromptSnapshot BuildSnapshot(ActorExtend extend, CultibookAsset original)
    {
        Actor actor = extend.Base;
        int level = extend.HasCultisys<Xian>() ? extend.GetCultisys<Xian>().CurrLevel : 0;
        ElementRoot root = extend.HasElementRoot() ? extend.GetElementRoot() : default;
        string elementName = extend.HasElementRoot()
            ? root.Type.GetName(Cultisyses.GetDisplayCultisys(extend)).Replace("五行", "杂")
            : "无灵根";
        string methodId = extend.GetMainCultibook()?.GetCultivateMethod()?.id ?? CultivateMethods.Standard.id;
        var snapshot = new CultibookPromptSnapshot
        {
            ActorName = actor.getName(),
            ActorLevel = level,
            ActorLevelName = Cultisyses.Xian.GetLevelName(level),
            ElementName = elementName,
            ElementDescription = $"金{root.Iron}木{root.Wood}水{root.Water}火{root.Fire}土{root.Earth}阴{root.Neg}阳{root.Pos}混沌{root.Entropy}",
            CultivateMethodId = methodId,
            CultivateMethodName = methodId.Localize(),
            AllowedCultivateMethods = string.Join(", ",
                Libraries.Manager.CultivateMethodLibrary.list.Select(method =>
                    $"\"{method.id.Localize()}\"({method.id})")),
        };

        if (extend.all_skills != null)
        {
            foreach (Entity skill in extend.all_skills.OrderBy(entity => entity.Id))
            {
                if (skill.IsNull || !skill.HasComponent<SkillContainer>()) continue;
                SkillContainer container = skill.GetComponent<SkillContainer>();
                if (string.IsNullOrEmpty(container.SkillEntityAssetID)) continue;
                snapshot.Skills.Add(new CultibookSkillPromptDto
                {
                    EntityId = skill.Id,
                    Name = skill.HasName ? skill.Name.value : container.SkillEntityAssetID.Localize(),
                });
            }
        }

        if (original != null)
        {
            var skills = new List<string>();
            foreach (SkillPoolEntry entry in original.SkillPool ?? new List<SkillPoolEntry>())
            {
                if (entry?.SkillContainer.IsNull != false ||
                    !entry.SkillContainer.HasComponent<SkillContainer>()) continue;
                string name = entry.SkillContainer.HasName
                    ? entry.SkillContainer.Name.value
                    : entry.SkillContainer.GetComponent<SkillContainer>().SkillEntityAssetID.Localize();
                skills.Add($"{name}，概率{entry.BaseChance}，熟练度阈值{entry.MasteryThreshold}，等级要求{entry.LevelRequirement}");
            }
            snapshot.Original = new CultibookOriginalPromptDto
            {
                Id = original.id,
                Name = original.Name,
                Description = original.Description,
                ElementRequirement = original.ElementReq,
                ElementAffinityThreshold = original.ElementAffinityThreshold,
                MinLevel = original.MinLevel,
                MaxLevel = original.MaxLevel,
                CultivateMethodId = original.CultivateMethodId,
                SkillPoolDescription = string.Join("；", skills),
            };
        }
        return snapshot;
    }

    private static Dictionary<int, Entity> BuildSkillHandles(ActorExtend extend,
        CultibookPromptSnapshot snapshot)
    {
        var result = new Dictionary<int, Entity>();
        if (extend?.all_skills == null || snapshot?.Skills == null) return result;
        HashSet<int> requested = new(snapshot.Skills.Select(skill => skill.EntityId));
        foreach (Entity skill in extend.all_skills)
        {
            if (skill.IsNull || !requested.Contains(skill.Id) || !skill.HasComponent<SkillContainer>()) continue;
            result[skill.Id] = skill;
        }
        return result;
    }

    private static void Cancel(CultibookRequestRecord request, CultibookRequestState state,
        string reasonLocaleKey)
    {
        if (request.State != CultibookRequestState.Pending) return;
        request.State = state;
        request.ErrorReasonLocaleKey = reasonLocaleKey;
        request.Cancellation.Cancel();
        request.Cancellation.Dispose();
    }

    private static void Tick()
    {
        long now = Stopwatch.GetTimestamp();
        ScratchRequestIds.Clear();
        foreach (var pair in Requests)
            if (pair.Value.State == CultibookRequestState.Pending) ScratchRequestIds.Add(pair.Key);

        for (int i = 0; i < ScratchRequestIds.Count; i++)
        {
            CultibookRequestRecord request = Requests[ScratchRequestIds[i]];
            if (request.WorldSessionId != worldSessionId)
            {
                Cancel(request, CultibookRequestState.Cancelled,
                    "Cultiway.ControlledTask.Reason.CultibookRequestCancelled");
                RemoveTerminal(request.RequestId);
                continue;
            }
            if (now >= request.DeadlineTimestamp)
            {
                Cancel(request, CultibookRequestState.Expired,
                    "Cultiway.ControlledTask.Reason.CultibookRequestExpired");
                continue;
            }
            Actor actor = World.world?.units?.get(request.ActorId);
            if (actor == null || actor.isRekt() || !actor.isAlive())
            {
                Cancel(request, CultibookRequestState.Cancelled,
                    "Cultiway.ControlledTask.Reason.ActorLost");
                RemoveTerminal(request.RequestId);
                continue;
            }
            BehaviourTaskActor expectedTask = request.Kind == CultibookRequestKind.Create
                ? ActorTasks.CreateCultibook
                : ActorTasks.ImproveCultibook;
            if (!ReferenceEquals(actor.ai?.task, expectedTask))
            {
                Cancel(request, CultibookRequestState.Cancelled,
                    "Cultiway.ControlledTask.Reason.CultibookRequestCancelled");
                RemoveTerminal(request.RequestId);
                continue;
            }
            if (request.OrderId > 0 &&
                (!ControlledTaskOrderService.TryGetActiveOrderId(request.ActorId, out long orderId) ||
                 orderId != request.OrderId))
            {
                Cancel(request, CultibookRequestState.Cancelled,
                    "Cultiway.ControlledTask.Reason.CultibookRequestCancelled");
                RemoveTerminal(request.RequestId);
            }
        }
    }

    private static void ClearWorldState()
    {
        foreach (CultibookRequestRecord request in Requests.Values)
        {
            if (request.State == CultibookRequestState.Pending) request.Cancellation.Cancel();
            request.Cancellation.Dispose();
        }
        Requests.Clear();
        ActiveRequestByActor.Clear();
        ScratchRequestIds.Clear();
        ScratchCancellationIds.Clear();
        worldSessionId++;
        if (worldSessionId <= 0) worldSessionId = 1;
    }

    private sealed class UpdateSystem : BaseSystem, IWorldStateClearable
    {
        protected override void OnUpdateGroup()
        {
            base.OnUpdateGroup();
            Tick();
        }

        void IWorldStateClearable.ClearWorldState()
        {
            CultibookRequestService.ClearWorldState();
        }
    }
}

internal sealed class CultibookControlledTaskConfigurator : IControlledTaskCommandConfigurator
{
    private static readonly IReadOnlyList<ControlledTaskParameterDefinition> NoParameters =
        Array.Empty<ControlledTaskParameterDefinition>();
    private readonly CultibookRequestKind kind;

    internal CultibookControlledTaskConfigurator(CultibookRequestKind requestKind)
    {
        kind = requestKind;
    }

    public IReadOnlyList<ControlledTaskParameterDefinition> Parameters => NoParameters;

    public IReadOnlyList<ControlledTaskOption> GetOptions(Actor actor, string parameterKey,
        ControlledTaskInvocation invocation)
    {
        return Array.Empty<ControlledTaskOption>();
    }

    public ControlledTaskAvailability Validate(Actor actor, ControlledTaskInvocation invocation)
    {
        return kind == CultibookRequestKind.Create
            ? CultibookRequestService.EvaluateCreate(actor)
            : CultibookRequestService.EvaluateImprove(actor);
    }

    public IControlledTaskExecutionContext Prepare(Actor actor, ControlledTaskInvocation invocation)
    {
        ControlledTaskAvailability availability = Validate(actor, invocation);
        if (!availability.Enabled) throw new InvalidOperationException(availability.ReasonLocaleKey);
        return new CultibookControlledOrderContext();
    }

    private sealed class CultibookControlledOrderContext : IControlledTaskOrderBoundContext
    {
        private long orderId;

        public void BindOrder(long boundOrderId)
        {
            orderId = boundOrderId;
        }

        public void OnOrderFinished(ControlledTaskOrderState state, string reasonLocaleKey)
        {
            CultibookRequestService.CancelOrder(orderId);
        }
    }
}
