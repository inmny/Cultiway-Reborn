using System;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.EventSystem;

/// <summary>
/// 基础事件系统，继承 BaseSystem 便于性能统计。
/// </summary>
public abstract class BaseEventSystem : BaseSystem
{
    internal abstract Type EventType { get; }

    protected override void OnUpdateGroup()
    {
        base.OnUpdateGroup();
        ProcessEvents();
    }

    /// <summary>
    /// 由子类实现事件处理逻辑。
    /// </summary>
    protected abstract void ProcessEvents();
}
