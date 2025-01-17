using Cultiway.Abstract;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV2;
using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Extensions;
using Cultiway.Core.SkillLibV2.Predefined;
using Cultiway.Core.SkillLibV2.Predefined.Modifiers;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Skills;

public class GroundThornSkills : ICanInit
{
    public static SkillEntityMeta SingleGroundThornEntity { get; private set; }

    public static TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext> GroundThornDamageActionMeta
    {
        get;
        private set;
    } = TriggerActions.GetCollisionDamageActionMeta(new([0, 0, 0, 0, 100, 0, 0, 0]),
        nameof(GroundThornDamageActionMeta));
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext> StartSingleGroundThorn { get; private set; }
    public void Init()
    {
        SingleGroundThornEntity = SkillEntityMeta.StartBuild(nameof(SingleGroundThornEntity))
            .AddAnim(SpriteTextureLoader.getSpriteList("cultiway/effect/ground_thorn"), 0.3f, 0.2f, false)
            .AddSphereObjCollisionTrigger(new ObjCollisionTrigger()
            {
                actor = true,
                building = true,
                enemy = true,
                TriggerActionMeta = GroundThornDamageActionMeta
            }, 0.5f)
            .AddTimeReachTrigger(1, TriggerActions.GetRecycleActionMeta<TimeReachTrigger, TimeReachContext>())
            .AllowModifier<ScaleModifier, float>(new ScaleModifier(1))
            .Build();

        StartSingleGroundThorn = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartSingleGroundThorn))
            .AppendAction(spawn_single_ground_thorn)
            .AllowModifier<SalvoCountModifier, int>(new SalvoCountModifier(1))
            .Build();
    }

    private void spawn_single_ground_thorn(ref StartSkillTrigger trigger, ref StartSkillContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        var salvo_count = action_modifiers.GetComponent<SalvoCountModifier>().Value;
        for (int i = 0; i < salvo_count; i++)
        {
            var entity = SingleGroundThornEntity.NewEntity();

            var user_ae = context.user;
            
            var data = entity.Data;
            data.Get<SkillCaster>().value = user_ae;
            data.Get<SkillStrength>().value = context.strength;
            data.Get<Position>().value = context.target.currentPosition;
            if (i != 0)
            {
                var edge = Mathf.Sqrt(salvo_count);
                data.Get<Position>().v2 += Toolbox.randomPointOnCircle(0, edge);
            }

            SingleGroundThornEntity.ApplyModifiers(entity,
                user_ae.GetSkillEntityModifiers(SingleGroundThornEntity.id,
                    SingleGroundThornEntity.default_modifier_container));
        }
    }
}