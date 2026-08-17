using System;
using System.Collections.Generic;
using ai.behaviours;
using Cultiway.Abstract;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.ControlledTasks;

public static class ControlledTaskOrderService
{
    private const float SuccessfulRetentionSeconds = 3f;
    private const float OtherRetentionSeconds = 6f;

    private static readonly Dictionary<long, ControlledTaskOrder> Orders = new();
    private static readonly Dictionary<long, long> ActiveOrderByActor = new();
    private static readonly Dictionary<long, ActorTaskRuntime> RuntimeByActor = new();
    private static readonly Dictionary<AiSystemActor, long> ActorByAiSystem = new();
    private static readonly List<long> ScratchIds = new();
    private static readonly List<long> ScratchActorIds = new();

    private static bool initialized;
    private static bool ticking;
    private static long nextOrderId = 1;

    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        ModClass.I.GeneralLogicSystems.Add(new UpdateSystem());
    }

    public static ControlledTaskStartResult TryBegin(long actorId, string commandId, ControlledTaskTarget target)
    {
        return TryBegin(actorId, commandId, new ControlledTaskInvocation(target, null));
    }

    public static ControlledTaskStartResult TryBegin(
        long actorId,
        string commandId,
        ControlledTaskInvocation invocation)
    {
        if (!ModClass.L.ControlledTaskCommandLibrary.TryGet(commandId,
                out ControlledTaskCommandAsset command))
            return ControlledTaskStartResult.Rejected("Cultiway.ControlledTask.Reason.CommandMissing");

        Actor actor = ResolveActor(actorId);
        ControlledTaskAvailability actorAvailability = ValidateControlledActor(actor);
        if (!actorAvailability.Enabled) return ControlledTaskStartResult.Rejected(actorAvailability.ReasonLocaleKey);
        if (invocation.Target.Mode != command.TargetMode)
            return ControlledTaskStartResult.Rejected("Cultiway.ControlledTask.Reason.TargetModeChanged");

        BehaviourTaskActor taskAsset = command.Task;
        if (taskAsset == null || !ReferenceEquals(AssetManager.tasks_actor.get(taskAsset.id), taskAsset))
            return ControlledTaskStartResult.Rejected("Cultiway.ControlledTask.Reason.TaskMissing");

        WorldTile tile = invocation.Target.ResolveTile();
        if (command.TargetMode == ControlledTaskTargetMode.None) tile = null;
        if (command.TargetMode == ControlledTaskTargetMode.WorldTile && tile == null)
            return ControlledTaskStartResult.Rejected("Cultiway.ControlledTask.Reason.TargetMissing");

        try
        {
            ControlledTaskAvailability availability = command.Evaluate(actor);
            if (!availability.Enabled) return ControlledTaskStartResult.Rejected(availability.ReasonLocaleKey);

            ControlledTaskAvailability invocationAvailability = command.ValidateInvocation(actor, invocation);
            if (!invocationAvailability.Enabled)
                return ControlledTaskStartResult.Rejected(invocationAvailability.ReasonLocaleKey);
        }
        catch (Exception exception)
        {
            ModClass.LogError(
                $"[ControlledTaskOrder] validation failed command={command.id} actor={actorId}: {exception}");
            return ControlledTaskStartResult.Rejected("Cultiway.ControlledTask.Reason.InternalError");
        }

        ActorTaskRuntime runtime = EnsureRuntime(actor);

        long orderId = nextOrderId++;
        IControlledTaskExecutionContext context;
        try
        {
            context = command.PrepareInvocation(actor, invocation);
        }
        catch (Exception exception)
        {
            ModClass.LogError(
                $"[ControlledTaskOrder] preparation failed command={command.id} actor={actorId}: {exception}");
            return ControlledTaskStartResult.Rejected("Cultiway.ControlledTask.Reason.InternalError");
        }

        if (ActiveOrderByActor.TryGetValue(actorId, out long priorOrderId))
            Finish(priorOrderId, ControlledTaskOrderState.Interrupted,
                "Cultiway.ControlledTask.Reason.ExternalInterrupt");

        var order = new ControlledTaskOrder(
            orderId,
            actorId,
            actor.getName(),
            command,
            taskAsset,
            runtime.Revision,
            Time.realtimeSinceStartup);
        Orders.Add(orderId, order);
        if (context != null) ControlledTaskExecutionContextStore.Put(orderId, context);

        try
        {
            using (ControlledTaskHandoffScope.Enter(actor)) ControllableUnit.clear(false);
            actor.finishPossessionStatus();
            actor.setTask(taskAsset.id, true, false, false);

            if (!ReferenceEquals(actor.ai.task, taskAsset))
                throw new InvalidOperationException($"Actor task '{taskAsset.id}' did not start.");

            command.ApplyTargetContext(actor, tile);
            if (!ReferenceEquals(actor.ai.task, taskAsset))
                throw new InvalidOperationException($"Actor task '{taskAsset.id}' was replaced during startup.");
        }
        catch (Exception exception)
        {
            ModClass.LogError(
                $"[ControlledTaskOrder] startup failed command={command.id} actor={actorId}: {exception}");
            if (actor != null && !actor.isRekt()) actor.cancelAllBeh();
            Finish(orderId, ControlledTaskOrderState.Failed, "Cultiway.ControlledTask.Reason.StartFailed");
            return ControlledTaskStartResult.Rejected("Cultiway.ControlledTask.Reason.StartFailed");
        }

        order.TaskRevision = runtime.Revision;
        ActiveOrderByActor[actorId] = orderId;
        return ControlledTaskStartResult.Started(orderId);
    }

    public static bool TryCancel(long orderId)
    {
        TickOrders();
        if (!Orders.TryGetValue(orderId, out ControlledTaskOrder order) ||
            order.State != ControlledTaskOrderState.Running)
            return false;

        Actor actor = ResolveActor(order.ActorId);
        if (actor == null || actor.isRekt())
        {
            Finish(orderId, ControlledTaskOrderState.ActorLost, "Cultiway.ControlledTask.Reason.ActorLost");
            return false;
        }

        if (!OwnsCurrentTask(order, actor))
        {
            Finish(orderId, ControlledTaskOrderState.Interrupted,
                "Cultiway.ControlledTask.Reason.ExternalInterrupt");
            return false;
        }

        using (ControlledTaskCancellationScope.Enter(order.ActorId)) actor.cancelAllBeh();
        if (order.State == ControlledTaskOrderState.Running)
            Finish(orderId, ControlledTaskOrderState.Cancelled,
                "Cultiway.ControlledTask.Reason.Cancelled");
        return true;
    }

    public static bool TryGetActiveOrderId(long actorId, out long orderId)
    {
        TickOrders();
        return ActiveOrderByActor.TryGetValue(actorId, out orderId);
    }

    public static bool TryGetExecutionContext<T>(long actorId, out T context)
        where T : class, IControlledTaskExecutionContext
    {
        if (TryGetActiveOrderId(actorId, out long orderId))
            return ControlledTaskExecutionContextStore.TryGet(orderId, out context);
        context = null;
        return false;
    }

    public static bool TryTakeExecutionContext<T>(long actorId, out T context)
        where T : class, IControlledTaskExecutionContext
    {
        if (!TryGetActiveOrderId(actorId, out long orderId) ||
            !ControlledTaskExecutionContextStore.Remove(orderId, out T typed))
        {
            context = null;
            return false;
        }

        context = typed;
        return true;
    }

    public static bool MarkExecutionCommitted(Actor actor, bool keepOrderRunning = false)
    {
        if (actor == null || !TryGetActiveOrderId(actor.getID(), out long orderId) ||
            !Orders.TryGetValue(orderId, out ControlledTaskOrder order) ||
            order.State != ControlledTaskOrderState.Running || !OwnsCurrentTask(order, actor))
            return false;

        order.ExecutionCommitted = true;
        if (!keepOrderRunning)
            Finish(orderId, ControlledTaskOrderState.Completed, string.Empty);
        return true;
    }

    public static bool MarkExecutionCompleted(Actor actor)
    {
        if (actor == null || !TryGetActiveOrderId(actor.getID(), out long orderId) ||
            !Orders.TryGetValue(orderId, out ControlledTaskOrder order) ||
            order.State != ControlledTaskOrderState.Running || !order.ExecutionCommitted)
            return false;
        Finish(orderId, ControlledTaskOrderState.Completed, string.Empty);
        return true;
    }

    public static bool ReportExecutionFailure(Actor actor, string reasonLocaleKey)
    {
        if (actor == null || !TryGetActiveOrderId(actor.getID(), out long orderId) ||
            !Orders.TryGetValue(orderId, out ControlledTaskOrder order) ||
            order.State != ControlledTaskOrderState.Running || !OwnsCurrentTask(order, actor))
            return false;

        order.ExecutionFailed = true;
        if (!string.IsNullOrEmpty(reasonLocaleKey)) order.ExecutionFailureReasonLocaleKey = reasonLocaleKey;
        return true;
    }

    public static void CopyVisibleOrders(List<ControlledTaskOrderView> destination)
    {
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        TickOrders();
        destination.Clear();

        ScratchIds.Clear();
        foreach (long orderId in Orders.Keys) ScratchIds.Add(orderId);
        ScratchIds.Sort();
        for (var i = 0; i < ScratchIds.Count; i++)
        {
            ControlledTaskOrder order = Orders[ScratchIds[i]];
            Actor actor = ResolveActor(order.ActorId);
            destination.Add(new ControlledTaskOrderView(
                order.OrderId,
                order.ActorId,
                order.ActorName,
                order.Command.id,
                order.Command.NameLocaleKey,
                order.Command.IconPath,
                order.State,
                order.ReasonLocaleKey,
                order.StartedAt,
                actor != null && !actor.isRekt(),
                order.State == ControlledTaskOrderState.Running && actor != null && !actor.isRekt() &&
                OwnsCurrentTask(order, actor)));
        }
    }

    public static Actor ResolveOrderActor(long actorId)
    {
        return ResolveActor(actorId);
    }

    internal static void NotifyTaskFinishing(AiSystemActor ai, bool completedTaskSequence)
    {
        if (ai == null || !ActorByAiSystem.TryGetValue(ai, out long actorId) ||
            !ActiveOrderByActor.TryGetValue(actorId, out long orderId) ||
            !Orders.TryGetValue(orderId, out ControlledTaskOrder order))
            return;

        Actor actor = ResolveActor(actorId);
        if (actor == null || actor.isRekt())
        {
            Finish(orderId, ControlledTaskOrderState.ActorLost, "Cultiway.ControlledTask.Reason.ActorLost");
            return;
        }
        if (!OwnsCurrentTask(order, actor))
        {
            Finish(orderId, ControlledTaskOrderState.Interrupted,
                "Cultiway.ControlledTask.Reason.ExternalInterrupt");
            return;
        }
        if (ControlledTaskCancellationScope.Contains(actorId))
        {
            Finish(orderId, ControlledTaskOrderState.Cancelled,
                "Cultiway.ControlledTask.Reason.Cancelled");
            return;
        }
        if (ControllableUnit.isControllingUnit(actor))
        {
            Finish(orderId, ControlledTaskOrderState.Interrupted,
                "Cultiway.ControlledTask.Reason.DirectControlInterrupt");
            return;
        }

        if (!completedTaskSequence || order.ExecutionFailed)
        {
            Finish(orderId, ControlledTaskOrderState.Failed,
                string.IsNullOrEmpty(order.ExecutionFailureReasonLocaleKey)
                    ? "Cultiway.ControlledTask.Reason.ExecutionFailed"
                    : order.ExecutionFailureReasonLocaleKey);
            return;
        }

        Finish(orderId, ControlledTaskOrderState.Completed, string.Empty);
    }

    private static ControlledTaskAvailability ValidateControlledActor(Actor actor)
    {
        if (actor == null || actor.isRekt())
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.ActorLost");
        if (actor.is_unconscious || actor.asset == null || actor.asset.id == "crabzilla" ||
            actor.asset.skip_fight_logic)
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.ActorUnavailable");
        if (!ControllableUnit.isControllingUnit() || ControllableUnit.count() != 1 ||
            !ReferenceEquals(ControllableUnit.getControllableUnit(), actor) ||
            !ControllableUnit.isControllingUnit(actor))
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.NotSingleControlledActor");
        return ControlledTaskAvailability.Available;
    }

    private static ActorTaskRuntime EnsureRuntime(Actor actor)
    {
        long actorId = actor.getID();
        if (RuntimeByActor.TryGetValue(actorId, out ActorTaskRuntime runtime) &&
            ReferenceEquals(runtime.Actor, actor))
            return runtime;
        if (runtime?.Actor?.ai != null) ActorByAiSystem.Remove(runtime.Actor.ai);

        runtime = new ActorTaskRuntime(actor);
        RuntimeByActor[actorId] = runtime;
        ActorByAiSystem[actor.ai] = actorId;
        actor.ai.subscribeToTaskSwitch(() => NotifyTaskSwitchById(actorId));
        return runtime;
    }

    private static void NotifyTaskSwitchById(long actorId)
    {
        Actor actor = ResolveActor(actorId);
        if (actor == null) return;
        NotifyTaskSwitch(actor);
    }

    private static void NotifyTaskSwitch(Actor actor)
    {
        if (actor == null || !RuntimeByActor.TryGetValue(actor.getID(), out ActorTaskRuntime runtime) ||
            !ReferenceEquals(runtime.Actor, actor))
            return;
        runtime.Revision++;
        if (!ActiveOrderByActor.TryGetValue(actor.getID(), out long orderId) ||
            !Orders.TryGetValue(orderId, out ControlledTaskOrder order) || OwnsCurrentTask(order, actor))
            return;
        Finish(orderId, ControlledTaskOrderState.Interrupted,
            "Cultiway.ControlledTask.Reason.ExternalInterrupt");
    }

    private static bool OwnsCurrentTask(ControlledTaskOrder order, Actor actor)
    {
        return RuntimeByActor.TryGetValue(order.ActorId, out ActorTaskRuntime runtime) &&
               ReferenceEquals(runtime.Actor, actor) && runtime.Revision == order.TaskRevision &&
               ReferenceEquals(actor.ai.task, order.TaskAsset);
    }

    private static Actor ResolveActor(long actorId)
    {
        return actorId > 0 && World.world?.units != null ? World.world.units.get(actorId) : null;
    }

    private static void TickOrders()
    {
        if (ticking) return;
        ticking = true;
        try
        {
            ScratchIds.Clear();
            foreach (long orderId in Orders.Keys) ScratchIds.Add(orderId);
            float now = Time.realtimeSinceStartup;
            for (var i = 0; i < ScratchIds.Count; i++)
            {
                long orderId = ScratchIds[i];
                if (!Orders.TryGetValue(orderId, out ControlledTaskOrder order)) continue;
                if (order.State == ControlledTaskOrderState.Running)
                {
                    Actor actor = ResolveActor(order.ActorId);
                    if (actor == null || actor.isRekt())
                    {
                        Finish(orderId, ControlledTaskOrderState.ActorLost,
                            "Cultiway.ControlledTask.Reason.ActorLost");
                    }
                    else if (!OwnsCurrentTask(order, actor))
                    {
                        Finish(orderId, ControlledTaskOrderState.Interrupted,
                            "Cultiway.ControlledTask.Reason.ExternalInterrupt");
                    }
                    continue;
                }

                float retention = order.State == ControlledTaskOrderState.Completed
                    ? SuccessfulRetentionSeconds
                    : OtherRetentionSeconds;
                if (order.FinishedAt >= 0f && now - order.FinishedAt >= retention) Orders.Remove(orderId);
            }

            ScratchActorIds.Clear();
            foreach (KeyValuePair<long, ActorTaskRuntime> entry in RuntimeByActor)
            {
                Actor runtimeActor = entry.Value.Actor;
                if (runtimeActor == null || runtimeActor.isRekt()) ScratchActorIds.Add(entry.Key);
            }
            for (var i = 0; i < ScratchActorIds.Count; i++)
            {
                long actorId = ScratchActorIds[i];
                if (!RuntimeByActor.TryGetValue(actorId, out ActorTaskRuntime runtime)) continue;
                RuntimeByActor.Remove(actorId);
                if (runtime.Actor?.ai != null && ActorByAiSystem.TryGetValue(runtime.Actor.ai, out long mapped) &&
                    mapped == actorId)
                    ActorByAiSystem.Remove(runtime.Actor.ai);
            }
        }
        finally
        {
            ticking = false;
        }
    }

    private static void Finish(long orderId, ControlledTaskOrderState state, string reasonLocaleKey)
    {
        if (!Orders.TryGetValue(orderId, out ControlledTaskOrder order) ||
            order.State != ControlledTaskOrderState.Running)
            return;

        order.State = state;
        order.ReasonLocaleKey = reasonLocaleKey ?? string.Empty;
        order.FinishedAt = Time.realtimeSinceStartup;
        if (ActiveOrderByActor.TryGetValue(order.ActorId, out long current) && current == orderId)
            ActiveOrderByActor.Remove(order.ActorId);

        if (!ControlledTaskExecutionContextStore.Remove(orderId,
                out IControlledTaskExecutionContext context)) return;
        try
        {
            context.OnOrderFinished(state, order.ReasonLocaleKey);
        }
        catch (Exception exception)
        {
            ModClass.LogError($"[ControlledTaskOrder] context cleanup failed order={orderId}: {exception}");
        }
    }

    private static void ClearWorldState()
    {
        ScratchIds.Clear();
        foreach (long orderId in Orders.Keys) ScratchIds.Add(orderId);
        for (int i = 0; i < ScratchIds.Count; i++)
        {
            if (Orders.TryGetValue(ScratchIds[i], out ControlledTaskOrder order) &&
                order.State == ControlledTaskOrderState.Running)
                Finish(order.OrderId, ControlledTaskOrderState.ActorLost,
                    "Cultiway.ControlledTask.Reason.ActorLost");
        }

        Orders.Clear();
        ActiveOrderByActor.Clear();
        RuntimeByActor.Clear();
        ActorByAiSystem.Clear();
        ControlledTaskExecutionContextStore.Clear();
        ScratchIds.Clear();
        nextOrderId = 1;
    }

    private sealed class ActorTaskRuntime
    {
        public Actor Actor { get; }
        public long Revision { get; set; }

        public ActorTaskRuntime(Actor actor)
        {
            Actor = actor;
        }
    }

    private sealed class ControlledTaskOrder
    {
        public long OrderId { get; }
        public long ActorId { get; }
        public string ActorName { get; }
        public ControlledTaskCommandAsset Command { get; }
        public BehaviourTaskActor TaskAsset { get; }
        public long TaskRevision { get; set; }
        public float StartedAt { get; }
        public ControlledTaskOrderState State { get; set; } = ControlledTaskOrderState.Running;
        public string ReasonLocaleKey { get; set; } = string.Empty;
        public float FinishedAt { get; set; } = -1f;
        public bool ExecutionFailed { get; set; }
        public bool ExecutionCommitted { get; set; }
        public string ExecutionFailureReasonLocaleKey { get; set; } = string.Empty;

        public ControlledTaskOrder(long orderId, long actorId, string actorName,
            ControlledTaskCommandAsset command, BehaviourTaskActor taskAsset, long taskRevision,
            float startedAt)
        {
            OrderId = orderId;
            ActorId = actorId;
            ActorName = actorName;
            Command = command;
            TaskAsset = taskAsset;
            TaskRevision = taskRevision;
            StartedAt = startedAt;
        }
    }

    private sealed class UpdateSystem : BaseSystem, IWorldStateClearable
    {
        protected override void OnUpdateGroup()
        {
            base.OnUpdateGroup();
            TickOrders();
        }

        void IWorldStateClearable.ClearWorldState()
        {
            ControlledTaskOrderService.ClearWorldState();
        }
    }
}
