using System.Text;
using Cultiway.Const;
using Cultiway.Core.Components;
using Cultiway.Core.GeoLib.Components;
using Cultiway.Core.GeoLib.Systems;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

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
        _system_root.Add(new RecycleEmptyGeoRegionSystem());


        ModClass.L.CustomMapModeLibrary.add(new CustomMapModeAsset()
        {
            id = "geo_region",
            icon_path = "cultiway/icons/iconGeoRegion",
            kernel_func = (int x, int y, ref Color32 out_color) =>
            {
                var tile = World.world.GetTile(x, y).GetExtend();
                var rels = tile.E.GetRelations<BelongToRelation>();
                foreach (var rel in rels)
                {
                    if (rel.entity.HasComponent<GeoRegion>())
                    {
                        out_color = rel.entity.GetComponent<GeoRegion>().color;
                        return;
                    }
                }
                out_color.a = 0;
            }
        });
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