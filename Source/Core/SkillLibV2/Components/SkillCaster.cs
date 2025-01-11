using Friflo.Engine.ECS;
using Friflo.Json.Fliox;

namespace Cultiway.Core.SkillLibV2.Components;

public struct SkillCaster : IComponent
{
    [Ignore]
    public ActorExtend value;
    [Ignore]
    public Actor       AsActor => value.Base;
}