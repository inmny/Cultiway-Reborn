using System.Collections.Generic;

namespace Cultiway.UI.SubWorlds.Render;

/// <summary>在单个小世界 RenderRoot 内传递本帧可见性、LOD 和地图变更。</summary>
internal sealed class SubWorldRenderState
{
    internal bool Visible { get; set; }
    internal bool OverviewMode { get; set; }
    internal List<int> DirtyTiles { get; } = new();
    internal bool GameplayVisible => Visible && !OverviewMode;
    internal bool OverviewVisible => Visible && OverviewMode;
}
