using ai;
using ai.behaviours;
using Cultiway.Content.FamilyFarming;

namespace Cultiway.Content.Behaviours;

/// <summary>农民每次只认领一个家庭农活或风车工位。</summary>
public sealed class BehPrepareFamilyFarm : BehCityActor
{
    /// <inheritdoc />
    public override BehResult execute(Actor pActor)
    {
        if (FamilyFarmService.Prepare(pActor)) return BehResult.Continue;
        if (VillageDuties.VillageOccupationService.TrySeasonalSupport(pActor)) return BehResult.Continue;
        pActor.timer_action = 2; return BehResult.Stop;
    }
}

/// <summary>沿原版路径实际前往田块或加工设施，失败时释放认领。</summary>
public sealed class BehGoToFamilyFarm : BehaviourActionActor
{
    /// <inheritdoc />
    public override void setupErrorChecks() { base.setupErrorChecks(); null_check_city = true; null_check_tile_target = true; }
    /// <inheritdoc />
    public override BehResult execute(Actor pActor)
    {
        if (pActor.goTo(pActor.beh_tile_target) != ExecuteEvent.False) return BehResult.Continue;
        FamilyFarmService.CannotReach(pActor); pActor.timer_action = 3; return BehResult.Stop;
    }
}

/// <summary>劳动后结算材料、检查需求并继续下一轮，避免高倍速时多等几个画面帧。</summary>
public sealed class BehCompleteFamilyFarm : BehCityActor
{
    /// <summary>保持原有的生活需求检查和连续劳动次数上限。</summary>
    private readonly BehCheckNeeds needs = new(12);

    /// <inheritdoc />
    public override BehResult execute(Actor pActor)
    {
        if (!FamilyFarmService.Complete(pActor)) return BehResult.Stop;
        return needs.execute(pActor) == BehResult.Continue ? BehResult.RestartTask : BehResult.Stop;
    }
}

/// <summary>播收使用短劳动动作，确保能在对应月份内经营小型家庭田块。</summary>
public sealed class BehAnimateFamilyFarm : BehaviourActionActor
{
    /// <inheritdoc />
    public override void setupErrorChecks() { base.setupErrorChecks(); null_check_tile_target = true; land_if_hovering = true; }
    /// <inheritdoc />
    public override BehResult execute(Actor pActor)
    {
        pActor.punchTargetAnimation(pActor.beh_tile_target.posV3);
        string action = FamilyFarmService.CurrentAction(pActor.id);
        pActor.timer_action = action is "Plant" or "Harvest" ? .2f : 1f;
        return BehResult.Continue;
    }
}
