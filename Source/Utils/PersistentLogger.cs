using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace Cultiway.Utils;

public class PersistentLogger
{
    private StringBuilder _builder = new();
    private string _path;
    private static readonly Dictionary<string, PersistentLogger> Loggers = new();
    public static PersistentLogger Get(string path)
    {
        if (!Loggers.TryGetValue(path, out var logger))
        {
            logger = new PersistentLogger(path);
            Loggers.Add(path, logger);
        }
        return logger;
    }

    private PersistentLogger(string path)
    {
        _path = path;
        if (File.Exists(path))
        {
            _builder.Append(File.ReadAllText(path));
        }
    }
    public void Log(string message)
    {
        lock (this)
        {
            _builder.AppendLine($"[{System.DateTime.Now:yyyy-MM-dd HH：mm：ss：ffff}]: {message}");
        }
    }

    internal static void Save()
    {
        foreach (var logger_item in Loggers)
        {
            var logger = logger_item.Value;
            lock (logger)
            {
                File.WriteAllText(logger._path, logger._builder.ToString());
            }
        }
    }
}