using System;
using System.Reflection;
using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.UI;
using Cultiway.Utils.Extension;
using NeoModLoader.General;
using NeoModLoader.General.Game.extensions;
using strings;
using UnityEngine;

namespace Cultiway.Content;

[Dependency(typeof(ActorJobs), typeof(Architectures), typeof(KingdomAssets))]
public partial class Actors : ExtendLibrary<ActorAsset, Actors>
{

    class CommonCreatureSetupAttribute : Attribute
    {
        
    }
    [CloneSource("$mob$")] public static ActorAsset Plant { get; private set; }

    protected override void OnInit()
    {
        RegisterAssets();
        SetupPlant();
        SetupMing();
        SetupConstraintSpirit();
        SetupFantasyCreatures();
    }

    protected override void GlobalPostInit()
    {
    }

    protected override void ActionAfterCreation(PropertyInfo prop, ActorAsset asset)
    {
        if (prop.GetCustomAttribute<CommonCreatureSetupAttribute>() != null)
        {
            asset.name_locale = asset.id;
            asset.texture_id = asset.id.Replace(".", "/");
            asset.has_advanced_textures = false;
            asset.has_baby_form = false;
            asset.can_turn_into_zombie = false;
            asset.need_colored_sprite = false;
            asset.name_template_sets = [S_NameSet.default_set];
            if (!asset.default_animal && !asset.civ)
            {
                asset.unit_other = true;
            }

            asset.use_phenotypes = false;
        }
    }

    private void SetupPlant()
    {
        Plant.can_turn_into_mush = false;
        Plant.can_turn_into_zombie = false;
        Plant.can_turn_into_ice_one = false;
        Plant.can_turn_into_tumor = false;
        Plant.can_turn_into_demon_in_age_of_chaos = false;
        Plant.run_to_water_when_on_fire = false;
        Plant.inspect_children = false;
        Plant.source_meat = false;
        Plant.color_hex = "#00FF00";
        Plant.base_stats[S.lifespan] = 1000;
        Plant.base_stats[S.speed] = -999999;
        Plant.base_stats._tags?.Remove(S_Tag.needs_food);
        Plant.job = [ActorJobs.PlantXianCultivator.id];
        Plant.actor_size = ActorSize.S0_Bug;
        Plant.shadow_texture = "unitShadow_2";
        Plant.max_random_amount = 1000;
        Plant.name_locale = Plant.id;
        Plant.texture_id = "plant";
        Plant.default_animal = false;
        Plant.civ = false;
        Plant.unit_other = true;
        Plant.has_advanced_textures = false;
        Plant.animation_idle = "walk_0,walk_1,walk_2".Split(',');
        Plant.animation_walk = "walk_0,walk_1,walk_2".Split(',');
        Plant.animation_swim = "walk_0,walk_1,walk_2".Split(',');

        Plant.kingdom_id_wild = SK.nature;

        Plant.GetExtend<ActorAssetExtend>().must_have_element_root = true;
        t = Plant;
        AddPhenotype("skin_light", "default_color");
        AddPhenotype("skin_dark", "default_color");
        AddPhenotype("skin_mixed", "default_color");

        AssetManager.biome_library.ForEach<BiomeAsset, BiomeLibrary>(biome => biome.addUnit(Plant.id));

    }

    protected override void PostInit(ActorAsset asset)
    {
        if (asset.avatar_prefab != string.Empty) asset.has_avatar_prefab = true;
        if (asset.get_override_sprite != null) asset.has_override_sprite = true;
        if (asset.get_override_avatar_frames != null) asset.has_override_avatar_frames = true;
        if (!string.IsNullOrEmpty(asset.architecture_id))
            asset.architecture_asset = AssetManager.architecture_library.get(asset.architecture_id);
        if (asset.spell_ids?.Count > 0)
        {
            asset.spells = new();
            asset.spells.mergeWith(asset.spell_ids);
        }
        if (asset.is_boat) AssetManager.actor_library.list_only_boat_assets.Add(asset);
        if (asset.color_hex != null) asset.color = new Color32?(Toolbox.makeColor(asset.color_hex));
        if (asset.check_flip == null) asset.check_flip = (_, _) => true;
        if (asset.shadow) AssetManager.actor_library.loadShadow(asset);
        if (!asset.isTemplateAsset()) AssetManager.actor_library.loadTexturesAndSprites(asset);
    }
}