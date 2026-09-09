using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Components.TrajParams;

/// <summary>
/// 技能实体共用的运动参数：基础速度、转向速度、起步速度曲线和碰撞高度门。
/// 生成时由运动配置一次写入；速度或转向小于等于零表示沿用各轨迹的默认值。
/// </summary>
public struct MotionParams : IComponent
{
    /// <summary>基础飞行速度；小于等于零时由轨迹回退到默认速度。</summary>
    public float Velocity;

    /// <summary>平滑转向速度（度/秒）；小于等于零时由轨迹回退到默认转向。</summary>
    public float TurnRate;

    /// <summary>出手瞬间的速度倍率。</summary>
    public float RampStart;

    /// <summary>过渡结束后的巡航速度倍率。</summary>
    public float RampEnd;

    /// <summary>从起始倍率加速到终止倍率的时间（秒）。</summary>
    public float RampDuration;

    /// <summary>已累计的飞行时间（秒），由速度计算每帧累加。</summary>
    public float RampElapsed;

    /// <summary>是否启用起步速度曲线。</summary>
    public bool HasRamp;

    /// <summary>是否限制碰撞判定的高度。</summary>
    public bool HasHeightGate;

    /// <summary>碰撞判定的最大高度。</summary>
    public float HeightGateMax;

    /// <summary>按当前已累计时间计算速度倍率；未启用曲线时恒为 1。</summary>
    public readonly float CurrentMultiplier
    {
        get
        {
            if (!HasRamp) return 1f;
            if (RampDuration <= 0.001f) return RampEnd;
            var t = Mathf.Clamp01(RampElapsed / RampDuration);
            // EaseOutQuad：迅速完成主要过渡，避免在屏幕上长时间展示加速过程。
            var eased = 1f - (1f - t) * (1f - t);
            return Mathf.Lerp(RampStart, RampEnd, eased);
        }
    }
}
