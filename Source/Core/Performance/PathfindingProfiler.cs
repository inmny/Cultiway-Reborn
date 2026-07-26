using System;
using System.Diagnostics;
using System.Threading;

namespace Cultiway.Core.Performance;

internal enum PathfindingBenchmarkMetric
{
    Reuse,
    ReuseMiss,
    Create,
    TaskCreate,
    Cancel,
    CancelEmpty,
    Enqueue,
    QueueWait,
    BackgroundPath,
    Count
}

internal static class PathfindingProfiler
{
    private static Session activeSession;

    internal static void SetEnabled(bool enabled)
    {
        Session current = Volatile.Read(ref activeSession);
        if (enabled)
        {
            if (current == null)
            {
                Interlocked.CompareExchange(ref activeSession, new Session(), null);
            }

            return;
        }

        if (current != null)
        {
            Interlocked.Exchange(ref activeSession, null);
        }
    }

    internal static Measurement Start()
    {
        return Start(Volatile.Read(ref activeSession));
    }

    internal static Measurement Start(Session session)
    {
        if (!IsCurrent(session))
        {
            return default;
        }

        return new Measurement(session, Stopwatch.GetTimestamp());
    }

    internal static long MarkEnqueued(Session session)
    {
        return IsCurrent(session) ? Stopwatch.GetTimestamp() : 0L;
    }

    internal static void RecordQueueWait(Session session, long enqueuedAt)
    {
        if (enqueuedAt == 0L || !IsCurrent(session))
        {
            return;
        }

        session.Record(
            PathfindingBenchmarkMetric.QueueWait,
            Math.Max(0L, Stopwatch.GetTimestamp() - enqueuedAt),
            false);
    }

    internal static Snapshot CaptureSnapshot()
    {
        Session session = Volatile.Read(ref activeSession);
        return session?.CaptureSnapshot() ?? default;
    }

    private static bool IsCurrent(Session session)
    {
        return session != null && ReferenceEquals(Volatile.Read(ref activeSession), session);
    }

    internal sealed class Session
    {
        private readonly long[] elapsedTicks = new long[(int)PathfindingBenchmarkMetric.Count];
        private readonly long[] counters = new long[(int)PathfindingBenchmarkMetric.Count];
        private readonly long timestampOverheadTicks = MeasureTimestampOverhead();

        internal void Record(PathfindingBenchmarkMetric metric, long ticks, bool subtractTimestampOverhead)
        {
            if (!IsCurrent(this))
            {
                return;
            }

            if (subtractTimestampOverhead)
            {
                ticks = Math.Max(0L, ticks - timestampOverheadTicks);
            }

            int index = (int)metric;
            Interlocked.Add(ref elapsedTicks[index], ticks);
            Interlocked.Increment(ref counters[index]);
        }

        internal Snapshot CaptureSnapshot()
        {
            return new Snapshot(
                this,
                Capture(PathfindingBenchmarkMetric.Reuse),
                Capture(PathfindingBenchmarkMetric.ReuseMiss),
                Capture(PathfindingBenchmarkMetric.Create),
                Capture(PathfindingBenchmarkMetric.TaskCreate),
                Capture(PathfindingBenchmarkMetric.Cancel),
                Capture(PathfindingBenchmarkMetric.CancelEmpty),
                Capture(PathfindingBenchmarkMetric.Enqueue),
                Capture(PathfindingBenchmarkMetric.QueueWait),
                Capture(PathfindingBenchmarkMetric.BackgroundPath));
        }

        private MetricSnapshot Capture(PathfindingBenchmarkMetric metric)
        {
            int index = (int)metric;
            return new MetricSnapshot(
                Interlocked.Read(ref elapsedTicks[index]),
                Interlocked.Read(ref counters[index]));
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
    }

    internal readonly struct Measurement
    {
        private readonly long startedAt;

        internal Measurement(Session session, long startedAt)
        {
            Session = session;
            this.startedAt = startedAt;
        }

        internal Session Session { get; }

        internal void Complete(PathfindingBenchmarkMetric metric)
        {
            if (Session == null)
            {
                return;
            }

            Session.Record(
                metric,
                Math.Max(0L, Stopwatch.GetTimestamp() - startedAt),
                true);
        }
    }

    internal readonly struct MetricSnapshot
    {
        internal MetricSnapshot(long elapsedTicks, long counter)
        {
            ElapsedTicks = elapsedTicks;
            Counter = counter;
        }

        internal long ElapsedTicks { get; }
        internal long Counter { get; }
        internal double Seconds => ElapsedTicks / (double)Stopwatch.Frequency;

        internal MetricSnapshot DeltaFrom(MetricSnapshot earlier)
        {
            return new MetricSnapshot(
                Math.Max(0L, ElapsedTicks - earlier.ElapsedTicks),
                Math.Max(0L, Counter - earlier.Counter));
        }
    }

    internal readonly struct Snapshot
    {
        internal Snapshot(
            Session session,
            MetricSnapshot reuse,
            MetricSnapshot reuseMiss,
            MetricSnapshot create,
            MetricSnapshot taskCreate,
            MetricSnapshot cancel,
            MetricSnapshot cancelEmpty,
            MetricSnapshot enqueue,
            MetricSnapshot queueWait,
            MetricSnapshot backgroundPath)
        {
            Session = session;
            Reuse = reuse;
            ReuseMiss = reuseMiss;
            Create = create;
            TaskCreate = taskCreate;
            Cancel = cancel;
            CancelEmpty = cancelEmpty;
            Enqueue = enqueue;
            QueueWait = queueWait;
            BackgroundPath = backgroundPath;
        }

        private Session Session { get; }
        internal MetricSnapshot Reuse { get; }
        internal MetricSnapshot ReuseMiss { get; }
        internal MetricSnapshot Create { get; }
        internal MetricSnapshot TaskCreate { get; }
        internal MetricSnapshot Cancel { get; }
        internal MetricSnapshot CancelEmpty { get; }
        internal MetricSnapshot Enqueue { get; }
        internal MetricSnapshot QueueWait { get; }
        internal MetricSnapshot BackgroundPath { get; }

        internal Snapshot DeltaFrom(Snapshot earlier)
        {
            if (Session == null || !ReferenceEquals(Session, earlier.Session))
            {
                return default;
            }

            return new Snapshot(
                Session,
                Reuse.DeltaFrom(earlier.Reuse),
                ReuseMiss.DeltaFrom(earlier.ReuseMiss),
                Create.DeltaFrom(earlier.Create),
                TaskCreate.DeltaFrom(earlier.TaskCreate),
                Cancel.DeltaFrom(earlier.Cancel),
                CancelEmpty.DeltaFrom(earlier.CancelEmpty),
                Enqueue.DeltaFrom(earlier.Enqueue),
                QueueWait.DeltaFrom(earlier.QueueWait),
                BackgroundPath.DeltaFrom(earlier.BackgroundPath));
        }
    }
}
