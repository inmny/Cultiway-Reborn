using System.Collections.Generic;
using System.Threading;

namespace Cultiway.Core.Performance;

/// <summary>
/// 分离 ActorManager 容器结构版本与四个元数据分区的成员变化。
/// Actor.setAlive(false) 会额外递增原版 manager version，
/// 但它只改变分类，不改变容器结构。
/// </summary>
internal static class ActorMetaPartitionVersion
{
    private static readonly object SyncRoot = new();
    private static readonly HashSet<Actor> DirtyActors =
        new();

    private static int version;
    private static int aliveManagerVersionBumps;

    internal static int Version => Volatile.Read(ref version);

    internal static int GetStructuralVersion(
        int managerVersion)
    {
        return unchecked(
            managerVersion -
            Volatile.Read(ref aliveManagerVersionBumps));
    }

    internal static int ConsumeDirtyActors(
        List<Actor> target)
    {
        lock (SyncRoot)
        {
            target.Clear();
            target.AddRange(DirtyActors);
            DirtyActors.Clear();
            return version;
        }
    }

    internal static void MarkAliveCall(
        Actor actor,
        bool previousAlive,
        bool nextAlive)
    {
        if (!nextAlive)
        {
            Interlocked.Increment(
                ref aliveManagerVersionBumps);
        }

        if (previousAlive == nextAlive)
        {
            return;
        }

        MarkPartitionChange(actor);
    }

    internal static void MarkKingdomChange(
        Actor actor,
        Kingdom nextKingdom)
    {
        Kingdom previousKingdom = actor.kingdom;
        if (!actor.isAlive() ||
            ReferenceEquals(previousKingdom, nextKingdom))
        {
            return;
        }

        if (previousKingdom == null ||
            nextKingdom == null ||
            previousKingdom.wild != nextKingdom.wild)
        {
            MarkPartitionChange(actor);
        }
    }

    private static void MarkPartitionChange(Actor actor)
    {
        lock (SyncRoot)
        {
            DirtyActors.Add(actor);
            unchecked
            {
                version++;
            }
        }
    }
}
