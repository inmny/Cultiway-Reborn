using Cultiway.Content.Railways;

namespace Cultiway.Content.UI.Railways;

/// <summary>把建站检查结果转换成原版按钮、顶部提示和悬浮说明使用的文本。</summary>
internal static class RailwayStationBuildFeedback
{
    /// <summary>手动发起只说明角色与建站条件，不把自动谋划的能力门槛混进来。</summary>
    /// <param name="actor">当前选中的人物。</param>
    internal static string GetFailure(Actor actor)
    {
        if (actor == null || !actor.hasCity()) return WindowRailwayInfo.T("BuildBlocked.NoCity");
        if (!Plots.BuildTrainStation.canBeDoneByRole(actor)) return WindowRailwayInfo.T("BuildBlocked.Leader");
        string reason = RailwayStationService.GetBuildBlockReason(actor);
        if (reason == null) return null;
        return reason == "Population"
            ? string.Format(WindowRailwayInfo.T("BuildBlocked.Population"), actor.city.getPopulationPeople(),
                RailwayStationService.MinimumPopulation)
            : WindowRailwayInfo.T("BuildBlocked." + reason);
    }

    /// <summary>悬浮说明列出本城实际人口、库存和完整用地要求。</summary>
    /// <param name="actor">当前选中的人物。</param>
    internal static string GetDetails(Actor actor)
    {
        string state = GetFailure(actor) ?? WindowRailwayInfo.T("BuildReady");
        if (actor == null || !actor.hasCity()) return state;
        var city = actor.city;
        var cost = RailwayStationService.StationCost;
        return state + "\n" + string.Format(WindowRailwayInfo.T("BuildPopulation"), city.getPopulationPeople(),
                   RailwayStationService.MinimumPopulation) + "\n" +
               string.Format(WindowRailwayInfo.T("BuildResources"), city.amount_wood, cost.wood,
                   city.amount_stone, cost.stone, city.amount_common_metals, cost.common_metals, city.amount_gold, cost.gold) +
               "\n" + WindowRailwayInfo.T("BuildSiteRule");
    }
}
