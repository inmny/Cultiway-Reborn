using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Debug;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Extensions;

/// <summary>
/// 宗门藏宝阁的容量、估值、入库、领取和借用规则。
/// </summary>
public static class SectTreasureRules
{
    /// <summary>
    /// 获取宗门当前的总库藏容量。
    /// </summary>
    public static int GetTreasureCapacity(Sect sect)
    {
        return Mathf.Max(0, Mathf.RoundToInt(sect.base_stats[WorldboxGame.BaseStats.TreasureCapacity.id]));
    }

    /// <summary>
    /// 获取宗门所有物当前占用的库藏容量；外借物品仍保留其容量名额。
    /// </summary>
    public static int GetTreasureUsedCapacity(Sect sect)
    {
        int used = 0;
        foreach (Entity item in sect.GetTreasures())
        {
            if (item.IsNull) continue;
            SpecialItemCategoryAsset category = ModClass.L.SpecialItemCategoryLibrary.Resolve(item);
            if (category != null)
            {
                used += Mathf.Max(1, category.storageWeight);
            }
        }

        return used;
    }

    /// <summary>
    /// 获取指定物品的品阶阶段。
    /// </summary>
    public static int GetTreasureStage(Entity item)
    {
        return item.TryGetComponent(out ItemLevel level) ? level.Stage : 0;
    }

    /// <summary>
    /// 获取特殊物品用于贡献和领取的基础价值。
    /// </summary>
    public static int GetTreasureValue(Entity item)
    {
        SpecialItemCategoryAsset category = ModClass.L.SpecialItemCategoryLibrary.Resolve(item);
        if (category == null) return 0;

        int rank = 0;
        if (item.TryGetComponent(out ItemLevel level))
        {
            rank = Mathf.Max(0, level);
        }

        return Mathf.Max(1, category.baseContributionCost * (rank + 1));
    }

    /// <summary>
    /// 计算物品首次贡献宗门时应获得的贡献。
    /// </summary>
    public static int GetTreasureContributionReward(Entity item)
    {
        return Mathf.Max(1, Mathf.CeilToInt(GetTreasureValue(item) * SectConst.TreasureContributionRewardRatio));
    }

    /// <summary>
    /// 计算成员领取或借用指定宗门物品需要消耗的贡献。
    /// </summary>
    public static int GetTreasureClaimCost(Actor actor, Entity item)
    {
        SpecialItemCategoryAsset category = ModClass.L.SpecialItemCategoryLibrary.Resolve(item);
        if (category == null) return int.MaxValue;

        float multiplier = actor.HasSectTreasureAccessPermissionFor(item)
            ? category.permissionCostMultiplier
            : SectConst.TreasureOutOfPermissionMultiplier;
        return Mathf.CeilToInt(GetTreasureValue(item) * multiplier);
    }

    /// <summary>
    /// 判断宗门能否接收一件新的特殊物品。
    /// </summary>
    public static bool CanAcceptTreasure(Sect sect, Entity item)
    {
        if (!IsStorableItem(item)) return false;
        if (GetTreasureOwner(item) != null) return false;

        SpecialItemCategoryAsset category = ModClass.L.SpecialItemCategoryLibrary.Resolve(item);
        if (category == null) return false;
        return GetTreasureUsedCapacity(sect) + Mathf.Max(1, category.storageWeight) <= GetTreasureCapacity(sect);
    }

    /// <summary>
    /// 将无主奖励或制作成果直接存入宗门，不发放贡献。
    /// </summary>
    public static bool TryStoreTreasure(Sect sect, Entity item)
    {
        if (!CanAcceptTreasure(sect, item)) return false;

        sect.AddTreasureOwnership(item);
        sect.AddSpecialItem(item);
        SectVerifyLog.Log("SectTreasureStore", $"sect={SectVerifyLog.Sect(sect)} item={item.Id} used={GetTreasureUsedCapacity(sect)}/{GetTreasureCapacity(sect)}");
        return true;
    }

    /// <summary>
    /// 将成员携带的物品贡献给宗门，并在首次贡献时发放贡献。
    /// </summary>
    public static bool TryContributeTreasure(Actor actor, Entity item)
    {
        Sect sect = actor.GetExtend().sect;
        if (!actor.CanDepositSectTreasure(sect)) return false;
        if (!Carries(actor.GetExtend().E, item)) return false;
        if (!CanAcceptTreasure(sect, item)) return false;

        sect.AddTreasureOwnership(item);
        sect.AddSpecialItem(item);
        var reward = GetTreasureContributionReward(item);   
        sect.AddContribution(actor, reward);

        SectVerifyLog.Log("SectTreasureContribute", $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(actor)} item={item.Id} reward={reward} used={GetTreasureUsedCapacity(sect)}/{GetTreasureCapacity(sect)}");
        return true;
    }

    /// <summary>
    /// 判断成员能否领取或借用指定宗门物品。
    /// </summary>
    public static bool CanClaimTreasure(Actor actor, Entity item)
    {
        Sect sect = actor.GetExtend().sect;
        if (sect == null || sect.isRekt()) return false;
        if (!sect.OwnsTreasure(item) || !sect.IsTreasureStored(item)) return false;
        if (!IsStorableItem(item)) return false;

        int cost = GetTreasureClaimCost(actor, item);
        return cost != int.MaxValue && actor.GetAvailableSectContribution() >= cost;
    }

    /// <summary>
    /// 领取消耗品或借用法宝，并同步贡献消耗与所有权状态。
    /// </summary>
    public static bool TryClaimTreasure(Actor actor, Entity item)
    {
        if (!CanClaimTreasure(actor, item)) return false;

        Sect sect = actor.GetExtend().sect;
        SpecialItemCategoryAsset category = ModClass.L.SpecialItemCategoryLibrary.Resolve(item);
        int cost = GetTreasureClaimCost(actor, item);
        if (!actor.TrySpendSectContribution(cost)) return false;

        if (category.withdrawalMode == SpecialItemWithdrawalMode.Transfer)
        {
            sect.RemoveTreasureOwnership(item);
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

    /// <summary>
    /// 将人物借用的宗门法宝归还给其所属宗门。
    /// </summary>
    public static bool TryReturnBorrowedTreasure(Actor actor, Entity item)
    {
        if (!item.TryGetComponent(out SectTreasureLoan loan)) return false;
        Sect sect = WorldboxGame.I?.Sects?.get(loan.SectId);
        if (sect == null || sect.isRekt() || !sect.OwnsTreasure(item)) return false;
        if (!Carries(actor.GetExtend().E, item)) return false;

        actor.GetExtend().UnequipArtifact(item);
        sect.AddSpecialItem(item);
        item.RemoveComponent<SectTreasureLoan>();
        SectVerifyLog.Log("SectTreasureReturn", $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(actor)} item={item.Id}");
        return true;
    }

    /// <summary>
    /// 归还人物从指定宗门借用的全部物品；用于死亡和离宗生命周期。
    /// </summary>
    public static void ReturnBorrowedTreasures(Actor actor, Sect sect)
    {
        using ListPool<Entity> items = new(actor.GetExtend().GetItems());
        foreach (Entity item in items)
        {
            if (!item.TryGetComponent(out SectTreasureLoan loan) || loan.SectId != sect.getID()) continue;
            TryReturnBorrowedTreasure(actor, item);
        }
    }

    /// <summary>
    /// 在人物死亡转移背包前归还其借用的宗门法宝。
    /// </summary>
    public static void ReturnBorrowedTreasuresOnDeath(ActorExtend actor)
    {
        using ListPool<Entity> items = new(actor.GetItems());
        foreach (Entity item in items)
        {
            if (!item.TryGetComponent(out SectTreasureLoan loan)) continue;
            Sect sect = WorldboxGame.I?.Sects?.get(loan.SectId);
            if (sect != null && !sect.isRekt() && sect.OwnsTreasure(item))
            {
                TryReturnBorrowedTreasure(actor.Base, item);
            }
            else
            {
                item.RemoveComponent<SectTreasureLoan>();
            }
        }
    }

    /// <summary>
    /// 宗门移除时解除全部宗门所有权，并将物品转交驻地城市或回收。
    /// </summary>
    public static void ReleaseTreasures(Sect sect)
    {
        using ListPool<Entity> treasures = new(sect.GetTreasures());
        City homeCity = sect.GetHomeCity();
        foreach (Entity item in treasures)
        {
            if (item.IsNull) continue;
            sect.RemoveTreasureOwnership(item);
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

    /// <summary>
    /// 尝试挑选人物愿意贡献且宗门能够接收的最低价值物品。
    /// </summary>
    public static bool TryPickContributionItem(Actor actor, out Entity selected)
    {
        selected = default;
        Sect sect = actor.GetExtend().sect;
        if (!actor.CanDepositSectTreasure(sect)) return false;

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
            if (!CanAcceptTreasure(sect, item)) continue;

            int value = GetTreasureValue(item);
            if (value >= selectedValue) continue;
            selected = item;
            selectedValue = value;
        }

        return !selected.IsNull;
    }

    /// <summary>
    /// 尝试为缺少丹药、符箓或法宝的成员挑选可领取物品。
    /// </summary>
    public static bool TryPickClaimItem(Actor actor, out Entity selected)
    {
        selected = default;
        Sect sect = actor.GetExtend().sect;
        if (sect == null || sect.isRekt()) return false;

        bool hasElixir = HasCategory(actor.GetExtend(), SpecialItemCategories.Elixir);
        bool hasTalisman = HasCategory(actor.GetExtend(), SpecialItemCategories.Talisman);
        bool hasArtifact = HasCategory(actor.GetExtend(), SpecialItemCategories.Artifact);
        int selectedScore = int.MinValue;

        foreach (Entity item in sect.GetItems())
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

            if (!CanClaimTreasure(actor, item)) continue;
            int score = priority + GetTreasureValue(item);
            if (score <= selectedScore) continue;
            selected = item;
            selectedScore = score;
        }

        return !selected.IsNull;
    }

    /// <summary>
    /// 判断人物当前是否存在贡献或领取藏宝阁物品的行为目标。
    /// </summary>
    public static bool CanDoAnyTreasureAction(Actor actor)
    {
        return TryPickContributionItem(actor, out _) || TryPickClaimItem(actor, out _);
    }

    /// <summary>
    /// 获取物品所属的宗门。
    /// </summary>
    public static Sect GetTreasureOwner(Entity item)
    {
        var owners = item.GetIncomingLinks<SectTreasureRelation>().Entities;
        foreach (Entity owner in owners)
        {
            if (owner.TryGetComponent(out SectInventoryBinder binder)) return binder.Sect;
        }

        return null;
    }

    /// <summary>
    /// 获取宗门外借物品当前记录的借用者。
    /// </summary>
    public static Actor GetTreasureBorrower(Entity item)
    {
        return item.TryGetComponent(out SectTreasureLoan loan)
            ? World.world.units.get(loan.BorrowerActorId)
            : null;
    }

    private static bool IsStorableItem(Entity item)
    {
        return !item.IsNull
               && item.HasComponent<SpecialItem>()
               && !item.Tags.HasAny(Tags.Get<TagUncompleted, TagConsumed, TagRecycle>());
    }

    private static bool Carries(Entity owner, Entity item)
    {
        var relations = owner.GetRelations<InventoryRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            if (relations[i].item == item) return true;
        }

        return false;
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
