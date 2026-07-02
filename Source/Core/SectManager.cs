using System.Collections.Generic;
using Cultiway.Content;
using Cultiway.Content.Extensions;
using Cultiway.Const;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;

namespace Cultiway.Core;

public class SectManager : MetaSystemManager<Sect, SectData>
{
    private bool _dirty_buildings = true;

    public SectManager()
    {
        type_id = WorldboxGame.HistoryMetaDatas.Sect.id;
    }

    public override void loadFromSave(List<SectData> pList)
    {
        base.loadFromSave(pList);
        setDirtyBuildings();
    }

    public override void addObject(Sect pObject)
    {
        base.addObject(pObject);
        setDirtyBuildings();
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

    public bool TryPromoteMember(Actor manager, Actor actor, SectRoleAsset role)
    {
        Sect sect = actor?.GetExtend().sect;
        return sect != null && sect.TryPromoteMember(manager, actor, role);
    }

    public bool TrySuccession(Sect sect)
    {
        return sect != null && sect.TrySuccession();
    }

    public void beginChecksBuildings()
    {
        if (!_dirty_buildings) return;

        updateDirtyBuildings();
        _dirty_buildings = false;
    }

    public void setDirtyBuildings()
    {
        _dirty_buildings = true;
    }

    private void updateDirtyBuildings()
    {
        clearAllBuildingLists();

        List<Building> buildings = World.world.buildings.getSimpleList();
        for (int i = 0; i < buildings.Count; i++)
        {
            Building building = buildings[i];
            if (building == null || building.data == null) continue;

            building.data.get(BuildingDataKeys.SectID_Long, out long sectId, -1);
            if (sectId < 0) continue;

            Sect sect = get(sectId);
            if (sect == null || sect.isRekt())
                continue;

            if (building.asset == null || !building.asset.IsSectBuilding() || !building.isUsable())
                continue;

            sect.ListBuilding(building);
        }
    }

    private void clearAllBuildingLists()
    {
        foreach (Sect sect in this)
        {
            sect.ClearBuildingList();
        }
    }
}
