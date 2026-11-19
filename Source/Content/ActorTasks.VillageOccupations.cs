using ai.behaviours;
using Cultiway.Content.Behaviours;

namespace Cultiway.Content;

/// <summary>村庄林业岗位的补种树木任务。</summary>
public partial class ActorTasks
{
    /// <summary>伐木交付后，寻找空地并补种一棵当地树木。</summary>
    public static BehaviourTaskActor VillagePlantTree { get; private set; }

    /// <summary>补种沿用原版移动和动作等待，不在村庄调度里直接生成树木。</summary>
    private static void InitVillageOccupationTasks()
    {
        VillagePlantTree.locale_key = "Cultiway.Village.PlantTree";
        VillagePlantTree.setIcon("ui/Icons/citizen_jobs/iconCitizenJobWoodcutter");
        VillagePlantTree.addBeh(new BehFindVillageTreeSite());
        VillagePlantTree.addBeh(new BehGoToTileTarget());
        VillagePlantTree.addBeh(new BehWait(2f));
        VillagePlantTree.addBeh(new BehPlantVillageTree());
        VillagePlantTree.addBeh(new BehWait(3f));
    }
}
