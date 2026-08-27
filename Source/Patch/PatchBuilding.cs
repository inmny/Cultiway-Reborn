using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Content;
using Cultiway.Content.Const;
using Cultiway.Core.BuildingComponents;
using Cultiway.Utils.Extension;
using HarmonyLib;

namespace Cultiway.Patch;

internal static class PatchBuilding
{
    private const int UpgradePriorityNatural = 0;
    private const int UpgradePriorityStatue = 1;
    private const int UpgradePriorityWall = 2;
    private const int UpgradePriorityHouse = 3;
    private const int UpgradePriorityFunctional = 4;
    private const int UpgradePriorityHall = 5;

    /// <summary>东方人族大厅只有在所属文化选择现代风格后，才能从第 3 级升到第 4 级（内部编号 2→3）。</summary>
    [HarmonyPrefix, HarmonyPatch(typeof(Building), nameof(Building.canBeUpgraded))]
    private static bool canBeUpgraded_eastern_hall_modern_prefix(Building __instance, ref bool __result)
    {
        string hall2Id = $"hall_{Actors.EasternHuman.id}_2";
        if (__instance?.asset?.id != hall2Id) return true;
        if (EasternHumanSkinStyles.IsSelected(__instance.city?.culture, "modern")) return true;

        __result = false;
        return false;
    }

    /// <summary>让建筑命中与单位命中共享当前原版攻击的临时伤害倍率。</summary>
    [HarmonyPrefix, HarmonyPatch(typeof(Building), nameof(Building.getHit))]
    private static void getHit_damage_scale_prefix(ref float pDamage)
    {
        pDamage = AttackDamageScaleContext.Apply(pDamage);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Building), nameof(Building.getHit))]
    private static void getHit_skaven_alert_postfix(Building __instance, float pDamage, BaseSimObject pAttacker)
    {
        if (pDamage > 0f && __instance.asset == Buildings.SkavenBlight &&
            SkavenEvolution.IsHostile(pAttacker, __instance.kingdom))
        {
            SkavenPackService.AlertNest(__instance, pAttacker);
        }
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(Building), nameof(Building.setBuilding))]
    private static IEnumerable<CodeInstruction> setBuilding_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();

        var index = list.FindIndex(x =>
            x.opcode == OpCodes.Ldfld && (x.operand as FieldInfo)?.Name == nameof(BuildingAsset.tower));
        if (index != -1)
        {
            var insert_idx = index - 1;
            var old_instruction = list[insert_idx];
            list.InsertRange(insert_idx, new []
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PatchBuilding), nameof(checkMoreBuildingComponents)))
            });
            old_instruction.MoveLabelsTo(list[insert_idx]);
        }
        return list;
    }

    private static void checkMoreBuildingComponents(Building building)
    {
        var bae = building.asset.GetExtend<BuildingAssetExtend>();
        if (bae.advanced_unit_spawner)
        {
            building.addComponent<AdvancedUnitSpawner>().Setup(bae.advanced_unit_spawner_config);
        }
        foreach (var portal_asset in ModClass.L.PortalLibrary.list)
        {
            if (portal_asset.Buildings.Contains(building.asset))
            {
                Portal portal = building.GetBuildingComponent<Portal>();
                if (portal == null)
                {
                    portal = building.addComponent<Portal>();
                    portal.Asset = portal_asset;
                }
                portal_asset.RequestRebuildGraph?.Invoke(portal);
                break;
            }
        }
    }
    [HarmonyPrefix, HarmonyPatch(typeof(Building), nameof(Building.startRemove))]
    private static void startRemove_prefix(Building __instance)
    {
        if (__instance.isOnRemove()) return;
        __instance.asset.GetExtend<BuildingAssetExtend>().action_on_removed
            ?.Invoke(__instance, __instance.current_tile);
    }
    [HarmonyPrefix, HarmonyPatch(typeof(Building), nameof(Building.makeRuins))]
    private static void makeRuins_prefix(Building __instance)
    {
        __instance.asset.GetExtend<BuildingAssetExtend>().action_on_ruins?.Invoke(__instance, __instance.current_tile);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Building), nameof(Building.startRemove))]
    private static void startRemove_postfix(Building __instance)
    {
        ClearWallsIfBonfire(__instance);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Building), nameof(Building.makeRuins))]
    private static void makeRuins_postfix(Building __instance)
    {
        ClearWallsIfBonfire(__instance);
    }

    /// <summary>篝火（城市核心）被摧毁/变废墟时，立即清除该城市的全部城墙（不等下次谋划）。</summary>
    private static void ClearWallsIfBonfire(Building b)
    {
        if (b?.asset == null || b.city == null) return;
        if (b.asset.type == "type_bonfire") Plots.ClearCityWalls(b.city);
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Building), nameof(Building.upgradeBuilding))]
    private static bool upgradeBuilding_clear_lower_priority_prefix(Building __instance, ref bool __result)
    {
        if (!__instance.canBeUpgraded()) return true;
        BuildingAsset target = AssetManager.buildings.get(__instance.asset.upgrade_to);
        if (target == null || HasSameFootprint(__instance.asset, target)) return true;
        if (!TryClearUpgradeFootprint(__instance, target))
        {
            __result = false;
            return false;
        }
        return true;
    }

    private static bool TryClearUpgradeFootprint(Building upgrading, BuildingAsset target)
    {
        return CheckUpgradeFootprint(upgrading, target, true);
    }

    internal static bool CanClearUpgradeFootprint(Building upgrading)
    {
        if (upgrading == null || !upgrading.canBeUpgraded()) return false;
        BuildingAsset target = AssetManager.buildings.get(upgrading.asset.upgrade_to);
        return target != null && (HasSameFootprint(upgrading.asset, target)
                                  || CheckUpgradeFootprint(upgrading, target, false));
    }

    private static bool CheckUpgradeFootprint(Building upgrading, BuildingAsset target, bool clear)
    {
        var buildingsToRemove = new HashSet<Building>();
        var wallsToRemove = new List<WorldTile>();
        int startX = upgrading.current_tile.pos.x - target.fundament.left;
        int startY = upgrading.current_tile.pos.y - target.fundament.bottom;
        int width = target.fundament.left + target.fundament.right + 1;
        int height = target.fundament.bottom + target.fundament.top + 1;

        // 先完整预检，避免清理部分建筑后才发现目标占地仍不可用。
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                WorldTile tile = World.world.GetTile(startX + x, startY + y);
                if (tile == null || tile.zone?.city != upgrading.city) return false;

                bool wall = tile.top_type?.wall == true;
                if (wall)
                {
                    if (tile.main_type?.can_build_on != true) return false;
                    wallsToRemove.Add(tile);
                }
                else if (tile.Type?.can_build_on != true)
                {
                    return false;
                }

                Building obstacle = tile.building;
                if (obstacle == null || obstacle == upgrading || buildingsToRemove.Contains(obstacle)) continue;
                if (!CanReplaceForUpgrade(target, obstacle.asset)) return false;
                buildingsToRemove.Add(obstacle);
            }
        }

        if (clear)
        {
            foreach (Building obstacle in buildingsToRemove) obstacle.startDestroyBuilding();
            foreach (WorldTile tile in wallsToRemove)
            {
                if (tile.top_type?.wall == true) tile.setTopTileType(null);
            }
        }
        return true;
    }

    private static bool CanReplaceForUpgrade(BuildingAsset target, BuildingAsset obstacle)
    {
        int targetPriority = GetUpgradePriority(target);
        int obstaclePriority = GetUpgradePriority(obstacle);
        if (targetPriority != obstaclePriority) return targetPriority > obstaclePriority;
        return targetPriority == UpgradePriorityHouse && target.upgrade_level > obstacle.upgrade_level;
    }

    private static int GetUpgradePriority(BuildingAsset asset)
    {
        if (asset.type == "type_hall") return UpgradePriorityHall;
        if (asset.type == "type_house") return UpgradePriorityHouse;
        if (asset.type == "type_statue") return UpgradePriorityStatue;
        if (asset.building_type == BuildingType.Building_Wheat
            || asset.flora_type is FloraType.Tree or FloraType.Plant or FloraType.Fungi
            || asset.type is "type_crops" or "type_tree" or "type_vegetation" or "type_flower")
        {
            return UpgradePriorityNatural;
        }
        return UpgradePriorityFunctional;
    }

    private static bool HasSameFootprint(BuildingAsset current, BuildingAsset target)
    {
        return current.fundament.left == target.fundament.left
               && current.fundament.right == target.fundament.right
               && current.fundament.top == target.fundament.top
               && current.fundament.bottom == target.fundament.bottom;
    }
}
