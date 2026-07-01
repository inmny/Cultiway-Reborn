using Cultiway.Abstract;

namespace Cultiway.Content;

/// <summary>
/// 11 new biomes. Each BiomeAsset is cloned from <c>biome_grass</c> (which brings
/// <c>spread_biome</c>, <c>grow_vegetation_auto</c>, the grow-type selectors and the
/// spawn lists) and repointed at its own <c><Biome>_high/_low</c> top tiles defined in
/// <see cref="TopTileTypes"/>. <c>generator_pot_amount &gt; 0</c> makes the vanilla
/// <c>BiomeLibrary.linkAssets</c> pool them so they also appear in world generation.
/// </summary>
[Dependency(typeof(TopTileTypes))]
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
        Setup(Bamboo,     "Bamboo",     "Bamboo_high",     "Bamboo_low",     pot);
        Setup(Candle,     "Candle",     "Candle_high",     "Candle_low",     pot);
        Setup(Cemetery,   "Cemetery",   "Cemetery_high",   "Cemetery_low",   pot);
        Setup(Coral,      "Coral",      "Coral_high",      "Coral_low",      pot);
        Setup(Dark,       "Dark",       "Dark_high",       "Dark_low",       pot);
        Setup(Fern,       "Fern",       "Fern_high",       "Fern_low",       pot);
        Setup(FleshBlood, "Flesh Blood","FleshBlood_high", "FleshBlood_low", pot);
        Setup(Knowledge,  "Knowledge",  "Knowledge_high",  "Knowledge_low",  pot);
        Setup(Oak,        "Oak",        "Oak_high",        "Oak_low",        pot);
        Setup(Rice,       "Rice",       "Rice_high",       "Rice_low",       pot);
        Setup(Titans,     "Titans",     "Titans_high",     "Titans_low",     pot);

        // Guarantee mapgen inclusion: manually pool each biome so MapGenerator.generateBiomes
        // can pick them even if vanilla BiomeLibrary.linkAssets already ran before mod init.
        EnsurePooled(Bamboo); EnsurePooled(Candle); EnsurePooled(Cemetery); EnsurePooled(Coral);
        EnsurePooled(Dark);   EnsurePooled(Fern);   EnsurePooled(FleshBlood);
        EnsurePooled(Knowledge); EnsurePooled(Oak); EnsurePooled(Rice); EnsurePooled(Titans);
    }

    private static void EnsurePooled(BiomeAsset asset)
    {
        if (!BiomeLibrary.pool_biomes.Contains(asset))
            AssetManager.biome_library.addBiomeToPool(asset);
    }

    private static void Setup(BiomeAsset asset, string localized_key, string tile_high, string tile_low, int pot)
    {
        asset.localized_key = localized_key;
        asset.tile_high = tile_high;
        asset.tile_low = tile_low;
        asset.generator_pot_amount = pot;
        asset.generator_max_size = 0; // uncapped footprint in mapgen
    }
}
