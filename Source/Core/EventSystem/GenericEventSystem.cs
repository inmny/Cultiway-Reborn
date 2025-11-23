using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Cultiway.Core.EventSystem;

/// <summary>
/// 通用事件系统，负责缓冲与派发具体事件类型。
/// </summary>
public abstract class GenericEventSystem<TEvent> : BaseEventSystem
{
    private readonly ConcurrentQueue<TEvent> _queue = new();
    private readonly List<TEvent> _buffer = new(32);

    protected virtual int MaxEventsPerUpdate => 32;

    protected GenericEventSystem()
    {
        EventSystemHub.Register(this);
    }

    internal override Type EventType => typeof(TEvent);

    public void Enqueue(TEvent evt)
    {
        _queue.Enqueue(evt);
    }

    protected override void ProcessEvents()
    {
        _buffer.Clear();
        for (var i = 0; i < MaxEventsPerUpdate && _queue.TryDequeue(out var evt); i++)
        {
            _buffer.Add(evt);
        }

        foreach (var evt in _buffer)
        {
            try
            {
                HandleEvent(evt);
            }
            catch (Exception e)
            {
                ModClass.LogErrorConcurrent(e.ToString());
            }
        }
    }

    /// <summary>
    /// 处理单个事件。
    /// </summary>
    protected abstract void HandleEvent(TEvent evt);
}
