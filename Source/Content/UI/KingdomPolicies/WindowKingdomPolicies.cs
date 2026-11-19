using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.KingdomPolicies;
using Cultiway.Content.UI.VillageDuties;
using Cultiway.Core;
using Cultiway.UI;
using NeoModLoader.api;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.KingdomPolicies;

/// <summary>国家统一的制度卡片与全世界共用的日长设置，沿用原版窗口和滚动控件。</summary>
public sealed class WindowKingdomPolicies : AbstractWideWindow<WindowKingdomPolicies>
{
    /// <summary>原版窗口编号。</summary>
    public const string Id = "Cultiway.UI.KingdomPolicies";
    /// <summary>沿用已验证的宽窗口尺寸。</summary>
    public static readonly Vector2 WindowSize = new(640, 380);
    /// <summary>当前窗口实例。</summary>
    private static WindowKingdomPolicies instance;
    /// <summary>本次编辑的国家。</summary>
    private static long kingdomId;
    /// <summary>详细参数入口使用的村庄。</summary>
    private static long cityId;
    /// <summary>重新打开另一个国家时读取新草稿。</summary>
    private static bool bindRequested;
    /// <summary>尚未提交的政策。</summary>
    private KingdomPolicyData draft;
    /// <summary>同步控件时不触发编辑。</summary>
    private bool binding;
    /// <summary>当前国家标题。</summary>
    private Text heading;
    /// <summary>国家作用范围与产权说明。</summary>
    private Text scope;
    /// <summary>已生效政策与草稿的城市忠诚度修正。</summary>
    private Text loyaltySummary;
    /// <summary>输入的一倍速日长。</summary>
    private InputField daySeconds;
    /// <summary>日长标签。</summary>
    private Text dayLabel;
    /// <summary>日长为世界共享的说明。</summary>
    private Text dayHint;
    /// <summary>国家教育政策的入学年龄输入。</summary>
    private InputField schoolAge;
    /// <summary>入学年龄说明。</summary>
    private Text schoolAgeLabel;
    /// <summary>编辑具体村庄参数的选择入口。</summary>
    private Button cityChoice;
    /// <summary>保存和撤销按钮。</summary>
    private Button apply, cancel;
    /// <summary>本次操作的结果。</summary>
    private Text feedback;
    /// <summary>结果对应的本地化编号。</summary>
    private string feedbackKey = "ScopeHint";
    /// <summary>卡片的固定控件，切换时仅更新状态。</summary>
    private readonly Dictionary<KingdomPolicyCard, CardView> cards = new();
    /// <summary>选择村庄时复用统一菜单。</summary>
    private UiOptionMenu cityMenu;
    /// <summary>原版滚动区域。</summary>
    private ScrollRect scroll;
    /// <summary>低频刷新时间，不覆盖未提交草稿。</summary>
    private float nextRefresh;

    /// <summary>一张制度卡的显示与直接操作控件。</summary>
    private sealed class CardView
    {
        /// <summary>名称。</summary>
        internal Text Title;
        /// <summary>制度作用与关闭效果。</summary>
        internal Text Description;
        /// <summary>启用该卡时的固定忠诚度影响。</summary>
        internal Text Loyalty;
        /// <summary>启用状态。</summary>
        internal Toggle Toggle;
        /// <summary>进入该制度详细参数的按钮。</summary>
        internal Button Configure;
        /// <summary>统一卡片背景。</summary>
        internal UiListRowChrome Chrome;
    }

    /// <summary>本功能共享的本地化入口。</summary>
    /// <param name="key">本功能内的文字编号。</param>
    public static string T(string key) => LocalizedTextManager.getText("Cultiway.Policies." + key);

    /// <summary>从国家详情或村庄进入同一套政策。</summary>
    /// <param name="kingdom">玩家选中的国家。</param>
    /// <param name="city">可选的村庄参数入口。</param>
    public static void Open(Kingdom kingdom, City city = null)
    {
        kingdomId = kingdom?.id ?? 0; cityId = city?.id ?? 0; bindRequested = true;
        ScrollWindow.showWindow(Id); instance?.ReloadDraft();
    }

    /// <summary>当前世界内的真实国家。</summary>
    private Kingdom Kingdom => World.world?.kingdoms?.get(kingdomId);
    /// <summary>当前选中的详细参数村庄，易主或消亡后重新选择。</summary>
    private City SelectedCity => Kingdom?.cities.FirstOrDefault(c => c.id == cityId && c.isAlive()) ??
        Kingdom?.cities.FirstOrDefault(c => c.isAlive());

    /// <summary>建立窗口、世界日长和两列政策卡，不重新制造原版窗口交互。</summary>
    protected override void Init()
    {
        instance = this;
        var context = UiWindowContext.Bind(BackgroundTransform);
        var m = UiTheme.Current.Metrics;
        var root = UiLayout.Create(BackgroundTransform, "KingdomPolicies", false, 560, 330, m.SpacingSm);
        root.transform.localPosition = new Vector3(0, -8);
        var owner = root.AddComponent<CanvasGroup>();
        var top = UiLayout.Create(root.transform, "Heading", true, 560, m.ControlSmall, m.SpacingSm);
        heading = UiElements.CreateText(top.transform, "Title", "", 356, m.ControlSmall, 9, TextAnchor.MiddleLeft, FontStyle.Bold);
        cityChoice = UiElements.CreateButton(top.transform, "City", "", 200, m.ControlSmall, ShowCities);
        var pane = UiScrollPane.CreateVertical(root.transform, "Cards", 560, 248);
        pane.AttachOriginalScrollbar(context.ScrollbarTemplate); pane.SetWindowFrame(m.SpacingMd); scroll = pane.ScrollRect;
        var content = UiLayout.Create(pane.Content, "PolicyContents", false, 500, 568, m.SpacingSm);
        scope = UiElements.CreateText(content.transform, "Scope", "", 500, 32, 7, TextAnchor.UpperLeft);
        loyaltySummary = UiElements.CreateText(content.transform, "LoyaltySummary", "", 500, m.ControlSmall, 7, TextAnchor.MiddleLeft);
        var time = UiLayout.Create(content.transform, "DayLength", true, 500, m.ControlMedium, m.SpacingSm);
        dayLabel = UiElements.CreateText(time.transform, "Label", "", 300, m.ControlMedium, 8, TextAnchor.MiddleLeft);
        daySeconds = UiElements.CreateInput(time.transform, "Seconds", "", "6", 120, m.ControlMedium);
        daySeconds.contentType = InputField.ContentType.DecimalNumber; daySeconds.characterLimit = 12;
        daySeconds.onValueChanged.AddListener(_ => { if (!binding) { feedbackKey = "Unsaved"; Refresh(); } });
        dayHint = UiElements.CreateText(content.transform, "DayHint", "", 500, 28, 7, TextAnchor.UpperLeft);
        var ownership = content.AddComponent<ToggleGroup>(); ownership.allowSwitchOff = false;
        var choices = (KingdomPolicyCard[])Enum.GetValues(typeof(KingdomPolicyCard));
        float contentHeight = 32 + m.ControlSmall + m.ControlMedium + 28 + 3 * m.SpacingSm;
        for (int i = 0; i < choices.Length; i += 2)
        {
            float height = choices.Skip(i).Take(2).Max(CardHeight);
            var row = UiLayout.Create(content.transform, "Row" + i, true, 500, height, m.SpacingSm);
            foreach (var card in choices.Skip(i).Take(2)) BuildCard(row.transform, card, ownership);
            contentHeight += height + m.SpacingSm;
        }
        UiLayout.SetSize(content.transform, 500, contentHeight);
        var footer = UiLayout.Create(root.transform, "Actions", true, 560, m.ControlMedium, m.SpacingSm);
        apply = UiElements.CreateButton(footer.transform, "Apply", "", 92, m.ControlMedium, ApplyDraft);
        cancel = UiElements.CreateButton(footer.transform, "Cancel", "", 92, m.ControlMedium, ReloadDraft);
        feedback = UiElements.CreateText(footer.transform, "Feedback", "", 368, m.ControlMedium, 7, TextAnchor.MiddleLeft);
        cityMenu = new UiOptionMenu(BackgroundTransform, owner, context.ScrollbarTemplate, new UiOptionMenuConfig {
            PanelWidth = 300, ColumnCount = 2, SearchPlaceholder = T("SearchCity"), EmptyText = T("NoCity"),
            CloseText = T("Close"), CloseTooltipTitle = "Cultiway.Policies.Close", CloseTooltipDescription = "Cultiway.Policies.Close" });
    }

    /// <summary>卡片使用同一层表面，说明不绑定整行悬停，开关与参数按钮分别操作。</summary>
    /// <param name="parent">当前两列行。</param>
    /// <param name="card">该张制度。</param>
    /// <param name="ownership">公有和私有共用的互斥组。</param>
    private void BuildCard(Transform parent, KingdomPolicyCard card, ToggleGroup ownership)
    {
        var m = UiTheme.Current.Metrics;
        float width = (500 - m.SpacingSm) / 2;
        var root = UiLayout.Create(parent, card.ToString(), false, width, CardHeight(card), m.SpacingSm);
        int padding = (int)m.SpacingSm;
        root.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(padding, padding, padding, padding);
        var view = new CardView { Chrome = UiListRowChrome.Attach(root, true) };
        view.Chrome.Button.onClick.AddListener(() => { if (draft != null) ChangeCard(card, !draft.Has(card)); });
        var line = UiLayout.Create(root.transform, "Header", true, width - padding * 2, m.ControlSmall, m.SpacingSm);
        bool configurable = DetailPage(card) >= 0;
        view.Title = UiElements.CreateText(line.transform, "Name", "", configurable ? 132 : 184, m.ControlSmall, 8, TextAnchor.MiddleLeft, FontStyle.Bold);
        if (configurable) view.Configure = UiElements.CreateButton(line.transform, "Configure", T("Configure"), 48, m.ControlSmall, () => OpenDetails(card));
        view.Toggle = UiElements.CreateToggle(line.transform, "Enabled", "", false, 44, m.ControlSmall);
        if (card is KingdomPolicyCard.PublicFarms or KingdomPolicyCard.PrivateFarms) view.Toggle.group = ownership;
        view.Toggle.onValueChanged.AddListener(value =>
        {
            if (binding || (card is KingdomPolicyCard.PublicFarms or KingdomPolicyCard.PrivateFarms) && !value) return;
            ChangeCard(card, value);
        });
        view.Description = UiElements.CreateText(root.transform, "Description", "", width - padding * 2, 42, 7, TextAnchor.UpperLeft);
        view.Loyalty = UiElements.CreateText(root.transform, "Loyalty", "", width - padding * 2, m.ControlSmall, 7, TextAnchor.MiddleLeft);
        if (card == KingdomPolicyCard.Education)
        {
            var age = UiLayout.Create(root.transform, "AdmissionAge", true, width - padding * 2, m.ControlSmall, m.SpacingSm);
            schoolAgeLabel = UiElements.CreateText(age.transform, "Label", "", 132, m.ControlSmall, 7, TextAnchor.MiddleLeft);
            schoolAge = UiElements.CreateInput(age.transform, "Age", "6", "6", 64, m.ControlSmall);
            schoolAge.contentType = InputField.ContentType.IntegerNumber; schoolAge.characterLimit = 3;
            schoolAge.onValueChanged.AddListener(_ => { if (!binding) { feedbackKey = "Unsaved"; Refresh(); } });
        }
        cards.Add(card, view);
    }

    /// <summary>统一为政策影响和教育参数预留空间，行与卡片保持一致。</summary>
    /// <param name="card">当前政策卡。</param>
    private static float CardHeight(KingdomPolicyCard card) => (card == KingdomPolicyCard.Education ? 112 : 80) +
        UiTheme.Current.Metrics.ControlSmall + UiTheme.Current.Metrics.SpacingSm;

    /// <summary>正值显示加号，零值不附加符号。</summary>
    /// <param name="value">忠诚度修正点数。</param>
    private static string Signed(int value) => value.ToString("+0;-0;0", CultureInfo.InvariantCulture);

    /// <summary>产权必须选择一种，编辑只改变草稿。</summary>
    /// <param name="card">玩家操作的制度。</param>
    /// <param name="value">想要启用或关闭。</param>
    private void ChangeCard(KingdomPolicyCard card, bool value)
    { if (draft == null) return; draft.Set(card, value); feedbackKey = "Unsaved"; Refresh(); }

    /// <summary>制度详细参数继续复用现有村庄窗口。</summary>
    /// <param name="card">需要配置的制度。</param>
    private static int DetailPage(KingdomPolicyCard card) => card switch {
        KingdomPolicyCard.WorkCalendar => 1, KingdomPolicyCard.OccupationPlanning => 4,
        KingdomPolicyCard.ArmyOrganization => 2, KingdomPolicyCard.PrivateFarms => 5, _ => -1 };

    /// <summary>进入已选村庄的参数页，不提交尚未应用的政策草稿。</summary>
    /// <param name="card">需要配置的卡片。</param>
    private void OpenDetails(KingdomPolicyCard card)
    { var city = SelectedCity; if (city != null) WindowVillageDuties.Open(city, DetailPage(card)); }

    /// <summary>参数村庄直接选择，支持按名称查找。</summary>
    private void ShowCities()
    {
        var kingdom = Kingdom; if (kingdom == null) return;
        var city = SelectedCity;
        cityMenu.SetLabels(T("SearchCity"), T("NoCity"), T("Close"));
        cityMenu.Show(T("ChooseCity"), kingdom.cities.Where(c => c.isAlive()).Select(c => new UiOptionMenuOption {
            Label = c.name, SearchText = c.name, Selected = c == city, Select = () => { cityId = c.id; Refresh(); } }).ToArray(), true);
    }

    /// <summary>取消修改或首次打开时读取国家政策与当前世界日长。</summary>
    private void ReloadDraft()
    {
        bindRequested = false; var kingdom = Kingdom;
        if (kingdom == null || !kingdom.isAlive()) return;
        draft = KingdomPolicyService.Get(kingdom).Copy(); binding = true;
        daySeconds.SetTextWithoutNotify(WorldCalendar.DaySeconds.ToString("0.###", CultureInfo.InvariantCulture));
        schoolAge.SetTextWithoutNotify(draft.SchoolAdmissionAge.ToString(CultureInfo.InvariantCulture));
        binding = false; feedbackKey = "ScopeHint"; Refresh();
    }

    /// <summary>先完成输入检查，再提交国家政策与世界日长。</summary>
    private void ApplyDraft()
    {
        var kingdom = Kingdom;
        if (kingdom == null || draft == null) return;
        var proposed = draft.Copy();
        if (!int.TryParse(schoolAge.text, out proposed.SchoolAdmissionAge) || !proposed.IsValid())
        { feedbackKey = "InvalidAdmissionAge"; Refresh(); return; }
        if (!float.TryParse(daySeconds.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds) ||
            !WorldCalendar.ValidDaySeconds(seconds)) { feedbackKey = "InvalidDayLength"; Refresh(); return; }
        if (KingdomPolicyService.Apply(kingdom, proposed) && WorldCalendar.SetDaySeconds(seconds))
        { feedbackKey = "Applied"; draft = KingdomPolicyService.Get(kingdom).Copy(); }
        Refresh();
    }

    /// <summary>统一刷新文字和选择态，不覆盖正在输入的日长或未提交卡片。</summary>
    private void Refresh()
    {
        nextRefresh = Time.unscaledTime + 1;
        var kingdom = Kingdom; bool valid = kingdom != null && kingdom.isAlive() && draft != null;
        apply.interactable = valid; cancel.interactable = valid; daySeconds.interactable = valid;
        schoolAge.interactable = valid;
        cityChoice.interactable = valid && SelectedCity != null;
        foreach (var view in cards.Values)
        {
            view.Toggle.interactable = valid; view.Chrome.Button.interactable = valid;
            if (view.Configure != null) view.Configure.interactable = valid && SelectedCity != null;
        }
        heading.text = valid ? kingdom.name + " · " + T("Title") : T("InvalidKingdom");
        if (!valid) return;
        scope.text = T("Scope"); dayLabel.text = T("DayLength"); dayHint.text = T("DayHint");
        loyaltySummary.text = string.Format(T("LoyaltySummary"), Signed(KingdomPolicyService.Get(kingdom).LoyaltyTotal), Signed(draft.LoyaltyTotal));
        schoolAgeLabel.text = T("SchoolAdmissionAge");
        cityChoice.GetComponentInChildren<Text>().text = T("City") + ": " + (SelectedCity?.name ?? T("NoCity"));
        apply.GetComponentInChildren<Text>().text = T("Apply"); cancel.GetComponentInChildren<Text>().text = T("Cancel");
        feedback.text = T(feedbackKey); binding = true;
        foreach (var pair in cards)
        {
            var view = pair.Value; bool enabled = draft.Has(pair.Key);
            view.Title.text = T(pair.Key + ".Name"); view.Description.text = T(pair.Key + ".Description");
            view.Loyalty.text = string.Format(T("LoyaltyEffect"), Signed(KingdomPolicyData.LoyaltyImpact(pair.Key)));
            view.Toggle.SetIsOnWithoutNotify(enabled); view.Chrome.SetState(enabled ? UiControlState.Selected : UiControlState.Normal);
            if (view.Configure != null) { view.Configure.interactable = SelectedCity != null; view.Configure.GetComponentInChildren<Text>().text = T("Configure"); }
        }
        binding = false; LayoutRebuilder.MarkLayoutForRebuild(scroll.content);
    }

    /// <inheritdoc />
    public override void OnNormalEnable() { if (bindRequested || draft == null) ReloadDraft(); else Refresh(); }
    /// <summary>窗口显示时低频刷新国家归属与本地化文字。</summary>
    private void Update() { if (Time.unscaledTime >= nextRefresh && Config.game_loaded) Refresh(); }
    /// <summary>关闭时释放村庄选择菜单的交互锁。</summary>
    private void OnDisable() { cityMenu?.Hide(); }
    /// <summary>换世界后丢弃原国家、村庄和编辑草稿。</summary>
    public static void ClearWorldState()
    {
        kingdomId = 0; cityId = 0; bindRequested = false;
        if (instance == null) return;
        instance.draft = null; instance.cityMenu?.Hide();
        if (instance.gameObject.activeInHierarchy) instance.GetComponent<ScrollWindow>().clickHide();
    }
}
