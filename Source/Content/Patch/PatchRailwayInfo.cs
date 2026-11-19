using Cultiway.Content.UI.Railways;
using HarmonyLib;

namespace Cultiway.Content.Patch;

/// <summary>接入原版城市详情和建站谋划说明。</summary>
internal static class PatchRailwayInfo
{
    /// <summary>原版报告发起失败后，用同一份建站检查显示明确原因。</summary>
    /// <param name="pButton">被点击的原版谋划按钮。</param>
    /// <param name="__result">原版是否成功发起。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(PlotsEditor), nameof(PlotsEditor.addAugmentation))]
    private static void ExplainStationFailure(PlotButton pButton, bool __result)
    {
        if (__result || pButton.getElementAsset() != Plots.BuildTrainStation) return;
        string reason = RailwayStationBuildFeedback.GetFailure(SelectedUnit.unit);
        if (reason != null) WorldTip.showNowTop(reason, false);
    }

    /// <summary>在固定建站图标的原版悬浮说明中展示当前条件和库存。</summary>
    /// <param name="pTooltip">原版悬浮说明。</param>
    /// <param name="pData">当前谋划。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(TooltipLibrary), nameof(TooltipLibrary.showPlotInEditor))]
    private static void ExplainStationConditions(Tooltip pTooltip, TooltipData pData)
    {
        if (pData.plot_asset == Plots.BuildTrainStation)
            pTooltip.addDescription("\n" + RailwayStationBuildFeedback.GetDetails(SelectedUnit.unit));
    }

    /// <summary>每次刷新只替换当前行回调，重复打开不会叠加监听。</summary>
    /// <param name="__instance">当前城市窗口。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(CityWindow), nameof(CityWindow.showStatsRows))]
    private static void AddRailwayEntry(CityWindow __instance)
    {
        var city = __instance.meta_object;
        if (city == null) return;
        var row = __instance.showStatRow("Cultiway.Railways.CityInfo", WindowRailwayInfo.T("Open"), pIconPath: "iconBoat");
        row.on_click_value = () => WindowRailwayInfo.OpenCity(city);
    }
}
