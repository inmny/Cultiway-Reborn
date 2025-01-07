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
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext> StartAllFireBlade;

    public void Init()
    {
        UntrajedFireBladeEntity = SkillEntityMeta.StartBuild()
            .AddAnim(SpriteTextureLoader.getSpriteList("cultiway/effect/fire_blade"), 0.1f, 0.2f, false)
            .AddSphereObjCollisionTrigger(new ObjCollisionTrigger
            {
                actor = true,
                enemy = true,
                TriggerActionMeta = FireBladeCollisionActionMeta
            }, 1)
            .SetTrajectory(Trajectories.GoForward, 20, 360)
            .AddTimeReachTrigger(1, TriggerActions.GetRecycleActionMeta<TimeReachTrigger, TimeReachContext>())
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
            .AllowModifier<ScaleModifier, float>(new ScaleModifier(1))
            .Build();
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
        var scale = modifiers_data.Get<ScaleModifier>().Value;
        data.Get<Scale>().value *= scale;

        foreach (Entity trigger_entity in entity.ChildEntities)
        {
            if (trigger_entity.HasComponent<ColliderSphere>())
                trigger_entity.GetComponent<ColliderSphere>().radius *= scale;
            if (trigger_entity.HasComponent<TimeReachTrigger>())
                trigger_entity.GetComponent<TimeReachTrigger>().target_time *= radius * Mathf.PI * data.Get<Velocity>().scale.magnitude;
        }
    }
}