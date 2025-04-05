using Cultiway.Abstract;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV2;
using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Components.TrajectoryParams;
using Cultiway.Core.SkillLibV2.Extensions;
using Cultiway.Core.SkillLibV2.Predefined;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;
using Cultiway.Utils;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Skills;

public class HugeSwordQiSkills : ICanInit
{
    public static SkillEntityMeta SwordQiEntity { get; private set; }
    public static SkillEntityMeta SwordQiCaster { get; private set; }
    
    public static TriggerActionMeta<TileCollisionTrigger, TileCollisionContext> SwordQiDamageTileAction { get; private set; } 

    public static TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext> SwordQiDamageObjAction { get; private set; } =
        TriggerActions.GetCollisionDamageActionMeta(new([100, 0, 0, 0, 0, 0, 0, 0]), nameof(SwordQiDamageObjAction));
    public static TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext> SwordQiCasterSpeedupAction { get; private set; }
    public static TriggerActionMeta<TileCollisionTrigger, TileCollisionContext> SpawnSwordQiAction { get; private set; }
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext> StartSwordQiCaster { get; private set; }
    public void Init()
    {
        SwordQiDamageTileAction = TriggerActionMeta<TileCollisionTrigger, TileCollisionContext>
            .StartBuild(nameof(SwordQiDamageTileAction))
            .AppendAction(sword_qi_damage_tile)
            .Build();
        SwordQiCasterSpeedupAction = TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext>
            .StartBuild(nameof(SwordQiCasterSpeedupAction))
            .AppendAction(sword_qi_caster_speed_up)
            .Build();
        SpawnSwordQiAction = TriggerActionMeta<TileCollisionTrigger, TileCollisionContext>
            .StartBuild(nameof(SpawnSwordQiAction))
            .AppendAction(spawn_sword_qi)
            .Build();
        SwordQiEntity = SkillEntityMeta.StartBuild(nameof(SwordQiEntity))
            .AddComponent(new Position())
            .AddSphereTileCollisionTrigger(new TileCollisionTrigger()
            {
                TriggerActionMeta = SwordQiDamageTileAction
            }, 0.1f)
            .AddSphereObjCollisionTrigger(new ObjCollisionTrigger()
            {
                actor = true,
                enemy = true,
                TriggerActionMeta = SwordQiDamageObjAction
            }, 0.1f)
            .AddTimeReachTrigger(60, TriggerActions.GetRecycleActionMeta<TimeReachTrigger, TimeReachContext>())
            .Build();
        SwordQiCaster = SkillEntityMeta.StartBuild(nameof(SwordQiCaster))
            .SetTrajectory(Trajectories.GoForward, 40)
            .AddSphereTileCollisionTrigger(new TileCollisionTrigger()
            {
                TriggerActionMeta = SpawnSwordQiAction
            }, 3f)
            .AddTimeIntervalTrigger(0.1f, SwordQiCasterSpeedupAction)
            .AddTimeReachTrigger(5, TriggerActions.GetRecycleActionMeta<TimeReachTrigger, TimeReachContext>())
            .Build();
        StartSwordQiCaster = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartSwordQiCaster))
            .AppendAction(spawn_sword_qi_caster)
            .Build();
    }

    private void spawn_sword_qi_caster(ref StartSkillTrigger trigger, ref StartSkillContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        var entity = SwordQiCaster.NewEntity();

        var user_ae = context.user;
        var user_pos = user_ae.Base.current_position;
        var target_pos = context.target.current_position;

        var world_rect = new Rect(0, 0, MapBox.width - 1, MapBox.height - 1);
        
        var start_pos = ShapeUtils.FindIntersectPoint(user_pos, (target_pos-user_pos).normalized, world_rect);
        
        var data = entity.Data;
        data.Get<SkillCaster>().value = user_ae;
        data.Get<SkillStrength>().value = context.strength;
        data.Get<Position>().v2 = start_pos;
        data.Get<Rotation>().in_plane = user_pos - target_pos;
        
        
        SwordQiCaster.ApplyModifiers(entity, user_ae.GetSkillEntityModifiers(SwordQiCaster.id, SwordQiCaster.default_modifier_container));
    }

    private void spawn_sword_qi(ref TileCollisionTrigger trigger, ref TileCollisionContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        var tile = context.Tile;
        var caster_data = skill_entity.Data;
        var entity = SwordQiEntity.NewEntity();

        var user_ae = caster_data.Get<SkillCaster>().value;
        var data = entity.Data;
        data.Get<SkillCaster>().value = user_ae;
        data.Get<SkillStrength>().value = caster_data.Get<SkillStrength>().value;
        data.Get<Position>().value = tile.posV;
        
        
        SwordQiEntity.ApplyModifiers(entity, user_ae.GetSkillEntityModifiers(SwordQiEntity.id, SwordQiEntity.default_modifier_container));
    }

    private void sword_qi_caster_speed_up(ref TimeIntervalTrigger trigger, ref TimeIntervalContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        skill_entity.GetComponent<Velocity>().scale += Vector3.one;
    }
    [Hotfixable]
    private void sword_qi_damage_tile(ref TileCollisionTrigger trigger, ref TileCollisionContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        var tile = context.Tile;
        if (tile.Type == WorldboxGame.TileTypes.PitDeepOcean)
        {
            return;
        }

        MapAction.terraformTile(tile, WorldboxGame.TileTypes.PitDeepOcean, null, WorldboxGame.Terraforms.RemoveAll);
    }
}