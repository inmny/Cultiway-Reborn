using Cultiway.Core.SkillLibV2.Systems;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV2.Components;

internal struct TrailBindRenderer : IComponent
{
    public RenderTrailSystem.CustomTrailRenderer value;
}