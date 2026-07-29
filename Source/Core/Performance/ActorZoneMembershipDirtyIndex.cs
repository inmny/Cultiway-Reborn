using System;
using System.Collections.Generic;
using System.Threading;
using Cultiway.Const;

namespace Cultiway.Core.Performance;

[Flags]
internal enum ActorZoneDirtyKind : byte
{
    None = 0,
    Spatial = 1,
    ChunkMetadata = 2,
    CityEligibility = 4
}

internal readonly struct ActorZoneDirtyEntry
{
    internal ActorZoneDirtyEntry(
        Actor actor,
        ActorZoneDirtyKind kind)
    {
        Actor = actor;
        Kind = kind;
    }

    internal Actor Actor { get; }
    internal ActorZoneDirtyKind Kind { get; }
}

/// <summary>
/// 按模拟工作线程收集会改变空间、chunk 元数据或城市候选关系的角色。
/// 同一轮角色任务结束后存在统一屏障，消费阶段不会与角色移动并发。
/// </summary>
internal static class ActorZoneMembershipDirtyIndex
{
    private static readonly ThreadLocal<
            Dictionary<Actor, ActorZoneDirtyKind>>
        DirtyActorsByThread =
            new(
                static () =>
                    new Dictionary<
                        Actor,
                        ActorZoneDirtyKind>(),
                trackAllValues: true);

    private static readonly Dictionary<
            Actor,
            ActorZoneDirtyKind>
        MergedActors =
        new();

    internal static void Mark(
        Actor actor,
        ActorZoneDirtyKind kind)
    {
        if (actor == null ||
            kind == ActorZoneDirtyKind.None ||
            !PerformanceSettings
                .EnableFramePriorityScheduler)
        {
            return;
        }

        Dictionary<Actor, ActorZoneDirtyKind>
            dirtyActors =
                DirtyActorsByThread.Value;
        if (dirtyActors.TryGetValue(
                actor,
                out ActorZoneDirtyKind previous))
        {
            dirtyActors[actor] =
                previous | kind;
        }
        else
        {
            dirtyActors.Add(actor, kind);
        }
    }

    internal static int Consume(
        List<ActorZoneDirtyEntry> target)
    {
        target.Clear();
        MergedActors.Clear();
        IList<Dictionary<
            Actor,
            ActorZoneDirtyKind>> buckets =
            DirtyActorsByThread.Values;
        for (int i = 0; i < buckets.Count; i++)
        {
            Dictionary<
                Actor,
                ActorZoneDirtyKind> bucket =
                    buckets[i];
            foreach (KeyValuePair<
                         Actor,
                         ActorZoneDirtyKind> pair in
                     bucket)
            {
                if (MergedActors.TryGetValue(
                        pair.Key,
                        out ActorZoneDirtyKind previous))
                {
                    MergedActors[pair.Key] =
                        previous | pair.Value;
                }
                else
                {
                    MergedActors.Add(
                        pair.Key,
                        pair.Value);
                }
            }

            bucket.Clear();
        }

        foreach (KeyValuePair<
                     Actor,
                     ActorZoneDirtyKind> pair in
                 MergedActors)
        {
            target.Add(
                new ActorZoneDirtyEntry(
                    pair.Key,
                    pair.Value));
        }

        MergedActors.Clear();
        return target.Count;
    }

    internal static void Clear()
    {
        MergedActors.Clear();
        IList<Dictionary<
            Actor,
            ActorZoneDirtyKind>> buckets =
            DirtyActorsByThread.Values;
        for (int i = 0; i < buckets.Count; i++)
        {
            buckets[i].Clear();
        }
    }
}
