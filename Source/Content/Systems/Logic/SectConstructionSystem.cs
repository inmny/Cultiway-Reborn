using Cultiway.Const;
using Cultiway.Content.Extensions;
using Cultiway.Content.Sects;
using Cultiway.Core;
using Cultiway.Core.Performance;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.Systems.Logic;

/// <summary>
/// 周期性刷新宗门建筑列表、自动开启工地并维护宗门岗位名额。
/// </summary>
public class SectConstructionSystem : BaseSystem
{
    private int _worldGeneration = -1;
    private double _nextTickTime;

    protected override void OnUpdateGroup()
    {
        base.OnUpdateGroup();

        if (!Config.game_loaded) return;
        ResetForNewWorld();
        double now = SimulationTime.Now;
        if (now < _nextTickTime) return;

        _nextTickTime = now + SectConst.SectConstructionCheckInterval;

        SectManager manager = WorldboxGame.I?.Sects;
        if (manager == null) return;

        manager.beginChecksBuildings();
        foreach (Sect sect in manager)
        {
            sect.RefreshSectJobs();
            SectConstructionService.TryOpen(sect);
        }
    }

    private void ResetForNewWorld()
    {
        int worldGeneration = SimulationTime.Generation;
        if (_worldGeneration == worldGeneration)
        {
            return;
        }

        _worldGeneration = worldGeneration;
        _nextTickTime = 0.0;
    }
}
