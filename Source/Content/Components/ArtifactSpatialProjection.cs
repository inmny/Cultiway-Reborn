using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>短暂剑影使用哪件真实法宝的组合模型；不作为长期资料保存。</summary>
public struct ArtifactSpatialProjection : IComponent
{
    /// <summary>由原剑阵执行持有的真实法宝。</summary>
    public Entity Artifact;
    /// <summary>这道剑影的真实器身体积比例，两个视角和碰撞共同使用。</summary>
    public float SizeRatio;
}
