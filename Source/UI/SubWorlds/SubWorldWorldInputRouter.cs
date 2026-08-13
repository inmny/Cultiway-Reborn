using System.Collections.Generic;
using Cultiway.Core.SubWorlds;
using Cultiway.Core.SubWorlds.Runtime;
using UnityEngine;

namespace Cultiway.UI.SubWorlds;

/// <summary>在主地图、小世界视图与外围空白之间分配世界输入。</summary>
internal sealed class SubWorldWorldInputRouter
{
    private readonly SubWorldManager manager;
    private readonly SubWorldSpatialLayout spatialLayout;
    private readonly IReadOnlyDictionary<long, SubWorldWorldView> worldViews;

    internal SubWorldWorldInputRouter(
        SubWorldManager manager,
        SubWorldSpatialLayout spatialLayout,
        IReadOnlyDictionary<long, SubWorldWorldView> worldViews)
    {
        this.manager = manager;
        this.spatialLayout = spatialLayout;
        this.worldViews = worldViews;
    }

    /// <summary>处理小世界输入，并返回本帧是否应跳过主世界 PlayerControl。</summary>
    internal bool Route()
    {
        if (!spatialLayout.HasOccupiedSlots || World.world.isOverUI()) return false;

        Vector2 worldPosition = World.world.camera.ScreenToWorldPoint(Input.mousePosition);
        if (spatialLayout.ContainsMainWorld(worldPosition)) return false;

        foreach (SubWorldWorldView worldView in worldViews.Values)
        {
            if (!worldView.WorldBounds.Contains(worldPosition)) continue;
            RouteToRuntime(worldView, worldPosition);
            return true;
        }
        return true;
    }

    private void RouteToRuntime(SubWorldWorldView worldView, Vector2 worldPosition)
    {
        if (MoveCamera.camera_drag_activated) return;
        if (InputHelpers.GetMouseButtonUp(0)) manager.SelectFromWorldView(worldView.InstanceId);
        if (!InputHelpers.mouseSupported || !InputHelpers.GetMouseButtonUp(1)) return;

        SubWorldRuntime runtime = manager.Get(worldView.InstanceId);
        Vector2 localPosition = worldView.Slot.ToLocal(worldPosition);
        int x = Mathf.FloorToInt(localPosition.x);
        int y = Mathf.FloorToInt(localPosition.y);
        int targetTileIndex = runtime.Grid.GetIndex(x, y);
        manager.IssueCommand(runtime.InstanceId, new MoveToTileCommand(
            runtime.InstanceId,
            runtime.Revision,
            runtime.PawnEntity.Id,
            targetTileIndex));
    }
}
