using System.Text;
using Cultiway.Const;
using Cultiway.Core.GeoLib.Systems;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.GeoLib;

public class Manager
{
    private SystemRoot _system_root;

    internal Manager(WorldboxGame game)
    {
        Game = game;
        _system_root = new SystemRoot(ModClass.I.TileExtendManager.World, "GeoLib.Logic");

        _system_root.Add(new ErosionSystem());
        _system_root.Add(new AntiErosionSystem());
        _system_root.Add(new RiverTrackSystem());
    }

    public WorldboxGame Game { get; private set; }

    public void UpdateLogic(UpdateTick update_tick)
    {
        if (!ModClass.I.TileExtendManager.Ready()) return;
        if (!GeneralSettings.EnableGeoSystems) return;
        _system_root.Update(update_tick);
    }
    public void SetMonitorPerf(bool monitor_perf)
    {
        _system_root.SetMonitorPerf(monitor_perf);
    }
    public void AppendPerfLog(StringBuilder sb)
    {
        sb.Append('\n');
        _system_root.AppendPerfLog(sb);
        sb.Append('\n');
    }
}