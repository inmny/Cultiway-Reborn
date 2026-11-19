using Cultiway.Content.Railways;
using Cultiway.UI;
using Cultiway.UI.Prefab;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.Railways;

/// <summary>原有单位窗口中的列车页面，进入详情无需先找线路管理器。</summary>
public sealed class RailwayPage : MonoBehaviour, IWorldBoundCreatureInfoPage
{
    /// <summary>当前车头。</summary>
    private Actor actor;
    /// <summary>简要状态。</summary>
    private Text summary;
    /// <summary>查看入口随当前语言刷新。</summary>
    private Button open;

    /// <summary>创建简要信息和固定查看入口。</summary>
    /// <param name="page">原有信息页。</param>
    public static void Setup(CreatureInfoPage page)
    {
        var component = page.gameObject.AddComponent<RailwayPage>();
        var root = UiLayout.Create(page.transform, "RailwayPage", false, 240, 200, UiTheme.Current.Metrics.SpacingMd);
        component.summary = UiElements.CreateText(root.transform, "Summary", "", 240, 80, 8, TextAnchor.UpperLeft);
        var button = component.open = UiElements.CreateIconTextButton(root.transform, "RailwayInfo", UiIcons.Info,
            WindowRailwayInfo.T("Open"), 136, UiTheme.Current.Metrics.ControlMedium, () => WindowRailwayInfo.Open(component.actor));
        UiTooltip.Set(button.gameObject, "Cultiway.Railways.Open", "Cultiway.Railways.OpenTip");
    }

    /// <summary>绑定当前真实车头。</summary>
    /// <param name="page">原有信息页。</param>
    /// <param name="actor">当前列车。</param>
    public static void Show(CreatureInfoPage page, Actor actor)
    {
        var component = page.GetComponent<RailwayPage>(); component.actor = actor;
        UiElements.SetButtonLabel(component.open, WindowRailwayInfo.T("Open"));
        var vehicle = RailwayFleetService.Get(actor);
        component.summary.text = vehicle == null ? WindowRailwayInfo.T("InvalidSelection") : actor.getName() + "\n" +
            WindowRailwayInfo.T("Mode." + vehicle.Mode) + "\n" + WindowRailwayInfo.StationName(World.world.buildings.get(vehicle.Home));
    }

    /// <summary>清世界时解除人物引用。</summary>
    public void ClearWorldBinding() { actor = null; summary.text = ""; }
}
