using Cultiway.Core;
using Cultiway.Core.SubWorlds.Runtime;
using Cultiway.UI.SubWorlds.Render;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cultiway.UI.SubWorlds;

/// <summary>拥有单个小世界 Unity 根节点，并驱动绑定其 EntityStore 的独立 RenderRoot。</summary>
internal sealed class SubWorldWorldView
{
    private readonly SubWorldRuntime runtime;
    private readonly GameObject root;
    private readonly SystemRoot renderRoot;
    private readonly SubWorldRenderState renderState;

    internal SubWorldWorldView(SubWorldRuntime runtime, SubWorldSpatialSlot slot)
    {
        this.runtime = runtime;
        Slot = slot;
        root = new GameObject($"SubWorld.{runtime.InstanceId}");
        root.transform.SetParent(MapBox.instance.transform, false);
        root.transform.position = new Vector3(slot.WorldOrigin.x, slot.WorldOrigin.y, 0f);

        renderState = new SubWorldRenderState();
        renderRoot = new SystemRoot(runtime.EntityStore, $"SubWorld.Render.{runtime.InstanceId}");
        renderRoot.Add(new SubWorldRenderPrepareSystem(runtime.Grid, renderState));
        renderRoot.Add(new SubWorldOverviewRenderSystem(
            runtime.InstanceId, runtime.Grid, renderState, root.transform));
        renderRoot.Add(new SubWorldTerrainRenderSystem(runtime.Grid, renderState, root.transform));
        renderRoot.Add(new SubWorldWallRenderSystem(runtime.Grid, runtime.Seed, renderState, root.transform));
        renderRoot.Add(new SubWorldBuildingRenderSystem(renderState, root.transform));
        renderRoot.Add(new SubWorldUnitRenderSystem(renderState, root.transform));
        renderRoot.Update(new UpdateTick(0f, Time.time));
    }

    internal long InstanceId => runtime.InstanceId;
    internal SubWorldSpatialSlot Slot { get; }
    internal Rect WorldBounds => Slot.WorldBounds;

    /// <summary>设置实例是否处于主相机视野，并返回状态是否发生变化。</summary>
    internal bool SetVisible(bool value)
    {
        if (renderState.Visible == value) return false;
        renderState.Visible = value;
        return true;
    }

    /// <summary>执行该实例本帧的完整渲染系统序列。</summary>
    internal void SyncVisibleState()
    {
        renderRoot.Update(new UpdateTick(Time.unscaledDeltaTime, Time.time));
    }

    internal void Destroy()
    {
        WorldSystemLifecycle.ClearSystemStates(renderRoot);
        renderRoot.RemoveStore(runtime.EntityStore);
        Object.Destroy(root);
    }
}
