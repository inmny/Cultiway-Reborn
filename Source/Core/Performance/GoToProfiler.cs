using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using ai.behaviours;

namespace Cultiway.Core.Performance;

internal enum GoToActionKind
{
    Other,
    TileTarget,
    BuildingTarget,
    ActorTarget
}

internal enum GoToTraceSegment
{
    Validate,
    Setup,
    Request,
    SameTile
}

internal enum GoToTraceOutcome
{
    Invalid,
    SameTile,
    Rejected,
    Requested
}

internal static class GoToProfiler
{
    internal const string ActionTileMetricId = "action.tile";
    internal const string ActionBuildingMetricId = "action.building";
    internal const string ActionActorMetricId = "action.actor";

    private const double SlowActionThresholdSeconds = 0.001;
    private static readonly long TimestampOverheadTicks = MeasureTimestampOverhead();

    [ThreadStatic]
    private static PrefixSnapshot pendingPrefix;

    [ThreadStatic]
    private static bool hasPendingPrefix;

    internal static GoToActionKind Classify(BehaviourActionActor action)
    {
        return action switch
        {
            BehGoToTileTarget => GoToActionKind.TileTarget,
            BehGoToBuildingTarget => GoToActionKind.BuildingTarget,
            BehGoToActorTarget => GoToActionKind.ActorTarget,
            _ => GoToActionKind.Other
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static CallMeasurement StartCall(Actor actor)
    {
        return SimulationTickBenchmark.IsCapturing
            ? new CallMeasurement(
                actor?.data?.id ?? -1L,
                Stopwatch.GetTimestamp(),
                GC.CollectionCount(0),
                GC.CollectionCount(1),
                GC.CollectionCount(2))
            : default;
    }

    internal static void CompleteAction(
        GoToActionKind kind,
        string actionId,
        double elapsedSeconds)
    {
        if (kind == GoToActionKind.Other)
        {
            hasPendingPrefix = false;
            return;
        }

        SimulationTickBenchmark.GetCurrentGcDeltas(
            out int tickGen0,
            out int tickGen1,
            out int tickGen2);
        elapsedSeconds = NormalizeSeconds(elapsedSeconds);
        SimulationTickBenchmark.RecordGoToActionMetric(
            GetActionMetricId(kind),
            elapsedSeconds);

        long completedAt = Stopwatch.GetTimestamp();
        long actionTicks = SecondsToStopwatchTicks(elapsedSeconds);
        long freshnessTolerance = Stopwatch.Frequency / 200L;
        bool hasMatchingPrefix =
            hasPendingPrefix &&
            pendingPrefix.CompletedAt > 0L &&
            completedAt - pendingPrefix.CompletedAt <= actionTicks + freshnessTolerance;
        PrefixSnapshot prefix = hasMatchingPrefix ? pendingPrefix : default;
        hasPendingPrefix = false;

        if (hasMatchingPrefix)
        {
            RecordPrefix(prefix);
            SimulationTickBenchmark.RecordGoToDetailMetric(
                "action.outer_gap",
                Math.Max(0.0, elapsedSeconds - prefix.TotalSeconds));
        }
        else
        {
            SimulationTickBenchmark.RecordGoToDetailMetric(
                "action.no_prefix",
                elapsedSeconds);
        }

        if (elapsedSeconds < SlowActionThresholdSeconds ||
            !SimulationTickBenchmark.TryClaimGoToSpikeLog())
        {
            return;
        }

        LogSlowAction(
            kind,
            actionId,
            elapsedSeconds,
            hasMatchingPrefix,
            prefix,
            tickGen0,
            tickGen1,
            tickGen2);
    }

    private static void RecordPrefix(PrefixSnapshot prefix)
    {
        SimulationTickBenchmark.RecordGoToDetailMetric(
            "prefix.total",
            prefix.TotalSeconds);
        SimulationTickBenchmark.RecordGoToDetailMetric(
            "prefix.validate",
            prefix.ValidateSeconds);
        SimulationTickBenchmark.RecordGoToDetailMetric(
            "prefix.setup",
            prefix.SetupSeconds);
        SimulationTickBenchmark.RecordGoToDetailMetric(
            "prefix.request",
            prefix.RequestSeconds);
        SimulationTickBenchmark.RecordGoToDetailMetric(
            "prefix.same_tile",
            prefix.SameTileSeconds);
        SimulationTickBenchmark.RecordGoToDetailMetric(
            "prefix.other",
            prefix.OtherSeconds);
    }

    private static void LogSlowAction(
        GoToActionKind kind,
        string actionId,
        double elapsedSeconds,
        bool hasMatchingPrefix,
        PrefixSnapshot prefix,
        int tickGen0,
        int tickGen1,
        int tickGen2)
    {
        string prefixDetails = hasMatchingPrefix
            ? string.Concat(
                "actor=", prefix.ActorId.ToString(CultureInfo.InvariantCulture),
                " prefix=", FormatMilliseconds(prefix.TotalSeconds),
                " outer_gap=", FormatMilliseconds(Math.Max(0.0, elapsedSeconds - prefix.TotalSeconds)),
                " validate=", FormatMilliseconds(prefix.ValidateSeconds),
                " setup=", FormatMilliseconds(prefix.SetupSeconds),
                " request=", FormatMilliseconds(prefix.RequestSeconds),
                " same_tile=", FormatMilliseconds(prefix.SameTileSeconds),
                " prefix_other=", FormatMilliseconds(prefix.OtherSeconds),
                " outcome=", prefix.Outcome.ToString(),
                " gc_prefix=", FormatCollections(
                    prefix.Gen0Collections,
                    prefix.Gen1Collections,
                    prefix.Gen2Collections))
            : "prefix=none";

        SimulationTickBenchmark.QueueGoToSpike(
            string.Concat(
                "[GoToSpike] action=", string.IsNullOrEmpty(actionId) ? kind.ToString() : actionId,
                " kind=", kind.ToString(),
                " total=", FormatMilliseconds(elapsedSeconds),
                " ", prefixDetails,
                " gc_tick_so_far=", FormatCollections(
                    tickGen0,
                    tickGen1,
                    tickGen2)));
    }

    private static string GetActionMetricId(GoToActionKind kind)
    {
        return kind switch
        {
            GoToActionKind.TileTarget => ActionTileMetricId,
            GoToActionKind.BuildingTarget => ActionBuildingMetricId,
            GoToActionKind.ActorTarget => ActionActorMetricId,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private static string FormatMilliseconds(double seconds)
    {
        return (seconds * 1000.0).ToString("0.000", CultureInfo.InvariantCulture) + "ms";
    }

    private static string FormatCollections(int gen0, int gen1, int gen2)
    {
        return string.Concat(
            gen0.ToString(CultureInfo.InvariantCulture),
            "/",
            gen1.ToString(CultureInfo.InvariantCulture),
            "/",
            gen2.ToString(CultureInfo.InvariantCulture));
    }

    private static double NormalizeSeconds(double seconds)
    {
        return double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0.0
            ? 0.0
            : seconds;
    }

    private static long SecondsToStopwatchTicks(double seconds)
    {
        if (seconds <= 0.0)
        {
            return 0L;
        }

        double ticks = seconds * Stopwatch.Frequency;
        return ticks >= long.MaxValue ? long.MaxValue : (long)ticks;
    }

    private static long MeasureTimestampOverhead()
    {
        long minimum = long.MaxValue;
        for (int i = 0; i < 16; i++)
        {
            long startedAt = Stopwatch.GetTimestamp();
            minimum = Math.Min(minimum, Stopwatch.GetTimestamp() - startedAt);
        }

        return minimum == long.MaxValue ? 0L : minimum;
    }

    private static long MeasureElapsedTicks(long startedAt)
    {
        return startedAt <= 0L
            ? 0L
            : Math.Max(0L, Stopwatch.GetTimestamp() - startedAt - TimestampOverheadTicks);
    }

    private static void PublishPrefix(PrefixSnapshot prefix)
    {
        pendingPrefix = prefix;
        hasPendingPrefix = true;
    }

    internal struct CallMeasurement
    {
        private readonly long actorId;
        private readonly long startedAt;
        private readonly int startGen0Collections;
        private readonly int startGen1Collections;
        private readonly int startGen2Collections;
        private long validateTicks;
        private long setupTicks;
        private long requestTicks;
        private long sameTileTicks;

        internal CallMeasurement(
            long actorId,
            long startedAt,
            int startGen0Collections,
            int startGen1Collections,
            int startGen2Collections)
        {
            this.actorId = actorId;
            this.startedAt = startedAt;
            this.startGen0Collections = startGen0Collections;
            this.startGen1Collections = startGen1Collections;
            this.startGen2Collections = startGen2Collections;
            validateTicks = 0L;
            setupTicks = 0L;
            requestTicks = 0L;
            sameTileTicks = 0L;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal long StartSegment()
        {
            return startedAt > 0L ? Stopwatch.GetTimestamp() : 0L;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void CompleteSegment(GoToTraceSegment segment, long segmentStartedAt)
        {
            if (startedAt <= 0L || segmentStartedAt <= 0L)
            {
                return;
            }

            long elapsedTicks = MeasureElapsedTicks(segmentStartedAt);
            switch (segment)
            {
                case GoToTraceSegment.Validate:
                    validateTicks += elapsedTicks;
                    break;
                case GoToTraceSegment.Setup:
                    setupTicks += elapsedTicks;
                    break;
                case GoToTraceSegment.Request:
                    requestTicks += elapsedTicks;
                    break;
                case GoToTraceSegment.SameTile:
                    sameTileTicks += elapsedTicks;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(segment), segment, null);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Complete(GoToTraceOutcome outcome)
        {
            if (startedAt <= 0L)
            {
                return;
            }

            long completedAt = Stopwatch.GetTimestamp();
            int gen0Collections = GC.CollectionCount(0);
            int gen1Collections = GC.CollectionCount(1);
            int gen2Collections = GC.CollectionCount(2);
            PublishPrefix(new PrefixSnapshot(
                actorId,
                completedAt,
                Math.Max(0L, completedAt - startedAt - TimestampOverheadTicks),
                validateTicks,
                setupTicks,
                requestTicks,
                sameTileTicks,
                Math.Max(0, gen0Collections - startGen0Collections),
                Math.Max(0, gen1Collections - startGen1Collections),
                Math.Max(0, gen2Collections - startGen2Collections),
                outcome));
        }
    }

    private readonly struct PrefixSnapshot
    {
        internal PrefixSnapshot(
            long actorId,
            long completedAt,
            long totalTicks,
            long validateTicks,
            long setupTicks,
            long requestTicks,
            long sameTileTicks,
            int gen0Collections,
            int gen1Collections,
            int gen2Collections,
            GoToTraceOutcome outcome)
        {
            ActorId = actorId;
            CompletedAt = completedAt;
            TotalSeconds = totalTicks / (double)Stopwatch.Frequency;
            ValidateSeconds = validateTicks / (double)Stopwatch.Frequency;
            SetupSeconds = setupTicks / (double)Stopwatch.Frequency;
            RequestSeconds = requestTicks / (double)Stopwatch.Frequency;
            SameTileSeconds = sameTileTicks / (double)Stopwatch.Frequency;
            OtherSeconds = Math.Max(
                0.0,
                TotalSeconds -
                ValidateSeconds -
                SetupSeconds -
                RequestSeconds -
                SameTileSeconds);
            Gen0Collections = gen0Collections;
            Gen1Collections = gen1Collections;
            Gen2Collections = gen2Collections;
            Outcome = outcome;
        }

        internal long ActorId { get; }
        internal long CompletedAt { get; }
        internal double TotalSeconds { get; }
        internal double ValidateSeconds { get; }
        internal double SetupSeconds { get; }
        internal double RequestSeconds { get; }
        internal double SameTileSeconds { get; }
        internal double OtherSeconds { get; }
        internal int Gen0Collections { get; }
        internal int Gen1Collections { get; }
        internal int Gen2Collections { get; }
        internal GoToTraceOutcome Outcome { get; }
    }
}
