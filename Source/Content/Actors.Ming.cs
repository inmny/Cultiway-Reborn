using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using MathNet.Numerics;
using NeoModLoader.api.attributes;
using NeoModLoader.General.Game.extensions;

namespace Cultiway.Content;

public partial class Actors 
{
    [CloneSource("unit_human"), AssetId($"unit_Cultiway.Actor.{nameof(Ming)}")] public static ActorAsset Ming { get; private set; }

    private void SetupMing()
    {
        Ming.nameLocale = Races.Ming.nameLocale;
        Ming.race = Races.Ming.id;
        Ming.icon = "iconMings";
        Ming.useSkinColors = false;
        Ming.color = Toolbox.makeColor("#005E72");
        Ming.body_separate_part_head = false;
        Ming.traits = new List<string>
        {
        };
        AssetManager.actor_library.t = Ming;
        AssetManager.actor_library.addColorSet(S_SkinColor.human_default);

        ActorAsset baby_asset =
            Clone(Ming.id.Replace("unit", "baby"),
                Ming.id);

        baby_asset.take_items = false;
        baby_asset.use_items = false;
        baby_asset.base_stats[S.speed] = 10f;
        baby_asset.traits = new List<string>
        {
        };
        baby_asset.can_turn_into_demon_in_age_of_chaos = false;
        baby_asset.years_to_grow_to_adult = 18;
        baby_asset.baby = true;
        baby_asset.growIntoID = Ming.id;
        baby_asset.animation_idle = "walk_3";
        baby_asset.traits.Add("peaceful");
        AssetManager.actor_library.cloneColorSetFrom(Ming.id);
    }
}