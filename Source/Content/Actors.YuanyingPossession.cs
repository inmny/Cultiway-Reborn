using Cultiway.Abstract;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Utils.Extension;
using NeoModLoader.General.Game.extensions;
using strings;

namespace Cultiway.Content;

public partial class Actors
{
    [CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset YuanyingSoul { get; private set; }

    /// <summary>配置只用于致死出逃期间的元婴形态。</summary>
    private void SetupYuanyingSoul()
    {
        YuanyingSoul.SetCamp(KingdomAssets.Spirit)
            .SetAnimWalk("walk_1", "walk_2", "walk_3")
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3")
            .SetAnimIdleRaw("walk_0_0,walk_0_1,walk_0_2,walk_0_3")
            .SetIcon("cultiway/icons/achievements/nascent_soul_formed")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .Stats(S.damage, 1f)
            .Stats(S.damage_range, 0.1f)
            .Stats(S.speed, 28f)
            .Stats(S.health, 50f)
            .Stats(S.armor, 0f)
            .Stats(S.lifespan, 10000f);

        YuanyingSoul.job = [ActorJobs.YuanyingPossession.id];
        YuanyingSoul.civ = false;
        YuanyingSoul.default_animal = false;
        YuanyingSoul.unit_other = true;
        YuanyingSoul.has_soul = false;
        YuanyingSoul.skip_save = true;
        YuanyingSoul.flying = true;
        YuanyingSoul.actor_size = ActorSize.S0_Bug;
        YuanyingSoul.shadow_texture = "unitShadow_2";
        YuanyingSoul.inspect_children = false;
        YuanyingSoul.source_meat = false;
        YuanyingSoul.can_turn_into_zombie = false;
        YuanyingSoul.can_turn_into_ice_one = false;
        YuanyingSoul.can_turn_into_mush = false;
        YuanyingSoul.can_turn_into_tumor = false;
        YuanyingSoul.can_turn_into_demon_in_age_of_chaos = false;
        YuanyingSoul.base_stats._tags?.Remove(S_Tag.needs_food);
    }
}
