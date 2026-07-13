using Cultiway.Const;
using Cultiway.Content.Extensions;
using Cultiway.Content.Sects;
using Cultiway.Core;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

/// <summary>
/// 周期性刷新宗门建筑列表、自动开启工地并维护宗门岗位名额。
/// </summary>
public class SectConstructionSystem : BaseSystem
{
    private float _nextTickTime;

    protected override void OnUpdateGroup()
    {
        base.OnUpdateGroup();

        if (!Config.game_loaded) return;
        if (Time.time < _nextTickTime) return;

        _nextTickTime = Time.time + SectConst.SectConstructionCheckInterval;

        SectManager manager = WorldboxGame.I?.Sects;
        if (manager == null) return;

        manager.beginChecksBuildings();
        foreach (Sect sect in manager)
        {
            sect.RefreshSectJobs();
            SectConstructionService.TryOpen(sect);
        }
    }
}
