using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cultiway.Content;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Cultiway.Content.Patch
{
    internal static class PatchAboutTrain
    {
        [HarmonyPrefix, HarmonyPatch(typeof(WorldTile), nameof(WorldTile.setTopTileType))]
        private static void setTopTileType_prefix(WorldTile __instance, ref TopTileType __state)
        {
            __state = __instance.top_type;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(WorldTile), nameof(WorldTile.setTopTileType))]
        private static void setTopTileType_postfix(WorldTile __instance, TopTileType __state)
        {
            if (__state == TopTileTypes.TrainTrack && __instance.top_type != TopTileTypes.TrainTrack)
            {
                TrainTrackRepairSystem.MarkTileDamaged(__instance);
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Building), "setState")]
        private static void setState_postfix(Building __instance)
        {
            if (__instance.asset == null || __instance.asset.id != Buildings.TrainStation.id)
            {
                return;
            }

            if (__instance.isRemoved() || __instance.isRuin())
            {
                TrainTrackRepairSystem.MarkStationDisabled(__instance);
            }
        }

        private static Dictionary<byte, Tile> _train_track_tiles = new();
        static PatchAboutTrain()
        {
            // 总共用四位来表示连通性，第一位上，第二位下，第三位左，第四位右
            // 至少有两位为1
            LoadTrainTrackTile(0b0011);
            LoadTrainTrackTile(0b0101);
            LoadTrainTrackTile(0b0110);
            LoadTrainTrackTile(0b1001);
            LoadTrainTrackTile(0b1010);
            LoadTrainTrackTile(0b1100);
            LoadTrainTrackTile(0b1110);
            LoadTrainTrackTile(0b0111);
            LoadTrainTrackTile(0b1011);
            LoadTrainTrackTile(0b1101);
            LoadTrainTrackTile(0b1111);
        }
        private static void LoadTrainTrackTile(byte connection)
        {
            Tile tTile = ScriptableObject.CreateInstance<Tile>();
            tTile.name = "TrainTrack_" + Convert.ToString(connection, 2).PadLeft(4, '0');
            tTile.sprite = SpriteTextureLoader.getSprite("tiles/"+ TopTileTypes.TrainTrack.id + "/" + Convert.ToString(connection, 2).PadLeft(4, '0'));
            _train_track_tiles[connection] = tTile;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(WorldTilemap), nameof(WorldTilemap.getVariation))]
        private static bool WorldTilemap_getVariation(WorldTile pTile, ref Tile __result)
        {
            if (pTile.top_type != TopTileTypes.TrainTrack) return true;
            // 根据相邻四格是否是铁轨来确定编号，顺序为 上下左右
            byte connection = 0;
            if (pTile.tile_up != null && pTile.tile_up.top_type == TopTileTypes.TrainTrack)
                connection |= 0b1000;
            if (pTile.tile_down != null && pTile.tile_down.top_type == TopTileTypes.TrainTrack)
                connection |= 0b0100;
            if (pTile.tile_left != null && pTile.tile_left.top_type == TopTileTypes.TrainTrack)
                connection |= 0b0010;
            if (pTile.tile_right != null && pTile.tile_right.top_type == TopTileTypes.TrainTrack)
                connection |= 0b0001;

            if (connection == 0b1000 || connection == 0b0100)
            {
                connection = 0b1100;
            }      
            else if (connection == 0b0010 || connection == 0b0001)
            {
                connection = 0b0011;
            }
            if (_train_track_tiles.TryGetValue(connection, out var tile))
            {
                __result = tile;
                return false;
            }

            return false;
        }
    }
}