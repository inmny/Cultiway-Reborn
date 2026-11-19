using ai.behaviours;
using Cultiway.Content.Behaviours;

namespace Cultiway.Content;

/// <summary>家庭农田使用原版移动、农具和劳动动作。</summary>
public partial class ActorTasks
{
    /// <summary>收割、翻土、播种、运粮与风车加工共用的单次农活。</summary>
    public static BehaviourTaskActor FamilyFarmWork { get; private set; }

    /// <summary>每次劳动后检查生活需求，最多连续十二次，集中收割后再交粮。</summary>
    private static void InitFamilyFarmTasks()
    {
        FamilyFarmWork.locale_key = "cultiway_farm_work";
        FamilyFarmWork.force_hand_tool = "hoe";
        FamilyFarmWork.cancellable_by_reproduction = true;
        FamilyFarmWork.setIcon("ui/Icons/citizen_jobs/iconCitizenJobFarmer");
        FamilyFarmWork.addBeh(new BehPrepareFamilyFarm());
        FamilyFarmWork.addBeh(new BehGoToFamilyFarm());
        for (int n = 0; n < 2; n++) FamilyFarmWork.addBeh(new BehAnimateFamilyFarm());
        FamilyFarmWork.addBeh(new BehCompleteFamilyFarm());
    }
}
