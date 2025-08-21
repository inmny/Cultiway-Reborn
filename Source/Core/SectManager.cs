using System.Collections.Generic;
using Cultiway.Utils.Extension;

namespace Cultiway.Core;

public class SectManager : MetaSystemManager<Sect, SectData>
{
    public SectManager()
    {
        type_id = WorldboxGame.HistoryMetaDatas.Sect.id;
    }
    public override void updateDirtyUnits()
    {
        List<Actor> units = World.world.units.units_only_alive;
        for (int i = 0; i < units.Count; i++)
        {
            Actor actor = units[i];
            Sect sect = actor.GetExtend().sect;
            if (sect != null && sect.isDirtyUnits())
            {
                sect.listUnit(actor);
            }
        }
    }

    public Sect BuildSect(Actor founder)
    {
        var sect = newObject();
        sect.Setup(founder);
        founder.GetExtend().SetSect(sect);

        return sect;
    }
}