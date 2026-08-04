using System;
using Cultiway.Core;

namespace Cultiway.Content.Libraries;

/// <summary>计算角色在指定地块执行某种环境修炼时的环境质量。</summary>
public delegate float CultivationTileQualityResolver(ActorExtend actor, WorldTile tile);

/// <summary>把当前环境质量转换为修炼方式倍率。</summary>
public delegate float CultivationEnvironmentMultiplierResolver(ActorExtend actor, float quality);

/// <summary>在一次环境修炼结算完成后应用该方式的风险或额外资源变化。</summary>
public delegate void CultivationEnvironmentTickAction(
    in CultivationTriggerContext context,
    float quality,
    CultivationSettlementResult settlement);

/// <summary>
/// 描述环境修炼共同需要的元素目标、选址、寻路、修炼资源和逐次结算扩展。
/// </summary>
public sealed class CultivationEnvironmentRule
{
    /// <summary>用于功法生成适合度计算的目标元素组成。</summary>
    public ElementComposition TargetComposition;

    /// <summary>返回地块的 0..1 环境质量；零表示只能作为无合适地点时的回退位置。</summary>
    public CultivationTileQualityResolver GetTileQuality;

    /// <summary>根据角色和当前环境质量计算方式倍率。</summary>
    public CultivationEnvironmentMultiplierResolver GetMultiplier;

    /// <summary>本方式从哪个实际资源池支付修炼消耗。</summary>
    public CultivationResourceAsset Resource;

    /// <summary>获得一点灵气需要消耗的资源量；浊气使用全局折算率。</summary>
    public float ResourcePerWakan = 1f;

    /// <summary>资源结算后执行的风险、残留物或其他临时效果。</summary>
    public CultivationEnvironmentTickAction AfterSettlement;

    /// <summary>返回功法生成时使用的 0..1 时代匹配度；无时代要求时为空。</summary>
    public Func<ActorExtend, float> GetEraMatch;

    /// <summary>候选地块存在建筑时是否拒绝，供日月和天雷等露天修炼使用。</summary>
    public bool PreferOutdoors;

    /// <summary>前往目标时是否允许路径经过水域。</summary>
    public bool WalkOnWater;

    /// <summary>前往目标时是否允许路径经过山体等阻挡地块。</summary>
    public bool WalkOnBlocks;

    /// <summary>前往目标时是否允许路径经过熔岩。</summary>
    public bool WalkOnLava;

    /// <summary>选址时是否允许原版标记为会伤害单位的地块。</summary>
    public bool AllowDamagingTerrain;

    /// <summary>安全地解析地块质量，并统一限制在 0..1。</summary>
    public float ResolveQuality(ActorExtend actor, WorldTile tile)
    {
        return tile == null || GetTileQuality == null
            ? 0f
            : UnityEngine.Mathf.Clamp01(GetTileQuality(actor, tile));
    }

    /// <summary>按角色当前地块解析环境倍率。</summary>
    public float ResolveMultiplier(ActorExtend actor)
    {
        if (actor?.Base == null || GetMultiplier == null) return 1f;
        return UnityEngine.Mathf.Max(0f, GetMultiplier(actor, ResolveQuality(actor, actor.Base.current_tile)));
    }
}
