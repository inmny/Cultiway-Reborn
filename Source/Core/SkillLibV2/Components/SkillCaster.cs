using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV2.Components;

public struct SkillCaster : IComponent
{
    public ActorExtend value;
    public Actor       AsActor => value.Base;
}