using System;
using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.Pathfinding;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.SubWorlds.Runtime;

/// <summary>查询小世界移动实体，在固定 tick 内认领共享 PathFinder 路径步并推进真实位置。</summary>
internal sealed class SubWorldMovementSystem : QuerySystem<Position, SubWorldMovement>
{
    private readonly SubWorldRuntime runtime;
    private readonly Dictionary<Entity, MoveToTileCommand> latestMoveCommands = new();

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
        latestMoveCommands.Clear();
        while (runtime.MoveCommandQueue.Count > 0)
        {
            MoveToTileCommand command = runtime.MoveCommandQueue.Dequeue();
            if (command.Revision != runtime.Revision) continue;
            latestMoveCommands[command.Entity] = command;
        }

        foreach (MoveToTileCommand command in latestMoveCommands.Values)
        {
            Entity entity = command.Entity;
            if (entity.IsNull || entity.Store != runtime.EntityStore || !entity.HasComponent<Position>() ||
                !entity.HasComponent<SubWorldMovement>()) continue;

            ref SubWorldMovement movement = ref entity.GetComponent<SubWorldMovement>();
            CancelHandle(movement.Handle);
            movement.ClearRoute();
            movement.SetTarget(command.TargetTileIndex);
        }

        latestMoveCommands.Clear();
    }

    private void UpdateEntity(Entity entity, ref Position position, ref SubWorldMovement movement)
    {
        if (movement.NextTileIndex >= 0)
        {
            UpdateCommittedMovement(entity, ref position, ref movement);
            return;
        }

        if (movement.TargetTileIndex < 0) return;
        if (movement.CurrentTileIndex == movement.TargetTileIndex)
        {
            CompleteIntent(ref movement);
            return;
        }

        EnsureFutureRoute(entity, ref movement);
        if (movement.TargetTileIndex < 0 || !movement.Handle.IsValid) return;
        PollRoute(ref movement);
        if (movement.NextTileIndex >= 0)
        {
            AdvanceCommittedMovement(entity, ref position, ref movement);
        }
    }

    private void UpdateCommittedMovement(Entity entity, ref Position position, ref SubWorldMovement movement)
    {
        bool retreating = movement.NextTileIndex == movement.CurrentTileIndex;
        if (!retreating && !IsStepLocallyValid(ref movement, movement.NextTileIndex,
                movement.PlannedTileFlags))
        {
            CancelHandle(movement.Handle, PathFailureReason.StepBlocked);
            movement.BeginRetreat();
            retreating = true;
        }

        if (!retreating && movement.TargetTileIndex >= 0)
        {
            EnsureFutureRoute(entity, ref movement);
        }

        AdvanceCommittedMovement(entity, ref position, ref movement);
    }

    private void EnsureFutureRoute(Entity entity, ref SubWorldMovement movement)
    {
        if (movement.TargetTileIndex < 0) return;
        int routeStartTile = movement.NextTileIndex >= 0 &&
                             movement.NextTileIndex != movement.CurrentTileIndex
            ? movement.NextTileIndex
            : movement.CurrentTileIndex;
        if (routeStartTile == movement.TargetTileIndex)
        {
            CancelHandle(movement.Handle);
            movement.ClearRoute();
            return;
        }

        if (movement.Handle.IsValid)
        {
            if (!IsRequestSnapshotStale(ref movement)) return;
            CancelHandle(movement.Handle, PathFailureReason.StepBlocked);
            movement.ClearRoute();
        }

        SubmitPath(entity, routeStartTile, ref movement);
    }

    private void SubmitPath(Entity entity, int startTileIndex, ref SubWorldMovement movement)
    {
        float baseSpeed = Math.Max(0.05f, movement.MoveSpeedTilesPerSecond /
            Math.Max(0.05f, PathfindingConfig.Default.WalkSpeedScale));
        PathTraversalProfile profile = PathTraversalProfile.StandardGround(baseSpeed);
        PathAgentKey agentKey = new(runtime.Navigation.WorldKey, entity.Id);
        PathRequest request = PathRequest.CreateSubWorld(
            agentKey,
            startTileIndex,
            movement.TargetTileIndex,
            runtime.Navigation.CurrentGrid,
            profile);
        PathSubmissionResult submission = PathFinder.Instance.RequestPathDetailed(request);
        if (!submission.Accepted || submission.SubmissionToken <= 0)
        {
            PathFailureReason reason = submission.FailureReason == PathFailureReason.None
                ? PathFailureReason.GeneratorException
                : submission.FailureReason;
            FailFutureRoute(ref movement, reason);
            return;
        }

        movement.BindRoute(
            new PathHandle(agentKey, submission.SubmissionToken),
            request.NavigationRevision);
    }

    private void PollRoute(ref SubWorldMovement movement)
    {
        if (IsRequestSnapshotStale(ref movement))
        {
            CancelHandle(movement.Handle, PathFailureReason.StepBlocked);
            movement.ClearRoute();
            return;
        }

        PathPollResult poll = PathFinder.Instance.OpenReadyCursor(
            movement.Handle, out PathFinder.ReadyPathCursor cursor);
        switch (poll.Kind)
        {
            case PathPollKind.Waiting:
                return;
            case PathPollKind.StepReady:
                if (!cursor.TryClaimCurrentStep(out PathStep claimedStep))
                {
                    movement.ClearRoute();
                    return;
                }

                if (!IsStepLocallyValid(ref movement, claimedStep.TileId, claimedStep.PlannedTileFlags))
                {
                    CancelHandle(movement.Handle, PathFailureReason.StepBlocked);
                    movement.ClearRoute();
                    return;
                }

                movement.CommitStep(claimedStep);
                return;
            case PathPollKind.Completed:
                if (movement.CurrentTileIndex == movement.TargetTileIndex)
                {
                    CompleteIntent(ref movement);
                }
                else
                {
                    movement.ClearRoute();
                }
                return;
            case PathPollKind.Failed:
                FailFutureRoute(ref movement, poll.FailureReason);
                return;
            case PathPollKind.Cancelled:
            case PathPollKind.NoRequest:
                movement.ClearRoute();
                return;
        }
    }

    private void AdvanceCommittedMovement(Entity entity, ref Position position, ref SubWorldMovement movement)
    {
        float remainingTime = runtime.Clock.Profile.fixed_step;
        while (remainingTime > 0.0001f && movement.NextTileIndex >= 0)
        {
            int destinationTile = movement.NextTileIndex;
            bool retreating = destinationTile == movement.CurrentTileIndex;
            int x = runtime.Grid.GetX(destinationTile);
            int y = runtime.Grid.GetY(destinationTile);
            Vector3 destination = new(x + 0.5f, y + 0.5f, position.z);
            Vector3 delta = destination - position.value;
            float distance = delta.magnitude;
            float speed = Math.Max(0.01f, movement.MoveSpeedTilesPerSecond *
                runtime.Navigation.GetWalkMultiplier(destinationTile));
            float movementBudget = speed * remainingTime;
            if (distance > movementBudget)
            {
                position.value += delta / Math.Max(distance, 0.0001f) * movementBudget;
                return;
            }

            position.value = destination;
            remainingTime -= distance / speed;
            if (!retreating)
            {
                movement.CurrentTileIndex = destinationTile;
            }
            movement.ClearCommittedStep();

            if (movement.TargetTileIndex < 0)
            {
                CancelHandle(movement.Handle);
                movement.ClearRoute();
                return;
            }

            if (movement.CurrentTileIndex == movement.TargetTileIndex)
            {
                CompleteIntent(ref movement);
                return;
            }

            EnsureFutureRoute(entity, ref movement);
            if (movement.TargetTileIndex < 0 || !movement.Handle.IsValid) return;
            PollRoute(ref movement);
        }
    }

    private bool IsRequestSnapshotStale(ref SubWorldMovement movement)
    {
        return movement.NavigationRevision != runtime.Navigation.CurrentGrid.Revision;
    }

    private bool IsStepLocallyValid(ref SubWorldMovement movement, int nextTile,
        PathTileFlags plannedTileFlags)
    {
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

    private void FailFutureRoute(ref SubWorldMovement movement, PathFailureReason reason)
    {
        CancelHandle(movement.Handle, reason);
        if (movement.NextTileIndex >= 0 && movement.NextTileIndex != movement.CurrentTileIndex)
        {
            movement.StopAtCommittedDestination();
            return;
        }

        movement.CompleteIntent();
    }

    private void CompleteIntent(ref SubWorldMovement movement)
    {
        CancelHandle(movement.Handle, PathFailureReason.None);
        movement.CompleteIntent();
    }

    private static void CancelHandle(PathHandle handle,
        PathFailureReason reason = PathFailureReason.CancelledByNewRequest)
    {
        if (handle.IsValid) PathFinder.Instance.Cancel(handle, reason);
    }
}
