using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cultiway.Abstract;
using Cultiway.Utils;
using UnityEngine;

namespace Cultiway.Content
{
    public class TopTileTypes : ExtendLibrary<TopTileType, TopTileTypes>
    {
        protected override bool AutoRegisterAssets()
        {
            return true;
        }

        [CloneSource("road")]
        public static TopTileType TrainTrack { get; private set; }

        // ---- Biome ground tiles (high/low). Cloned from grass so rank_type and tile
        // behavior carry over; biome_id is repointed to the matching BiomeAsset in OnInit.
        // AssetId forces the top-tile id to match the tiles/<id> sprite folder exactly.
        [CloneSource("grass_high"), AssetId("Bamboo_high")]     public static TopTileType BambooHigh { get; private set; }
        [CloneSource("grass_low"),  AssetId("Bamboo_low")]      public static TopTileType BambooLow { get; private set; }
        [CloneSource("grass_high"), AssetId("Candle_high")]     public static TopTileType CandleHigh { get; private set; }
        [CloneSource("grass_low"),  AssetId("Candle_low")]      public static TopTileType CandleLow { get; private set; }
        [CloneSource("grass_high"), AssetId("Cemetery_high")]   public static TopTileType CemeteryHigh { get; private set; }
        [CloneSource("grass_low"),  AssetId("Cemetery_low")]    public static TopTileType CemeteryLow { get; private set; }
        [CloneSource("grass_high"), AssetId("Coral_high")]      public static TopTileType CoralHigh { get; private set; }
        [CloneSource("grass_low"),  AssetId("Coral_low")]       public static TopTileType CoralLow { get; private set; }
        [CloneSource("grass_high"), AssetId("Dark_high")]       public static TopTileType DarkHigh { get; private set; }
        [CloneSource("grass_low"),  AssetId("Dark_low")]        public static TopTileType DarkLow { get; private set; }
        [CloneSource("grass_high"), AssetId("Fern_high")]       public static TopTileType FernHigh { get; private set; }
        [CloneSource("grass_low"),  AssetId("Fern_low")]        public static TopTileType FernLow { get; private set; }
        [CloneSource("grass_high"), AssetId("FleshBlood_high")] public static TopTileType FleshBloodHigh { get; private set; }
        [CloneSource("grass_low"),  AssetId("FleshBlood_low")]  public static TopTileType FleshBloodLow { get; private set; }
        [CloneSource("grass_high"), AssetId("Knowledge_high")]  public static TopTileType KnowledgeHigh { get; private set; }
        [CloneSource("grass_low"),  AssetId("Knowledge_low")]   public static TopTileType KnowledgeLow { get; private set; }
        [CloneSource("grass_high"), AssetId("Oak_high")]        public static TopTileType OakHigh { get; private set; }
        [CloneSource("grass_low"),  AssetId("Oak_low")]         public static TopTileType OakLow { get; private set; }
        [CloneSource("grass_high"), AssetId("Rice_high")]       public static TopTileType RiceHigh { get; private set; }
        [CloneSource("grass_low"),  AssetId("Rice_low")]        public static TopTileType RiceLow { get; private set; }
        [CloneSource("grass_high"), AssetId("Titans_high")]     public static TopTileType TitansHigh { get; private set; }
        [CloneSource("grass_low"),  AssetId("Titans_low")]      public static TopTileType TitansLow { get; private set; }

        protected override void OnInit()
        {
            // Load all biome sprites (icons + tiles + drops) from disk into SpriteTextureLoader
            // before PostInit calls getSpriteList for the tiles below.
            TileTypeBase.last_z = Math.Max(TileTypeBase.last_z, (int)TileZIndexes.grey_goo);

            TrainTrack.walk_multiplier = 1f;
            TrainTrack.setDrawLayer(TileZIndexes.nothing);

            // Repoint each cloned tile's biome_id at the new biome so the tile <-> biome
            // link resolves to our BiomeAsset instead of biome_grass.
            BambooLow.biome_id = "biome_bamboo";
            BambooLow.color_hex = null;
            BambooLow.setDrawLayer(TileZIndexes.nothing);
            BambooHigh.biome_id = "biome_bamboo";
            BambooHigh.color_hex = null;
            BambooHigh.setDrawLayer(TileZIndexes.nothing);

            CandleLow.biome_id = "biome_candle";
            CandleLow.color_hex = null;
            CandleLow.setDrawLayer(TileZIndexes.nothing);
            CandleHigh.biome_id = "biome_candle";
            CandleHigh.color_hex = null;
            CandleHigh.setDrawLayer(TileZIndexes.nothing);

            CemeteryLow.biome_id = "biome_cemetery";
            CemeteryLow.color_hex = null;
            CemeteryLow.setDrawLayer(TileZIndexes.nothing);
            CemeteryHigh.biome_id = "biome_cemetery";
            CemeteryHigh.color_hex = null;
            CemeteryHigh.setDrawLayer(TileZIndexes.nothing);

            CoralLow.biome_id = "biome_coral";
            CoralLow.color_hex = null;
            CoralLow.setDrawLayer(TileZIndexes.nothing);
            CoralHigh.biome_id = "biome_coral";
            CoralHigh.color_hex = null;
            CoralHigh.setDrawLayer(TileZIndexes.nothing);

            DarkLow.biome_id = "biome_dark";
            DarkLow.color_hex = null;
            DarkLow.setDrawLayer(TileZIndexes.nothing);
            DarkHigh.biome_id = "biome_dark";
            DarkHigh.color_hex = null;
            DarkHigh.setDrawLayer(TileZIndexes.nothing);

            FernLow.biome_id = "biome_fern";
            FernLow.color_hex = null;
            FernLow.setDrawLayer(TileZIndexes.nothing);
            FernHigh.biome_id = "biome_fern";
            FernHigh.color_hex = null;
            FernHigh.setDrawLayer(TileZIndexes.nothing);

            FleshBloodLow.biome_id = "biome_fleshblood";
            FleshBloodLow.color_hex = null;
            FleshBloodLow.setDrawLayer(TileZIndexes.nothing);
            FleshBloodHigh.biome_id = "biome_fleshblood";
            FleshBloodHigh.color_hex = null;
            FleshBloodHigh.setDrawLayer(TileZIndexes.nothing);

            KnowledgeLow.biome_id = "biome_knowledge";
            KnowledgeLow.color_hex = null;
            KnowledgeLow.setDrawLayer(TileZIndexes.nothing);
            KnowledgeHigh.biome_id = "biome_knowledge";
            KnowledgeHigh.color_hex = null;
            KnowledgeHigh.setDrawLayer(TileZIndexes.nothing);

            OakLow.biome_id = "biome_oak";
            OakLow.color_hex = null;
            OakLow.setDrawLayer(TileZIndexes.nothing);
            OakHigh.biome_id = "biome_oak";
            OakHigh.color_hex = null;
            OakHigh.setDrawLayer(TileZIndexes.nothing);

            RiceLow.biome_id = "biome_rice";
            RiceLow.color_hex = null;
            RiceLow.setDrawLayer(TileZIndexes.nothing);
            RiceHigh.biome_id = "biome_rice";
            RiceHigh.color_hex = null;
            RiceHigh.setDrawLayer(TileZIndexes.nothing);

            TitansLow.biome_id = "biome_titans";
            TitansLow.color_hex = null;
            TitansLow.setDrawLayer(TileZIndexes.nothing);
            TitansHigh.biome_id = "biome_titans";
            TitansHigh.color_hex = null;
            TitansHigh.setDrawLayer(TileZIndexes.nothing);
        }

        protected override void PostInit(TopTileType asset)
        {
            base.PostInit(asset);
            HashSet<BiomeTag> biome_tags = asset.biome_tags;
            asset.has_biome_tags = biome_tags != null && biome_tags.Count > 0;
            if (asset.color_hex != null)
            {
                asset.color = Toolbox.makeColor(asset.color_hex);
            }
            if (asset.edge_color_hex != null)
            {
                asset.edge_color = Toolbox.makeColor(asset.edge_color_hex);
            }
            if (!string.IsNullOrEmpty(asset.biome_id))
            {
                asset.biome_asset = AssetManager.biome_library.get(asset.biome_id);
            }
            Sprite[] tSpritesArr = SpriteTextureLoader.getSpriteList("tiles/" + asset.id, false);
            if (tSpritesArr?.Length > 0)
            {
                asset.sprites = new TileSprites();
                foreach (Sprite tSprite in tSpritesArr)
                {
                    asset.sprites.addVariation(tSprite, asset.id);
                }

                // color_hex 未配置时，用第一张地块贴图的平均颜色作为地块 color
                if (string.IsNullOrEmpty(asset.color_hex))
                {
                    asset.color = ColorUtils.GetAverageColor(tSpritesArr[0]);
                }
            }
            World.world.tilemap.createTileMapFor(asset);
        }

    }
}
