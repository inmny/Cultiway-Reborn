using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV2;
using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Components.TrajectoryParams;
using Cultiway.Core.SkillLibV2.Extensions;
using Cultiway.Core.SkillLibV2.Predefined;
using Cultiway.Core.SkillLibV2.Predefined.Modifiers;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Skills;

public class CommonBladeSkills : ICanInit
{
    public static SkillEntityMeta UntrajedFireBladeEntity;

    public static TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext> FireBladeCollisionActionMeta =
        TriggerActions.GetCollisionDamageActionMeta(new([0, 0, 0, 100, 0, 0, 0, 0]));

    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext> StartSelfSurroundFireBlade;
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext> StartForwardFireBlade;
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext> StartAllFireBlade;

    public void Init()
    {
        UntrajedFireBladeEntity = SkillEntityMeta.StartBuild(nameof(UntrajedFireBladeEntity))
            .AddAnim(SpriteTextureLoader.getSpriteList("cultiway/effect/fire_blade"), 0.1f, 0.2f, false)
            .AddSphereObjCollisionTrigger(new ObjCollisionTrigger
            {
                actor = true,
                building = true,
                enemy = true,
                TriggerActionMeta = FireBladeCollisionActionMeta
            }, 1)
            .SetTrajectory(Trajectories.GoForward, 20, 360)
            .AddTimeReachTrigger(1, TriggerActions.GetRecycleActionMeta<TimeReachTrigger, TimeReachContext>())
            .AllowModifier<ScaleModifier, float>(new ScaleModifier(1))
            .AppendModifierApplication(fire_blade_modifiers_application)
            .Build();
        FireBladeCollisionActionMeta.StartModify()
            .AppendAction(((ref ObjCollisionTrigger trigger, ref ObjCollisionContext context, Entity entity,
                Entity                              modifiers) =>
            {
                var target = context.obj;
                if (!target.isAlive()) return;
                target.addStatusEffect(WorldboxGame.StatusEffects.Burning.id);
            }));
        StartSelfSurroundFireBlade = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartSelfSurroundFireBlade))
            .AppendAction(spawn_self_surround_fire_blade)
            .Build();
        StartForwardFireBlade = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartForwardFireBlade))
            .AppendAction(spawn_forward_fire_blade)
            .Build();
    }

    private void fire_blade_modifiers_application(Entity entity, Entity modifiers)
    {
        var data = entity.Data;
        var modifiers_data = modifiers.Data;
        
        var scale_mod = modifiers_data.Get<ScaleModifier>().Value;
        data.Get<Scale>().value *= scale_mod;
        foreach (Entity trigger_entity in entity.ChildEntities)
        {
            if (trigger_entity.HasComponent<ColliderSphere>())
                trigger_entity.GetComponent<ColliderSphere>().radius *= scale_mod;
        }
    }
    [Hotfixable]
    private void spawn_forward_fire_blade(ref StartSkillTrigger trigger, ref StartSkillContext context, Entity skill_entity, Entity modifiers)
    {
        Entity entity = UntrajedFireBladeEntity.NewEntity();
        
        ActorExtend user_ae = context.user;
        Actor user = user_ae.Base;
        
        var data = entity.Data;
        data.Get<SkillCaster>().value = user_ae;
        data.Get<SkillStrength>().value = context.strength;
        data.Get<Position>().value = user.currentPosition;
        data.Get<Trajectory>().meta = Trajectories.GoForward;
        if (!skill_entity.IsNull && skill_entity.TryGetComponent(out Rotation rot))
            data.Get<Rotation>().value = rot.value;
        else
        {
            data.Get<Rotation>().Setup(user, context.target);
        }

        var modifiers_data = modifiers.Data;

        foreach (Entity trigger_entity in entity.ChildEntities)
        {
            if (trigger_entity.HasComponent<TimeReachTrigger>())
                trigger_entity.GetComponent<TimeReachTrigger>().target_time *= 4;
        }
        UntrajedFireBladeEntity.ApplyModifiers(entity, context.user.GetSkillEntityModifiers(UntrajedFireBladeEntity.id, UntrajedFireBladeEntity.default_modifier_container));
    }

    [Hotfixable]
    private void spawn_self_surround_fire_blade(
        ref StartSkillTrigger trigger,
        ref StartSkillContext context,
        Entity                starter_entity,
        Entity                modifiers)
    {
        Entity entity = UntrajedFireBladeEntity.NewEntity();
        
        ActorExtend user_ae = context.user;
        Actor user = user_ae.Base;
        var radius = Toolbox.DistVec2Float(user.currentPosition, context.target.currentPosition);
        entity.AddComponent(new SurroundRadius(radius));
        
        var data = entity.Data;
        data.Get<SkillCaster>().value = user_ae;
        data.Get<SkillStrength>().value = context.strength;
        data.Get<Position>().value = user.currentPosition;
        data.Get<Trajectory>().meta = Trajectories.SelfSurround;
        data.Get<Velocity>().scale.Scale(Vector3.one * Mathf.Sqrt(radius));

        var modifiers_data = modifiers.Data;

        foreach (Entity trigger_entity in entity.ChildEntities)
        {
            if (trigger_entity.HasComponent<TimeReachTrigger>())
                trigger_entity.GetComponent<TimeReachTrigger>().target_time *= radius * Mathf.PI * data.Get<Velocity>().scale.magnitude;
        }
        UntrajedFireBladeEntity.ApplyModifiers(entity, context.user.GetSkillEntityModifiers(UntrajedFireBladeEntity.id, UntrajedFireBladeEntity.default_modifier_container));
    }
}