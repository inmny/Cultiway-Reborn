using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Debug;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Sects;

public static class SectTreasureService
{
    public static bool TryContribute(Actor actor, Entity item)
    {
        Sect sect = actor.GetExtend().sect;
        if (!SectTreasurePolicy.CanDeposit(actor, sect)) return false;
        if (!SectTreasurePolicy.IsCarriedBy(actor, item)) return false;
        if (!SectTreasurePolicy.CanAccept(sect, item)) return false;

        sect.Treasures.AddOwnership(item);
        sect.Treasures.Add(item);
        int reward = SectTreasureMetrics.GetContributionReward(item);
        sect.AddContribution(actor, reward);

        SectVerifyLog.Log("SectTreasureContribute", $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(actor)} item={item.Id} reward={reward} used={SectTreasureMetrics.GetUsedCapacity(sect)}/{SectTreasureMetrics.GetCapacity(sect)}");
        return true;
    }

    public static bool TryClaim(Actor actor, Entity item)
    {
        if (!SectTreasurePolicy.CanClaim(actor, item)) return false;

        Sect sect = actor.GetExtend().sect;
        SpecialItemCategoryAsset category = ModClass.L.SpecialItemCategoryLibrary.Resolve(item);
        int cost = SectTreasurePolicy.GetClaimCost(actor, item);
        if (!actor.TrySpendSectContribution(cost)) return false;

        if (category.withdrawalMode == SpecialItemWithdrawalMode.Transfer)
        {
            sect.Treasures.RemoveOwnership(item);
            actor.GetExtend().AddSpecialItem(item);
        }
        else
        {
            actor.GetExtend().AddSpecialItem(item);
            item.AddComponent(new SectTreasureLoan
            {
                SectId = sect.getID(),
                BorrowerActorId = actor.getID()
            });
            actor.GetExtend().EquipArtifact(item);
        }

        SectVerifyLog.Log("SectTreasureClaim", $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(actor)} item={item.Id} category={category.id} mode={category.withdrawalMode} cost={cost}");
        return true;
    }

    public static bool TryReturnBorrowed(Actor actor, Entity item)
    {
        if (!item.TryGetComponent(out SectTreasureLoan loan)) return false;
        Sect sect = WorldboxGame.I?.Sects?.get(loan.SectId);
        if (sect == null || sect.isRekt() || !sect.Treasures.Owns(item)) return false;
        if (!SectTreasurePolicy.IsCarriedBy(actor, item)) return false;

        actor.GetExtend().UnequipArtifact(item);
        sect.Treasures.Add(item);
        item.RemoveComponent<SectTreasureLoan>();
        SectVerifyLog.Log("SectTreasureReturn", $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(actor)} item={item.Id}");
        return true;
    }

    public static void ReturnBorrowed(Actor actor, Sect sect)
    {
        using ListPool<Entity> items = new(actor.GetExtend().GetItems());
        foreach (Entity item in items)
        {
            if (!item.TryGetComponent(out SectTreasureLoan loan) || loan.SectId != sect.getID()) continue;
            TryReturnBorrowed(actor, item);
        }
    }

    public static void ReturnBorrowedOnDeath(ActorExtend actor)
    {
        using ListPool<Entity> items = new(actor.GetItems());
        foreach (Entity item in items)
        {
            if (!item.TryGetComponent(out SectTreasureLoan loan)) continue;
            Sect sect = WorldboxGame.I?.Sects?.get(loan.SectId);
            if (sect != null && !sect.isRekt() && sect.Treasures.Owns(item))
            {
                TryReturnBorrowed(actor.Base, item);
            }
            else
            {
                item.RemoveComponent<SectTreasureLoan>();
            }
        }
    }

    public static void Release(Sect sect)
    {
        using ListPool<Entity> treasures = new(sect.Treasures.GetOwnedItems());
        City homeCity = sect.GetHomeCity();
        foreach (Entity item in treasures)
        {
            if (item.IsNull) continue;
            sect.Treasures.RemoveOwnership(item);
            if (item.HasComponent<SectTreasureLoan>()) item.RemoveComponent<SectTreasureLoan>();

            if (homeCity != null && homeCity.isAlive())
            {
                homeCity.GetExtend().AddSpecialItem(item);
            }
            else
            {
                item.AddTag<TagRecycle>();
            }
        }

        SectVerifyLog.Log("SectTreasuresReleased", $"sect={SectVerifyLog.Sect(sect)} count={treasures.Count} city={homeCity?.name ?? "null"}");
    }

    public static Actor GetBorrower(Entity item)
    {
        return item.TryGetComponent(out SectTreasureLoan loan)
            ? World.world.units.get(loan.BorrowerActorId)
            : null;
    }
}
