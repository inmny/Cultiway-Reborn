using Cultiway.Core.Visuals;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>剑影本轮真实空间姿态，原版和 Worldfall 直接读取。</summary>
public struct ArtifactSpatialProjection : IComponent
{
    internal SpatialVisualMeshInstance Pose;
}
