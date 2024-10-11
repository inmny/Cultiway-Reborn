using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV2.Components;

public struct SkillCaster : IComponent
{
    public Entity      value;
    public ActorBinder AsActor => value.GetComponent<ActorBinder>();
}