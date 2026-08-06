using System;
using System.Collections.Generic;
using Cultiway.Utils.Extension;

namespace Cultiway.Core;

/// <summary>
/// 汇总角色当前真实活动的任务栏展示。低优先级长期上下文会保留，
/// 高优先级即时动作追加在后，而不是互相覆盖。
/// </summary>
public static class ActorActivityPresentationRegistry
{
    /// <summary>尝试解析一个角色活动展示片段。</summary>
    public delegate bool PresentationProvider(
        Actor actor,
        out ActorActivityPresentationSegment segment);

    private static readonly List<Entry> Entries = new();

    /// <summary>注册展示提供者；优先级越高，其文本越靠近最终即时动作。</summary>
    public static void Register(PresentationProvider provider, int priority)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        for (var i = 0; i < Entries.Count; i++)
        {
            if (Entries[i].Provider == provider) return;
        }
        Entries.Add(new Entry(provider, priority));
        Entries.Sort((left, right) => left.Priority.CompareTo(right.Priority));
    }

    /// <summary>按优先级合并全部有效片段。</summary>
    public static bool TryResolve(
        Actor actor,
        out ActorActivityPresentation presentation)
    {
        using var segments = new ListPool<ResolvedSegment>();
        for (var i = 0; i < Entries.Count; i++)
        {
            Entry entry = Entries[i];
            if (!entry.Provider(actor, out ActorActivityPresentationSegment segment) ||
                string.IsNullOrEmpty(segment.PrimaryLocaleKey))
                continue;
            segments.Add(new ResolvedSegment(entry.Priority, segment));
        }

        if (segments.Count == 0)
        {
            presentation = default;
            return false;
        }

        segments.Sort((left, right) => left.Priority.CompareTo(right.Priority));
        using var localeKeys = new ListPool<string>();
        double startedAt = double.MaxValue;
        for (var i = 0; i < segments.Count; i++)
        {
            ActorActivityPresentationSegment segment = segments[i].Segment;
            localeKeys.Add(segment.PrimaryLocaleKey);
            if (!string.IsNullOrEmpty(segment.SecondaryLocaleKey))
                localeKeys.Add(segment.SecondaryLocaleKey);
            startedAt = Math.Min(startedAt, segment.StartedAt);
        }
        presentation = new ActorActivityPresentation(localeKeys.ToArray(), startedAt);
        return true;
    }

    /// <summary>展示提供者与固定优先级。</summary>
    private readonly struct Entry
    {
        /// <summary>创建提供者记录。</summary>
        internal Entry(PresentationProvider provider, int priority)
        {
            Provider = provider;
            Priority = priority;
        }

        internal PresentationProvider Provider { get; }
        internal int Priority { get; }
    }

    /// <summary>已经附加注册优先级的临时片段。</summary>
    private readonly struct ResolvedSegment
    {
        /// <summary>创建排序片段。</summary>
        internal ResolvedSegment(int priority, ActorActivityPresentationSegment segment)
        {
            Priority = priority;
            Segment = segment;
        }

        internal int Priority { get; }
        internal ActorActivityPresentationSegment Segment { get; }
    }
}

/// <summary>单个系统提供的一至两段活动本地化文本。</summary>
public readonly struct ActorActivityPresentationSegment
{
    /// <summary>创建展示片段。</summary>
    public ActorActivityPresentationSegment(
        string primaryLocaleKey,
        string secondaryLocaleKey,
        double startedAt)
    {
        PrimaryLocaleKey = primaryLocaleKey;
        SecondaryLocaleKey = secondaryLocaleKey;
        StartedAt = startedAt;
    }

    /// <summary>主要活动本地化键。</summary>
    public string PrimaryLocaleKey { get; }

    /// <summary>可选的细分动作本地化键。</summary>
    public string SecondaryLocaleKey { get; }

    /// <summary>该活动开始时的世界时间。</summary>
    public double StartedAt { get; }
}

/// <summary>任务栏最终使用的已合并活动展示。</summary>
public readonly struct ActorActivityPresentation
{
    /// <summary>创建合并结果。</summary>
    internal ActorActivityPresentation(string[] localeKeys, double startedAt)
    {
        LocaleKeys = localeKeys ?? Array.Empty<string>();
        StartedAt = startedAt;
    }

    /// <summary>按“长期上下文到即时动作”排列的本地化键。</summary>
    public IReadOnlyList<string> LocaleKeys { get; }

    /// <summary>所有有效片段中最早的开始时间。</summary>
    public double StartedAt { get; }
}
