using Cultiway.Abstract;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Utils.Extension;
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
        ConstraintSpirit.actorSize = ActorSize.S0_Bug;
        ConstraintSpirit.shadowTexture = "unitShadow_2";
        ConstraintSpirit.prefab = "p_unit";
        ConstraintSpirit.nameLocale = "Cultiway.Actor.ConstraintSpirit";
        ConstraintSpirit.has_override_sprite = true;
        ConstraintSpirit.get_override_sprite = actor =>
        {
            return SpriteTextureLoader.getSprite("actors/default_constraint_spirit/walk_0");
        };

        ConstraintSpirit.race = SK.undead;
        ConstraintSpirit.kingdom = SK.undead;
        
        ActorExtend.RegisterActionOnDeath(ae =>
        {
            if (
                ae.Base.asset == ConstraintSpirit ||
                Toolbox.randomChance(ContentSetting.ConstraintSpiritSpawnChance))
            {
                var actor = ae.Base;
                var cs = World.world.units.spawnNewUnit(ConstraintSpirit.id, actor.currentTile);

                cs.GetExtend().CloneAllFrom(ae);

                cs.data.set(ContentActorDataKeys.ConstraintSpiritJob_string,
                    actor.ai?.job.id ?? ActorJobs.RandomMove.id);
            }
        });
    }
}