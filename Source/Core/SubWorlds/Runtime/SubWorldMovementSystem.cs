using System;
using Cultiway.Core.Components;
using Cultiway.Core.Pathfinding;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.SubWorlds.Runtime;

/// <summary>查询小世界移动实体，在固定 tick 内提交、验证并执行共享 PathFinder 路径。</summary>
internal sealed class SubWorldMovementSystem : QuerySystem<Position, SubWorldMovement>
{
    private readonly SubWorldRuntime runtime;

    internal SubWorldMovementSystem(SubWorldRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    protected override void OnUpdate()
    {
        ApplyMoveCommands();
        Query.ForEachEntity((ref Position position, ref SubWorldMovement movement, Entity entity) =>
        {
            UpdateEntity(entity, ref position, ref movement);
        });
    }

    private void ApplyMoveCommands()
    {
        int commandCount = 0;
        while (commandCount++ < 32 && runtime.MoveCommandQueue.Count > 0)
        {
            MoveToTileCommand command = runtime.MoveCommandQueue.Dequeue();
            if (command.Revision != runtime.Revision) continue;
            Entity entity = command.Entity;
            if (entity.IsNull || entity.Store != runtime.EntityStore || !entity.HasComponent<Position>() ||
                !entity.HasComponent<SubWorldMovement>()) continue;

            ref Position position = ref entity.GetComponent<Position>();
            ref SubWorldMovement movement = ref entity.GetComponent<SubWorldMovement>();
            CancelHandle(movement.Handle);
            SnapToCurrentTile(ref position, movement.CurrentTileIndex);
            movement.BeginIntent(command.TargetTileIndex);
        }
    }

    private void UpdateEntity(Entity entity, ref Position position, ref SubWorldMovement movement)
    {
        if (movement.TargetTileIndex < 0) return;

        if (!movement.Handle.IsValid)
        {
            SubmitPath(entity, ref movement);
        }

        if (movement.TargetTileIndex < 0) return;
        if (movement.NextTileIndex < 0)
        {
            PollPath(ref position, ref movement);
        }

        if (movement.TargetTileIndex < 0 || movement.NextTileIndex < 0) return;
        if (!ValidateStep(ref movement, movement.NextTileIndex, movement.PlannedTileFlags))
        {
            ReplanBlockedPath(ref position, ref movement);
            return;
        }

        AdvanceCurrentStep(entity, ref position, ref movement);
    }

    private void SubmitPath(Entity entity, ref SubWorldMovement movement)
    {
        float baseSpeed = Math.Max(0.05f, movement.MoveSpeedTilesPerSecond /
            Math.Max(0.05f, PathfindingConfig.Default.WalkSpeedScale));
        PathTraversalProfile profile = PathTraversalProfile.StandardGround(baseSpeed);
        PathAgentKey agentKey = new(runtime.Navigation.WorldKey, entity.Id);
        PathRequest request = PathRequest.CreateSubWorld(
            agentKey,
            movement.CurrentTileIndex,
            movement.TargetTileIndex,
            runtime.Navigation.CurrentGrid,
            profile);
        PathSubmissionResult submission = PathFinder.Instance.RequestPathDetailed(request);
        if (!submission.Accepted || submission.SubmissionToken <= 0)
        {
            PathFailureReason reason = submission.FailureReason == PathFailureReason.None
                ? PathFailureReason.GeneratorException
                : submission.FailureReason;
            FailPath(ref movement, reason);
            return;
        }

        movement.BindRequest(
            new PathHandle(agentKey, submission.SubmissionToken),
            request.NavigationRevision);
    }

    private void PollPath(ref Position position, ref SubWorldMovement movement)
    {
        if (IsRequestSnapshotStale(ref movement))
        {
            ReplanBlockedPath(ref position, ref movement);
            return;
        }

        PathPollResult poll = PathFinder.Instance.OpenReadyCursor(movement.Handle, out _);
        switch (poll.Kind)
        {
            case PathPollKind.Waiting:
                return;
            case PathPollKind.StepReady:
                if (!ValidateStep(ref movement, poll.Step.TileId, poll.Step.PlannedTileFlags))
                {
                    ReplanBlockedPath(ref position, ref movement);
                    return;
                }

                movement.SetCurrentStep(poll.Step);
                return;
            case PathPollKind.Completed:
                ArriveAtTarget(ref position, ref movement);
                return;
            case PathPollKind.Failed:
                FailPath(ref movement, poll.FailureReason);
                return;
            case PathPollKind.Cancelled:
            case PathPollKind.NoRequest:
                SnapToCurrentTile(ref position, movement.CurrentTileIndex);
                movement.PrepareReplan();
                return;
        }
    }

    private bool TryOpenCurrentStep(ref SubWorldMovement movement,
        out PathFinder.ReadyPathCursor cursor)
    {
        PathPollResult poll = PathFinder.Instance.OpenReadyCursor(movement.Handle, out cursor);
        return poll.Kind == PathPollKind.StepReady && poll.Step.TileId == movement.NextTileIndex;
    }

    private void AdvanceCurrentStep(Entity entity, ref Position position, ref SubWorldMovement movement)
    {
        float remainingTime = runtime.Clock.Profile.fixed_step;
        while (remainingTime > 0.0001f && movement.TargetTileIndex >= 0 && movement.NextTileIndex >= 0)
        {
            int nextTile = movement.NextTileIndex;
            int x = runtime.Grid.GetX(nextTile);
            int y = runtime.Grid.GetY(nextTile);
            Vector3 target = new(x + 0.5f, y + 0.5f, position.z);
            Vector3 delta = target - position.value;
            float distance = delta.magnitude;
            float speed = Math.Max(0.01f, movement.MoveSpeedTilesPerSecond *
                runtime.Navigation.GetWalkMultiplier(nextTile));
            float movementBudget = speed * remainingTime;
            int previousTile = movement.CurrentTileIndex;
            if (!TryOpenCurrentStep(ref movement, out PathFinder.ReadyPathCursor cursor) ||
                !cursor.TryExecuteCurrentStep(
                    step => AdvanceOwnedStep(step, entity, nextTile, target, delta, distance, movementBudget),
                    out StepAdvanceResult advanceResult) ||
                advanceResult == StepAdvanceResult.Invalid)
            {
                CancelHandle(movement.Handle);
                SnapToCurrentTile(ref position, previousTile);
                movement.PrepareReplan();
                return;
            }

            if (advanceResult == StepAdvanceResult.Partial) return;
            remainingTime -= distance / speed;
            cursor.Consume();
            if (nextTile == movement.TargetTileIndex)
            {
                ArriveAtTarget(ref position, ref movement);
                return;
            }

            movement.ClearCurrentStep();
            PollPath(ref position, ref movement);
        }
    }

    private static StepAdvanceResult AdvanceOwnedStep(PathStep step, Entity entity, int expectedTile,
        Vector3 target, Vector3 delta, float distance, float movementBudget)
    {
        if (step.TileId != expectedTile) return StepAdvanceResult.Invalid;

        ref Position position = ref entity.GetComponent<Position>();
        if (distance > movementBudget)
        {
            position.value += delta / Math.Max(distance, 0.0001f) * movementBudget;
            return StepAdvanceResult.Partial;
        }

        position.value = target;
        entity.GetComponent<SubWorldMovement>().CurrentTileIndex = expectedTile;
        return StepAdvanceResult.Reached;
    }

    private bool IsRequestSnapshotStale(ref SubWorldMovement movement)
    {
        return movement.NavigationRevision != runtime.Navigation.CurrentGrid.Revision;
    }

    private bool ValidateStep(ref SubWorldMovement movement, int nextTile,
        PathTileFlags plannedTileFlags)
    {
        if (movement.NavigationRevision != runtime.Navigation.CurrentGrid.Revision) return false;
        if ((uint)nextTile >= (uint)runtime.Grid.TileCount) return false;

        int currentTile = movement.CurrentTileIndex;
        int currentX = runtime.Grid.GetX(currentTile);
        int currentY = runtime.Grid.GetY(currentTile);
        int nextX = runtime.Grid.GetX(nextTile);
        int nextY = runtime.Grid.GetY(nextTile);
        int dx = Math.Abs(nextX - currentX);
        int dy = Math.Abs(nextY - currentY);
        if (dx > 1 || dy > 1 || dx + dy == 0) return false;
        if (!runtime.Navigation.IsTerrainPassable(nextTile)) return false;

        if (dx == 1 && dy == 1 &&
            (!runtime.Navigation.IsTerrainPassable(runtime.Grid.GetIndex(nextX, currentY)) ||
             !runtime.Navigation.IsTerrainPassable(runtime.Grid.GetIndex(currentX, nextY))))
        {
            return false;
        }

        return runtime.Navigation.CurrentGrid.TryGetTile(nextTile, out PathTileSnapshot tile) &&
               tile.Flags == plannedTileFlags;
    }

    private void ReplanBlockedPath(ref Position position, ref SubWorldMovement movement)
    {
        int currentTile = movement.CurrentTileIndex;
        CancelHandle(movement.Handle, PathFailureReason.StepBlocked);
        SnapToCurrentTile(ref position, currentTile);
        movement.PrepareReplan();
    }

    private void ArriveAtTarget(ref Position position, ref SubWorldMovement movement)
    {
        SnapToCurrentTile(ref position, movement.CurrentTileIndex);
        CancelHandle(movement.Handle, PathFailureReason.None);
        movement.CompleteIntent();
    }

    private void FailPath(ref SubWorldMovement movement, PathFailureReason reason)
    {
        CancelHandle(movement.Handle, reason);
        movement.CompleteIntent();
    }

    private static void CancelHandle(PathHandle handle,
        PathFailureReason reason = PathFailureReason.CancelledByNewRequest)
    {
        if (handle.IsValid) PathFinder.Instance.Cancel(handle, reason);
    }

    private enum StepAdvanceResult : byte
    {
        Invalid,
        Partial,
        Reached
    }

    private void SnapToCurrentTile(ref Position position, int tileIndex)
    {
        position.value = new Vector3(
            runtime.Grid.GetX(tileIndex) + 0.5f,
            runtime.Grid.GetY(tileIndex) + 0.5f,
            position.z);
    }
}
