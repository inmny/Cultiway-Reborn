using System.Collections.Generic;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Sects;

public static class SectTreasurePlanner
{
    public static bool TryPickContribution(Actor actor, out Entity selected)
    {
        selected = default;
        Sect sect = actor.GetExtend().sect;
        if (!SectTreasurePolicy.CanDeposit(actor, sect)) return false;

        Dictionary<SpecialItemCategoryAsset, int> counts = new();
        using ListPool<Entity> items = new(actor.GetExtend().GetItems());
        foreach (Entity item in items)
        {
            SpecialItemCategoryAsset category = ModClass.L.SpecialItemCategoryLibrary.Resolve(item);
            if (category == null || item.HasComponent<SectTreasureLoan>()) continue;
            counts[category] = counts.TryGetValue(category, out int count) ? count + 1 : 1;
        }

        int selectedValue = int.MaxValue;
        foreach (Entity item in items)
        {
            SpecialItemCategoryAsset category = ModClass.L.SpecialItemCategoryLibrary.Resolve(item);
            if (category == null || item.HasComponent<SectTreasureLoan>()) continue;
            int retainCount = category == SpecialItemCategories.Ingredient ? 3 : 1;
            if (counts[category] <= retainCount) continue;
            if (category == SpecialItemCategories.Artifact && actor.GetExtend().IsArtifactEquipped(item)) continue;
            if (!SectTreasurePolicy.CanAccept(sect, item)) continue;

            int value = SectTreasureMetrics.GetValue(item);
            if (value >= selectedValue) continue;
            selected = item;
            selectedValue = value;
        }

        return !selected.IsNull;
    }

    public static bool TryPickClaim(Actor actor, out Entity selected)
    {
        selected = default;
        Sect sect = actor.GetExtend().sect;
        if (sect == null || sect.isRekt()) return false;

        bool hasElixir = HasCategory(actor.GetExtend(), SpecialItemCategories.Elixir);
        bool hasTalisman = HasCategory(actor.GetExtend(), SpecialItemCategories.Talisman);
        bool hasArtifact = HasCategory(actor.GetExtend(), SpecialItemCategories.Artifact);
        int selectedScore = int.MinValue;

        foreach (Entity item in sect.Treasures.GetStoredItems())
        {
            SpecialItemCategoryAsset category = ModClass.L.SpecialItemCategoryLibrary.Resolve(item);
            int priority;
            if (category == SpecialItemCategories.Artifact && !hasArtifact)
            {
                priority = 3000;
            }
            else if (category == SpecialItemCategories.Talisman && !hasTalisman)
            {
                priority = 2000;
            }
            else if (category == SpecialItemCategories.Elixir && !hasElixir)
            {
                priority = 1000;
            }
            else
            {
                continue;
            }

            if (!SectTreasurePolicy.CanClaim(actor, item)) continue;
            int score = priority + SectTreasureMetrics.GetValue(item);
            if (score <= selectedScore) continue;
            selected = item;
            selectedScore = score;
        }

        return !selected.IsNull;
    }

    public static bool CanPlanAny(Actor actor)
    {
        return TryPickContribution(actor, out _) || TryPickClaim(actor, out _);
    }

    private static bool HasCategory(ActorExtend actor, SpecialItemCategoryAsset category)
    {
        foreach (Entity item in actor.GetItems())
        {
            if (ModClass.L.SpecialItemCategoryLibrary.Resolve(item) == category) return true;
        }

        return false;
    }
}
