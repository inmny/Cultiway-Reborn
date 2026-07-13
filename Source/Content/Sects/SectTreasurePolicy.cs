using Cultiway.Const;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Sects;

public static class SectTreasurePolicy
{
    public static bool CanDeposit(Actor actor, Sect sect)
    {
        return IsMember(actor, sect)
               && actor.HasSectPermission(SectPermissions.DepositTreasure);
    }

    public static bool HasAccessPermission(Actor actor, Entity item)
    {
        int stage = SectTreasureMetrics.GetStage(item);
        if (stage >= SectConst.TreasureHighPermissionMinStage)
        {
            return actor.HasSectPermission(SectPermissions.AccessHighTreasure);
        }

        if (stage >= SectConst.TreasureCorePermissionMinStage)
        {
            return actor.HasSectPermission(SectPermissions.AccessCoreTreasure)
                   || actor.HasSectPermission(SectPermissions.AccessHighTreasure);
        }

        return actor.HasSectPermission(SectPermissions.AccessBasicTreasure)
               || actor.HasSectPermission(SectPermissions.AccessCoreTreasure)
               || actor.HasSectPermission(SectPermissions.AccessHighTreasure);
    }

    public static bool CanAccept(Sect sect, Entity item)
    {
        if (sect == null || sect.isRekt()) return false;
        if (!IsStorable(item)) return false;
        if (SectTreasureInventory.FindOwner(item) != null) return false;

        SpecialItemCategoryAsset category = ModClass.L.SpecialItemCategoryLibrary.Resolve(item);
        if (category == null) return false;
        return SectTreasureMetrics.GetUsedCapacity(sect) + System.Math.Max(1, category.storageWeight)
               <= SectTreasureMetrics.GetCapacity(sect);
    }

    public static bool CanClaim(Actor actor, Entity item)
    {
        Sect sect = actor.GetExtend().sect;
        if (sect == null || sect.isRekt()) return false;
        if (!sect.Treasures.Owns(item) || !sect.Treasures.IsStored(item)) return false;
        if (!IsStorable(item)) return false;

        int cost = GetClaimCost(actor, item);
        return cost != int.MaxValue && actor.GetAvailableSectContribution() >= cost;
    }

    public static int GetClaimCost(Actor actor, Entity item)
    {
        SpecialItemCategoryAsset category = ModClass.L.SpecialItemCategoryLibrary.Resolve(item);
        if (category == null) return int.MaxValue;

        float multiplier = HasAccessPermission(actor, item)
            ? category.permissionCostMultiplier
            : SectConst.TreasureOutOfPermissionMultiplier;
        return Mathf.CeilToInt(SectTreasureMetrics.GetValue(item) * multiplier);
    }

    internal static bool IsCarriedBy(Actor actor, Entity item)
    {
        var relations = actor.GetExtend().E.GetRelations<InventoryRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            if (relations[i].item == item) return true;
        }

        return false;
    }

    private static bool IsMember(Actor actor, Sect sect)
    {
        return actor != null
               && !actor.isRekt()
               && sect != null
               && !sect.isRekt()
               && actor.GetExtend().sect == sect;
    }

    private static bool IsStorable(Entity item)
    {
        return !item.IsNull
               && item.HasComponent<SpecialItem>()
               && !item.Tags.HasAny(Tags.Get<TagUncompleted, TagConsumed, TagRecycle>());
    }
}
