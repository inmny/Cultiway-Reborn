using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>定义内置修炼资源及其读取和消耗行为。</summary>
public sealed class CultivationResources
    : ExtendLibrary<CultivationResourceAsset, CultivationResources>
{
    private const float FortunePerGold = 10f;
    private const float PersonalDirtyWakanStorageMonths = 2f;

    /// <summary>任意单位死亡时沉积到所在地块的固定浊气量。</summary>
    public const float DeathDirtyWakanYield = 100f;

    /// <summary>地块中的洁净灵气。</summary>
    public static CultivationResourceAsset WorldWakan { get; private set; }

    /// <summary>地块中尚未被吸收的浊气。</summary>
    public static CultivationResourceAsset TileDirtyWakan { get; private set; }

    /// <summary>角色自身暂存的浊气。</summary>
    public static CultivationResourceAsset PersonalDirtyWakan { get; private set; }

    /// <summary>角色凭国王或城主身份可支配的国运。</summary>
    public static CultivationResourceAsset RoleFortune { get; private set; }

    protected override bool AutoRegisterAssets() => true;

    protected override void OnInit()
    {
        WorldWakan.DisplayNameKey = "Cultiway.Cultivation.Resource.WorldWakan";
        WorldWakan.IconPath = "cultiway/icons/iconWakan";
        WorldWakan.GetAvailable = GetWorldWakan;
        WorldWakan.WithdrawUpTo = WithdrawWorldWakan;

        TileDirtyWakan.DisplayNameKey = "Cultiway.Cultivation.Resource.TileDirtyWakan";
        TileDirtyWakan.IconPath = "cultiway/icons/iconWakan";
        TileDirtyWakan.GetAvailable = GetTileDirtyWakan;
        TileDirtyWakan.WithdrawUpTo = WithdrawTileDirtyWakan;

        PersonalDirtyWakan.DisplayNameKey = "Cultiway.Cultivation.Resource.PersonalDirtyWakan";
        PersonalDirtyWakan.IconPath = "cultiway/icons/iconWakan";
        PersonalDirtyWakan.GetAvailable = ReadPersonalDirtyWakan;
        PersonalDirtyWakan.GetCapacity = ReadPersonalDirtyWakanCapacity;
        PersonalDirtyWakan.WithdrawUpTo = WithdrawPersonalDirtyWakan;

        RoleFortune.DisplayNameKey = "Cultiway.Cultivation.Resource.RoleFortune";
        RoleFortune.IconPath = "cultiway/icons/artifact_atoms/fortune_vein_core";
        RoleFortune.GetAvailable = GetRoleFortune;
        RoleFortune.WithdrawUpTo = WithdrawRoleFortune;
    }

    /// <summary>读取指定地块当前洁净灵气。</summary>
    private static float GetWorldWakan(in CultivationResourceContext context)
    {
        ResolveTile(in context, out int x, out int y);
        return Mathf.Max(0f, WakanMap.I.map[x, y]);
    }

    /// <summary>从指定地块扣除洁净灵气。</summary>
    private static float WithdrawWorldWakan(in CultivationResourceContext context, float requestedAmount)
    {
        ResolveTile(in context, out int x, out int y);
        float current = Mathf.Max(0f, WakanMap.I.map[x, y]);
        float actual = Mathf.Min(current, Mathf.Max(0f, requestedAmount));
        WakanMap.I.map[x, y] = current - actual;
        return actual;
    }

    /// <summary>读取指定地块当前浊气。</summary>
    private static float GetTileDirtyWakan(in CultivationResourceContext context)
    {
        ResolveTile(in context, out int x, out int y);
        return Mathf.Max(0f, DirtyWakanMap.I.map[x, y]);
    }

    /// <summary>从指定地块扣除浊气。</summary>
    private static float WithdrawTileDirtyWakan(in CultivationResourceContext context, float requestedAmount)
    {
        ResolveTile(in context, out int x, out int y);
        float current = Mathf.Max(0f, DirtyWakanMap.I.map[x, y]);
        float actual = Mathf.Min(current, Mathf.Max(0f, requestedAmount));
        DirtyWakanMap.I.map[x, y] = current - actual;
        return actual;
    }

    /// <summary>读取角色当前暂存的个人浊气。</summary>
    public static float GetPersonalDirtyWakan(ActorExtend actor)
    {
        return actor != null && actor.TryGetComponent(out CultivationResourceState state)
            ? Mathf.Max(0f, state.personal_dirty_wakan)
            : 0f;
    }

    /// <summary>按角色当前灵气上限计算个人浊气缓存容量。</summary>
    public static float GetPersonalDirtyWakanCapacity(ActorExtend actor)
    {
        if (actor?.Base == null) return 0f;
        return Mathf.Max(0f, actor.Base.stats[BaseStatses.MaxWakan.id]) *
               ContentSetting.DirtyWakanToWakanRatio * PersonalDirtyWakanStorageMonths;
    }

    /// <summary>在角色个人容量范围内加入浊气，并返回实际加入量。</summary>
    public static float AddPersonalDirtyWakan(ActorExtend actor, float requestedAmount)
    {
        if (requestedAmount <= 0f || actor == null || !actor.HasComponent<CultivationResourceState>()) return 0f;
        ref CultivationResourceState state = ref actor.GetComponent<CultivationResourceState>();
        float current = Mathf.Max(0f, state.personal_dirty_wakan);
        float actual = Mathf.Min(requestedAmount,
            Mathf.Max(0f, GetPersonalDirtyWakanCapacity(actor) - current));
        state.personal_dirty_wakan = current + actual;
        return actual;
    }

    /// <summary>从角色个人缓存扣除不超过请求量的浊气，并返回实际扣除量。</summary>
    public static float SpendPersonalDirtyWakan(ActorExtend actor, float requestedAmount)
    {
        if (requestedAmount <= 0f || actor == null || !actor.HasComponent<CultivationResourceState>()) return 0f;
        ref CultivationResourceState state = ref actor.GetComponent<CultivationResourceState>();
        float current = Mathf.Max(0f, state.personal_dirty_wakan);
        float actual = Mathf.Min(requestedAmount, current);
        state.personal_dirty_wakan = current - actual;
        return actual;
    }

    /// <summary>为通用修炼资源委托读取角色个人浊气。</summary>
    private static float ReadPersonalDirtyWakan(in CultivationResourceContext context)
    {
        return GetPersonalDirtyWakan(context.Actor);
    }

    /// <summary>为通用资源展示读取角色个人浊气容量。</summary>
    private static float ReadPersonalDirtyWakanCapacity(in CultivationResourceContext context)
    {
        return GetPersonalDirtyWakanCapacity(context.Actor);
    }

    /// <summary>从角色个人浊气缓存中扣除资源。</summary>
    private static float WithdrawPersonalDirtyWakan(
        in CultivationResourceContext context,
        float requestedAmount)
    {
        return SpendPersonalDirtyWakan(context.Actor, requestedAmount);
    }

    /// <summary>读取角色凭当前政治身份可支配的国运总量。</summary>
    private static float GetRoleFortune(in CultivationResourceContext context)
    {
        Actor actor = context.Actor?.Base;
        if (actor == null) return 0f;

        bool isKing = actor.kingdom != null && actor.kingdom.king == actor;
        if (!isKing)
            return actor.city != null && actor.city.leader == actor
                ? GetSpendableGold(actor.city) * FortunePerGold
                : 0f;

        float available = 0f;
        for (var i = 0; i < actor.kingdom.cities.Count; i++)
            available += GetSpendableGold(actor.kingdom.cities[i]) * FortunePerGold;
        return available;
    }

    /// <summary>根据角色政治身份直接从城市金库扣除与国运等价的繁荣资源。</summary>
    private static float WithdrawRoleFortune(in CultivationResourceContext context, float requestedAmount)
    {
        Actor actor = context.Actor?.Base;
        if (actor == null || requestedAmount <= 0f) return 0f;

        int requestedGold = Mathf.CeilToInt(requestedAmount / FortunePerGold);
        int spentGold = 0;
        bool isKing = actor.kingdom != null && actor.kingdom.king == actor;
        if (!isKing)
        {
            if (actor.city != null && actor.city.leader == actor)
                spentGold = WithdrawCityGold(actor.city, requestedGold);
            return Mathf.Min(requestedAmount, spentGold * FortunePerGold);
        }

        if (actor.city != null)
            spentGold += WithdrawCityGold(actor.city, requestedGold);
        for (var i = 0; i < actor.kingdom.cities.Count && spentGold < requestedGold; i++)
        {
            City city = actor.kingdom.cities[i];
            if (city == actor.city) continue;
            spentGold += WithdrawCityGold(city, requestedGold - spentGold);
        }
        return Mathf.Min(requestedAmount, spentGold * FortunePerGold);
    }

    /// <summary>返回不触及原版城市安全库存的可支配金币。</summary>
    private static int GetSpendableGold(City city)
    {
        return city == null || !city.isAlive()
            ? 0
            : Mathf.Max(0, city.amount_gold - ResourceLibrary.gold.supply_bound_take);
    }

    /// <summary>从一个城市实际扣除不超过请求值的可支配金币。</summary>
    private static int WithdrawCityGold(City city, int requestedGold)
    {
        int toSpend = Mathf.Min(GetSpendableGold(city), Mathf.Max(0, requestedGold));
        if (toSpend <= 0) return 0;
        int before = city.amount_gold;
        city.takeResource(ResourceLibrary.gold.id, toSpend);
        return Mathf.Clamp(before - city.amount_gold, 0, toSpend);
    }

    /// <summary>解析资源所在的显式地块或角色当前地块。</summary>
    private static void ResolveTile(in CultivationResourceContext context, out int x, out int y)
    {
        if (context.TileX >= 0 && context.TileY >= 0)
        {
            x = context.TileX;
            y = context.TileY;
            return;
        }

        Vector2Int position = context.Actor.Base.current_tile.pos;
        x = position.x;
        y = position.y;
    }
}
