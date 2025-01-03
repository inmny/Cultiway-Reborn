using Friflo.Engine.ECS;

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
}