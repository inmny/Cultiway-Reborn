using Cultiway.Abstract;
using System.Linq;

namespace Cultiway.Content.VillageDuties;

/// <summary>空闲孩童更常回家休息，实际生产、修炼和其他任务继续使用原有选择。</summary>
[Dependency(typeof(Decisions))]
internal sealed class ChildLeisureContent : ICanInit
{
    /// <summary>复用原版回家和闲逛决策，只调整空闲孩童的选择倾向。</summary>
    public void Init()
    {
        var home = AssetManager.decisions_library.get("stay_in_own_home");
        var original = home.action_check_launch;
        home.action_check_launch = actor => PrefersHome(actor) || original?.Invoke(actor) != false;
        AdjustWeight(home, 8f);
        AdjustWeight(AssetManager.decisions_library.get("city_idle_walking"), .2f);
        AdjustWeight(AssetManager.decisions_library.get("random_move_near_house"), .2f);
        AdjustWeight(AssetManager.decisions_library.get("random_move"), .2f);
        AdjustWeight(Decisions.CivTravelToCity, .2f);
    }

    /// <summary>有可用住房、没有实际任务和当月岗位的未成年居民才减少闲逛。</summary>
    /// <param name="actor">本次原版决策的人物。</param>
    internal static bool PrefersHome(Actor actor)
    {
        if (!VillageDutyService.IsResident(actor) || actor.isAdult() || actor.isWarrior() || actor.citizen_job != null ||
            actor.isCarryingResources() || actor.isFighting() || !VillageDutyService.CanChangeAssignment(actor)) return false;
        string job = actor.ai.job?.id;
        if (job != null && job != "unit_citizen" && job != "decision" && !actor.asset.job_baby.Contains(job)) return false;
        if (actor.canWork() && VillageDutyService.CanTakeCivilianJob(actor) &&
            VillageMilitary.VillageMilitaryService.Get(actor.city).Occupations.ContainsKey(actor.id)) return false;
        var home = actor.getHomeBuilding();
        return home != null && !home.current_tile.isOnFire() && home.current_tile.isSameIsland(actor.current_tile);
    }

    /// <summary>保留其他居民和现有自定义权重，只改变空闲孩童的相对选择概率。</summary>
    /// <param name="decision">原版回家或闲逛决策。</param>
    /// <param name="factor">空闲孩童使用的权重倍数。</param>
    private static void AdjustWeight(DecisionAsset decision, float factor)
    {
        var original = decision.weight_calculate_custom;
        decision.weight_calculate_custom = actor => (original?.Invoke(actor) ?? decision.weight) * (PrefersHome(actor) ? factor : 1f);
        decision.has_weight_custom = true;
    }
}
