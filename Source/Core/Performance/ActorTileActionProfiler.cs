using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Cultiway.Core.Performance;

internal sealed class ActorTileActionProfiler
{
    private const int SamplingWindowTicks = 64;
    private const int RouteCapacity = 1 << 11;

    private const int RouteMask = 0x0f;
    private const int RouteSkipped = 1;
    private const int RouteFlying = 2;
    private const int RouteBlock = 3;
    private const int RouteGround = 4;
    private const int RouteLiquid = 5;
    private const int RouteOther = 6;

    private const int FlagFire = 1 << 4;
    private const int FlagWaterCreature = 1 << 5;
    private const int FlagBreakLiquid = 1 << 6;
    private const int FlagOceanDamage = 1 << 7;
    private const int FlagDrowning = 1 << 8;
    private const int FlagTileDamage = 1 << 9;
    private const int FlagBuildingStep = 1 << 10;

    private readonly double[] routeSeconds = new double[RouteCapacity];
    private readonly long[] routeCalls = new long[RouteCapacity];
    private readonly List<int> usedRoutes = new();
    private bool[] sampledBatches = Array.Empty<bool>();
    private bool active;
    private int nextBatchIndex;
    private long fullCalls;
    private long sampledCalls;
    private long timestampOverheadTicks;

    internal bool Active => active;

    internal void Start(int batchCount)
    {
        ResetSamples();
        active = SimulationTickBenchmark.IsCapturing && batchCount > 0;
        if (!active)
        {
            return;
        }

        timestampOverheadTicks = MeasureTimestampOverhead();
        EnsureBatchCapacity(batchCount);
        Array.Clear(sampledBatches, 0, batchCount);
        int sampleCount = Math.Max(
            1,
            (batchCount + SamplingWindowTicks - 1) / SamplingWindowTicks);
        if (nextBatchIndex >= batchCount)
        {
            nextBatchIndex = 0;
        }

        for (int i = 0; i < sampleCount; i++)
        {
            sampledBatches[(nextBatchIndex + i) % batchCount] = true;
        }

        nextBatchIndex = (nextBatchIndex + sampleCount) % batchCount;
    }

    internal bool TryRunSampledJob(
        BatchActors batch,
        Job<Actor> job,
        int batchListIndex)
    {
        if (!active ||
            batchListIndex < 0 ||
            batchListIndex >= sampledBatches.Length ||
            !sampledBatches[batchListIndex])
        {
            return false;
        }

        RunSampledJob(batch, job.container);
        return true;
    }

    internal void RecordFullCalls(ObjectContainer<Actor> container)
    {
        if (active)
        {
            fullCalls += container.Count;
        }
    }

    internal void Finish()
    {
        if (!active || sampledCalls <= 0L || fullCalls <= 0L)
        {
            ResetSamples();
            active = false;
            return;
        }

        double scale = fullCalls / (double)sampledCalls;
        for (int i = 0; i < usedRoutes.Count; i++)
        {
            int route = usedRoutes[i];
            long estimatedCalls = (long)Math.Round(routeCalls[route] * scale);
            SimulationTickBenchmark.RecordActorTileActionMetric(
                FormatRoute(route),
                routeSeconds[route] * scale,
                estimatedCalls);
        }

        ResetSamples();
        active = false;
    }

    internal void Abort()
    {
        ResetSamples();
        active = false;
    }

    private void RunSampledJob(
        BatchActors batch,
        ObjectContainer<Actor> container)
    {
        if (container.Count == 0 && !container.isDirtyContainer())
        {
            return;
        }

        container.checkAddRemove();
        Actor[] actors = container.getFastSimpleArray();
        int count = container.Count;
        batch._array = actors;
        batch._count = count;
        if (World.world.isPaused())
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            Actor actor = actors[i];
            int route = ClassifyRoute(actor);
            long startedAt = Stopwatch.GetTimestamp();
            actor.u5_curTileAction();
            long elapsedTicks = Math.Max(
                0L,
                Stopwatch.GetTimestamp() - startedAt - timestampOverheadTicks);
            double seconds = elapsedTicks / (double)Stopwatch.Frequency;
            if (routeCalls[route] == 0L)
            {
                usedRoutes.Add(route);
            }

            routeSeconds[route] += seconds;
            routeCalls[route]++;
            sampledCalls++;
        }
    }

    private static int ClassifyRoute(Actor actor)
    {
        if (actor._update_done || actor.position_height > 0f)
        {
            return RouteSkipped;
        }

        WorldTile tile = actor.current_tile;
        TileTypeBase type = tile.Type;
        if (actor.isFlying())
        {
            return RouteFlying;
        }

        if (type.block && !actor.ignoresBlocks())
        {
            return RouteBlock;
        }

        int route;
        if (type.ground)
        {
            route = RouteGround;
            if (tile.isOnFire() && !actor.isImmuneToFire())
            {
                route |= FlagFire;
            }

            if (actor.isWaterCreature() && !actor.asset.force_land_creature)
            {
                route |= FlagWaterCreature;
            }
        }
        else if (type.liquid)
        {
            route = RouteLiquid;
            if (type.damaged_when_walked)
            {
                route |= FlagBreakLiquid;
            }

            if (actor.isDamagedByOcean() &&
                type.ocean &&
                !actor.isUnderDamageCooldown() &&
                !actor._shake_active)
            {
                route |= FlagOceanDamage;
            }

            if (!actor.hasTag("fast_swimming") &&
                !actor.isWaterCreature() &&
                !actor.isInAir())
            {
                route |= FlagDrowning;
            }
        }
        else
        {
            route = RouteOther;
        }

        if (type.damage_units &&
            !actor.isUnderDamageCooldown() &&
            (!type.lava || (actor.asset.die_in_lava && !actor.isImmuneToFire())))
        {
            route |= FlagTileDamage;
        }

        if (tile.hasBuilding() && tile.building.asset.has_step_action)
        {
            route |= FlagBuildingStep;
        }

        return route;
    }

    private static string FormatRoute(int route)
    {
        string id = (route & RouteMask) switch
        {
            RouteSkipped => "skip.update_done_or_height",
            RouteFlying => "skip.flying",
            RouteBlock => "block",
            RouteGround => "ground",
            RouteLiquid => "liquid",
            _ => "other"
        };
        if ((route & FlagFire) != 0)
        {
            id += "+fire";
        }

        if ((route & FlagWaterCreature) != 0)
        {
            id += "+water_creature";
        }

        if ((route & FlagBreakLiquid) != 0)
        {
            id += "+break_liquid";
        }

        if ((route & FlagOceanDamage) != 0)
        {
            id += "+ocean_damage";
        }

        if ((route & FlagDrowning) != 0)
        {
            id += "+drowning";
        }

        if ((route & FlagTileDamage) != 0)
        {
            id += "+tile_damage";
        }

        if ((route & FlagBuildingStep) != 0)
        {
            id += "+building_step";
        }

        return id;
    }

    private void EnsureBatchCapacity(int batchCount)
    {
        if (sampledBatches.Length < batchCount)
        {
            sampledBatches = new bool[batchCount];
        }
    }

    private static long MeasureTimestampOverhead()
    {
        long minimum = long.MaxValue;
        for (int i = 0; i < 16; i++)
        {
            long startedAt = Stopwatch.GetTimestamp();
            long elapsed = Stopwatch.GetTimestamp() - startedAt;
            minimum = Math.Min(minimum, elapsed);
        }

        return minimum == long.MaxValue ? 0L : minimum;
    }

    private void ResetSamples()
    {
        for (int i = 0; i < usedRoutes.Count; i++)
        {
            int route = usedRoutes[i];
            routeSeconds[route] = 0.0;
            routeCalls[route] = 0L;
        }

        usedRoutes.Clear();
        fullCalls = 0L;
        sampledCalls = 0L;
    }
}
