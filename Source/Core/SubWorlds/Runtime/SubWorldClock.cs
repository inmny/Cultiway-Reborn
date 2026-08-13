using System;
using Cultiway.Core.SubWorlds.Model;

namespace Cultiway.Core.SubWorlds.Runtime;

/// <summary>
/// 为单个小世界实例累计未缩放时间并生成完整固定 tick。
/// </summary>
internal sealed class SubWorldClock
{
    private double accumulator;
    private float runningLocalSpeed = 1f;

    /// <summary>
    /// 使用指定时钟配置创建实例时钟。
    /// </summary>
    /// <param name="profile">该实例生命周期内固定使用的时钟配置。</param>
    internal SubWorldClock(SubWorldClockProfileAsset profile)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        LocalSpeed = 1f;
    }

    /// <summary>该实例绑定的静态时钟配置。</summary>
    internal SubWorldClockProfileAsset Profile { get; }

    /// <summary>已经完成的逻辑 tick 对应的局部时间，单位为秒。</summary>
    internal double LocalTime { get; private set; }

    /// <summary>已经完成的固定 tick 数。</summary>
    internal long TickIndex { get; private set; }

    /// <summary>当前局部速度倍率；值 0 表示暂停。</summary>
    internal float LocalSpeed { get; private set; }

    /// <summary>当前实例是否处于局部暂停状态。</summary>
    internal bool IsPaused => LocalSpeed == 0f;

    /// <summary>累计时间是否足够执行一个完整固定 tick。</summary>
    internal bool HasPendingTick => !IsPaused && accumulator >= Profile.fixed_step;

    /// <summary>下一个完整 tick 结束时的局部时间。</summary>
    internal double NextLocalTime => LocalTime + Profile.fixed_step;

    /// <summary>
    /// 将一个渲染帧经过的未缩放时间计入实例 accumulator。
    /// </summary>
    /// <param name="unscaledDeltaTime">渲染帧经过的未缩放秒数。</param>
    /// <param name="parentPaused">主世界当前是否暂停。</param>
    internal void Accumulate(float unscaledDeltaTime, bool parentPaused)
    {
        if (IsPaused || parentPaused && !Profile.runs_while_parent_paused) return;
        accumulator += unscaledDeltaTime * Profile.default_local_rate * LocalSpeed;
    }

    /// <summary>
    /// 尝试设置此配置允许的局部速度。
    /// </summary>
    /// <param name="localSpeed">待设置的局部速度倍率。</param>
    /// <returns>配置允许该速度时为 <see langword="true"/>。</returns>
    internal bool TrySetLocalSpeed(float localSpeed)
    {
        if (!Profile.AllowsLocalSpeed(localSpeed)) return false;
        LocalSpeed = localSpeed;
        if (localSpeed > 0f) runningLocalSpeed = localSpeed;
        return true;
    }

    /// <summary>
    /// 暂停实例，或恢复到暂停前最后使用的非零局部速度。
    /// </summary>
    /// <param name="paused">要设置的暂停状态。</param>
    internal void SetPaused(bool paused)
    {
        _ = TrySetLocalSpeed(paused ? 0f : runningLocalSpeed);
    }

    /// <summary>
    /// 消费一个固定步长并提交对应的局部时间和 TickIndex。
    /// </summary>
    internal void CompleteTick()
    {
        accumulator -= Profile.fixed_step;
        LocalTime += Profile.fixed_step;
        TickIndex++;
    }
}
