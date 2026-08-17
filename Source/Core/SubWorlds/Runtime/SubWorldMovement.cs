using Cultiway.Core.Pathfinding;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SubWorlds.Runtime;

/// <summary>小世界移动实体的终身组件，保存移动意图、未来路线和已经认领的当前移动段。</summary>
internal struct SubWorldMovement : IComponent
{
    internal SubWorldMovement(float moveSpeedTilesPerSecond, int currentTileIndex)
    {
        this = default;
        MoveSpeedTilesPerSecond = moveSpeedTilesPerSecond;
        CurrentTileIndex = currentTileIndex;
        TargetTileIndex = -1;
        NextTileIndex = -1;
    }

    internal float MoveSpeedTilesPerSecond;
    internal int CurrentTileIndex;
    internal int TargetTileIndex;

    internal PathHandle Handle;
    internal long NavigationRevision;

    internal int NextTileIndex;
    internal PathTileFlags PlannedTileFlags;

    internal void SetTarget(int targetTileIndex)
    {
        TargetTileIndex = targetTileIndex;
    }

    internal void BindRoute(PathHandle handle, long navigationRevision)
    {
        Handle = handle;
        NavigationRevision = navigationRevision;
    }

    internal void ClearRoute()
    {
        Handle = default;
        NavigationRevision = 0;
    }

    internal void CommitStep(PathStep step)
    {
        NextTileIndex = step.TileId;
        PlannedTileFlags = step.PlannedTileFlags;
    }

    internal void ClearCommittedStep()
    {
        NextTileIndex = -1;
        PlannedTileFlags = default;
    }

    internal void BeginRetreat()
    {
        ClearRoute();
        NextTileIndex = CurrentTileIndex;
        PlannedTileFlags = default;
    }

    internal void StopAtCommittedDestination()
    {
        TargetTileIndex = NextTileIndex;
        ClearRoute();
    }

    internal void CompleteIntent()
    {
        TargetTileIndex = -1;
        ClearRoute();
        ClearCommittedStep();
    }
}
