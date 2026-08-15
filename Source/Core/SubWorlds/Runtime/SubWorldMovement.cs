using Cultiway.Core.Pathfinding;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SubWorlds.Runtime;

/// <summary>小世界移动实体的终身组件，包含完整移动配置和由固定 tick 独占写入的运行状态。</summary>
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
    internal int NextTileIndex;
    internal long NavigationRevision;
    internal PathTileFlags PlannedTileFlags;

    internal void BeginIntent(int targetTileIndex)
    {
        TargetTileIndex = targetTileIndex;
        ClearPathState();
    }

    internal void BindRequest(PathHandle handle, long navigationRevision)
    {
        Handle = handle;
        NavigationRevision = navigationRevision;
        ClearCurrentStep();
    }

    internal void SetCurrentStep(PathStep step)
    {
        NextTileIndex = step.TileId;
        PlannedTileFlags = step.PlannedTileFlags;
    }

    internal void ClearCurrentStep()
    {
        NextTileIndex = -1;
        PlannedTileFlags = default;
    }

    internal void PrepareReplan()
    {
        ClearPathState();
    }

    internal void CompleteIntent()
    {
        TargetTileIndex = -1;
        ClearPathState();
    }

    private void ClearPathState()
    {
        Handle = default;
        NavigationRevision = 0;
        ClearCurrentStep();
    }
}
