using Cultiway.Core.SkillLibV2.Components;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV2.Systems;

public class LogicColliderOctreeSystem : QuerySystem<Collider, Position, Rotation>
{
    protected override void OnUpdate()
    {
    }
}