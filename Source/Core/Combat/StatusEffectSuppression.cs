using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Core.Combat;

/// <summary>统一计算负面状态受到的战力等级压制。</summary>
public static class StatusEffectSuppression
{
    /// <summary>目标每高一个战力等级，负面状态持续时间保留的比例。</summary>
    private const float DurationRatioPerPowerLevel = 0.001f;

    /// <summary>低于此持续时间的负面状态直接视为无效。</summary>
    private const float MinimumEffectiveDuration = 0.1f;

    /// <summary>根据施加者与目标的战力等级差计算实际持续时间。</summary>
    /// <param name="target">承受状态的目标。</param>
    /// <param name="effect">准备施加的状态类型。</param>
    /// <param name="duration">压制前的持续时间。</param>
    /// <param name="source">状态来源；可以为空。</param>
    /// <param name="sourcePowerLevel">已经固定的来源战力等级；为空时尝试从来源人物读取。</param>
    /// <param name="resolvedDuration">返回压制后的持续时间。</param>
    /// <returns>状态仍有足够持续时间时返回真。</returns>
    public static bool TryResolveDuration(
        ActorExtend target,
        StatusEffectAsset effect,
        float duration,
        BaseSimObject source,
        float? sourcePowerLevel,
        out float resolvedDuration)
    {
        resolvedDuration = Mathf.Max(0f, duration);
        if (target == null || effect == null || resolvedDuration <= 0f) return false;
        if (!effect.GetExtend<StatusAssetExtend>().negative) return true;

        if (!sourcePowerLevel.HasValue && source != null && source.isActor() && !source.isRekt())
            sourcePowerLevel = source.a.GetExtend().GetPowerLevel();
        if (!sourcePowerLevel.HasValue) return true;

        float levelGap = target.GetPowerLevel() - sourcePowerLevel.Value;
        if (levelGap <= 0f) return true;

        resolvedDuration *= Mathf.Pow(DurationRatioPerPowerLevel, levelGap);
        return resolvedDuration >= MinimumEffectiveDuration;
    }
}
