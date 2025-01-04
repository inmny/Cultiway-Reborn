using Cultiway.Core.SkillLibV2.Systems;
using Cultiway.Core.Systems.Render;
using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

internal struct AnimBindRenderer : IComponent
{
    public RenderAnimFrameSystem.AnimRenderer value;
}