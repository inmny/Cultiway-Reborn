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
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Bloodsucker { get; private set; }
    private void SetupFantasyCreatures()
    {
        Bloodsucker.SetCamp(KingdomAssets.Vampire)
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3)
            .SetAnimSwim(ActorAnimationSequences.swim_0_3)
            .SetIcon("cultiway/icons/races/iconBloodsucker")
            .Stats(S.damage, 30)
            .Stats(S.speed, 40)
            .Stats(S.health, 30);
    }
}