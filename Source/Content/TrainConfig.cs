using System;

namespace Cultiway.Content;

/// <summary>
/// 火车调度相关的固定参数，统一放这里便于后续调整。
/// </summary>
internal static class TrainConfig
{
    /// <summary>车站间最小发车间隔（秒）。</summary>
    public const float MinDepartInterval = 5f;

    /// <summary>始发前聚客的最长等待时间（秒）。</summary>
    public const float PrepareWaitMax = 5f;

    /// <summary>每站基础停靠时间（秒）。</summary>
    public const float StopBaseWait = 3f;

    /// <summary>额外等待迟到乘客的最长时间（秒）。</summary>
    public const float StopExtraWaitMax = 7f;

    /// <summary>单个乘客被视为迟到并放弃的阈值（秒）。</summary>
    public const float LateIgnoreTime = 10f;

    /// <summary>实验功能：启用后车站会定时发空车（无乘客也发车）。</summary>
    public static bool ExperimentalTimedDispatchEnabled = false;

    /// <summary>实验功能：同一车站空车发车周期（秒）。</summary>
    public const float ExperimentalTimedDispatchInterval = 20f;
}

