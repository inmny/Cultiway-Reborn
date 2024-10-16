using Cultiway.Core.SkillLibV2.Api;
using Cultiway.Core.SkillLibV2.Predefined;

namespace Cultiway.Core.SkillLibV2.Extensions;

public static class TriggerActionMetaBuilderTools
{
    public static TriggerActionMeta<TTrigger, TContext>.MetaBuilder AddCastCountIncrease<TTrigger, TContext>(
        this TriggerActionMeta<TTrigger, TContext>.MetaBuilder builder)
        where TContext : struct, IEventContext
        where TTrigger : struct, IEventTrigger<TTrigger, TContext>
    {
        return builder.AppendAction(TriggerActions.cast_count_increase);
    }
}