using System;

namespace Cultiway.Core.Performance;

/// <summary>
/// 当前世界的权威玩法时间。它只随模拟 tick 推进，不受渲染帧率和实际执行耗时影响。
/// </summary>
internal static class SimulationTime
{
    private static MapBox boundWorld;
    private static MapStats boundMapStats;
    private static int boundWorldSeedId = -1;
    private static int generation;
    private static bool tickActive;
    private static double committedTime;
    private static double pendingTime;

    public static int Generation => generation;
    public static int BoundWorldSeedId => boundWorldSeedId;
    public static bool IsBound => boundWorld != null;
    public static double DiagnosticTime => tickActive ? pendingTime : committedTime;

    public static double Now
    {
        get
        {
            ValidateBoundWorld(boundWorld);
            return tickActive ? pendingTime : committedTime;
        }
    }

    public static float NowFloat => (float)Now;

    public static void BindWorld(MapBox world)
    {
        if (world?.map_stats == null)
        {
            throw new InvalidOperationException("无法绑定尚未创建 MapStats 的世界");
        }

        int worldSeedId = MapBox.current_world_seed_id;
        if (ReferenceEquals(boundWorld, world) &&
            ReferenceEquals(boundMapStats, world.map_stats) &&
            boundWorldSeedId == worldSeedId)
        {
            SynchronizeFromWorld(world);
            return;
        }

        boundWorld = world;
        boundMapStats = world.map_stats;
        boundWorldSeedId = worldSeedId;
        committedTime = world.getCurWorldTime();
        pendingTime = committedTime;
        tickActive = false;
        generation++;
    }

    public static void UnbindWorld()
    {
        tickActive = false;
        pendingTime = committedTime;
        boundWorld = null;
        boundMapStats = null;
        boundWorldSeedId = -1;
    }

    public static void BeginTick(MapBox world, float deltaTime)
    {
        ValidateBoundWorld(world);
        if (tickActive)
        {
            throw new InvalidOperationException("上一个模拟 tick 尚未结束");
        }

        committedTime = world.getCurWorldTime();
        pendingTime = committedTime + Math.Max(0f, deltaTime);
        tickActive = true;
    }

    public static void CompleteTick(MapBox world)
    {
        ValidateBoundWorld(world);
        if (!tickActive)
        {
            return;
        }

        committedTime = world.getCurWorldTime();
        pendingTime = committedTime;
        tickActive = false;
    }

    public static void CancelTick()
    {
        if (boundWorld != null && IsCurrentBoundWorld(boundWorld))
        {
            committedTime = boundWorld.getCurWorldTime();
        }

        tickActive = false;
        pendingTime = committedTime;
    }

    public static void SynchronizeFromWorld(MapBox world)
    {
        ValidateBoundWorld(world);
        if (tickActive)
        {
            throw new InvalidOperationException("模拟 tick 内不能同步玩法时间");
        }

        committedTime = world.getCurWorldTime();
        pendingTime = committedTime;
    }

    private static void ValidateBoundWorld(MapBox world)
    {
        if (world == null || boundWorld == null)
        {
            throw new InvalidOperationException("当前没有已绑定的模拟世界");
        }

        if (!ReferenceEquals(boundWorld, world) || !IsCurrentBoundWorld(world))
        {
            throw new InvalidOperationException("模拟时钟与当前世界不匹配");
        }
    }

    private static bool IsCurrentBoundWorld(MapBox world)
    {
        return ReferenceEquals(boundWorld, world) &&
               ReferenceEquals(boundMapStats, world.map_stats) &&
               boundWorldSeedId == MapBox.current_world_seed_id;
    }
}
