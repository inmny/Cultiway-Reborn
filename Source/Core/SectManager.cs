using System.Collections.Generic;
using Cultiway.Content;
using Cultiway.Core.Libraries;
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
        if (!SectRules.CanFoundSect(founder))
        {
            return null;
        }

        var sect = newObject();
        sect.Setup(founder);

        return sect;
    }

    public bool JoinSect(Sect sect, Actor actor)
    {
        return sect != null && sect.JoinSect(actor);
    }

    public bool JoinSect(Sect sect, Actor actor, SectJoinProfile profile)
    {
        return sect != null && sect.JoinSect(actor, profile);
    }

    public bool LeaveSect(Actor actor)
    {
        Sect sect = actor?.GetExtend().sect;
        return sect != null && sect.LeaveSect(actor);
    }

    public bool PromoteMember(Actor actor, SectRoleAsset role)
    {
        Sect sect = actor?.GetExtend().sect;
        return sect != null && sect.PromoteMember(actor, role);
    }

    public bool TrySuccession(Sect sect)
    {
        return sect != null && sect.TrySuccession();
    }
}
