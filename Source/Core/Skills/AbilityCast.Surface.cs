namespace Cultiway.Core.Skills;

/// <summary>原制作表面消失后沿共同结束路径停止仍依赖它的附属分支。</summary>
public sealed partial class AbilityCast
{
    /// <summary>已脱离的子体保留，尚未开始的表面分支也不能借用未来原面。</summary>
    /// <param name="node">已经转为另一种动态主体的原部位。</param>
    private void EndSurfaceChildren(int node)
    {
        foreach (int child in Plan.Nodes[node].Children)
            if (!nodes[child].Free && Plan.Nodes[child].AnchorSurface != null) EndNode(child, true);
    }
}
