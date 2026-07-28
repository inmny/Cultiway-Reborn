using System.Threading;

namespace Cultiway.Core.Performance;

/// <summary>
/// 只记录会改变 ActorManager 四个元数据分区成员关系的事件。
/// 普通王国、城市、语言等元数据脏标记不应触发万人角色表重建。
/// </summary>
internal static class ActorMetaPartitionVersion
{
    private static int version;

    internal static int Version => Volatile.Read(ref version);

    internal static void MarkKingdomChange(
        Actor actor,
        Kingdom nextKingdom)
    {
        Kingdom previousKingdom = actor.kingdom;
        if (ReferenceEquals(previousKingdom, nextKingdom))
        {
            return;
        }

        if (previousKingdom == null ||
            nextKingdom == null ||
            previousKingdom.wild != nextKingdom.wild)
        {
            Interlocked.Increment(ref version);
        }
    }
}
