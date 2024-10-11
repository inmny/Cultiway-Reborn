using Cultiway.Core.GeoLib.Systems;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.GeoLib;

public class Manager
{
    private SystemRoot _system_root;

    internal Manager()
    {
        _system_root = new SystemRoot(ModClass.I.TileExtendManager.World, "GeoLib.Logic");

        _system_root.Add(new ErosionSystem());
        _system_root.Add(new AntiErosionSystem());
        _system_root.Add(new RiverTrackSystem());
    }

    public void UpdateLogic(UpdateTick update_tick)
    {
        if (!ModClass.I.TileExtendManager.Ready()) return;
        _system_root.Update(update_tick);
    }
}