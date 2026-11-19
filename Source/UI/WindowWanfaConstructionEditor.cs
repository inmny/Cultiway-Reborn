using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.Skills;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using NeoModLoader.api;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>万法阁的构造改造，使用独立草稿和正式人物学习入口。</summary>
public sealed partial class WindowWanfaConstructionEditor : AbstractWideWindow<WindowWanfaConstructionEditor>
{
    /// <summary>沿用原版宽窗口的稳定编号及尺寸。</summary>
    public const string Id = "Cultiway.UI.WindowWanfaConstructionEditor";
    /// <summary>与现有万法阁相同的外部尺寸。</summary>
    public static readonly Vector2 WindowSize = new(600f, 360f);
    /// <summary>打开窗口前冻结的来源。</summary>
    private static string pendingId;
    /// <summary>可选的实际学习者。</summary>
    private static Actor pendingActor;
    /// <summary>当前窗口，供统一打开入口使用。</summary>
    private static WindowWanfaConstructionEditor instance;
    /// <summary>正式内容读写入口。</summary>
    private IConstructionEditingAccess access;
    /// <summary>完整来源与独立草稿。</summary>
    private ConstructionEditSession session;
    /// <summary>本轮共用校验所得的计划，用于显示已解析的来源部位。</summary>
    private CompiledAbility previewPlan;
    /// <summary>学习者及其稳定编号，防止原版对象池重用。</summary>
    private Actor learner;
    /// <summary>选择人物时的稳定编号。</summary>
    private long learnerId;
    /// <summary>有界撤销与重做，只保存资料。</summary>
    private readonly List<AbilityDefinition> undo = new(), redo = new();
    /// <summary>草稿中的生成部位，刷新后重新绑定。</summary>
    private readonly List<ConstructionNode> parts = new();
    /// <summary>当前选择的部位及参数页。</summary>
    private int part, page, handle, bodyHandle;
    /// <summary>刷新与退出状态。</summary>
    private bool refreshing, closingApproved, resumeClose, valid;
    /// <summary>基本控件与状态文本。</summary>
    private InputField nameInput;
    /// <summary>学习者与部位选择。</summary>
    private Button learnerButton, partButton, undoButton, redoButton, copyButton, replaceButton;
    /// <summary>共用编译摘要及错误。</summary>
    private Text summary, state;
    /// <summary>共用搜索选择、确认及条目池。</summary>
    private UiOptionMenu menu;
    /// <summary>未保存退出确认。</summary>
    private UiModal closeModal;
    /// <summary>复用万法阁参数行。</summary>
    private MonoObjPool<WanfaEditorRow> rows;
    /// <summary>窗口内的互斥页。</summary>
    private readonly UiSegmentedTabs tabs = new();

    /// <summary>从目录或人物页打开当前正式构造。</summary>
    /// <param name="id">构造编号。</param>
    /// <param name="actor">指定学习者。</param>
    public static void Open(string id, Actor actor = null)
    {
        if (instance != null && instance.session != null && !instance.session.Matches(instance.session.Draft))
        { WorldTip.showNow(T("FinishDraft"), false, "top", 3f); return; }
        pendingId = id; pendingActor = actor;
        ScrollWindow.showWindow(Id);
    }

    /// <summary>只在本窗口解析固定控件并绑定一次事件。</summary>
    protected override void Init()
    {
        instance = this; access = ConstructionEditing.Access;
        UiWindowContext context = UiWindowContext.Bind(BackgroundTransform);
        UiWideWindowFit.Attach(BackgroundTransform);
        GameObject root = UiLayout.Create(BackgroundTransform, "ConstructionEditor", false, 520f, 318f, 4f);
        root.transform.localPosition = new Vector3(0f, -8f);
        CanvasGroup canvas = root.AddComponent<CanvasGroup>();
        GameObject header = UiLayout.Create(root.transform, "Header", true, 520f, 24f, 4f);
        nameInput = UiElements.CreateInput(header.transform, "Name", "", T("Name"), 248f, 24f);
        nameInput.characterLimit = 128;
        nameInput.onEndEdit.AddListener(value => { if (!refreshing) Change(() => session.Draft.Name = value); });
        learnerButton = UiElements.CreateIconTextButton(header.transform, "Learner", UiIcons.Select, T("ChooseLearner"), 248f, 24f, ChooseLearner);
        GameObject navigation = UiLayout.Create(root.transform, "Pages", true, 520f, 22f, 4f);
        string[] pages = { "Properties", "Path", "BodyMotion", "Placement", "ImpactPage", "StepsPage" };
        for (int i = 0; i < pages.Length; i++)
        {
            int index = i;
            Button button = UiElements.CreateButton(navigation.transform, "Page" + i, T(pages[i]), (520f - 4f * (pages.Length - 1)) / pages.Length, 22f,
                () => { page = index; Refresh(); });
            tabs.Add(button);
            BindText(button.GetComponentInChildren<Text>(), pages[i]);
        }
        GameObject selection = UiLayout.Create(root.transform, "Selection", true, 520f, 22f, 4f);
        partButton = UiElements.CreateIconTextButton(selection.transform, "Part", UiIcons.Options, T("Part"), 180f, 22f, ChoosePart);
        summary = UiElements.CreateText(selection.transform, "Summary", "", 316f, 22f, 6);
        UiScrollPane pane = UiScrollPane.CreateVertical(root.transform, "Parameters", 520f, 180f);
        pane.AttachOriginalScrollbar(context.ScrollbarTemplate); pane.SetWindowFrame(UiTheme.Current.Metrics.SpacingMd);
        rows = new MonoObjPool<WanfaEditorRow>(WanfaEditorRow.Prefab, pane.Content);
        state = UiElements.CreateText(root.transform, "State", "", 500f, 28f, 6);
        GameObject footer = UiLayout.Create(root.transform, "Footer", true, 520f, 22f, 4f);
        undoButton = UiElements.CreateIconButton(footer.transform, "Undo", UiIcons.Undo, 28f, 22f, () => History(undo, redo));
        redoButton = UiElements.CreateIconButton(footer.transform, "Redo", UiIcons.Undo, 28f, 22f, () => History(redo, undo), iconRotation: 180f);
        copyButton = UiElements.CreateButton(footer.transform, "LearnCopy", T("LearnCopy"), 132f, 22f, () => Save(false));
        replaceButton = UiElements.CreateButton(footer.transform, "Replace", T("Replace"), 132f, 22f, () => Save(true));
        Button close = UiElements.CreateButton(footer.transform, "Close", T("Close"), 88f, 22f, RequestClose);
        BindText(copyButton.GetComponentInChildren<Text>(), "LearnCopy");
        BindText(replaceButton.GetComponentInChildren<Text>(), "Replace");
        BindText(close.GetComponentInChildren<Text>(), "Close");
        locateIssueButton = UiElements.CreateButton(footer.transform, "LocateIssue", T("LocateIssue"), 88f, 22f, LocateIssue);
        BindText(locateIssueButton.GetComponentInChildren<Text>(), "LocateIssue");
        locateIssueButton.gameObject.SetActive(false);
        BindText((Text)nameInput.placeholder, "Name");
        UiTooltip.Set(undoButton.gameObject, "Cultiway.Construction.Undo", "Cultiway.Construction.Undo");
        UiTooltip.Set(redoButton.gameObject, "Cultiway.Construction.Redo", "Cultiway.Construction.Redo");
        UiTooltip.Set(copyButton.gameObject, "Cultiway.Construction.LearnCopy", "Cultiway.Construction.CopyHint");
        UiTooltip.Set(replaceButton.gameObject, "Cultiway.Construction.Replace", "Cultiway.Construction.ReplaceHint");
        menu = new UiOptionMenu(BackgroundTransform, canvas, context.ScrollbarTemplate, new UiOptionMenuConfig
        {
            SearchPlaceholder = "Cultiway.Construction.Search",
            EmptyText = "Cultiway.Construction.Empty",
            CloseText = "Cultiway.Construction.Close",
            LocalizeLabels = true,
            CloseTooltipTitle = "Cultiway.Construction.Close",
            CloseTooltipDescription = "Cultiway.Construction.Close"
        });
        UiWindowFrame confirm = UiWindowFrame.CreateContentSize(BackgroundTransform, "Unsaved", 260f, 100f);
        GameObject content = UiLayout.Create(confirm.Content, "Body", false, 260f, 100f, 4f);
        BindText(UiElements.CreateText(content.transform, "Message", T("Unsaved"), 252f, 36f, 7), "Unsaved");
        BindText(UiElements.CreateButton(content.transform, "Discard", T("Discard"), 252f, 24f, CloseNow).GetComponentInChildren<Text>(), "Discard");
        BindText(UiElements.CreateButton(content.transform, "Continue", T("Continue"), 252f, 24f, () => closeModal.Hide()).GetComponentInChildren<Text>(), "Continue");
        closeModal = new UiModal(confirm.Root.gameObject, canvas);
        InitPerformancePreview(canvas);
        access.WorldClearing += ClearWorld;
        Cultiway.Core.Visuals.Forms.FormResources.Reloaded += ResourcesReloaded;
    }

    /// <summary>绑定冻结的打开请求，或恢复原版关闭后弹出的未保存提示。</summary>
    public override void OnNormalEnable()
    {
        if (resumeClose) { resumeClose = false; Refresh(); closeModal.Show(); return; }
        if (pendingId == null) return;
        session = new ConstructionEditSession(access.Read(pendingId));
        learner = pendingActor; learnerId = learner?.data?.id ?? 0;
        pendingId = null; pendingActor = null;
        contactPanels.Clear();
        undo.Clear(); redo.Clear(); part = page = handle = bodyHandle = stepChoice = nativeChoice = operationScope = 0; curvePurpose = 0; curveLayer = 0; closingApproved = false; Refresh();
    }

    /// <summary>保留原版窗口关闭流程，未保存时在下一帧恢复并显示确认。</summary>
    public override void OnNormalDisable()
    {
        ClosePerformancePreview();
        menu.Clear(); closeModal.Hide();
        if (session == null || closingApproved) return;
        if (session.Matches(session.Draft)) { ReleaseDraft(); return; }
        resumeClose = true; World.world.StartCoroutine(Reopen());
    }

    /// <summary>原版关闭完成之后再恢复同一草稿。</summary>
    private IEnumerator Reopen()
    { yield return new WaitForEndOfFrame(); if (resumeClose) ScrollWindow.showWindow(Id, true, false); }

    /// <summary>实际人物仍是选择时的同一个对象。</summary>
    private bool HasLearner => learner?.data?.id == learnerId && learnerId != 0 && learner.isAlive() && learner.hasHealth();

    /// <summary>统一刷新所有状态和条目，不在多个回调中各自改可用性。</summary>
    private void Refresh()
    {
        if (session == null) return;
        refreshing = true;
        try
        {
            parts.Clear(); if (session.Draft.Root != null) Visit(session.Draft.Root);
            part = Mathf.Clamp(part, 0, Mathf.Max(0, parts.Count - 1));
            nameInput.SetTextWithoutNotify(session.Draft.Name);
            UiElements.SetButtonLabel(learnerButton, HasLearner ? string.Format(T("Learner"), learner.getName()) : T("ChooseLearner"));
            UiElements.SetButtonLabel(partButton, parts.Count == 0 ? T("BodyOnly") : PartName(part));
            partButton.interactable = parts.Count > 0;
            valid = false; previewPlan = null; state.text = ""; ClearConstructionIssue();
            try
            {
                CompiledAbility plan = previewPlan = access.Validate(session.Draft);
                valid = !string.IsNullOrWhiteSpace(session.Draft.Name);
                state.text = valid ? (session.Matches(session.Draft) ? T("Saved") : T("Modified")) : T("NameRequired");
                summary.text = string.Format(T("Summary"), plan.FormCount, plan.Cost,
                    ConstructionPresentation.ResourceNames(plan), plan.Cooldown);
                UiTooltip.Set(summary.gameObject, session.Draft.Name,
                    ConstructionPresentation.Explain(plan));
            }
            catch (Exception error) when (error is InvalidDataException or IOException or ArgumentException)
            {
                ShowConstructionIssue(error);
                summary.text = T("InvalidSummary");
                UiTooltip.Clear(summary.gameObject);
            }

            state.color = valid ? UiTheme.Current.Palette.PrimaryText : UiTheme.Current.Palette.Error;
            undoButton.interactable = undo.Count > 0; redoButton.interactable = redo.Count > 0;
            copyButton.interactable = valid && HasLearner;
            replaceButton.interactable = copyButton.interactable && access.List(learner.GetExtend()).Any(e => e.Id == session.SourceId);
            rows.Clear(); tabs.SetSelected(page);
            if (page == 0) Properties(); else if (page == 1) PathPage(); else if (page == 2) BodyPage(); else if (page == 3) PlacementPage(); else if (page == 4) ImpactPage(); else StepsPage();
        }
        finally { refreshing = false; }

        void Visit(ConstructionNode node) { parts.Add(node); foreach (ConstructionNode child in node.Children) Visit(child); }
    }

    /// <summary>从真实部位生成友好的选择名称。</summary>
    /// <param name="index">草稿部位索引。</param>
    private string PartName(int index) => string.Format(T(index == 0 ? "RootPart" : "ChildPart"),
        LocalizedTextManager.getText("Cultiway.Construction.Form." + parts[index].Form), index);

    /// <summary>从所有实际部位中选择，不将展开的挂点集合误当成另一份资料。</summary>
    private void ChoosePart() => menu.Show(T("Part"), parts.Select((node, index) => new UiOptionMenuOption
    { Label = PartName(index), Selected = part == index, Select = () => { part = index; Refresh(); } }).ToArray(), true);

    /// <summary>使用搜索菜单选择实际仍活着的人物。</summary>
    private void ChooseLearner()
    {
        menu.Show(T("ChooseLearner"), World.world.units.getSimpleList().Where(a => a.isAlive() && a.hasHealth()).Select(a =>
        {
            long id = a.id;
            return new UiOptionMenuOption
            {
                Label = a.getName(),
                SearchText = a.getName() + " " + a.asset.id.Localize(),
                Selected = HasLearner && learnerId == id,
                Select = () => { learner = World.world.units.get(id); learnerId = id; Refresh(); }
            };
        }).ToArray(), true);
    }

    /// <summary>修改草稿并保留最多64步历史。</summary>
    /// <param name="change">仅修改当前草稿的命令。</param>
    private void Change(Action change)
    {
        if (refreshing || session == null) return;
        if (undo.Count == 64) undo.RemoveAt(0);
        undo.Add(ConstructionEditing.Copy(session.Draft)); redo.Clear(); change(); Refresh();
    }

    /// <summary>完整恢复资料，不把界面对象写入历史。</summary>
    /// <param name="from">读取的历史。</param>
    /// <param name="to">保存当前资料的历史。</param>
    private void History(List<AbilityDefinition> from, List<AbilityDefinition> to)
    {
        if (from.Count == 0) return;
        to.Add(ConstructionEditing.Copy(session.Draft)); session.Draft = from[from.Count - 1]; from.RemoveAt(from.Count - 1); Refresh();
    }

    /// <summary>经正式入口保存并学习，再以新修订作为后续编辑基线。</summary>
    /// <param name="replace">是否只替换此人的原掌握关系。</param>
    private void Save(bool replace)
    {
        if (!HasLearner) { Refresh(); return; }
        try
        {
            AbilityDefinition saved = access.Apply(learner.GetExtend(), session, replace);
            session = new ConstructionEditSession(saved); undo.Clear(); redo.Clear(); Refresh();
            WorldTip.showNow(T("Applied"), false, "top", 2f);
        }
        catch (Exception error) when (error is InvalidDataException or IOException or ArgumentException or InvalidOperationException)
        { ShowConstructionIssue(error); }
    }

    /// <summary>通过明确的放弃动作关闭，或继续编辑。</summary>
    private void RequestClose()
    { if (session != null && !session.Matches(session.Draft)) closeModal.Show(); else CloseNow(); }

    /// <summary>释放当前编辑绑定并沿用原版关闭。</summary>
    private void CloseNow()
    {
        closingApproved = true; resumeClose = false; closeModal.Hide(); ReleaseDraft(); GetComponent<ScrollWindow>().clickHide();
    }

    /// <summary>释放编辑绑定和历史，不在正常关闭后保留人物引用。</summary>
    private void ReleaseDraft()
    {
        ClosePerformancePreview();
        session = null; previewPlan = null; learner = null; learnerId = 0; ClearConstructionIssue();
        undo.Clear(); redo.Clear(); parts.Clear(); rows.Clear(); menu.Clear();
    }

    /// <summary>切换世界时不再恢复旧人物和旧草稿。</summary>
    private void ClearWorld()
    {
        pendingId = null; pendingActor = null;
        closingApproved = true; resumeClose = false; ReleaseDraft(); closeModal.Hide();
        if (gameObject.activeInHierarchy) GetComponent<ScrollWindow>().clickHide();
    }

    /// <summary>解除窗口拥有的外部订阅。</summary>
    /// <summary>资源更新后重新编译当前草稿，保留未保存内容及撤销历史。</summary>
    /// <param name="version">已经发布的新资源版本。</param>
    private void ResourcesReloaded(int version)
    {
        ClosePerformancePreview();
        previewPlan = null;
        if (session != null && gameObject.activeInHierarchy) Refresh();
    }

    /// <summary>销毁窗口时解除世界和资源通知，不让旧预览继续持有资源。</summary>
    private void OnDestroy()
    {
        ReleasePerformanceViewport();
        access.WorldClearing -= ClearWorld;
        Cultiway.Core.Visuals.Forms.FormResources.Reloaded -= ResourcesReloaded;
        if (instance == this) instance = null;
    }

    /// <summary>本功能固定文本的统一入口。</summary>
    /// <param name="key">局部文本键。</param>
    private static string T(string key) => LocalizedTextManager.getText("Cultiway.Construction." + key);

    /// <summary>固定文本复用原版本地化组件，语言切换无需重建窗口。</summary>
    /// <param name="text">当前固定文本。</param>
    /// <param name="key">局部文本键。</param>
    private static void BindText(Text text, string key)
        => text.gameObject.AddComponent<LocalizedText>().setKeyAndUpdate("Cultiway.Construction." + key);
}
