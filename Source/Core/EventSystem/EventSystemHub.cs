using System;
using System.Collections.Concurrent;

namespace Cultiway.Core.EventSystem;

/// <summary>
/// 事件系统注册表，用于异步线程安全发布。
/// </summary>
public static class EventSystemHub
{
    private static readonly ConcurrentDictionary<Type, BaseEventSystem> Systems = new();

    internal static void Register(BaseEventSystem system)
    {
        Systems[system.EventType] = system;
    }

    public static bool TryPublish<TEvent>(TEvent evt)
    {
        if (Systems.TryGetValue(typeof(TEvent), out var system) && system is GenericEventSystem<TEvent> typed)
        {
            typed.Enqueue(evt);
            return true;
        }

        return false;
    }

    public static void Publish<TEvent>(TEvent evt)
    {
        if (!TryPublish(evt))
        {
            ModClass.LogWarningConcurrent($"未找到事件系统: {typeof(TEvent).Name}");
        }
    }
}
