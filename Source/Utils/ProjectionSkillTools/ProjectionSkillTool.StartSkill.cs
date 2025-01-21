using System;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Core.SkillLibV2;
using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Predefined.Modifiers;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;
using Friflo.Engine.ECS;

namespace Cultiway.Utils.ProjectionSkillTools;

public partial class ProjectionSkillTool
{
    private TriggerActionMeta<StartSkillTrigger, StartSkillContext>.MetaBuilder _start_skill_builder;
    private SkillEntityMeta.MetaBuilder _caster_entity_builder;
    private TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext> _random_cast_action;

    private void InitStartSkill()
    {
        _start_skill_builder = TriggerActionMeta<StartSkillTrigger, StartSkillContext>.StartBuild(_id);
        _caster_entity_builder = SkillEntityMeta.StartBuild(_id + "Caster")
            .AllowModifier<SalvoCountModifier, int>(new SalvoCountModifier(1))
            .AllowModifier<CastCountModifier, int>(new CastCountModifier(1));
        _random_cast_action = TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext>.StartBuild(_id + "RandomCast")
            .AppendAction(random_cast)
            .Build();
    }

    private void random_cast(ref TimeIntervalTrigger trigger, ref TimeIntervalContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        var salvo_count = entity_modifiers.GetComponent<SalvoCountModifier>().Value;
        for (int i = 0; i < salvo_count; i++)
        {
            var entity = _entity_meta.NewEntity();

            var caster_data = skill_entity.Data;

            var user_ae = caster_data.Get<SkillCaster>().value;
            
            
            
            
            _entity_meta.ApplyModifiers(
                entity,
                user_ae.GetSkillEntityModifiers(_entity_meta.id, _entity_meta.default_modifier_container)
            );
        }
        var cast_count = entity_modifiers.GetComponent<CastCountModifier>().Value;
        if (context.trigger_times >= cast_count)
        {
            skill_entity.AddTag<TagRecycle>();
        }
    }
}