using System;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Content.SpiritVeins;
using Cultiway.Core;
using Cultiway.Core.Components;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>一次修炼资源消耗与灵气产出的实际结算结果。</summary>
public readonly struct CultivationSettlementResult
{
    /// <summary>创建一条已经提交的结算结果。</summary>
    public CultivationSettlementResult(float resourceSpent, float wakanGained)
    {
        ResourceSpent = resourceSpent;
        WakanGained = wakanGained;
    }

    /// <summary>实际扣除的修炼资源量。</summary>
    public float ResourceSpent { get; }

    /// <summary>角色实际获得的灵气。</summary>
    public float WakanGained { get; }
}

/// <summary>修炼资源扣除和灵气写入的统一结算入口。</summary>
public static class CultivationSettlementService
{
    /// <summary>是否输出修炼结算调试记录；默认关闭。</summary>
    public static bool EnableDebugTracing { get; set; }

    /// <summary>消耗指定修炼资源并按固定兑换率获得灵气，只扣除实际可转化的数量。</summary>
    public static CultivationSettlementResult ConvertToWakan(
        ActorExtend actor,
        CultivationResourceAsset resource,
        float requestedWakan,
        float resourcePerWakan,
        int tileX = -1,
        int tileY = -1)
    {
        if (actor == null || resource?.WithdrawUpTo == null || requestedWakan <= 0f || resourcePerWakan <= 0f ||
            !actor.HasCultisys<Xian>()) return default;

        if (ReferenceEquals(resource, CultivationResources.WorldWakan))
        {
            int resolvedX = tileX >= 0 ? tileX : actor.Base.current_tile?.x ?? -1;
            int resolvedY = tileY >= 0 ? tileY : actor.Base.current_tile?.y ?? -1;
            int tileId = WorldWakanService.GetTileId(resolvedX, resolvedY);
            if (actor.HasElementRoot())
            {
                SpiritVeinManager manager = WorldboxGame.I?.SpiritVeins;
                requestedWakan *= 1f + (manager?.GetElementMatchBonus(tileId, actor.GetElementRoot()) ?? 0f);
            }
        }

        ref Xian xian = ref actor.GetCultisys<Xian>();
        float maximum = Mathf.Max(0f, actor.Base.stats[BaseStatses.MaxWakan.id]);
        float outputCapacity = Mathf.Max(0f, maximum - xian.wakan);
        float outputRequest = Mathf.Min(requestedWakan, outputCapacity);
        if (outputRequest <= 0f) return default;

        var resourceContext = new CultivationResourceContext(actor, tileX, tileY);
        float resourceRequest = outputRequest * resourcePerWakan;
        float spent = Mathf.Clamp(resource.WithdrawUpTo(in resourceContext, resourceRequest), 0f, resourceRequest);
        float gained = WakanResourceService.Gain(actor, ref xian, spent / resourcePerWakan);
        Trace(actor, resource, spent, gained);
        return new CultivationSettlementResult(spent, gained);
    }

    /// <summary>不消耗外部修炼资源而增加灵气，并返回实际增加量。</summary>
    public static float GainWakan(ActorExtend actor, float requestedWakan)
    {
        if (actor == null || requestedWakan <= 0f || !actor.HasCultisys<Xian>()) return 0f;
        ref Xian xian = ref actor.GetCultisys<Xian>();
        return WakanResourceService.Gain(actor, ref xian, requestedWakan);
    }

    /// <summary>按地块灵气浓度执行一次不计入修炼方式实践的自然吸收。</summary>
    [Hotfixable]
    public static float AbsorbAmbientWakan(
        ActorExtend actor,
        float multiplier,
        float targetMaximum = -1f)
    {
        if (actor?.Base?.current_tile == null || multiplier <= 0f) return 0f;
        Vector2Int position = actor.Base.current_tile.pos;
        var resourceContext = new CultivationResourceContext(actor, position.x, position.y);
        float available = CultivationResources.WorldWakan.GetAvailable(in resourceContext);
        float current = actor.GetCultisys<Xian>().wakan;
        float maximum = targetMaximum > 0f
            ? Mathf.Min(targetMaximum, actor.Base.stats[BaseStatses.MaxWakan.id])
            : actor.Base.stats[BaseStatses.MaxWakan.id];
        float requested = Mathf.Min(
            Mathf.Max(0f, maximum - current),
            Mathf.Log10(available + 1f) * multiplier);
        return ConvertToWakan(actor, CultivationResources.WorldWakan, requested, 1f,
            position.x, position.y).WakanGained;
    }

    private static void Trace(
        ActorExtend actor,
        CultivationResourceAsset resource,
        float spent,
        float gained)
    {
        if (!EnableDebugTracing || spent <= 0f) return;
        ModClass.LogInfoConcurrent(
            $"修炼结算 actor={actor.Base.data.id} resource={resource.id} spent={spent:F3} wakan={gained:F3}");
    }
}
