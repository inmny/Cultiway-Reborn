using Cultiway.Core.SkillLibV2.Api;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV2.Components;

public struct SkillEntity : IComponent
{
    public SkillEntityMeta Meta { get; internal set; }

    public void NewTrigger<TTrigger, TContext>()
        where TContext : struct, IEventContext
        where TTrigger : struct, IEventTrigger<TTrigger, TContext>
    {
    }
}