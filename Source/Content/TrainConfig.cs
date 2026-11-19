using System;

namespace Cultiway.Content;

/// <summary>
/// 火车调度相关的固定参数，统一放这里便于后续调整。
/// </summary>
internal static class TrainConfig
{
    /// <summary>车站间最小发车间隔（秒）。</summary>
    public const float MinDepartInterval = 5f;
    /// <summary>移动和寻路估时统一使用每模拟秒的轨道格数。</summary>
    public static float TrackSpeed => Actors.Train.base_stats[strings.S.speed] *
                                     Core.Pathfinding.PathfindingConfig.Default.WalkSpeedScale;

    /// <summary>每站基础停靠时间（秒）。</summary>
    public const float StopBaseWait = 3f;

    /// <summary>额外等待迟到乘客的最长时间（秒）。</summary>
    public const float StopExtraWaitMax = 7f;

    /// <summary>连续无法返回归属站五分钟后自爆，使用随游戏倍速推进的模拟秒。</summary>
    public const float StrandedTimeout = 300f;

}

