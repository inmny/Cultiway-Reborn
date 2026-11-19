using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Cultiway.Content;
using Cultiway.Core.Pathfinding;
using Cultiway.Patch;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Cultiway.Content.Patch
{
    internal static class PatchAboutTrain
    {
        /// <summary>新建筑的完整占地避开铁路及周围一格，车站接轨和存档加载除外。</summary>
        /// <param name="__instance">已检查过完整占地的建筑管理器。</param>
        /// <param name="pNewBuildingAsset">准备放置的建筑。</param>
        /// <param name="pType">本次放置用途。</param>
        /// <param name="__result">原版完整占地检查结果。</param>
        [HarmonyPostfix, HarmonyPatch(typeof(BuildingManager), nameof(BuildingManager.canBuildFrom))]
        private static void ReserveRailwayForCityBuildings(BuildingManager __instance, BuildingAsset pNewBuildingAsset,
            BuildPlacingType pType, ref bool __result)
        {
            if (__result && pType == BuildPlacingType.New && !pNewBuildingAsset.flora &&
                pNewBuildingAsset != Buildings.TrainStation)
                __result = !__instance._temp_list_tiles.Any(Railways.RailTrackRoutePlanner.IsReservedForTrack);
        }

        /// <summary>仅保护仍受维护的铁路，废弃轨道沿用原版抽样和道路衰退。</summary>
        /// <param name="instructions">原版道路衰退逻辑。</param>
        [HarmonyTranspiler, HarmonyPatch(typeof(WorldBehaviourActions), nameof(WorldBehaviourActions.updateRoadDecay))]
        private static IEnumerable<CodeInstruction> KeepRailwayOutOfRoadDecay(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            int index = code.FindIndex(c => c.Calls(AccessTools.PropertyGetter(typeof(WorldTile), nameof(WorldTile.Type))));
            code[index].opcode = OpCodes.Call;
            code[index].operand = AccessTools.Method(typeof(PatchAboutTrain), nameof(CanRoadDecay));
            code.RemoveAt(index + 1);
            return code;
        }

        /// <summary>运行、施工和列车占用保护铁轨，失去这些用途后允许自然清理。</summary>
        /// <param name="tile">原版随机抽中的地块。</param>
        private static bool CanRoadDecay(WorldTile tile)
            => tile.Type.road && (tile.top_type != TopTileTypes.TrainTrack || !TrainTrackRepairSystem.IsMaintainedTrack(tile));

        /// <summary>车辆保留原版城市归属，但不计入居民人口。</summary>
        /// <param name="pActor">正在归类的单位。</param>
        [HarmonyPrefix, HarmonyPatch(typeof(City), nameof(City.listUnit))]
        private static bool SkipTrainInCityPopulation(Actor pActor) => pActor.asset != Actors.Train;

        /// <summary>列车不计入国家人口或船队。</summary>
        /// <param name="pActor">正在归类的单位。</param>
        [HarmonyPrefix, HarmonyPatch(typeof(Kingdom), nameof(Kingdom.listUnit))]
        private static bool SkipTrainInKingdomPopulation(Actor pActor) => pActor.asset != Actors.Train;
        /// <summary>直接删除单位不会触发原版受击死亡回调，也必须先下客。</summary>
        /// <param name="__instance">即将被原版移除的单位。</param>
        [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.die))]
        private static void BeforeTrainDeath(Actor __instance)
        {
            if (__instance.asset == Actors.Train)
                TrainTransportSystem.Instance?.CancelTrain(__instance, true);
            else Railways.RailwayPassengerRules.EndJourney(__instance, false);
        }

        /// <summary>在原版保存单位时保存编组与在途行程。</summary>
        /// <param name="__instance">当前单位。</param>
        [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.prepareForSave))]
        private static void SaveTrain(Actor __instance) => Railways.RailwaySaveService.SaveTrain(__instance);

        /// <summary>在原版保存建筑时保存真实铁路连接。</summary>
        /// <param name="__instance">当前建筑。</param>
        [HarmonyPrefix, HarmonyPatch(typeof(Building), nameof(Building.prepareForSave))]
        private static void SaveStation(Building __instance) => Railways.RailwaySaveService.SaveStation(__instance);

        /// <summary>原版恢复乘客前，先恢复车厢及真实轨道。</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(SaveManager), nameof(SaveManager.loadBoatStates))]
        private static void BeforeLoadPassengers() => Railways.RailwaySaveService.BeforeLoadPassengers();

        /// <summary>原版恢复乘客后，恢复同一批列车的行程。</summary>
        [HarmonyPostfix, HarmonyPatch(typeof(SaveManager), nameof(SaveManager.loadBoatStates))]
        private static void AfterLoadPassengers() => Railways.RailwaySaveService.AfterLoadPassengers();

        /// <summary>底层地形也会破坏轨道，不能只监听铁轨贴图。</summary>
        /// <param name="__instance">刚改变地形的格子。</param>
        [HarmonyPostfix, HarmonyPatch(typeof(WorldTile), nameof(WorldTile.setTileType), typeof(TileType), typeof(bool))]
        private static void GroundChanged(WorldTile __instance)
        {
            TrainTrackRepairSystem.MarkTileDamaged(__instance);
        }
        /// <summary>保留变化前的地表，供铁路受损和贴图更新使用。</summary>
        /// <param name="__instance">即将改变地表的格子。</param>
        /// <param name="__state">变化前的地表。</param>
        [HarmonyPrefix, HarmonyPatch(typeof(WorldTile), nameof(WorldTile.setTopTileType))]
        private static void setTopTileType_prefix(WorldTile __instance, ref TopTileType __state)
        {
            __state = __instance.top_type;
        }

        /// <summary>轨道增减时重新绘制自身和四周铁轨，清除上一批施工留下的端头。</summary>
        /// <param name="__instance">刚改变地表的格子。</param>
        /// <param name="__state">变化前的地表。</param>
        [HarmonyPostfix, HarmonyPatch(typeof(WorldTile), nameof(WorldTile.setTopTileType))]
        private static void setTopTileType_postfix(WorldTile __instance, TopTileType __state)
        {
            if (__state != __instance.top_type)
            {
                TrainTrackRepairSystem.MarkTileDamaged(__instance);
                // 读档先恢复地表、后恢复地形；加载结束后原版会统一绘制地图。
                if (!SmoothLoader.isLoading() &&
                    (__state == TopTileTypes.TrainTrack || __instance.top_type == TopTileTypes.TrainTrack))
                {
                    World.world.redrawRenderedTile(__instance);
                    foreach (WorldTile neighbour in __instance.neighbours)
                        if (neighbour.top_type == TopTileTypes.TrainTrack)
                            World.world.redrawRenderedTile(neighbour);
                }
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Building), nameof(Building.setState))]
        private static void setState_postfix(Building __instance)
        {
            BuildingPlaced(__instance);
            if (__instance.asset != Buildings.TrainStation) return;
            Railways.RailwayManualTrackService.RequestScan();
            if (__instance.isRemoved() || __instance.isRuin())
            {
                TrainTrackRepairSystem.MarkStationDisabled(__instance);
            }
            else TrainTrackRepairSystem.RequestRebuild();
        }

        /// <summary>建筑初次设置状态早于占地生成，生成结束后再检查被覆盖的轨道。</summary>
        /// <param name="__instance">已经完成占地登记的新建筑。</param>
        [HarmonyPostfix, HarmonyPatch(typeof(Building), nameof(Building.setBuilding))]
        private static void BuildingPlaced(Building __instance)
        {
            foreach (var tile in __instance.tiles) TrainTrackRepairSystem.MarkTileDamaged(tile);
            if ((__instance.asset.city_building && __instance.city?.hasBuildingType(Buildings.TrainStation.type) == true) ||
                __instance.tiles.Any(t => t.neighbours?.Any(n => n?.building?.asset == Buildings.TrainStation) == true))
                TrainTrackRepairSystem.RequestRebuild();
        }

        /// <summary>易主和领土变化后重新检查实际运营许可。</summary>
        /// <param name="__instance">刚改变归属的建筑。</param>
        [HarmonyPostfix, HarmonyPatch(typeof(Building), nameof(Building.setKingdom))]
        private static void StationOwnerChanged(Building __instance)
        {
            if (__instance.asset == Buildings.TrainStation)
            {
                TrainTrackRepairSystem.RequestRebuild();
                Railways.RailwayManualTrackService.RequestScan();
            }
        }

        /// <summary>领土变化会影响途经线路的许可。</summary>
        [HarmonyPostfix, HarmonyPatch(typeof(TileZone), nameof(TileZone.setCity))]
        private static void TerritoryChanged()
        {
            TrainTrackRepairSystem.RequestRebuild();
            Railways.RailwayManualTrackService.RequestScan();
        }

        private static Dictionary<byte, Tile> _train_track_tiles = new();
        static PatchAboutTrain()
        {
            // 总共用四位来表示连通性，第一位上，第二位下，第三位左，第四位右
            LoadTrainTrackTile(0b0000);
            LoadTrainTrackTile(0b0001);
            LoadTrainTrackTile(0b0010);
            LoadTrainTrackTile(0b0100);
            LoadTrainTrackTile(0b1000);
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
            if (Railways.RailwayStationService.ConnectsTrack(pTile, pTile.tile_up))
                connection |= 0b1000;
            if (Railways.RailwayStationService.ConnectsTrack(pTile, pTile.tile_down))
                connection |= 0b0100;
            if (Railways.RailwayStationService.ConnectsTrack(pTile, pTile.tile_left))
                connection |= 0b0010;
            if (Railways.RailwayStationService.ConnectsTrack(pTile, pTile.tile_right))
                connection |= 0b0001;

            if (_train_track_tiles.TryGetValue(connection, out var tile))
            {
                __result = tile;
                return false;
            }

            return false;
        }
    }
}
