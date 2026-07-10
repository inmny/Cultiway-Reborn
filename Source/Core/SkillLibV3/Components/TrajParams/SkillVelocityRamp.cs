using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Components.TrajParams;

/// <summary>
/// 法术起步速度曲线：出手时使用 <see cref="StartMultiplier"/>，在 <see cref="RampDuration"/> 内
/// 平滑过渡到 <see cref="EndMultiplier"/>。默认用于短促的起步冲量，而非飞行中的长时间蓄势。
/// 由 <c>SkillTrajectories.GetVelocity</c> 读取并乘到基础速度上。
/// </summary>
public struct SkillVelocityRamp : IComponent
{
    /// <summary>出手瞬间的速度倍率。</summary>
    public float StartMultiplier;

    /// <summary>过渡结束后的巡航速度倍率。</summary>
    public float EndMultiplier;

    /// <summary>从起始倍率加速到终止倍率的时间（秒）。</summary>
    public float RampDuration;

    /// <summary>已累计的飞行时间（秒），由 GetVelocity 每帧累加。</summary>
    public float Elapsed;

    /// <summary>
    /// 按当前 Elapsed 计算速度倍率。过渡阶段使用 EaseOut 曲线，
    /// 超过 RampDuration 后恒定在 EndMultiplier。
    /// </summary>
    public readonly float CurrentMultiplier
    {
        get
        {
            if (RampDuration <= 0.001f) return EndMultiplier;
            var t = Mathf.Clamp01(Elapsed / RampDuration);
            // EaseOutQuad：迅速完成主要过渡，避免在屏幕上长时间展示加速过程。
            var eased = 1f - (1f - t) * (1f - t);
            return Mathf.Lerp(StartMultiplier, EndMultiplier, eased);
        }
    }
}
