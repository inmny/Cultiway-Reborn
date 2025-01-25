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
    [CloneSource("_mob")] public static ActorAsset ConstraintSpirit { get; private set; }

    private void SetupConstraintSpirit()
    {
        ConstraintSpirit.canTurnIntoMush = false;
        ConstraintSpirit.canTurnIntoZombie = false;
        ConstraintSpirit.canTurnIntoIceOne = false;
        ConstraintSpirit.canTurnIntoTumorMonster = false;
        ConstraintSpirit.can_turn_into_demon_in_age_of_chaos = false;
        ConstraintSpirit.run_to_water_when_on_fire = false;
        ConstraintSpirit.inspect_children = false;
        ConstraintSpirit.needFood = false;
        ConstraintSpirit.animal = false;
        ConstraintSpirit.procreate = false;
        ConstraintSpirit.source_meat = false;
        ConstraintSpirit.base_stats[S.max_age] = 10000;
        ConstraintSpirit.job = ActorJobs.RandomMove.id;
        ConstraintSpirit.canBeCitizen = true;
        ConstraintSpirit.actorSize = ActorSize.S0_Bug;
        ConstraintSpirit.shadowTexture = "unitShadow_2";
        ConstraintSpirit.prefab = "p_unit";
        ConstraintSpirit.nameLocale = "Cultiway.Actor.ConstraintSpirit";
        ConstraintSpirit.race = Races.Ming.id;
        ConstraintSpirit.texture_path = "default_constraint_spirit";
        ConstraintSpirit.animation_walk = "walk_0,walk_1,walk_2,walk_3";
        ConstraintSpirit.animation_swim = "walk_0,walk_1,walk_2,walk_3";
        ConstraintSpirit.animation_idle = "walk_0";

        //ConstraintSpirit.kingdom = KingdomAssets.Ming.id;
        
        ActorExtend.RegisterActionOnDeath([Hotfixable](ae) =>
        {
            var actor = ae.Base;
            if (!actor.asset.has_soul) return;
            if (
                actor.asset == ConstraintSpirit ||
                Toolbox.randomChance(ContentSetting.ConstraintSpiritSpawnChance))
            {
                WorldTile tile_to_spawn;
                if (actor.asset == ConstraintSpirit)
                {
                    var home_building_id = actor.data.homeBuildingID;
                    if (string.IsNullOrEmpty(home_building_id))
                    {
                        return;
                    }
                    var home_building = World.world.buildings.get(home_building_id);
                    if (home_building == null)
                    {
                        return;
                    }
                    tile_to_spawn = home_building.currentTile;
                }
                else
                {
                    tile_to_spawn = actor.currentTile;
                }
                var cs = World.world.units.spawnNewUnit(ConstraintSpirit.id, tile_to_spawn);

                cs.GetExtend().CloneAllFrom(ae);
                cs.GetExtend().AddComponent(new ConstraintSpirit());

                if (actor.citizen_job != null)
                    cs.data.set(ContentActorDataKeys.ConstraintSpiritCitizenJob_string,
                    actor.citizen_job.id);
                cs.data.set(ContentActorDataKeys.ConstraintSpiritJob_string,
                    actor.ai?.job?.id ?? ActorJobs.RandomMove.id);
            }
        });
    }
}