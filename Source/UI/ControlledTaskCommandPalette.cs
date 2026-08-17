using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.ControlledTasks;
using strings;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>附体角色的任务入口、筛选目录、详情与提交边界。</summary>
internal sealed class ControlledTaskCommandPalette : MonoBehaviour
{
    private const float PanelWidth = 318f;
    private const float PanelHeight = 276f;
    private const float RefreshInterval = 0.2f;

    private static ControlledTaskCommandLibrary CommandLibrary => ModClass.L.ControlledTaskCommandLibrary;
    private static ControlledTaskCommandPalette instance;

    private readonly List<ResolvedCommand> resolvedCommands = new();
    private readonly List<CategoryButton> categoryButtons = new();
    private readonly HashSet<ControlledTaskCategory> activeCategories = new();
    private readonly Vector3[] corners = new Vector3[4];

    private PossessionUI boundUi;
    private RectTransform rootRect;
    private RectTransform characterPanel;
    private RectTransform actionHintsPanel;
    private Button entryButton;
    private RectTransform entryRect;
    private RectTransform panelRect;
    private Text detailTitle;
    private Text detailBody;
    private Text targetModeText;
    private Button actionButton;
    private Text actionButtonText;
    private UiSearchField searchField;
    private UiScrollPane commandPane;
    private MonoObjPool<ControlledTaskCommandSlot> slotPool;
    private UiSegmentedTabs categoryTabs;
    private ControlledTaskCategory? selectedCategory;
    private string selectedCommandId;
    private string confirmationCommandId;
    private string submissionReasonLocaleKey;
    private long sessionActorId;
    private long entryActorId;
    private int entryLibraryRevision = -1;
    private float nextEntryRefreshAt;
    private float nextRefreshAt;
    private bool rightPanelWasActive;
    private bool ownsRightPanelOverride;
    private bool panelOpen;
    private bool visualsBuilt;

    internal static bool IsOpen => instance != null && instance.panelOpen;

    internal static void Ensure()
    {
        if (instance != null) return;
        var root = new GameObject("CultiwayControlledTaskCommandPalette", typeof(RectTransform),
            typeof(ControlledTaskCommandPalette));
        Transform parent = CanvasMain.instance?.canvas_ui?.transform;
        if (parent != null) root.transform.SetParent(parent, false);
    }

    internal static void ToggleFromHotkey()
    {
        Ensure();
        if (ControlledTaskTargetSelection.IsActive)
        {
            ControlledTaskTargetSelection.CancelToPalette();
            return;
        }
        if (instance == null) return;
        if (instance.panelOpen) instance.CloseSession();
        else instance.Open();
    }

    internal static bool ConsumesPointerInput()
    {
        if (instance == null) return false;
        if (instance.entryButton != null && instance.entryButton.gameObject.activeInHierarchy &&
            ContainsPointer(instance.entryRect))
            return true;
        return instance.panelOpen && instance.panelRect != null && ContainsPointer(instance.panelRect);
    }

    internal static void ReturnFromTargetSelection(string commandId, string reasonLocaleKey = null)
    {
        if (instance == null) return;
        instance.selectedCommandId = commandId;
        instance.submissionReasonLocaleKey = reasonLocaleKey;
        instance.panelOpen = true;
        instance.panelRect.gameObject.SetActive(true);
        instance.AcquireRightPanel();
        instance.nextRefreshAt = 0f;
        instance.RefreshCommands();
    }

    internal static void CompleteHandoff()
    {
        if (instance != null) instance.EndSessionAfterHandoff();
    }

    internal static void ClearWorldState()
    {
        if (instance != null) instance.CloseSession();
    }

    private static bool ContainsPointer(RectTransform rect)
    {
        if (rect == null) return false;
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, eventCamera);
    }

    private void Awake()
    {
        instance = this;
        rootRect = GetComponent<RectTransform>();
        UiLayout.Stretch(rootRect);
    }

    private void Update()
    {
        if (!EnsureBound())
        {
            SetEntryVisible(false);
            return;
        }

        bool hasControlledMain = TryGetControlledMain(out Actor actor);
        SetEntryVisible(hasControlledMain && CommandLibrary.Commands.Count > 0);
        PositionHud();

        if (!hasControlledMain)
        {
            if (panelOpen) CloseSession();
            return;
        }

        long actorId = actor.getID();
        if (actorId != entryActorId || entryLibraryRevision != CommandLibrary.Revision ||
            Time.unscaledTime >= nextEntryRefreshAt)
        {
            entryActorId = actorId;
            entryLibraryRevision = CommandLibrary.Revision;
            nextEntryRefreshAt = Time.unscaledTime + 0.5f;
            UpdateEntryState(actor);
        }
        if (!panelOpen || Time.unscaledTime < nextRefreshAt) return;
        nextRefreshAt = Time.unscaledTime + RefreshInterval;
        RefreshCommands();
    }

    private bool EnsureBound()
    {
        PossessionUI ui = PossessionUI.instance;
        Transform canvasRoot = CanvasMain.instance?.canvas_ui?.transform;
        if (ui == null || canvasRoot == null) return false;
        Transform inner = ui.transform.Find("Inner");
        if (inner == null) return false;
        if (boundUi == ui && transform.parent == canvasRoot && visualsBuilt) return true;

        bool reacquireRightPanel = sessionActorId > 0;
        RestoreRightPanel();
        boundUi = ui;
        transform.SetParent(canvasRoot, false);
        UiLayout.Stretch(rootRect);
        characterPanel = inner.Find("Character Panel") as RectTransform;
        actionHintsPanel = inner.Find("Right") as RectTransform;
        if (!visualsBuilt) BuildVisuals();
        if (reacquireRightPanel) AcquireRightPanel();
        return true;
    }

    private void BuildVisuals()
    {
        entryButton = UiElements.CreateIconButton(transform, "TaskCommandEntry", "ui/icons/iconShowTasks",
            24f, 24f, Open, 3f);
        entryRect = entryButton.GetComponent<RectTransform>();
        entryRect.anchorMin = entryRect.anchorMax = Vector2.zero;
        entryRect.pivot = new Vector2(0f, 0.5f);

        UiWindowFrame panel = UiWindowFrame.CreateContentSize(transform, "TaskCommandPanel",
            PanelWidth, PanelHeight, UiTheme.Current.Metrics.SpacingXs);
        panelRect = panel.Root;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);

        BuildHeader(panel.Content);
        BuildCategories(panel.Content);
        BuildSearch(panel.Content);
        BuildCommandGrid(panel.Content);
        BuildDetails(panel.Content);

        panel.Root.gameObject.SetActive(false);
        visualsBuilt = true;
    }

    private void BuildHeader(Transform parent)
    {
        RectTransform row = CreateAnchoredRow(parent, "Header", 306f, 24f, -4f);
        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(row, false);
        RectTransform iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(2f, 0f);
        iconRect.sizeDelta = new Vector2(18f, 18f);
        UiResources.SetImage(icon.GetComponent<Image>(), "ui/icons/iconShowTasks");
        icon.GetComponent<Image>().raycastTarget = false;

        Text title = UiElements.CreateText(row, "Title", "Cultiway.ControlledTask.UI.Title".Localize(),
            230f, 22f, 9, TextAnchor.MiddleLeft, FontStyle.Bold);
        title.rectTransform.anchorMin = Vector2.zero;
        title.rectTransform.anchorMax = Vector2.one;
        title.rectTransform.offsetMin = new Vector2(25f, 1f);
        title.rectTransform.offsetMax = new Vector2(-28f, -1f);
        title.GetComponent<LayoutElement>().ignoreLayout = true;
        title.raycastTarget = false;

        Button close = UiElements.CreateIconButton(row, "Close", UiIcons.Cancel, 24f, 22f, CloseSession, 4f);
        RectTransform closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.anchoredPosition = Vector2.zero;
    }

    private void BuildCategories(Transform parent)
    {
        GameObject bar = UiLayout.Create(parent, "Categories", true, 306f, 22f, 2f,
            TextAnchor.MiddleCenter);
        RectTransform rect = bar.GetComponent<RectTransform>();
        AnchorTop(rect, -32f);
        categoryTabs = new UiSegmentedTabs();
        AddCategoryButton(bar.transform, null, "Cultiway.ControlledTask.Category.All");
        AddCategoryButton(bar.transform, ControlledTaskCategory.Movement,
            "Cultiway.ControlledTask.Category.Movement");
        AddCategoryButton(bar.transform, ControlledTaskCategory.Cultivation,
            "Cultiway.ControlledTask.Category.Cultivation");
        AddCategoryButton(bar.transform, ControlledTaskCategory.Crafting,
            "Cultiway.ControlledTask.Category.Crafting");
        AddCategoryButton(bar.transform, ControlledTaskCategory.Research,
            "Cultiway.ControlledTask.Category.Research");
        AddCategoryButton(bar.transform, ControlledTaskCategory.Sect,
            "Cultiway.ControlledTask.Category.Sect");
        AddCategoryButton(bar.transform, ControlledTaskCategory.Affairs,
            "Cultiway.ControlledTask.Category.Affairs");
        categoryTabs.SetSelected(0);
    }

    private void AddCategoryButton(Transform parent, ControlledTaskCategory? category, string localeKey)
    {
        Button button = UiElements.CreateButton(parent, category?.ToString() ?? "All", localeKey.Localize(),
            42f, 22f, null);
        button.onClick.AddListener(() =>
        {
            selectedCategory = category;
            confirmationCommandId = null;
            submissionReasonLocaleKey = null;
            categoryTabs.SetSelected(button);
            RefreshCommands();
            commandPane.ResetToTop();
        });
        categoryTabs.Add(button);
        categoryButtons.Add(new CategoryButton(category, button));
    }

    private void BuildSearch(Transform parent)
    {
        searchField = UiSearchField.Create(parent, "Search", string.Empty,
            "Cultiway.ControlledTask.UI.Search".Localize(), 306f, 22f);
        AnchorTop(searchField.Input.GetComponent<RectTransform>(), -58f);
        searchField.Input.onValueChanged.AddListener(_ =>
        {
            confirmationCommandId = null;
            submissionReasonLocaleKey = null;
            RefreshCommands();
            commandPane.ResetToTop();
        });
    }

    private void BuildCommandGrid(Transform parent)
    {
        commandPane = UiScrollPane.CreateGrid(parent, "Commands", 306f, 112f, 2,
            new Vector2(145f, 28f), new Vector2(3f, 3f));
        AnchorTop(commandPane.Root, -84f);
        ControlledTaskCommandSlot template = ControlledTaskCommandSlot.CreateTemplate(commandPane.Content);
        template.gameObject.SetActive(false);
        slotPool = new MonoObjPool<ControlledTaskCommandSlot>(
            template,
            commandPane.Content,
            slot => slot.Initialize(SelectCommand),
            null,
            slot => slot.Clear());
    }

    private void BuildDetails(Transform parent)
    {
        detailTitle = UiElements.CreateText(parent, "DetailTitle", string.Empty, 190f, 17f, 8,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        SetTopRect(detailTitle.rectTransform, new Vector2(7f, -200f), new Vector2(190f, 17f));
        detailTitle.color = UiTheme.Current.Palette.AccentText;

        targetModeText = UiElements.CreateText(parent, "TargetMode", string.Empty, 108f, 17f, 7,
            TextAnchor.MiddleRight);
        SetTopRightRect(targetModeText.rectTransform, new Vector2(-7f, -200f), new Vector2(108f, 17f));
        targetModeText.color = UiTheme.Current.Palette.MutedText;

        detailBody = UiElements.CreateText(parent, "DetailBody", string.Empty, 298f, 31f, 7,
            TextAnchor.UpperLeft, FontStyle.Normal, VerticalWrapMode.Truncate);
        SetTopRect(detailBody.rectTransform, new Vector2(7f, -218f), new Vector2(298f, 31f));
        detailBody.resizeTextForBestFit = true;
        detailBody.resizeTextMinSize = 5;
        detailBody.resizeTextMaxSize = 7;

        actionButton = UiElements.CreateIconTextButton(parent, "Execute", UiIcons.Confirm, string.Empty,
            116f, 24f, ExecuteSelected);
        RectTransform actionRect = actionButton.GetComponent<RectTransform>();
        actionRect.anchorMin = actionRect.anchorMax = new Vector2(1f, 0f);
        actionRect.pivot = new Vector2(1f, 0f);
        actionRect.anchoredPosition = new Vector2(-7f, 6f);
        actionButtonText = actionButton.GetComponentInChildren<Text>();

        Text hint = UiElements.CreateText(parent, "InputHint", "Cultiway.ControlledTask.UI.InputHint".Localize(),
            180f, 24f, 6, TextAnchor.MiddleLeft);
        RectTransform hintRect = hint.rectTransform;
        hintRect.anchorMin = hintRect.anchorMax = Vector2.zero;
        hintRect.pivot = Vector2.zero;
        hintRect.anchoredPosition = new Vector2(7f, 6f);
        hint.color = UiTheme.Current.Palette.MutedText;
    }

    private void Open()
    {
        if (!TryGetControlledActor(out Actor actor) || CommandLibrary.Commands.Count == 0) return;
        ControlledSkillTargetSelection.ClearWorldState();
        sessionActorId = actor.getID();
        panelOpen = true;
        panelRect.gameObject.SetActive(true);
        AcquireRightPanel();
        confirmationCommandId = null;
        submissionReasonLocaleKey = null;
        nextRefreshAt = 0f;
        RefreshCommands();
        SetEntrySelected(true);
    }

    private void CloseSession()
    {
        panelOpen = false;
        if (panelRect != null) panelRect.gameObject.SetActive(false);
        sessionActorId = 0;
        confirmationCommandId = null;
        submissionReasonLocaleKey = null;
        RestoreRightPanel();
        SetEntrySelected(false);
    }

    private void EndSessionAfterHandoff()
    {
        panelOpen = false;
        if (panelRect != null) panelRect.gameObject.SetActive(false);
        sessionActorId = 0;
        confirmationCommandId = null;
        RestoreRightPanel();
        SetEntrySelected(false);
    }

    private void AcquireRightPanel()
    {
        if (ownsRightPanelOverride || actionHintsPanel == null) return;
        rightPanelWasActive = actionHintsPanel.gameObject.activeSelf;
        actionHintsPanel.gameObject.SetActive(false);
        ownsRightPanelOverride = true;
    }

    private void RestoreRightPanel()
    {
        if (!ownsRightPanelOverride) return;
        if (actionHintsPanel != null) actionHintsPanel.gameObject.SetActive(rightPanelWasActive);
        ownsRightPanelOverride = false;
    }

    private void RefreshCommands()
    {
        if (!panelOpen || !TryResolveSessionActor(out Actor actor)) return;
        resolvedCommands.Clear();
        activeCategories.Clear();
        IReadOnlyList<ControlledTaskCommandAsset> commands = CommandLibrary.Commands;
        for (var i = 0; i < commands.Count; i++) activeCategories.Add(commands[i].Category);
        UpdateCategoryVisibility();

        string query = searchField.Input.text?.Trim() ?? string.Empty;
        for (var i = 0; i < commands.Count; i++)
        {
            ControlledTaskCommandAsset command = commands[i];
            if (selectedCategory.HasValue && command.Category != selectedCategory.Value) continue;
            if (!MatchesSearch(command, query)) continue;
            ControlledTaskAvailability availability;
            try
            {
                availability = command.Evaluate(actor);
            }
            catch (Exception exception)
            {
                ModClass.LogError($"[ControlledTaskPalette] evaluation failed command={command.id}: {exception}");
                availability = ControlledTaskAvailability.Unavailable(
                    "Cultiway.ControlledTask.Reason.InternalError");
            }
            resolvedCommands.Add(new ResolvedCommand(command, availability));
        }

        if (!ContainsResolved(selectedCommandId))
            selectedCommandId = resolvedCommands.Count > 0 ? resolvedCommands[0].Command.id : null;

        slotPool.ResetToStart();
        for (var i = 0; i < resolvedCommands.Count; i++)
        {
            ResolvedCommand resolved = resolvedCommands[i];
            ControlledTaskCommandSlot slot = slotPool.GetNext();
            slot.transform.SetSiblingIndex(i + 1);
            slot.Setup(resolved.Command, resolved.Availability, resolved.Command.id == selectedCommandId);
        }
        slotPool.ClearUnsed();
        searchField.Input.gameObject.SetActive(commands.Count > 10);
        RefreshDetails();
    }

    private void RefreshDetails()
    {
        if (!TryGetResolved(selectedCommandId, out ResolvedCommand resolved))
        {
            detailTitle.text = "Cultiway.ControlledTask.UI.NoResults".Localize();
            detailBody.text = string.Empty;
            targetModeText.text = string.Empty;
            actionButton.interactable = false;
            actionButtonText.text = "Cultiway.ControlledTask.UI.Execute".Localize();
            UiStateStyle.ApplyVisual(actionButton, UiControlState.Disabled);
            return;
        }

        detailTitle.text = resolved.Command.NameLocaleKey.Localize();
        targetModeText.text = resolved.Command.TargetMode == ControlledTaskTargetMode.WorldTile
            ? "Cultiway.ControlledTask.Target.WorldTile".Localize()
            : "Cultiway.ControlledTask.Target.None".Localize();

        if (!string.IsNullOrEmpty(submissionReasonLocaleKey))
        {
            detailBody.text = submissionReasonLocaleKey.Localize();
            detailBody.color = UiTheme.Current.Palette.Error;
        }
        else if (!resolved.Availability.Enabled)
        {
            detailBody.text = resolved.Availability.ReasonLocaleKey.Localize();
            detailBody.color = UiTheme.Current.Palette.Warning;
        }
        else if (confirmationCommandId == resolved.Command.id)
        {
            detailBody.text = "Cultiway.ControlledTask.UI.ConfirmDescription".Localize();
            detailBody.color = UiTheme.Current.Palette.Warning;
        }
        else
        {
            detailBody.text = resolved.Command.DescriptionLocaleKey.Localize();
            detailBody.color = UiTheme.Current.Palette.PrimaryText;
        }

        actionButton.interactable = resolved.Availability.Enabled;
        if (resolved.Command.TargetMode == ControlledTaskTargetMode.WorldTile)
            actionButtonText.text = "Cultiway.ControlledTask.UI.SelectLocation".Localize();
        else if (confirmationCommandId == resolved.Command.id)
            actionButtonText.text = "Cultiway.ControlledTask.UI.ConfirmExecute".Localize();
        else
            actionButtonText.text = "Cultiway.ControlledTask.UI.Execute".Localize();
        UiStateStyle.ApplyVisual(actionButton,
            resolved.Availability.Enabled ? UiControlState.Normal : UiControlState.Disabled);
    }

    private void SelectCommand(string commandId)
    {
        selectedCommandId = commandId;
        confirmationCommandId = null;
        submissionReasonLocaleKey = null;
        RefreshCommands();
    }

    private void ExecuteSelected()
    {
        if (!TryResolveSessionActor(out Actor actor) ||
            !TryGetResolved(selectedCommandId, out ResolvedCommand resolved) ||
            !resolved.Availability.Enabled)
            return;

        if (resolved.Command.RequiresConfirmation && confirmationCommandId != resolved.Command.id)
        {
            confirmationCommandId = resolved.Command.id;
            RefreshDetails();
            return;
        }

        submissionReasonLocaleKey = null;
        if (resolved.Command.TargetMode == ControlledTaskTargetMode.WorldTile)
        {
            panelOpen = false;
            panelRect.gameObject.SetActive(false);
            ControlledTaskTargetSelection.Begin(actor.getID(), resolved.Command.id);
            return;
        }

        ControlledTaskStartResult result = ControlledTaskOrderService.TryBegin(
            actor.getID(), resolved.Command.id, ControlledTaskTarget.None);
        if (!result.Success)
        {
            submissionReasonLocaleKey = result.ReasonLocaleKey;
            confirmationCommandId = null;
            RefreshCommands();
            return;
        }
        EndSessionAfterHandoff();
    }

    private void UpdateEntryState(Actor actor)
    {
        bool canOpen = ControllableUnit.count() == 1 && ReferenceEquals(ControllableUnit.getControllableUnit(), actor);
        entryButton.interactable = canOpen;
        UiStateStyle.ApplyVisual(entryButton,
            panelOpen ? UiControlState.Selected : canOpen ? UiControlState.Normal : UiControlState.Disabled);

        int enabled = 0;
        IReadOnlyList<ControlledTaskCommandAsset> commands = CommandLibrary.Commands;
        if (canOpen)
        {
            for (var i = 0; i < commands.Count; i++)
            {
                try
                {
                    if (commands[i].Evaluate(actor).Enabled) enabled++;
                }
                catch
                {
                    // 注册命令的异常会在面板刷新时记录；入口只给出稳定计数。
                }
            }
        }
        string hotkey = WorldboxGame.Hotkeys.GetHotkeyText(WorldboxGame.Hotkeys.IssueControlledTask, "B");
        string summary = canOpen
            ? string.Format("Cultiway.ControlledTask.UI.EntrySummary".Localize(), enabled, commands.Count, hotkey)
            : "Cultiway.ControlledTask.UI.EntryMultiple".Localize();
        UiTooltip.Set(entryButton.gameObject,
            "Cultiway.ControlledTask.UI.Entry".Localize(),
            summary);
    }

    private void UpdateCategoryVisibility()
    {
        int nonEmpty = activeCategories.Count;
        Transform bar = categoryButtons[0].Button.transform.parent;
        bar.gameObject.SetActive(nonEmpty > 1);
        for (var i = 1; i < categoryButtons.Count; i++)
        {
            CategoryButton item = categoryButtons[i];
            item.Button.gameObject.SetActive(item.Category.HasValue && activeCategories.Contains(item.Category.Value));
        }
        if (selectedCategory.HasValue && !activeCategories.Contains(selectedCategory.Value))
        {
            selectedCategory = null;
            categoryTabs.SetSelected(categoryButtons[0].Button);
        }
    }

    private static bool MatchesSearch(ControlledTaskCommandAsset command, string query)
    {
        if (string.IsNullOrEmpty(query)) return true;
        string name = command.NameLocaleKey.Localize();
        string category = GetCategoryLocaleKey(command.Category).Localize();
        return name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
               category.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
               command.id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string GetCategoryLocaleKey(ControlledTaskCategory category)
    {
        return $"Cultiway.ControlledTask.Category.{category}";
    }

    private bool ContainsResolved(string commandId)
    {
        if (string.IsNullOrEmpty(commandId)) return false;
        for (var i = 0; i < resolvedCommands.Count; i++)
            if (resolvedCommands[i].Command.id == commandId) return true;
        return false;
    }

    private bool TryGetResolved(string commandId, out ResolvedCommand resolved)
    {
        for (var i = 0; i < resolvedCommands.Count; i++)
        {
            if (resolvedCommands[i].Command.id != commandId) continue;
            resolved = resolvedCommands[i];
            return true;
        }
        resolved = default;
        return false;
    }

    private bool TryResolveSessionActor(out Actor actor)
    {
        actor = sessionActorId > 0 && World.world?.units != null ? World.world.units.get(sessionActorId) : null;
        return actor != null && !actor.isRekt() && TryGetControlledActor(out Actor controlled) &&
               ReferenceEquals(actor, controlled);
    }

    private static bool TryGetControlledMain(out Actor actor)
    {
        return ControlledCultivatorSkillControls.TryGetControlledActor(out actor);
    }

    private static bool TryGetControlledActor(out Actor actor)
    {
        actor = null;
        if (!TryGetControlledMain(out Actor controlled) ||
            ControllableUnit.count() != 1 || !ReferenceEquals(ControllableUnit.getControllableUnit(), controlled))
            return false;
        actor = controlled;
        return true;
    }

    private void PositionHud()
    {
        if (rootRect == null || characterPanel == null || entryRect == null || panelRect == null) return;
        characterPanel.GetWorldCorners(corners);
        Vector3 lowerLeft = rootRect.InverseTransformPoint(corners[0]);
        Vector3 upperRight = rootRect.InverseTransformPoint(corners[2]);
        float x = upperRight.x - rootRect.rect.xMin + 4f;
        float y = (lowerLeft.y + upperRight.y) * 0.5f - rootRect.rect.yMin;
        x = Mathf.Clamp(x, 4f, rootRect.rect.width - entryRect.rect.width - 4f);
        entryRect.anchoredPosition = new Vector2(x, y);

        float panelBottom = upperRight.y - rootRect.rect.yMin + 6f;
        float panelHeight = panelRect.sizeDelta.y;
        panelBottom = Mathf.Clamp(panelBottom, 4f,
            Mathf.Max(4f, rootRect.rect.height - panelHeight - 4f));
        panelRect.anchoredPosition = new Vector2(0f, panelBottom);
    }

    private void SetEntryVisible(bool visible)
    {
        if (entryButton != null) entryButton.gameObject.SetActive(visible);
    }

    private void SetEntrySelected(bool selected)
    {
        if (entryButton != null) UiStateStyle.ApplyVisual(entryButton,
            selected ? UiControlState.Selected : UiControlState.Normal);
    }

    private static RectTransform CreateAnchoredRow(Transform parent, string name, float width, float height,
        float top)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);
        AnchorTop(rect, top);
        return rect;
    }

    private static void AnchorTop(RectTransform rect, float top)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, top);
    }

    private static void SetTopRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetTopRightRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private readonly struct ResolvedCommand
    {
        public ControlledTaskCommandAsset Command { get; }
        public ControlledTaskAvailability Availability { get; }

        public ResolvedCommand(ControlledTaskCommandAsset command, ControlledTaskAvailability availability)
        {
            Command = command;
            Availability = availability;
        }
    }

    private readonly struct CategoryButton
    {
        public ControlledTaskCategory? Category { get; }
        public Button Button { get; }

        public CategoryButton(ControlledTaskCategory? category, Button button)
        {
            Category = category;
            Button = button;
        }
    }
}
