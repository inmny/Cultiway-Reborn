using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Attributes;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using NeoModLoader.General.Game.extensions;
using strings;

namespace Cultiway.Content;

public partial class Actors
{
    // 对于一个生物，就是直接这边创建一个类似Bloodsucker的玩意，包括上面方括号的内容
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Bloodsucker { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Anubis { get; private set; }
    private void SetupFantasyCreatures()
    {
        Bloodsucker.SetCamp(KingdomAssets.Vampire)
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3)
            .SetAnimSwim(ActorAnimationSequences.swim_0_3)
            .SetIcon("cultiway/icons/races/iconBloodsucker")
            .Stats(S.damage, 30)
            .Stats(S.speed, 40)
            .Stats(S.health, 30);
        Anubis.SetCamp(KingdomAssets.Undead)
            .SetAnimWalk(S_Anim.walk_0, S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/Anubis/main/walk_0")
            .SetJumpAnimation(false)
            .SetDefaultWeapons(S_Item.evil_staff)
            .Stats(S.damage, 100)
            .Stats(S.scale, 0.2f);
    }
}