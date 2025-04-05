using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using NeoModLoader.General.Game.extensions;

namespace Cultiway.Content;

public partial class Actors 
{
    [CloneSource("$mob$")] public static ActorAsset ConstraintSpirit { get; private set; }

    private void SetupConstraintSpirit()
    {
        ConstraintSpirit.can_turn_into_mush = false;
        ConstraintSpirit.can_turn_into_zombie = false;
        ConstraintSpirit.can_turn_into_ice_one = false;
        ConstraintSpirit.can_turn_into_tumor = false;
        ConstraintSpirit.can_turn_into_demon_in_age_of_chaos = false;
        ConstraintSpirit.run_to_water_when_on_fire = false;
        ConstraintSpirit.inspect_children = false;
        ConstraintSpirit.source_meat = false;
        ConstraintSpirit.base_stats[S.lifespan] = 10000;
        ConstraintSpirit.base_stats._tags?.Remove(S_Tag.needs_food);
        ConstraintSpirit.job = [ActorJobs.RandomMove.id];
        ConstraintSpirit.actor_size = ActorSize.S0_Bug;
        ConstraintSpirit.shadow_texture = "unitShadow_2";
        ConstraintSpirit.name_locale = "Cultiway.ConstraintSpirit";
        ConstraintSpirit.kingdom_id_wild = KingdomAssets.NoMadsMing.id;
        ConstraintSpirit.texture_id = "default_constraint_spirit";
        ConstraintSpirit.animation_walk = "walk_0,walk_1,walk_2,walk_3".Split(',');
        ConstraintSpirit.animation_swim = "walk_0,walk_1,walk_2,walk_3".Split(',');
        ConstraintSpirit.animation_idle = "walk_0".Split(',');

        //ConstraintSpirit.kingdom = KingdomAssets.Ming.id;
        
        ActorExtend.RegisterActionOnDeath([Hotfixable](ae) =>
        {
            var actor = ae.Base;
            if (!actor.asset.has_soul) return;
            if (
                actor.asset == ConstraintSpirit ||
                Randy.randomChance(ContentSetting.ConstraintSpiritSpawnChance))
            {
                WorldTile tile_to_spawn;
                if (actor.asset == ConstraintSpirit)
                {
                    var home_building_id = actor.data.homeBuildingID;
                    var home_building = World.world.buildings.get(home_building_id);
                    if (home_building == null)
                    {
                        return;
                    }
                    tile_to_spawn = home_building.current_tile;
                }
                else
                {
                    tile_to_spawn = actor.current_tile;
                }

                if (World.world.units.cloneUnit(actor, tile_to_spawn))
                {
                    var cs = World.world.units.get(World.world.map_stats.id_unit - 1);
                    cs.GetExtend().CloneLeftFrom(ae);
                    cs.GetExtend().AddComponent(new ConstraintSpirit());

                    if (actor.citizen_job != null)
                        cs.data.set(ContentActorDataKeys.ConstraintSpiritCitizenJob_string,
                            actor.citizen_job.id);
                    cs.data.set(ContentActorDataKeys.ConstraintSpiritJob_string,
                        actor.ai?.job?.id ?? ActorJobs.RandomMove.id);
                }
            
            }
        });
    }
}