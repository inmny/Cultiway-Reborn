using Cultiway.Content.VillageDuties;
using Cultiway.Content.VillageMilitary;
using Cultiway.UI;
using Cultiway.UI.Prefab;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.VillageDuties;

/// <summary>人物详情中的身份、军功、军籍与制度入口。</summary>
public sealed class VillageMilitaryPage : MonoBehaviour, IWorldBoundCreatureInfoPage
{
    /// <summary>当前展示人物，换世界时解除绑定。</summary>
    private Actor actor;
    /// <summary>人物实际身份和工作状态。</summary>
    private Text summary;
    /// <summary>所属村庄的设置入口。</summary>
    private Button open;

    /// <summary>仅建立一次布局，之后复用文本与回调。</summary>
    /// <param name="page">已有生物详情中的页面。</param>
    public static void Setup(CreatureInfoPage page)
    {
        var component = page.gameObject.AddComponent<VillageMilitaryPage>();
        var root = UiLayout.Create(page.transform, "VillageMilitary", false, 240, 200, 6);
        component.summary = UiElements.CreateText(root.transform, "Summary", "", 240, 160, 8, TextAnchor.UpperLeft);
        component.open = UiElements.CreateIconTextButton(root.transform, "VillageDuties", UiIcons.Info,
            WindowVillageDuties.T("Open"), 180, UiTheme.Current.Metrics.ControlMedium,
            () => { if (VillageDutyService.IsResident(component.actor)) WindowVillageDuties.Open(component.actor.city, 3); });
    }

    /// <summary>与村庄窗口共享分类和军功数据，不保存另一份人物状态。</summary>
    /// <param name="page">已经创建的页面。</param>
    /// <param name="actor">当前选中的真实人物。</param>
    public static void Show(CreatureInfoPage page, Actor actor)
    {
        var component = page.GetComponent<VillageMilitaryPage>(); component.actor = actor;
        var data = VillageMilitaryService.Get(actor.city);
        var squad = VillageMilitaryService.SquadOf(actor);
        var kind = VillageDutyService.Classify(actor);
        string status = squad != null ? WindowVillageDuties.SquadName(data, squad) :
            data.Reserves.Contains(actor.id) ? WindowVillageDuties.T("ReserveFree") : WindowVillageDuties.T("None");
        string duty = kind is VillageResidentKind.Manager or VillageResidentKind.Special ? WindowVillageDuties.T("CalendarExempt") :
            WindowVillageDuties.T(VillageDutyService.IsWorkMonth(actor.city) ? "Work" : "Rest");
        component.summary.text = actor.getName() + "\n" + actor.city.name + "\n\n" +
            WindowVillageDuties.T("Kind." + kind) + " · " + duty + "\n" +
            WindowVillageDuties.F("MeritValue", MilitaryMeritService.Get(actor)) + "\n" + status + "\n" + WindowVillageDuties.TaskText(actor);
        UiElements.SetButtonLabel(component.open, WindowVillageDuties.T("Title"));
    }

    /// <summary>清世界时解除人物引用。</summary>
    public void ClearWorldBinding() { actor = null; summary.text = ""; }
}
