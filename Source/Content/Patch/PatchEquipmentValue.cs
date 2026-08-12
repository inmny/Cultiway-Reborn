using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace Cultiway.Content.Patch;

/// <summary>让城市发放、战利品换装和装备打造统一使用角色语境下的装备价值。</summary>
internal static class PatchEquipmentValue
{
    private static readonly AccessTools.FieldRef<City, int> StorageVersionRef =
        AccessTools.FieldRefAccess<City, int>("_storage_version");

    [HarmonyPrefix, HarmonyPatch(typeof(ItemCrafting), nameof(ItemCrafting.craftItem))]
    private static bool CraftItemPrefix(Actor pActor, string pCreatorName, EquipmentType pType, int pTries,
        City pCity, ref bool __result)
    {
        __result = TryCraftBestEquipment(pActor, pCreatorName, pType, pTries, pCity);
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(City), nameof(City.giveItem))]
    private static bool GiveItemPrefix(Actor pActor, List<long> pItems, City pCity, ref bool __result)
    {
        __result = GiveBestItem(pActor, pItems, pCity);
        return false;
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(Actor), nameof(Actor.takeItems))]
    private static IEnumerable<CodeInstruction> TakeItemsTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return ReplaceItemValueReads(instructions, 2, nameof(Actor.takeItems));
    }

    /// <summary>
    /// 将原版 Item.getValue() 调用替换为带当前角色装备偏好的价值计算。
    /// 目标方法的实例就是获得装备的角色。
    /// </summary>
    private static IEnumerable<CodeInstruction> ReplaceItemValueReads(IEnumerable<CodeInstruction> instructions,
        int expectedCount, string patchedMethod)
    {
        var getValue = AccessTools.Method(typeof(Item), nameof(Item.getValue));
        var resolveValue = AccessTools.Method(typeof(EquipmentValueService),
            nameof(EquipmentValueService.ResolveItemValue));
        var replaced = 0;

        foreach (var instruction in instructions)
        {
            if (!instruction.Calls(getValue))
            {
                yield return instruction;
                continue;
            }

            // 原调用栈已有 Item；再压入 Actor 后改调静态方法 (Item, Actor)。
            var loadActor = new CodeInstruction(OpCodes.Ldarg_0);
            loadActor.labels.AddRange(instruction.labels);
            loadActor.blocks.AddRange(instruction.blocks);
            instruction.labels.Clear();
            instruction.blocks.Clear();
            yield return loadActor;
            yield return new CodeInstruction(OpCodes.Call, resolveValue);
            replaced++;
        }

        if (replaced != expectedCount)
            throw new InvalidOperationException(
                $"{patchedMethod} 中预期替换 {expectedCount} 个 Item.getValue 调用，实际为 {replaced} 个");
    }

    private static bool GiveBestItem(Actor actor, List<long> items, City city)
    {
        if (items.Count == 0 || !actor.understandsHowToUseItems()) return false;

        using var bestIndices = new ListPool<int>();
        int bestValue = int.MinValue;
        for (var i = 0; i < items.Count; i++)
        {
            Item candidate = World.world.items.get(items[i]);
            int value = EquipmentValueService.ResolveItemValue(candidate, actor);
            if (value < bestValue) continue;
            if (value > bestValue)
            {
                bestValue = value;
                bestIndices.Clear();
            }
            bestIndices.Add(i);
        }

        int selectedIndex = bestIndices.GetRandom();
        Item selected = World.world.items.get(items[selectedIndex]);
        ActorEquipmentSlot slot = actor.equipment.getSlot(selected.getAsset().equipment_type);
        if (!slot.isEmpty() &&
            bestValue <= EquipmentValueService.ResolveItemValue(slot.getItem(), actor)) return false;

        Item replaced = slot.getItem();
        if (replaced != null) slot.takeAwayItem();
        items.RemoveAt(selectedIndex);
        slot.setItem(selected, actor);
        actor.setStatsDirty();
        if (replaced != null) city.data.equipment.addItem(city, replaced, items);
        ref int storageVersion = ref StorageVersionRef(city);
        storageVersion++;
        return true;
    }

    private static bool TryCraftBestEquipment(
        Actor actor,
        string creatorName,
        EquipmentType type,
        int tries,
        City city)
    {
        ActorEquipmentSlot slot = actor.equipment.getSlot(type);
        Item current = slot.getItem();
        if (current != null && current.isCursed()) return false;

        EquipmentAsset selected = SelectCraftingAsset(actor, type, city, current);
        if (selected == null) return false;

        Item crafted = World.world.items.generateItem(selected, actor.kingdom, creatorName, tries, actor);
        if (current != null)
        {
            slot.takeAwayItem();
            city.tryToPutItem(current);
        }
        slot.setItem(crafted, actor);
        actor.spendMoney(selected.get_total_cost);
        if (selected.cost_resource_id_1 != "none")
            city.takeResource(selected.cost_resource_id_1, selected.cost_resource_1);
        if (selected.cost_resource_id_2 != "none")
            city.takeResource(selected.cost_resource_id_2, selected.cost_resource_2);
        return true;
    }

    private static EquipmentAsset SelectCraftingAsset(
        Actor actor,
        EquipmentType type,
        City city,
        Item current)
    {
        var candidates = new HashSet<EquipmentAsset>();
        string subtype;
        if (type == EquipmentType.Weapon)
        {
            subtype = actor.hasCulture() ? actor.culture.getPreferredWeaponSubtypeIDs() : null;
            if (string.IsNullOrEmpty(subtype)) subtype = ItemLibrary.default_weapon_pool.GetRandom();
            if (actor.hasCulture() && actor.culture.hasPreferredWeaponsToCraft() && Randy.randomBool())
                candidates.UnionWith(actor.culture.getPreferredWeaponAssets());

            foreach (EquipmentAsset asset in AssetManager.items.pot_weapon_assets_all)
            {
                if (EquipmentValueService.ResolvePreferenceBonus(asset, actor) > 0) candidates.Add(asset);
            }
        }
        else
        {
            subtype = AssetManager.items.getEquipmentType(type);
        }
        candidates.UnionWith(AssetManager.items.equipment_by_subtypes[subtype]);

        int currentValue = EquipmentValueService.ResolveItemValue(current, actor);
        using var bestAssets = new ListPool<EquipmentAsset>();
        int bestValue = currentValue;
        foreach (EquipmentAsset candidate in candidates)
        {
            if (candidate.isTemplateAsset() || !CanAfford(actor, candidate, city)) continue;
            int value = EquipmentValueService.ResolveAssetValue(candidate, actor);
            if (value <= bestValue) continue;
            if (value > bestValue)
            {
                bestValue = value;
                bestAssets.Clear();
            }
            bestAssets.Add(candidate);
        }
        return bestAssets.Count == 0 ? null : bestAssets.GetRandom();
    }

    private static bool CanAfford(Actor actor, EquipmentAsset asset, City city)
    {
        if (!actor.hasEnoughMoney(asset.get_total_cost)) return false;
        if (asset.cost_resource_id_1 != "none" &&
            asset.cost_resource_1 > city.getResourcesAmount(asset.cost_resource_id_1)) return false;
        if (asset.cost_resource_id_2 != "none" &&
            asset.cost_resource_2 > city.getResourcesAmount(asset.cost_resource_id_2)) return false;
        return true;
    }
}
