using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cultiway.Core.Logging;

public sealed class CultiLogFileSink
{
    private readonly object _lock = new();
    private string _folder;
    private StreamWriter _writer;
    private string _currentFileDate;
    private string _currentPath;
    private long _lastFlushTicks;

    public bool Enabled { get; private set; }

    public string CurrentPath
    {
        get
        {
            lock (_lock)
            {
                return _currentPath;
            }
        }
    }

    public void Initialize(string folder)
    {
        _folder = folder;
        Directory.CreateDirectory(_folder);
    }

    public void SetEnabled(bool enabled, bool resetToday = false)
    {
        lock (_lock)
        {
            Enabled = enabled;
            if (Enabled)
            {
                EnsureWriterLocked(resetToday);
                _writer.Flush();
            }
            else
            {
                CloseLocked();
            }
        }
    }

    public void WriteBatch(List<CultiLogRecord> batch)
    {
        if (!Enabled || batch == null || batch.Count == 0) return;

        lock (_lock)
        {
            if (!Enabled) return;
            EnsureWriterLocked(false);

            StringBuilder sb = new(512);
            for (int i = 0; i < batch.Count; i++)
            {
                sb.Clear();
                CultiLogJson.AppendRecord(sb, batch[i], false);
                _writer.WriteLine(sb.ToString());
            }

            long now = DateTime.UtcNow.Ticks;
            if (now - _lastFlushTicks > TimeSpan.TicksPerSecond)
            {
                _writer.Flush();
                _lastFlushTicks = now;
            }
        }
    }

    public void Flush()
    {
        lock (_lock)
        {
            _writer?.Flush();
        }
    }

    public void Close()
    {
        lock (_lock)
        {
            CloseLocked();
        }
    }

    private void EnsureWriterLocked(bool resetToday)
    {
        string date = DateTime.Now.ToString("yyyyMMdd");
        if (_writer != null && _currentFileDate == date && !resetToday) return;

        CloseLocked();
        _currentFileDate = date;
        _currentPath = Path.Combine(_folder, $"cultiway-{date}.cultilog");
        _writer = new StreamWriter(_currentPath, !resetToday, new UTF8Encoding(false), 64 * 1024);
        _lastFlushTicks = DateTime.UtcNow.Ticks;
    }

    private void CloseLocked()
    {
        if (_writer == null) return;
        _writer.Flush();
        _writer.Dispose();
        _writer = null;
        _currentFileDate = null;
        _currentPath = null;
    }
}
