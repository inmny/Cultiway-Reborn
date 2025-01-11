using Cultiway.Core.SkillLibV2.Api;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox;

namespace Cultiway.Core.SkillLibV2.Components;

public struct SkillEntity : IComponent
{
    [Ignore]
    public SkillEntityMeta Meta { get; internal set; }
}