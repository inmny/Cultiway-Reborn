using Cultiway.Abstract;
using Cultiway.Content.Extensions;
using NeoModLoader.General.Game.extensions;
using strings;

namespace Cultiway.Content;

public partial class Actors
{
    /// <summary>化神肉身毁灭后由原人物身份使用的无身元神资产。</summary>
    [CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset BodilessYuanshen { get; private set; }

    /// <summary>配置不带元婴寻主与寿尽规则的无身元神形态。</summary>
    private void SetupBodilessYuanshen()
    {
        BodilessYuanshen.SetCamp(KingdomAssets.Spirit)
            .SetAnimWalk("walk_0", "walk_1", "walk_2", "walk_3")
            .SetAnimSwimRaw("walk_0,walk_1,walk_2,walk_3")
            .SetAnimIdleRaw("walk_0")
            .SetIcon("cultiway/icons/artifact_atoms/soul_core")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .Stats(S.damage, 1f)
            .Stats(S.damage_range, 0.1f)
            .Stats(S.speed, 24f)
            .Stats(S.health, 120f)
            .Stats(S.armor, 0f)
            .Stats(S.lifespan, 10000f);

        BodilessYuanshen.texture_asset = new ActorTextureSubAsset("actors/default_constraint_spirit/", false)
        {
            texture_path_main = "actors/default_constraint_spirit",
            texture_heads = string.Empty
        };
        BodilessYuanshen.job = [ActorJobs.RandomMove.id];
        BodilessYuanshen.civ = false;
        BodilessYuanshen.default_animal = false;
        BodilessYuanshen.unit_other = true;
        BodilessYuanshen.has_soul = false;
        BodilessYuanshen.skip_save = true;
        BodilessYuanshen.flying = true;
        BodilessYuanshen.actor_size = ActorSize.S0_Bug;
        BodilessYuanshen.shadow_texture = "unitShadow_2";
        BodilessYuanshen.inspect_children = false;
        BodilessYuanshen.source_meat = false;
        BodilessYuanshen.can_turn_into_zombie = false;
        BodilessYuanshen.can_turn_into_ice_one = false;
        BodilessYuanshen.can_turn_into_mush = false;
        BodilessYuanshen.can_turn_into_tumor = false;
        BodilessYuanshen.can_turn_into_demon_in_age_of_chaos = false;
        BodilessYuanshen.base_stats._tags?.Remove(S_Tag.needs_food);
    }
}
