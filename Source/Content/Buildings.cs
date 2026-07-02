using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Extensions;
using Cultiway.Core.Libraries;
using NeoModLoader.General.Game.extensions;
using strings;
using UnityEngine;

namespace Cultiway.Content;
[Dependency(typeof(Actors), typeof(KingdomAssets))]
public partial class Buildings : ExtendLibrary<BuildingAsset, Buildings>
{
    class CommonBuildingSetupAttribute : Attribute
    {
    }
    [CloneSource(BuildingLibrary.TEMPLATE_CITY_COLORED_BUILDING)]
    public static BuildingAsset TrainStation { get; private set; }

    /// <summary>群系专属树 → 其所属群系 id。PatchBiomeTree 据此把跨群系的树摧毁。</summary>
    public static readonly Dictionary<string, string> BiomeTreeHome = new();

    // ---- Biome-dedicated trees. Cloned from tree_green_1; SetupTree repoints main_path
    // to buildings/<id> so NML loads GameResources/buildings/<Biome>_tree (main_/ruin_/mini_),
    // and self-propagates instead of spreading vanilla tree_green_1.
    [CloneSource("tree_green_1"), AssetId("Bamboo_tree")]     public static BuildingAsset BambooTree { get; private set; }
    [CloneSource("tree_green_1"), AssetId("Candle_tree")]     public static BuildingAsset CandleTree { get; private set; }
    [CloneSource("tree_green_1"), AssetId("Cemetery_tree")]   public static BuildingAsset CemeteryTree { get; private set; }
    [CloneSource("tree_green_1"), AssetId("Coral_tree")]      public static BuildingAsset CoralTree { get; private set; }
    [CloneSource("tree_green_1"), AssetId("Dark_tree")]       public static BuildingAsset DarkTree { get; private set; }
    [CloneSource("tree_green_1"), AssetId("Fern_tree")]       public static BuildingAsset FernTree { get; private set; }
    [CloneSource("tree_green_1"), AssetId("FleshBlood_tree")] public static BuildingAsset FleshBloodTree { get; private set; }
    [CloneSource("tree_green_1"), AssetId("Knowledge_tree")]  public static BuildingAsset KnowledgeTree { get; private set; }
    [CloneSource("tree_green_1"), AssetId("Oak_tree")]        public static BuildingAsset OakTree { get; private set; }
    [CloneSource("tree_green_1"), AssetId("Rice_tree")]       public static BuildingAsset RiceTree { get; private set; }
    [CloneSource("tree_green_1"), AssetId("Titans_tree")]     public static BuildingAsset TitansTree { get; private set; }

    // ---- Biome-dedicated small flora (花草). Cloned from $flora_small$; SetupPlant repoints
    // main_path to buildings/<id> so NML loads GameResources/buildings/<Biome> (main_/mini_).
    // $flora_small$ has has_ruin_state=false and no biome_tags_growth, matching the sprite sets.
    [CloneSource(BuildingLibrary.TEMPLATE_FLORA_SMALL), AssetId("Bamboo")]     public static BuildingAsset BambooPlant { get; private set; }
    [CloneSource(BuildingLibrary.TEMPLATE_FLORA_SMALL), AssetId("Candle")]     public static BuildingAsset CandlePlant { get; private set; }
    [CloneSource(BuildingLibrary.TEMPLATE_FLORA_SMALL), AssetId("Cemetery")]   public static BuildingAsset CemeteryPlant { get; private set; }
    [CloneSource(BuildingLibrary.TEMPLATE_FLORA_SMALL), AssetId("Coral")]      public static BuildingAsset CoralPlant { get; private set; }
    [CloneSource(BuildingLibrary.TEMPLATE_FLORA_SMALL), AssetId("Dark")]       public static BuildingAsset DarkPlant { get; private set; }
    [CloneSource(BuildingLibrary.TEMPLATE_FLORA_SMALL), AssetId("Fern")]       public static BuildingAsset FernPlant { get; private set; }
    [CloneSource(BuildingLibrary.TEMPLATE_FLORA_SMALL), AssetId("FleshBlood")] public static BuildingAsset FleshBloodPlant { get; private set; }
    [CloneSource(BuildingLibrary.TEMPLATE_FLORA_SMALL), AssetId("Knowledge")]  public static BuildingAsset KnowledgePlant { get; private set; }
    [CloneSource(BuildingLibrary.TEMPLATE_FLORA_SMALL), AssetId("Oak")]        public static BuildingAsset OakPlant { get; private set; }
    [CloneSource(BuildingLibrary.TEMPLATE_FLORA_SMALL), AssetId("Rice")]       public static BuildingAsset RicePlant { get; private set; }
    [CloneSource(BuildingLibrary.TEMPLATE_FLORA_SMALL), AssetId("Titans")]     public static BuildingAsset TitansPlant { get; private set; }

    [CloneSource(SB.hall_human_0)]
    public static BuildingAsset SectHall { get; private set; }
    [CloneSource(SB.library_human)]
    public static BuildingAsset SectScripturePavilion { get; private set; }
    protected override bool AutoRegisterAssets() => true;
    protected override void OnInit()
    {
        SetupFantasyBuildings();
        SetupEasternHumanBuildings();
        //SetupMingRaceBuildings();
        SetupBiomeTrees();
        SetupBiomePlants();
        SetupTrainStation();
        SetupSectBuildings();
    }

    private static void SetupBiomeTrees()
    {
        // 默认摇动；以下群系的树不摇动：血肉/墓园/巨灵/烛火/珊瑚/知识
        SetupTree(BambooTree, "biome_bamboo");
        SetupTree(CandleTree, "biome_candle", wobble: false);
        SetupTree(CemeteryTree, "biome_cemetery", wobble: false);
        SetupTree(CoralTree, "biome_coral", wobble: false);
        SetupTree(DarkTree, "biome_dark");
        SetupTree(FernTree, "biome_fern");
        SetupTree(FleshBloodTree, "biome_fleshblood", wobble: false);
        SetupTree(KnowledgeTree, "biome_knowledge", wobble: false);
        SetupTree(OakTree, "biome_oak");
        SetupTree(RiceTree, "biome_rice");
        SetupTree(TitansTree, "biome_titans", wobble: false);

        // 部分树木的资源产出。pNewList:true 替换继承自 tree_green_1 的默认产出。
        CemeteryTree.addResource("stone", 1, pNewList: true);
        CemeteryTree.addResource("wood", 1);

        FleshBloodTree.addResource("meat", 2, pNewList: true);
        FleshBloodTree.addResource("worms", 1);
        FleshBloodTree.addResource("bones", 1);
        FleshBloodTree.addResource("leather", 1); // 皮

        TitansTree.addResource("bones", 1, pNewList: true);
        TitansTree.addResource("common_metals", 1); // 矿石

        CoralTree.addResource("sushi", 1, pNewList: true); // 鱼（无 fish 资源，用 sushi）

        RiceTree.addResource("wheat", 1, pNewList: true);
    }

    private static void SetupTree(BuildingAsset tree, string biome_id, bool wobble = true)
    {
        // tree_green_1 uses main_path "buildings/trees/"; repoint to "buildings/" so sprites
        // resolve from GameResources/buildings/<id>/ where the per-biome art lives.
        tree.main_path = "buildings/";
        // Cloned spread_ids still point at vanilla tree_green_1; make the tree self-propagate.
        tree.spread_ids = new[] { tree.id };
        // 锁定在本群系：禁止自繁殖扩散。植被只由群系 grow_vegetation_auto 在本群系地块上生成
        //（tryGrowVegetationRandom 走 tile 自己的 biome 池），不会蔓延到相邻群系。
        tree.spread_chance = 0f;
        // 记录所属群系，供 PatchBiomeTree 把落到其他群系上的本树摧毁。
        BiomeTreeHome[tree.id] = biome_id;
        // 摇晃来自 material：tree_green_1 的 "tree" 是 wobbly material（随风摇）。不摇动的树改用
        // 静态 "building"（= BuildingRendererSettings.cur_default_material），渲染本身不受影响。
        if (!wobble)
        {
            tree.material = BuildingRendererSettings.material_building;
        }
    }

    private static void SetupBiomePlants()
    {
        SetupPlant(BambooPlant);
        SetupPlant(CandlePlant);
        SetupPlant(CemeteryPlant);
        SetupPlant(CoralPlant);
        SetupPlant(DarkPlant);
        SetupPlant(FernPlant);
        SetupPlant(FleshBloodPlant);
        SetupPlant(KnowledgePlant);
        SetupPlant(OakPlant);
        SetupPlant(RicePlant);
        SetupPlant(TitansPlant);
    }

    private static void SetupPlant(BuildingAsset plant)
    {
        // $flora_small$ uses main_path "buildings/vegetation/"; repoint to "buildings/" so sprites
        // resolve from GameResources/buildings/<id>/ where the per-biome art lives.
        plant.main_path = "buildings/";
        // Concrete vanilla plants set spread_ids to themselves; the template leaves it null.
        plant.spread_ids = new[] { plant.id };
        // 锁定在本群系：禁止自繁殖扩散（同 SetupTree，只由群系 grow 在本地块生成）。
        plant.spread_chance = 0f;
    }

    protected override void ActionAfterCreation(PropertyInfo prop, BuildingAsset asset)
    {
        if (prop.GetCustomAttribute<CommonBuildingSetupAttribute>() != null)
        {
        }
    }

    protected override void PostInit(BuildingAsset asset)
    {
        base.PostInit(asset);
        asset.atlas_asset = AssetManager.dynamic_sprites_library.get(asset.atlas_id);
        
        if (asset.step_action != null)
        {
            asset.has_step_action = true;
        }
        asset.has_biome_tags = asset.biome_tags_growth is { Count: > 0 };
        asset.has_biome_tags_spread = asset.biome_tags_spread is { Count: > 0 };
    }
    private void SetupTrainStation()
    {
        TrainStation.has_sprite_construction = false;
        TrainStation.build_place_batch = false;
        TrainStation.priority = 100;
        TrainStation.group = "train_station";
    }

    private void SetupSectBuildings()
    {
        SetupSectBuildingBase(SectHall, SectConst.BuildingTypeHall);
        SectHall.priority = 110;
        SectHall.cost = new ConstructionCost(10, 5, 0, 30);
        SectHall.base_stats["health"] = 300f;

        SetupSectBuildingBase(SectScripturePavilion, SectConst.BuildingTypeScripturePavilion);
        SectScripturePavilion.priority = 90;
        SectScripturePavilion.cost = new ConstructionCost(0, 12, 2, 50);
        SectScripturePavilion.base_stats["health"] = 350f;
    }

    private static void SetupSectBuildingBase(BuildingAsset asset, string type)
    {
        asset.AsSectBuilding(type);
        asset.main_path = "buildings/sects/";
        asset.kingdom = string.Empty;
        asset.civ_kingdom = string.Empty;
        asset.storage = false;
        asset.storage_only_food = false;
        asset.book_slots = 0;
        asset.can_units_live_here = false;
        asset.housing_slots = 0;
        asset.housing_happiness = 0;
        asset.max_houses = 0;
        asset.loot_generation = 0;
        asset.produce_biome_food = false;
        asset.can_be_upgraded = false;
        asset.upgrade_to = string.Empty;
        asset.upgraded_from = string.Empty;
        asset.upgrade_level = 0;
        asset.ignore_other_buildings_for_upgrade = false;
        asset.can_be_living_house = false;
        asset.can_be_living_plant = false;
    }

    private void SetupEasternHumanBuildings()
    {
        void CloneHuman(string building_id)
        {   
            var asset = Clone(building_id.Replace(SA.human, Actors.EasternHuman.id), building_id);
            asset.main_path = $"buildings/civ_main/{Actors.EasternHuman.id}/";
            asset.group = Actors.EasternHuman.id;
            asset.kingdom = KingdomAssets.NoMadsEasternHuman.id;
            asset.civ_kingdom = KingdomAssets.EasternHuman.id;
            asset.upgrade_to = asset.upgrade_to.Replace(SA.human, Actors.EasternHuman.id);
            asset.upgraded_from = asset.upgraded_from.Replace(SA.human, Actors.EasternHuman.id);
            if (asset.docks)
            {
                PortalLibrary.Dock.Buildings.Add(asset);
            }
        }

        void CloneList(params string[] building_ids)
        {
            foreach (var building_id in building_ids)
                CloneHuman(building_id);
        }
        CloneList(
            SB.watch_tower_human, SB.fishing_docks_human, SB.docks_human, SB.barracks_human, SB.temple_human, 
            SB.windmill_human_0, SB.windmill_human_1, 
            SB.tent_human, SB.house_human_0, SB.house_human_1, SB.house_human_2, SB.house_human_3, SB.house_human_4, SB.house_human_5, 
            SB.hall_human_0, SB.hall_human_1, SB.hall_human_2
        );
/*
        AssetManager.buildings.get($"tent_{Races.Ming.id}").fundament = new BuildingFundament(1,        1, 1,  0);
        AssetManager.buildings.get($"house_{Races.Ming.id}_0").fundament = new BuildingFundament(3,     3, 4,  0);
        AssetManager.buildings.get($"house_{Races.Ming.id}_1").fundament = new BuildingFundament(3,     3, 4,  0);
        AssetManager.buildings.get($"house_{Races.Ming.id}_2").fundament = new BuildingFundament(3,     3, 4,  0);
        AssetManager.buildings.get($"house_{Races.Ming.id}_3").fundament = new BuildingFundament(4,     4, 6,  0);
        AssetManager.buildings.get($"house_{Races.Ming.id}_4").fundament = new BuildingFundament(5,     5, 9,  0);
        AssetManager.buildings.get($"house_{Races.Ming.id}_5").fundament = new BuildingFundament(5,     5, 9,  0);
        AssetManager.buildings.get($"hall_{Races.Ming.id}_0").fundament = new BuildingFundament(4,      4, 7,  0);
        AssetManager.buildings.get($"hall_{Races.Ming.id}_1").fundament = new BuildingFundament(5,      5, 9,  0);
        AssetManager.buildings.get($"hall_{Races.Ming.id}_2").fundament = new BuildingFundament(8,      8, 14, 0);
        AssetManager.buildings.get($"temple_{Races.Ming.id}").fundament = new BuildingFundament(3,      3, 5,  0);
        AssetManager.buildings.get($"barracks_{Races.Ming.id}").fundament = new BuildingFundament(3,    3, 7,  0);
        AssetManager.buildings.get($"windmill_{Races.Ming.id}_0").fundament = new BuildingFundament(2,  1, 2,  0);
        AssetManager.buildings.get($"windmill_{Races.Ming.id}_1").fundament = new BuildingFundament(2,  2, 2,  0);
        AssetManager.buildings.get($"watch_tower_{Races.Ming.id}").fundament = new BuildingFundament(2, 2, 3,  0);
        */
    }
/*
    private void SetupMingRaceBuildings()
    {
        Clone($"bonfire_{Actors.Ming.id}", SB.bonfire);
        t.main_path = "buildings/civ_main/ming";
        t.group = Actors.Ming.id;
        t.civ_kingdom = KingdomAssets.Ming.id;

        
        void CloneHuman(string building_id)
        {
            var asset = Clone(building_id.Replace(SA.human, Actors.Ming.id), building_id);
            asset.main_path = "buildings/civ_main/ming";
            asset.group = Actors.Ming.id;
            asset.civ_kingdom = KingdomAssets.Ming.id;
            asset.upgrade_to = asset.upgrade_to.Replace(SA.human, Actors.Ming.id);
            asset.upgraded_from = asset.upgraded_from.Replace(SA.human, Actors.Ming.id);
        }

        void CloneList(params string[] building_ids)
        {
            foreach (var building_id in building_ids)
                CloneHuman(building_id);
        }
        CloneList(
            SB.watch_tower_human, SB.fishing_docks_human, SB.docks_human, SB.barracks_human, SB.temple_human, 
            SB.windmill_human_0, SB.windmill_human_1, 
            SB.tent_human, SB.house_human_0, SB.house_human_1, SB.house_human_2, SB.house_human_3, SB.house_human_4, SB.house_human_5, 
            SB.hall_human_0, SB.hall_human_1, SB.hall_human_2
        );
/*
        AssetManager.buildings.get($"tent_{Races.Ming.id}").fundament = new BuildingFundament(1,        1, 1,  0);
        AssetManager.buildings.get($"house_{Races.Ming.id}_0").fundament = new BuildingFundament(3,     3, 4,  0);
        AssetManager.buildings.get($"house_{Races.Ming.id}_1").fundament = new BuildingFundament(3,     3, 4,  0);
        AssetManager.buildings.get($"house_{Races.Ming.id}_2").fundament = new BuildingFundament(3,     3, 4,  0);
        AssetManager.buildings.get($"house_{Races.Ming.id}_3").fundament = new BuildingFundament(4,     4, 6,  0);
        AssetManager.buildings.get($"house_{Races.Ming.id}_4").fundament = new BuildingFundament(5,     5, 9,  0);
        AssetManager.buildings.get($"house_{Races.Ming.id}_5").fundament = new BuildingFundament(5,     5, 9,  0);
        AssetManager.buildings.get($"hall_{Races.Ming.id}_0").fundament = new BuildingFundament(4,      4, 7,  0);
        AssetManager.buildings.get($"hall_{Races.Ming.id}_1").fundament = new BuildingFundament(5,      5, 9,  0);
        AssetManager.buildings.get($"hall_{Races.Ming.id}_2").fundament = new BuildingFundament(8,      8, 14, 0);
        AssetManager.buildings.get($"temple_{Races.Ming.id}").fundament = new BuildingFundament(3,      3, 5,  0);
        AssetManager.buildings.get($"barracks_{Races.Ming.id}").fundament = new BuildingFundament(3,    3, 7,  0);
        AssetManager.buildings.get($"windmill_{Races.Ming.id}_0").fundament = new BuildingFundament(2,  1, 2,  0);
        AssetManager.buildings.get($"windmill_{Races.Ming.id}_1").fundament = new BuildingFundament(2,  2, 2,  0);
        AssetManager.buildings.get($"watch_tower_{Races.Ming.id}").fundament = new BuildingFundament(2, 2, 3,  0);
    }*/
}
