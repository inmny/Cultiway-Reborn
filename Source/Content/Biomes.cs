using System.Collections.Generic;
using Cultiway.Abstract;

namespace Cultiway.Content;

/// <summary>
/// 11 new biomes. Each BiomeAsset is cloned from <c>biome_grass</c> (which brings
/// <c>spread_biome</c>, <c>grow_vegetation_auto</c>, the grow-type selectors and the
/// spawn lists) and repointed at its own <c><Biome>_high/_low</c> top tiles defined in
/// <see cref="TopTileTypes"/>. <c>generator_pot_amount &gt; 0</c> makes the vanilla
/// <c>BiomeLibrary.linkAssets</c> pool them so they also appear in world generation.
/// </summary>
[Dependency(typeof(TopTileTypes), typeof(Buildings), typeof(Actors))]
public class Biomes : ExtendLibrary<BiomeAsset, Biomes>
{
    [CloneSource("biome_grass"), AssetId("biome_bamboo")]     public static BiomeAsset Bamboo { get; private set; }
    [CloneSource("biome_grass"), AssetId("biome_candle")]     public static BiomeAsset Candle { get; private set; }
    [CloneSource("biome_grass"), AssetId("biome_cemetery")]   public static BiomeAsset Cemetery { get; private set; }
    [CloneSource("biome_grass"), AssetId("biome_coral")]      public static BiomeAsset Coral { get; private set; }
    [CloneSource("biome_grass"), AssetId("biome_dark")]       public static BiomeAsset Dark { get; private set; }
    [CloneSource("biome_grass"), AssetId("biome_fern")]       public static BiomeAsset Fern { get; private set; }
    [CloneSource("biome_grass"), AssetId("biome_fleshblood")] public static BiomeAsset FleshBlood { get; private set; }
    [CloneSource("biome_grass"), AssetId("biome_knowledge")]  public static BiomeAsset Knowledge { get; private set; }
    [CloneSource("biome_grass"), AssetId("biome_oak")]        public static BiomeAsset Oak { get; private set; }
    [CloneSource("biome_grass"), AssetId("biome_rice")]       public static BiomeAsset Rice { get; private set; }
    [CloneSource("biome_grass"), AssetId("biome_titans")]     public static BiomeAsset Titans { get; private set; }

    protected override bool AutoRegisterAssets() => true;

    protected override void OnInit()
    {
        const int pot = 3; // mapgen weight; >0 also gates inclusion in pool_biomes
        Setup(Bamboo,     "Bamboo",     "Bamboo_high",     "Bamboo_low",     pot, "Bamboo_tree",     "Bamboo");
        Setup(Candle,     "Candle",     "Candle_high",     "Candle_low",     pot, "Candle_tree",     "Candle");
        Setup(Cemetery,   "Cemetery",   "Cemetery_high",   "Cemetery_low",   pot, "Cemetery_tree",   "Cemetery");
        Setup(Coral,      "Coral",      "Coral_high",      "Coral_low",      pot, "Coral_tree",      "Coral");
        Setup(Dark,       "Dark",       "Dark_high",       "Dark_low",       pot, "Dark_tree",       "Dark");
        Setup(Fern,       "Fern",       "Fern_high",       "Fern_low",       pot, "Fern_tree",       "Fern");
        Setup(FleshBlood, "Flesh Blood","FleshBlood_high", "FleshBlood_low", pot, "FleshBlood_tree", "FleshBlood");
        Setup(Knowledge,  "Knowledge",  "Knowledge_high",  "Knowledge_low",  pot, "Knowledge_tree",  "Knowledge");
        Setup(Oak,        "Oak",        "Oak_high",        "Oak_low",        pot, "Oak_tree",        "Oak");
        Setup(Rice,       "Rice",       "Rice_high",       "Rice_low",       pot, "Rice_tree",       "Rice");
        Setup(Titans,     "Titans",     "Titans_high",     "Titans_low",     pot, "Titans_tree",     "Titans");

        // Guarantee mapgen inclusion: manually pool each biome so MapGenerator.generateBiomes
        // can pick them even if vanilla BiomeLibrary.linkAssets already ran before mod init.
        EnsurePooled(Bamboo); EnsurePooled(Candle); EnsurePooled(Cemetery); EnsurePooled(Coral);
        EnsurePooled(Dark);   EnsurePooled(Fern);   EnsurePooled(FleshBlood);
        EnsurePooled(Knowledge); EnsurePooled(Oak); EnsurePooled(Rice); EnsurePooled(Titans);

        SetupSpawns();
        SetupExclusions();
    }

    /// <summary>部分群系排除灌木 / 矿物（石头、矿石）。getRandomAssetFromPot 对 null 池子返回 null，故不生成。</summary>
    private static void SetupExclusions()
    {
        // 不生成灌木：烛火/墓园/血肉/巨灵/珊瑚/水稻
        BiomeAsset[] no_bushes = { Candle, Cemetery, FleshBlood, Titans, Coral, Rice };
        foreach (BiomeAsset b in no_bushes)
        {
            b.pot_bushes_spawn = null;
        }
        // 不生成石头和矿石（矿物）：烛火/血肉/水稻
        BiomeAsset[] no_minerals = { Candle, FleshBlood, Rice };
        foreach (BiomeAsset b in no_minerals)
        {
            b.grow_minerals_auto = false;
            b.pot_minerals_spawn = null;
        }
    }

    /// <summary>各群系自动生成的专属生物。addUnit 会置 pot_spawn_units_auto=true。</summary>
    private static void SetupSpawns()
    {
        Bamboo.addUnit(Actors.Panda.id);
        Cemetery.addUnit(Actors.GhostFire.id);            // 鬼火精灵
        Dark.addUnit(Actors.Bloodsucker.id);              // 吸血鬼
        Dark.addUnit(Actors.Werewolf.id);                 // 狼人
        FleshBlood.addUnit("tumor_monster_animal");       // 原版肿瘤塔召唤物
        Oak.addUnit(Actors.OakTreants.id);                // 橡树人
        Titans.addUnit("skeleton");                       // 骷髅
        Candle.addUnit(Actors.CandleGenie.id);            // 烛火精灵
        Coral.addUnit(Actors.FishPeopleShaman.id);        // 鱼人萨满
        Coral.addUnit(Actors.FishPeopleSoldiers.id);      // 鱼人小兵
        Coral.addUnit(Actors.FishPeopleWarrior.id);       // 鱼人战士
        Knowledge.addUnit(Actors.KnowledgeGenie.id);      // 知识精灵
        Rice.addUnit("crab");                             // 螃蟹
        // 蕨类：恐龙
        Fern.addUnit(Actors.Dreadnoughtus.id);            // 无畏巨龙
        Fern.addUnit(Actors.Triceratops.id);              // 三角龙
        Fern.addUnit(Actors.TyrannosaurusRex.id);         // 霸王龙
        Fern.addUnit(Actors.Diplodocus.id);               // 梁龙
        Fern.addUnit(Actors.Pterodactyl.id);              // 翼龙
        Fern.addUnit(Actors.Velociraptor.id);             // 迅猛龙
    }

    private static void EnsurePooled(BiomeAsset asset)
    {
        if (!BiomeLibrary.pool_biomes.Contains(asset))
            AssetManager.biome_library.addBiomeToPool(asset);
    }

    private static void Setup(BiomeAsset asset, string localized_key, string tile_high, string tile_low, int pot, string tree_id, string plant_id)
    {
        asset.localized_key = localized_key;
        asset.tile_high = tile_high;
        asset.tile_low = tile_low;
        asset.generator_pot_amount = pot;
        asset.generator_max_size = 0; // uncapped footprint in mapgen
        // 替换继承自 biome_grass 的植被池：该群系只生长自己的专属树和花草
        //（BuildingActions → grow_type_selector_* → getRandomAssetFromPot(pot_*_spawn)）。
        asset.grow_vegetation_auto = true;
        asset.pot_trees_spawn = new List<string> { tree_id };
        asset.pot_plants_spawn = new List<string> { plant_id };
        // 清掉继承自 biome_grass 的野生动物/智慧生物刷新（狼/狐/羊/鸡/人/矮人...），
        // 之后 SetupSpawns 只把指定生物加回 pot_units_spawn。
        asset.pot_units_spawn = null;
        asset.pot_sapient_units_spawn = null;
    }
}
