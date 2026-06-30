using System;
using System.Collections.Generic;

namespace Cultiway.Core.Logging;

public sealed class CultiLogRecentStore
{
    private readonly object _lock = new();
    private CultiLogRecord[] _records;
    private int _nextIndex;
    private int _count;

    public CultiLogRecentStore(int capacity)
    {
        _records = new CultiLogRecord[Math.Max(16, capacity)];
    }

    public int Capacity
    {
        get
        {
            lock (_lock) return _records.Length;
        }
    }

    public int Count
    {
        get
        {
            lock (_lock) return _count;
        }
    }

    public void Add(CultiLogRecord record)
    {
        lock (_lock)
        {
            _records[_nextIndex] = record;
            _nextIndex = (_nextIndex + 1) % _records.Length;
            if (_count < _records.Length) _count++;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            Array.Clear(_records, 0, _records.Length);
            _nextIndex = 0;
            _count = 0;
        }
    }

    public void Resize(int capacity)
    {
        capacity = Math.Max(16, capacity);
        lock (_lock)
        {
            if (capacity == _records.Length) return;

            List<CultiLogRecord> snapshot = SnapshotLocked(null, Math.Min(_count, capacity), false);
            _records = new CultiLogRecord[capacity];
            _nextIndex = 0;
            _count = 0;

            for (int i = 0; i < snapshot.Count; i++)
            {
                _records[_nextIndex] = snapshot[i];
                _nextIndex = (_nextIndex + 1) % _records.Length;
                _count++;
            }
        }
    }

    public List<CultiLogRecord> Snapshot(CultiLogFilter filter, int maxCount, bool newestFirst = true)
    {
        lock (_lock)
        {
            return SnapshotLocked(filter, maxCount, newestFirst);
        }
    }

    private List<CultiLogRecord> SnapshotLocked(CultiLogFilter filter, int maxCount, bool newestFirst)
    {
        maxCount = maxCount <= 0 ? _count : Math.Min(maxCount, _count);
        List<CultiLogRecord> result = new(maxCount);

        for (int i = 0; i < _count && result.Count < maxCount; i++)
        {
            int index = newestFirst
                ? (_nextIndex - 1 - i + _records.Length) % _records.Length
                : (_nextIndex - _count + i + _records.Length) % _records.Length;

            CultiLogRecord record = _records[index];
            if (filter != null && !filter.Matches(record)) continue;
            result.Add(record);
        }

        return result;
    }
}
