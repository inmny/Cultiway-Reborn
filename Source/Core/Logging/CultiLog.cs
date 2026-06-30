using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cultiway.Core.Logging;

public static class CultiLog
{
    private const int DefaultMaxBacklog = 262144;
    private const int WorkerBatchSize = 4096;

    private static readonly ConcurrentQueue<CultiLogRecord> Queue = new();
    private static readonly CultiLogRecentStore Recent = new(4096);
    private static readonly CultiLogFileSink FileSink = new();

    private static volatile bool _initialized;
    private static volatile bool _running;
    private static Task _worker;
    private static string _folder;

    private static long _sequence;
    private static long _written;
    private static long _dropped;
    private static int _queuedCount;
    private static int _maxBacklog = DefaultMaxBacklog;

    private static volatile bool _enabled;
    private static long _categoryMask = (long)CultiLogCategory.None;
    private static int _minLevel = (int)CultiLogLevel.Debug;

    public static bool Enabled => _enabled;
    public static bool DiskEnabled => FileSink.Enabled;
    public static CultiLogCategory CategoryMask => (CultiLogCategory)_categoryMask;
    public static CultiLogLevel MinLevel => (CultiLogLevel)_minLevel;

    public static CultiLogArgs.CultiLogArgsBuilder Args(int capacity = 4)
    {
        return CultiLogArgs.Create(capacity);
    }

    public static void Initialize(string modFolder)
    {
        if (_initialized) return;

        _folder = Path.Combine(modFolder, "Logs");
        Directory.CreateDirectory(_folder);
        FileSink.Initialize(_folder);

        _running = true;
        _worker = Task.Factory.StartNew(WorkerLoop, TaskCreationOptions.LongRunning);
        _initialized = true;
        _ = CultiLogEvents.General.Message;
        _ = CultiLogEvents.General.Error;
        _ = CultiLogEvents.Sect.Verify;
        _ = CultiLogEvents.Combat.DamageResolved;
    }

    public static void Shutdown()
    {
        _running = false;
        try
        {
            _worker?.Wait(3000);
        }
        catch
        {
            // 退出阶段不能让日志线程异常阻塞游戏关闭。
        }

        FileSink.Close();
    }

    public static void SetEnabled(bool enabled)
    {
        _enabled = enabled;
    }

    public static void SetDiskEnabled(bool enabled, bool resetToday = false)
    {
        FileSink.SetEnabled(enabled, resetToday);
    }

    public static string GetDiskFilePath()
    {
        return FileSink.CurrentPath;
    }

    public static void SetMinLevel(CultiLogLevel level)
    {
        _minLevel = (int)level;
    }

    public static void SetCategoryMask(CultiLogCategory categories)
    {
        _categoryMask = (long)categories;
    }

    public static bool IsCategoryEnabled(CultiLogCategory category)
    {
        if (category == CultiLogCategory.None) return false;
        return (CategoryMask & category) != 0;
    }

    public static bool SetCategoryEnabled(CultiLogCategory category, bool enabled)
    {
        if (category == CultiLogCategory.None) return false;

        CultiLogCategory mask = CategoryMask;
        mask = enabled ? mask | category : mask & ~category;
        SetCategoryMask(mask);
        return enabled;
    }

    public static bool ToggleCategory(CultiLogCategory category)
    {
        bool enabled = !IsCategoryEnabled(category);
        return SetCategoryEnabled(category, enabled);
    }

    public static void SetCategoryMask(string categories)
    {
        SetCategoryMask(ParseCategoryMask(categories, CategoryMask));
    }

    public static void SetRecentCapacity(int capacity)
    {
        Recent.Resize(capacity);
    }

    public static void SetMaxBacklog(int maxBacklog)
    {
        _maxBacklog = Math.Max(1024, maxBacklog);
    }

    public static bool IsEnabled(CultiLogEventDef def)
    {
        if (!_enabled || def == null) return false;
        if ((def.Category & (CultiLogCategory)_categoryMask) == 0) return false;
        return (int)def.Level >= _minLevel;
    }

    public static void Emit(CultiLogEventDef def, CultiLogArgs args = null, long actorId = -1, long targetId = -1,
        long entityId = -1, int x = int.MinValue, int y = int.MinValue)
    {
        EmitCore(def, args, actorId, targetId, entityId, x, y, false);
    }

    private static void EmitWhenLogEnabled(CultiLogEventDef def, CultiLogArgs args = null, long actorId = -1,
        long targetId = -1, long entityId = -1, int x = int.MinValue, int y = int.MinValue)
    {
        EmitCore(def, args, actorId, targetId, entityId, x, y, true);
    }

    private static void EmitCore(CultiLogEventDef def, CultiLogArgs args, long actorId, long targetId, long entityId,
        int x, int y, bool ignoreEventFilters)
    {
        if (ignoreEventFilters)
        {
            if (!_enabled || def == null) return;
        }
        else if (!IsEnabled(def)) return;

        int queued = Interlocked.Increment(ref _queuedCount);
        if (queued > _maxBacklog)
        {
            Interlocked.Decrement(ref _queuedCount);
            Interlocked.Increment(ref _dropped);
            return;
        }

        Queue.Enqueue(new CultiLogRecord
        {
            Sequence = Interlocked.Increment(ref _sequence),
            RealTicks = DateTime.UtcNow.Ticks,
            WorldTime = GetWorldTime(),
            EventId = def.Id,
            EventName = def.Name,
            Template = def.Template,
            Category = def.Category,
            Level = def.Level,
            ActorId = actorId,
            TargetId = targetId,
            EntityId = entityId,
            X = x,
            Y = y,
            Args = args?.Items ?? CultiLogArgs.Empty.Items
        });
    }

    public static class General
    {
        public static void Info(string message)
        {
            Emit(CultiLogEvents.General.Message, Args(1).Str("message", message).Build());
        }

        public static void Error(string message)
        {
            Emit(CultiLogEvents.General.Error, Args(1).Str("message", message).Build());
        }
    }

    public static class Sect
    {
        public static bool VerifyEnabled => IsEnabled(CultiLogEvents.Sect.Verify);

        public static void Verify(string action, string message, long actorId = -1, long targetId = -1)
        {
            Emit(
                CultiLogEvents.Sect.Verify,
                Args(2)
                    .Str("action", action)
                    .Str("message", message)
                    .Build(),
                actorId,
                targetId);
        }
    }

    public static class Combat
    {
        public static bool DamageResolvedEnabled => Enabled;

        public static void DamageResolved(string message, long targetId, long attackerId)
        {
            EmitWhenLogEnabled(
                CultiLogEvents.Combat.DamageResolved,
                Args(1).Str("message", message).Build(),
                targetId,
                attackerId);
        }
    }

    public static List<CultiLogRecord> GetRecent(CultiLogFilter filter = null, int maxCount = 512)
    {
        return Recent.Snapshot(filter, maxCount);
    }

    public static void ClearRecent()
    {
        Recent.Clear();
    }

    public static string ExportRecentToJsonl(CultiLogFilter filter = null, int maxCount = 10000, bool renderMessage = true)
    {
        string exportFolder = Path.Combine(_folder, "Exports");
        Directory.CreateDirectory(exportFolder);
        string path = Path.Combine(exportFolder, $"cultiway-log-{DateTime.Now:yyyyMMdd-HHmmss}.cultilog");
        List<CultiLogRecord> records = Recent.Snapshot(filter, maxCount, false);

        using StreamWriter writer = new(path, false, new UTF8Encoding(false), 64 * 1024);
        StringBuilder sb = new(512);
        for (int i = 0; i < records.Count; i++)
        {
            sb.Clear();
            CultiLogJson.AppendRecord(sb, records[i], renderMessage);
            writer.WriteLine(sb.ToString());
        }

        return path;
    }

    public static CultiLogStats GetStats()
    {
        return new CultiLogStats(
            _enabled,
            FileSink.Enabled,
            (CultiLogCategory)_categoryMask,
            (CultiLogLevel)_minLevel,
            _queuedCount,
            Recent.Count,
            Recent.Capacity,
            Interlocked.Read(ref _written),
            Interlocked.Read(ref _dropped));
    }

    public static string GetStatsText()
    {
        CultiLogStats stats = GetStats();
        return $"[CultiLog] enabled={stats.Enabled} disk={stats.DiskEnabled} level>={stats.MinLevel} categories={stats.CategoryMask} queued={stats.QueuedCount} recent={stats.RecentCount}/{stats.RecentCapacity} written={stats.WrittenCount} dropped={stats.DroppedCount}";
    }

    public static CultiLogCategory ParseCategoryMask(string text, CultiLogCategory fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;

        text = text.Trim();
        if (text.Equals("all", StringComparison.OrdinalIgnoreCase)) return CultiLogCategory.All;
        if (text.Equals("none", StringComparison.OrdinalIgnoreCase)) return CultiLogCategory.None;

        CultiLogCategory result = CultiLogCategory.None;
        string[] parts = text.Split(new[] { ',', '|', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (Enum.TryParse(parts[i], true, out CultiLogCategory category))
            {
                result |= category;
            }
        }

        return result == CultiLogCategory.None ? fallback : result;
    }

    private static void WorkerLoop()
    {
        List<CultiLogRecord> batch = new(WorkerBatchSize);
        while (_running || !Queue.IsEmpty)
        {
            batch.Clear();
            while (batch.Count < WorkerBatchSize && Queue.TryDequeue(out CultiLogRecord record))
            {
                Interlocked.Decrement(ref _queuedCount);
                Recent.Add(record);
                batch.Add(record);
            }

            if (batch.Count > 0)
            {
                Interlocked.Add(ref _written, batch.Count);
                FileSink.WriteBatch(batch);
                continue;
            }

            Thread.Sleep(10);
        }

        FileSink.Flush();
    }

    private static float GetWorldTime()
    {
        try
        {
            return World.world == null ? 0f : (float)World.world.getCurWorldTime();
        }
        catch
        {
            return 0f;
        }
    }
}
