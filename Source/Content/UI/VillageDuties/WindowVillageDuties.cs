using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.VillageDuties;
using Cultiway.Content.VillageMilitary;
using Cultiway.Content.UI.KingdomPolicies;
using Cultiway.UI;
using Cultiway.Utils.Extension;
using NeoModLoader.api;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.VillageDuties;

/// <summary>村庄的人员、月历、唯一军队编制和军功管理窗口。</summary>
public sealed partial class WindowVillageDuties : AbstractWideWindow<WindowVillageDuties>
{
    /// <summary>原版窗口编号。</summary>
    public const string Id = "Cultiway.UI.WindowVillageDuties";
    /// <summary>沿用原版宽窗口的安全显示尺寸。</summary>
    public static readonly Vector2 WindowSize = new(640, 380);
    /// <summary>当前窗口实例。</summary>
    private static WindowVillageDuties instance;
    /// <summary>打开时固定的村庄编号。</summary>
    private static long cityId;
    /// <summary>下次打开时重新绑定编辑草稿。</summary>
    private static bool bindRequested;
    /// <summary>本村月份与国家统一编制的本地草稿。</summary>
    private VillageDutyPolicy draft;
    /// <summary>草稿所属国家，村庄易主后重新绑定，避免向新国家误提交旧设置。</summary>
    private long draftKingdomId;
    /// <summary>当前页签。</summary>
    private int page;
    /// <summary>每页固定容器。</summary>
    private readonly GameObject[] pages = new GameObject[6];
    /// <summary>页签按钮。</summary>
    private readonly Button[] tabButtons = new Button[6];
    /// <summary>互斥页签状态。</summary>
    private UiSegmentedTabs tabs;
    /// <summary>页面共享的原版滚动区域。</summary>
    private ScrollRect scroll;
    /// <summary>普通背景内容的交互组，供模态框临时禁用。</summary>
    private CanvasGroup owner;
    /// <summary>当前村庄标题。</summary>
    private Text heading;
    /// <summary>当前国家统一政策入口。</summary>
    private Button policies;
    /// <summary>编制设置会影响的国家与村庄数量。</summary>
    private Text organizationScope;
    /// <summary>总览正文。</summary>
    private Text overview;
    /// <summary>待应用配置与人数预览。</summary>
    private Text feedback;
    /// <summary>统一提交按钮。</summary>
    private Button apply;
    /// <summary>全部本地化固定文本。</summary>
    private readonly List<(Text text, string key)> translated = new();
    /// <summary>编制名称、称谓、数量和阵列列数输入框。</summary>
    private readonly InputField[] fields = new InputField[10];
    /// <summary>每个原版月份的二元状态。</summary>
    private readonly Toggle[] months = new Toggle[12];
    /// <summary>原版军队实例名称。</summary>
    private InputField armyName;
    /// <summary>军队名称是否由用户实际编辑。</summary>
    private bool armyNameChanged;
    /// <summary>正在同步草稿时不触发编辑回调。</summary>
    private bool binding;
    /// <summary>编制树的池化行。</summary>
    private MonoObjPool<VillageInfoRow> armyRows;
    /// <summary>人员列表的池化行。</summary>
    private MonoObjPool<VillageInfoRow> peopleRows;
    /// <summary>编制树容器。</summary>
    private Transform armyList;
    /// <summary>人员列表容器。</summary>
    private Transform peopleList;
    /// <summary>已展开的营。</summary>
    private readonly HashSet<int> expanded = new();
    /// <summary>复用的阵列预览。</summary>
    private VillageFormationPreview formation;
    /// <summary>预览层级，零为小队、一为营、二为原版军队。</summary>
    private int previewLevel;
    /// <summary>被预览的营或队。</summary>
    private int previewUnit;
    /// <summary>姓名搜索框。</summary>
    private InputField search;
    /// <summary>人员类别与军籍筛选入口。</summary>
    private Button filter;
    /// <summary>排序入口。</summary>
    private Button sorting;
    /// <summary>统一直接选择菜单。</summary>
    private UiOptionMenu options;
    /// <summary>当前筛选。</summary>
    private int filterIndex;
    /// <summary>军功或姓名排序。</summary>
    private int sortIndex;
    /// <summary>人员分页。</summary>
    private int peoplePage;
    /// <summary>上一页和下一页。</summary>
    private Button previous, next;
    /// <summary>分页说明。</summary>
    private Text pageText;
    /// <summary>名称与重新评定共用的模态框。</summary>
    private UiModal modal;
    /// <summary>模态说明。</summary>
    private Text modalText;
    /// <summary>模态名称输入。</summary>
    private InputField modalInput;
    /// <summary>模态确认动作。</summary>
    private Action modalAction;
    /// <summary>下次状态刷新，不覆盖用户正在编辑的草稿。</summary>
    private float nextRefresh;

    /// <summary>从村庄或原版军队打开同一个领域窗口。</summary>
    /// <param name="city">需要查看的村庄。</param>
    /// <param name="selectedPage">首次展示的页面序号。</param>
    public static void Open(City city, int selectedPage = 0)
    {
        cityId = city?.id ?? 0; bindRequested = true;
        if (instance != null) { instance.page = selectedPage; instance.farmFamily = long.MinValue; }
        ScrollWindow.showWindow(Id); instance?.Refresh();
    }

    /// <summary>原版月历和界面共享本地化入口。</summary>
    /// <param name="key">本功能下的文字编号。</param>
    public static string T(string key) => LocalizedTextManager.getText("Cultiway.Village." + key);

    /// <summary>格式化当前语言的固定说明。</summary>
    /// <param name="key">带格式参数的文字编号。</param>
    /// <param name="args">需要填入的名称和数量。</param>
    internal static string F(string key, params object[] args) => string.Format(T(key), args);

    /// <summary>空自定义名称使用当前语言的层级名。</summary>
    /// <param name="custom">玩家的自定义称呼。</param>
    /// <param name="key">空名称采用的默认文字编号。</param>
    internal static string Label(string custom, string key) => string.IsNullOrWhiteSpace(custom) ? T(key) : custom;

    /// <summary>当前世界内的目标村庄，切世界时会由统一清理入口解绑。</summary>
    private City City => World.world?.cities?.get(cityId);

    /// <summary>建立一次结构和事件绑定。</summary>
    protected override void Init()
    {
        instance = this;
        var context = UiWindowContext.Bind(BackgroundTransform);
        var m = UiTheme.Current.Metrics;
        var root = UiLayout.Create(BackgroundTransform, "VillageDuties", false, 560, 330, m.SpacingSm);
        root.transform.localPosition = new Vector3(0, -8); owner = root.AddComponent<CanvasGroup>();
        var top = UiLayout.Create(root.transform, "Heading", true, 560, m.ControlSmall, m.SpacingSm);
        heading = UiElements.CreateText(top.transform, "Title", "", 440, m.ControlSmall, 9, TextAnchor.MiddleLeft, FontStyle.Bold);
        policies = UiElements.CreateButton(top.transform, "Policies", "", 116, m.ControlSmall,
            () => { var city = City; if (city != null) WindowKingdomPolicies.Open(city.kingdom, city); });
        var bar = UiLayout.Create(root.transform, "Tabs", true, 560, m.ControlSmall, m.SpacingSm);
        tabs = new UiSegmentedTabs();
        string[] keys = { "Overview", "Calendar", "Organization", "People", "Occupations", "Farms" };
        for (int i = 0; i < keys.Length; i++)
        {
            int index = i;
            var button = tabButtons[i] = UiElements.CreateButton(bar.transform, "Tab" + i, T(keys[i]),
                (560 - (keys.Length - 1) * m.SpacingSm) / keys.Length, m.ControlSmall, () => SelectPage(index));
            translated.Add((button.GetComponentInChildren<Text>(), keys[i])); tabs.Add(button);
        }
        var pane = UiScrollPane.CreateVertical(root.transform, "Content", 560, 214);
        pane.AttachOriginalScrollbar(context.ScrollbarTemplate); pane.SetWindowFrame(m.SpacingMd);
        scroll = pane.ScrollRect;
        for (int i = 0; i < pages.Length; i++) pages[i] = UiLayout.Create(pane.Content, "Page" + i, false, 500, 200, m.SpacingSm);
        BuildOverview(); BuildCalendar(); BuildOrganization(); BuildPeople(); BuildOccupations(); BuildFarms();
        var footer = UiLayout.Create(root.transform, "Actions", true, 560, m.ControlMedium, m.SpacingSm);
        apply = Button(footer.transform, "Apply", 92, ApplyDraft);
        Button(footer.transform, "Cancel", 92, ReloadDraft);
        feedback = UiElements.CreateText(footer.transform, "Feedback", "", 368, m.ControlMedium, 7, TextAnchor.MiddleLeft);
        options = new UiOptionMenu(BackgroundTransform, owner, context.ScrollbarTemplate, new UiOptionMenuConfig {
            PanelWidth = 300, ColumnCount = 2, CloseText = T("Close"), CloseTooltipTitle = "Cultiway.Village.Close",
            CloseTooltipDescription = "Cultiway.Village.Close", SearchPlaceholder = T("Search"), EmptyText = T("None") });
        BuildModal();
    }

    /// <summary>创建统一的本地化按钮。</summary>
    /// <param name="parent">按钮所属区域。</param>
    /// <param name="key">按钮文字编号。</param>
    /// <param name="width">按钮宽度。</param>
    /// <param name="action">点击后执行的操作。</param>
    private Button Button(Transform parent, string key, float width, Action action)
    {
        var button = UiElements.CreateButton(parent, key, T(key), width, UiTheme.Current.Metrics.ControlMedium, action.Invoke);
        translated.Add((button.GetComponentInChildren<Text>(), key)); return button;
    }

    /// <summary>创建统一的本地化说明。</summary>
    /// <param name="parent">说明所属区域。</param>
    /// <param name="key">说明文字编号。</param>
    /// <param name="height">预留的正文高度。</param>
    private Text Text(Transform parent, string key, float height = 24)
    {
        var text = UiElements.CreateText(parent, key, T(key), 500, height, 7, TextAnchor.UpperLeft);
        translated.Add((text, key)); return text;
    }

    /// <summary>原版返回窗口时保留未提交草稿。</summary>
    public override void OnNormalEnable() { if (bindRequested || draft == null) ReloadDraft(); else Refresh(); }

    /// <summary>只在窗口可见时刷新状态，输入框和月份草稿不被覆盖。</summary>
    private void Update() { if (Time.unscaledTime >= nextRefresh && Config.game_loaded) Refresh(); }

    /// <summary>切换页面保留草稿并回到该页顶部。</summary>
    /// <param name="selected">需要显示的页面序号。</param>
    private void SelectPage(int selected)
    { page = selected; peoplePage = 0; farmPage = 0; Refresh(); scroll.verticalNormalizedPosition = 1; }

    /// <summary>取消未提交修改，重新绑定当前村庄设置。</summary>
    private void ReloadDraft()
    {
        bindRequested = false;
        var city = City;
        if (city == null || !city.isAlive()) return;
        var data = VillageMilitaryService.Get(city);
        draft = data.Policy.Copy();
        draft.CopyOrganizationFrom(VillageMilitaryService.GetKingdomPolicy(city.kingdom));
        draftKingdomId = city.kingdom.id; binding = true;
        string[] values = { draft.SquadName, draft.BattalionName, draft.ArmyName, draft.SquadLeaderTitle,
            draft.BattalionLeaderTitle, draft.ArmyLeaderTitle, draft.SquadSize.ToString(), draft.SquadsPerBattalion.ToString(),
            draft.BattalionsPerArmy.ToString(), draft.FormationColumns.ToString() };
        for (int i = 0; i < fields.Length; i++) fields[i].SetTextWithoutNotify(values[i]);
        armyName.SetTextWithoutNotify(city.army?.name ?? ""); armyNameChanged = false;
        for (int i = 0; i < months.Length; i++) months[i].SetIsOnWithoutNotify(draft.IsWorkMonth(i + 1));
        foreach (var pair in occupationFields)
            pair.Value.SetTextWithoutNotify(draft.OccupationTargets.TryGetValue(pair.Key, out int target) ? target.ToString() : "");
        var farm = FamilyFarming.FamilyFarmService.Get(city);
        farmFields[0].SetTextWithoutNotify(farm.Width.ToString()); farmFields[1].SetTextWithoutNotify(farm.Height.ToString());
        farmFields[2].SetTextWithoutNotify(farm.MaxArea.ToString());
        binding = false; Refresh();
    }

    /// <summary>整体刷新可见页面的统计、条目和语义状态。</summary>
    private void Refresh()
    {
        if (heading == null || !Config.game_loaded) return;
        nextRefresh = Time.unscaledTime + 1;
        if (bindRequested || draft == null) { ReloadDraft(); return; }
        var city = City;
        if (city == null || !city.isAlive()) { heading.text = T("InvalidCity"); apply.interactable = false; return; }
        if (city.kingdom.id != draftKingdomId) { ReloadDraft(); return; }
        foreach (var item in translated) item.text.text = T(item.key);
        heading.text = city.name + " · " + T("Title"); tabs.SetSelected(page);
        policies.GetComponentInChildren<Text>().text = WindowKingdomPolicies.T("Title");
        for (int i = 0; i < pages.Length; i++) pages[i].SetActive(i == page);
        var data = VillageMilitaryService.Get(city);
        if (page == 0) RefreshOverview(city, data);
        if (page == 1) RefreshMonths();
        if (page == 2) RefreshOrganization(city, data);
        if (page == 3) RefreshPeople(city, data);
        if (page == 4) RefreshOccupations(city, data);
        if (page == 5) RefreshFarms(city);
        RefreshPreview();
        LayoutRebuilder.MarkLayoutForRebuild(scroll.content);
    }

    /// <summary>换世界时丢弃旧对象绑定和池化回调。</summary>
    public static void ClearWorldState()
    {
        cityId = 0; bindRequested = false;
        if (instance == null) return;
        instance.draft = null; instance.expanded.Clear(); instance.modal?.Hide();
        instance.armyRows?.Clear(); instance.peopleRows?.Clear();
        instance.farmRows?.Clear(); instance.farmFamily = long.MinValue;
        if (instance.gameObject.activeInHierarchy) instance.GetComponent<ScrollWindow>().clickHide();
    }

    /// <summary>关闭时解除模态锁，回收界面持有的领域引用。</summary>
    private void OnDisable() { modal?.Hide(); options?.Hide(); }

    /// <summary>窗口销毁时解除静态绑定。</summary>
    private void OnDestroy() { if (instance == this) instance = null; }
}
