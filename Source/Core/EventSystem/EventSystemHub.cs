using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Cultiway.Core.EventSystem;

/// <summary>
/// 事件系统注册表，用于异步线程安全发布。
/// </summary>
public static class EventSystemHub
{
    private static readonly ConcurrentDictionary<Type, BaseEventSystem> Systems = new();
    private static readonly object Sync = new();
    private static int paused;

    internal static bool IsPaused => Volatile.Read(ref paused) != 0;

    internal static void Register(BaseEventSystem system)
    {
        lock (Sync)
        {
            Systems[system.EventType] = system;
        }
    }

    public static bool TryPublish<TEvent>(TEvent evt)
    {
        lock (Sync)
        {
            if (paused != 0) return false;
            if (Systems.TryGetValue(typeof(TEvent), out var system) && system is GenericEventSystem<TEvent> typed)
            {
                typed.Enqueue(evt);
                return true;
            }
        }

        return false;
    }

    public static void Publish<TEvent>(TEvent evt)
    {
        if (!TryPublish(evt) && !IsPaused)
        {
            ModClass.LogWarningConcurrent($"未找到事件系统: {typeof(TEvent).Name}");
        }
    }

    internal static void PauseAndClear()
    {
        lock (Sync)
        {
            Volatile.Write(ref paused, 1);
            foreach (var system in Systems.Values)
            {
                system.ClearPendingEvents();
            }
        }
    }

    internal static void ClearQueuedEvents()
    {
        lock (Sync)
        {
            foreach (var system in Systems.Values)
            {
                system.ClearPendingEvents();
            }
        }
    }

    internal static void Resume()
    {
        Volatile.Write(ref paused, 0);
    }
}
