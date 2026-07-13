using System.Collections.Generic;

namespace Cultiway.Core;

/// <summary>
///     Mod 内容可注册的主动工作选择入口。高优先级选择器先于低优先级选择器执行。
/// </summary>
public static class ActorJobSelectionRegistry
{
    /// <summary>
    ///     主动工作选择器。返回 true 表示已经决定工作，并应通过 jobId 返回有效 ActorJob 标识。
    /// </summary>
    public delegate bool ActorJobSelector(Actor actor, ref string jobId);

    /// <summary>按优先级降序保存选择器；相同委托只允许注册一次。</summary>
    private static readonly List<Entry> _entries = new();

    /// <summary>
    ///     注册一个主动工作选择器。priority 越大越先执行；重复注册同一委托不会改变原有优先级。
    /// </summary>
    public static void Register(ActorJobSelector selector, int priority = 0)
    {
        if (selector == null) return;
        for (var i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Selector == selector) return;
        }

        _entries.Add(new Entry(selector, priority));
        _entries.Sort((left, right) => right.Priority.CompareTo(left.Priority));
    }

    /// <summary>
    ///     按优先级询问已注册选择器，首个成功结果会写入 jobId 并终止后续选择。
    /// </summary>
    public static bool TrySelect(Actor actor, ref string jobId)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Selector(actor, ref jobId)) return true;
        }
        return false;
    }

    /// <summary>选择器及其固定优先级的内部记录。</summary>
    private readonly struct Entry
    {
        /// <summary>创建一条带固定优先级的选择器记录。</summary>
        public Entry(ActorJobSelector selector, int priority)
        {
            Selector = selector;
            Priority = priority;
        }

        /// <summary>实际参与工作决策的回调。</summary>
        public ActorJobSelector Selector { get; }

        /// <summary>选择器执行优先级；数值越大越早执行。</summary>
        public int Priority { get; }
    }
}
