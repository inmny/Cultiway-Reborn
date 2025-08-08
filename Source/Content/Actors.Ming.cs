using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using MathNet.Numerics;
using NeoModLoader.api.attributes;
using NeoModLoader.General.Game.extensions;
using strings;

namespace Cultiway.Content;

public partial class Actors 
{/*
    [CloneSource("$civ_advanced_unit$")] public static ActorAsset Ming { get; private set; }
    [CloneSource(SA.boat_trading_human)] public static ActorAsset MingBoatTrading { get; private set; }
    [CloneSource(SA.boat_transport_human)] public static ActorAsset MingBoatTransport { get; private set; }*/
    private void SetupMing()
    {/*
        t = Ming;
        t.name_template_sets = [S_NameSet.human_default_set];
        t.build_order_template_id = BuildingOrders.Classic.id;
        t.music_theme = "Humans_Neutral";
        t.texture_id = Ming.id;
        t.kingdom_id_wild = KingdomAssets.Ming.id;
        t.kingdom_id_civilization = KingdomAssets.NoMadsMing.id;
        t.name_locale = Ming.id;
        t.texture_asset = new ActorTextureSubAsset("actors/species/civs/Cultiway.Ming/", true)
        {
            render_heads_for_children = false
        };
        t.render_heads_for_babies = false;
        // TODO: 分类
        t.icon = "iconMing";
        t.color_hex = "#486A8E";
        t.can_turn_into_zombie = false;
        t.can_turn_into_demon_in_age_of_chaos = false;
        t.can_turn_into_ice_one = false;
        t.can_turn_into_mush = false;
        t.can_turn_into_tumor = false;
        t.disable_jump_animation = true;
        //t.actor_asset_id_trading = MingBoatTrading.id;
        //t.actor_asset_id_transport = MingBoatTransport.id;
        t.base_stats[S.mass_2] = 65;
        t.addGenome(new ValueTuple<string, float>[]
        {
            new("health", 100f),
            new("stamina", 100f),
            new("bonus_sex_random", 2f),
            new("bad", 2f),
            new("lifespan", 70f),
            new("damage", 15f),
            new("speed", 10f),
            new("offspring", 5f),
            new("diplomacy", 3f),
            new("warfare", 3f),
            new("stewardship", 3f),
            new("intelligence", 3f)
        });
        t.addSubspeciesTrait("reproduction_strategy_viviparity");
        t.addSubspeciesTrait("gestation_long");
        t.addSubspeciesTrait("reproduction_sexual");
        t.addSubspeciesTrait("bad_genes");
        t.addSubspeciesTrait("advanced_hippocampus");
        t.addSubspeciesTrait("stomach");
        t.addSubspeciesTrait("amygdala");
        t.addSubspeciesTrait("wernicke_area");
        t.addSubspeciesTrait("diet_omnivore");
        t.addSubspeciesTrait("polyphasic_sleep");
        t.addSubspeciesTrait("nocturnal_dormancy");
        t.addClanTrait("divine_dozen");
        t.addCultureTrait("city_layout_the_grand_arrangement");
        t.addCultureTrait("city_layout_stone_garden");
        t.addCultureTrait("roads");
        t.addCultureTrait("statue_lovers");
        t.addCultureTrait("pep_talks");
        t.addCultureTrait("youth_reverence");
        t.addCultureTrait("expansionists");
        t.addLanguageTrait("nicely_structured_grammar");
        t.addReligionTrait("bloodline_bond");
        t.addReligionTrait("rite_of_roaring_skies");
        t.addReligionTrait("cast_shield");
        t.addTrait("ambitious");
        t.production = new string[] { "bread", "pie" };
        AddPhenotype("skin_light", "default_color");
        AddPhenotype("skin_dark", "default_color");
        AddPhenotype("skin_mixed", "default_color");*/
    }

    private void AddPhenotype(string phenotype, string color)
    {
        AssetManager.actor_library.t = t;
        AssetManager.actor_library.addPhenotype(phenotype, color);
    }
}