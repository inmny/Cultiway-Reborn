using System;

namespace Cultiway.Core.Performance;

/// <summary>
/// 权威玩法时间。它单调递增，只随模拟 tick 推进，不受渲染帧率和实际执行耗时影响。
/// </summary>
internal static class SimulationTime
{
    private static bool initialized;
    private static bool tickActive;
    private static double committedTime;
    private static double pendingTime;

    public static double Now
    {
        get
        {
            EnsureInitialized();
            return tickActive ? pendingTime : committedTime;
        }
    }

    public static float NowFloat => (float)Now;

    public static void BeginTick(float deltaTime)
    {
        EnsureInitialized();
        if (tickActive)
        {
            throw new InvalidOperationException("上一个模拟 tick 尚未结束");
        }

        pendingTime = committedTime + Math.Max(0f, deltaTime);
        tickActive = true;
    }

    public static void CompleteTick()
    {
        if (!tickActive)
        {
            return;
        }

        committedTime = pendingTime;
        tickActive = false;
    }

    public static void CancelTick()
    {
        tickActive = false;
        pendingTime = committedTime;
    }

    public static void AdvanceWithoutTransaction(float deltaTime)
    {
        EnsureInitialized();
        if (tickActive)
        {
            throw new InvalidOperationException("模拟 tick 内不能额外推进玩法时间");
        }

        committedTime += Math.Max(0f, deltaTime);
    }

    private static void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        committedTime = World.world?.getCurSessionTime() ?? 0.0;
        pendingTime = committedTime;
        initialized = true;
    }
}
