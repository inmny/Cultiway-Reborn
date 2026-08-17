using Cultiway.Core.SubWorlds.Runtime;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.UI.SubWorlds.Render;

/// <summary>为当前渲染帧确定 LOD，并且唯一消费 Grid 的 dirty tile。</summary>
internal sealed class SubWorldRenderPrepareSystem : BaseSystem
{
    private readonly SubWorldGrid grid;
    private readonly SubWorldRenderState state;

    internal SubWorldRenderPrepareSystem(SubWorldGrid grid, SubWorldRenderState state)
    {
        this.grid = grid;
        this.state = state;
    }

    protected override void OnUpdateGroup()
    {
        state.OverviewMode = MapBox.isRenderMiniMap();
        state.DirtyTiles.Clear();
        if (state.Visible) grid.ConsumeDirtyTiles(state.DirtyTiles);
    }
}
