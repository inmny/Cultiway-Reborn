using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Components.TrajParams;

/// <summary>
/// 法术速度加速曲线：出手时按 <see cref="StartMultiplier"/> 较慢，在 <see cref="RampDuration"/> 内
/// 平滑加速到 <see cref="EndMultiplier"/>，营造"蓄势→弹射冲刺"的节奏感。
/// 由 <c>SkillTrajectories.GetVelocity</c> 读取并乘到基础速度上。
/// </summary>
public struct SkillVelocityRamp : IComponent
{
    /// <summary>出手时的速度倍率（&lt; 1 = 比基础速度慢，蓄势）。</summary>
    public float StartMultiplier;

    /// <summary>加速结束时的速度倍率（&gt; 1 = 比基础速度快，冲刺）。</summary>
    public float EndMultiplier;

    /// <summary>从起始倍率加速到终止倍率的时间（秒）。</summary>
    public float RampDuration;

    /// <summary>已累计的飞行时间（秒），由 GetVelocity 每帧累加。</summary>
    public float Elapsed;

    /// <summary>
    /// 按当前 Elapsed 计算速度倍率。加速阶段用 EaseOut 曲线（先快后慢的加速），
    /// 超过 RampDuration 后恒定在 EndMultiplier。
    /// </summary>
    public readonly float CurrentMultiplier
    {
        get
        {
            if (RampDuration <= 0.001f) return EndMultiplier;
            var t = Mathf.Clamp01(Elapsed / RampDuration);
            // EaseOutQuad：加速先快后慢，视觉上"弹出去"
            var eased = 1f - (1f - t) * (1f - t);
            return Mathf.Lerp(StartMultiplier, EndMultiplier, eased);
        }
    }
}
