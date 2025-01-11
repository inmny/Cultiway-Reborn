using Friflo.Engine.ECS;
using Friflo.Json.Fliox;

namespace Cultiway.Core.Components;

public struct StatusOverwriteStats : IComponent
{
    public BaseStats stats;

    public static void CopyValue(in StatusOverwriteStats source, ref StatusOverwriteStats target,
        in                          CopyContext          context)
    {
        if (source.stats == null) return;
        if (target.stats == null)
        {
            target.stats = new BaseStats();
        }
        else
        {
            target.stats.clear();
        }
        target.stats.mergeStats(source.stats);
    }

    public override bool Equals(object obj)
    {
        return obj is StatusOverwriteStats other && Equals(other);
    }

    public bool Equals(StatusOverwriteStats other)
    {
        return Equals(stats, other.stats);
    }

    public override int GetHashCode()
    {
        return (stats != null ? stats.GetHashCode() : 0);
    }

    public static bool operator ==(StatusOverwriteStats left, StatusOverwriteStats right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(StatusOverwriteStats left, StatusOverwriteStats right)
    {
        return !left.Equals(right);
    }
}