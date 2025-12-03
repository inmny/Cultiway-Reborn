using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cultiway.Abstract;
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

        protected override void OnInit()
        {
            TrainTrack.walk_multiplier = 1f;
            TrainTrack.render_z = 999;
            TrainTrack.draw_layer_name = TrainTrack.id;
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
            }
            World.world.tilemap.createTileMapFor(asset);
        }
    }
}
