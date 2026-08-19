using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Core.ControlledTasks;
using strings;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>受控任务参数选择器；只维护短时 UI 选择，不在选项刷新阶段触发玩法副作用。</summary>
internal sealed class ControlledTaskParameterPicker
{
    private const float PanelWidth = 390f;
    private const float PanelHeight = 318f;
    private const float BodyHeight = 144f;
    private const float FullWidthOptionWidth = PanelWidth - 12f;
    private const float CompactOptionWidth = 248f;
    private const int GridColumnCount = 10;
    private const float GridSpacing = 3f;

    private readonly UiWindowFrame frame;
    private readonly UiModal modal;
    private readonly Text title;
    private readonly Text parameterLabel;
    private readonly Text summary;
    private readonly Text error;
    private readonly InputField search;
    private readonly UiScrollPane listOptionsPane;
    private readonly UiScrollPane gridOptionsPane;
    private readonly MonoObjPool<ControlledTaskOptionListRow> listOptionPool;
    private readonly MonoObjPool<ControlledTaskOptionGridCell> gridOptionPool;
    private readonly Button previousButton;
    private readonly Button nextButton;
    private readonly Text nextButtonLabel;
    private readonly Dictionary<string, List<string>> selections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> optionLabels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ControlledTaskOption> visibleOptions =
        new(StringComparer.Ordinal);

    private ControlledTaskCommandAsset command;
    private Actor actor;
    private IReadOnlyList<ControlledTaskParameterDefinition> parameters = Array.Empty<ControlledTaskParameterDefinition>();
    private Action<ControlledTaskInvocation> confirmed;
    private Action cancelled;
    private int parameterIndex;
    private bool visible;

    internal bool IsVisible => visible;
    internal RectTransform Root => frame.Root;

    internal ControlledTaskParameterPicker(
        Transform parent,
        CanvasGroup owner,
        Action<ControlledTaskInvocation> onConfirmed,
        Action onCancelled)
    {
        confirmed = onConfirmed;
        cancelled = onCancelled;

        frame = UiWindowFrame.CreateContentSize(parent, "TaskParameterPicker",
            PanelWidth, PanelHeight, UiTheme.Current.Metrics.SpacingMd);
        frame.Root.anchorMin = frame.Root.anchorMax = new Vector2(0.5f, 0.5f);
        frame.Root.pivot = new Vector2(0.5f, 0.5f);
        frame.Root.anchoredPosition = Vector2.zero;

        Transform content = frame.Content;
        GameObject layoutObject = UiLayout.Create(content, "Layout", false, PanelWidth, PanelHeight, 5f,
            TextAnchor.UpperCenter);
        layoutObject.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(4, 4, 3, 3);

        GameObject header = UiLayout.Create(layoutObject.transform, "Header", true, PanelWidth, 24f, 4f);
        title = UiElements.CreateText(header.transform, "Title", string.Empty, PanelWidth - 32f, 24f, 9,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        Button close = UiElements.CreateIconButton(header.transform, "Close", UiIcons.Cancel, 24f, 22f,
            Cancel);
        UiTooltip.Set(close.gameObject, "Cultiway.ControlledTask.UI.Cancel".Localize(),
            "Cultiway.ControlledTask.UI.ParameterCancelDescription".Localize());

        parameterLabel = UiElements.CreateText(layoutObject.transform, "Parameter", string.Empty,
            PanelWidth, 20f, 7, TextAnchor.MiddleLeft, FontStyle.Bold);
        search = UiSearchField.Create(layoutObject.transform, "Search", string.Empty,
            "Cultiway.ControlledTask.UI.SearchOptions".Localize(), PanelWidth, 22f).Input;
        search.onValueChanged.AddListener(_ => RefreshOptions(false));

        listOptionsPane = UiScrollPane.CreateVertical(layoutObject.transform, "ListOptions", PanelWidth, BodyHeight);
        listOptionsPane.SetSurface(UiSurface.WindowInner, UiTheme.Current.Metrics.SpacingXs, false);
        VerticalLayoutGroup listLayout = listOptionsPane.Content.GetComponent<VerticalLayoutGroup>();
        listLayout.childControlWidth = false;
        listLayout.childForceExpandWidth = false;
        listLayout.childAlignment = TextAnchor.UpperLeft;

        gridOptionsPane = UiScrollPane.CreateGrid(layoutObject.transform, "GridOptions", PanelWidth, BodyHeight,
            GridColumnCount, new Vector2(ControlledTaskOptionGridCell.CellSize,
                ControlledTaskOptionGridCell.CellSize), new Vector2(GridSpacing, GridSpacing));
        gridOptionsPane.SetSurface(UiSurface.WindowInner, UiTheme.Current.Metrics.SpacingXs, false);
        gridOptionsPane.Content.GetComponent<GridLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
        gridOptionsPane.Root.gameObject.SetActive(false);

        ControlledTaskOptionListRow listTemplate =
            ControlledTaskOptionListRow.CreateTemplate(listOptionsPane.Content, FullWidthOptionWidth);
        listTemplate.gameObject.SetActive(false);
        listOptionPool = new MonoObjPool<ControlledTaskOptionListRow>(
            listTemplate,
            listOptionsPane.Content,
            row => row.Initialize(ToggleByKey),
            null,
            row => row.Clear());

        ControlledTaskOptionGridCell gridTemplate =
            ControlledTaskOptionGridCell.CreateTemplate(gridOptionsPane.Content);
        gridTemplate.gameObject.SetActive(false);
        gridOptionPool = new MonoObjPool<ControlledTaskOptionGridCell>(
            gridTemplate,
            gridOptionsPane.Content,
            cell => cell.Initialize(ToggleByKey),
            null,
            cell => cell.Clear());

        summary = UiElements.CreateText(layoutObject.transform, "Summary", string.Empty, PanelWidth, 25f, 6,
            TextAnchor.MiddleLeft);
        summary.resizeTextForBestFit = true;
        summary.resizeTextMinSize = 5;
        summary.resizeTextMaxSize = 7;

        error = UiElements.CreateText(layoutObject.transform, "Error", string.Empty, PanelWidth, 22f, 6,
            TextAnchor.MiddleLeft);
        error.color = UiTheme.Current.Palette.Error;

        GameObject footer = UiLayout.Create(layoutObject.transform, "Footer", true, PanelWidth, 25f, 4f,
            TextAnchor.MiddleCenter);
        previousButton = UiElements.CreateIconTextButton(footer.transform, "Previous", UiIcons.Previous,
            "Cultiway.ControlledTask.UI.PreviousParameter".Localize(), 108f, 24f, Previous);
        nextButton = UiElements.CreateIconTextButton(footer.transform, "Next", UiIcons.Next, string.Empty,
            126f, 24f, Next);
        nextButtonLabel = nextButton.GetComponentInChildren<Text>();
        UiElements.CreateIconTextButton(footer.transform, "Cancel", UiIcons.Cancel,
            "Cultiway.ControlledTask.UI.Cancel".Localize(), 82f, 24f, Cancel);

        modal = new UiModal(frame.Root.gameObject, owner);
    }

    internal void Show(Actor targetActor, ControlledTaskCommandAsset targetCommand,
        ControlledTaskInvocation initialInvocation)
    {
        actor = targetActor;
        command = targetCommand;
        parameters = command?.Parameters ?? Array.Empty<ControlledTaskParameterDefinition>();
        parameterIndex = 0;
        selections.Clear();
        optionLabels.Clear();
        visibleOptions.Clear();
        foreach (ControlledTaskParameterDefinition definition in parameters)
        {
            List<string> values = new();
            foreach (string value in initialInvocation.GetSelections(definition.Key))
                if (!string.IsNullOrEmpty(value) && !values.Contains(value)) values.Add(value);
            selections[definition.Key] = values;
        }

        title.text = command?.NameLocaleKey.Localize() ?? string.Empty;
        search.SetTextWithoutNotify(string.Empty);
        error.text = string.Empty;
        visible = true;
        modal.Show();
        RefreshOptions(true);
    }

    internal void Hide()
    {
        if (!visible) return;
        visible = false;
        modal.Hide();
    }

    internal void CancelSilently()
    {
        if (!visible) return;
        visible = false;
        modal.Hide();
    }

    private void RefreshOptions(bool resetScroll)
    {
        if (!visible || command == null || parameterIndex < 0 || parameterIndex >= parameters.Count) return;
        ControlledTaskParameterDefinition definition = parameters[parameterIndex];
        bool useGrid = definition.Layout == ControlledTaskParameterLayout.ItemGrid;
        listOptionsPane.Root.gameObject.SetActive(!useGrid);
        gridOptionsPane.Root.gameObject.SetActive(useGrid);
        parameterLabel.text = definition.NameLocaleKey.Localize();

        IReadOnlyList<ControlledTaskOption> options;
        try
        {
            options = command.GetOptions(actor, definition.Key, BuildInvocation()) ??
                      Array.Empty<ControlledTaskOption>();
        }
        catch (Exception exception)
        {
            ModClass.LogError($"[ControlledTaskPicker] option query failed command={command.id}: {exception}");
            options = Array.Empty<ControlledTaskOption>();
            error.text = "Cultiway.ControlledTask.Reason.InternalError".Localize();
        }

        List<string> current = GetSelection(definition.Key);
        HashSet<string> validKeys = new(options
            .Where(option => option?.Enabled == true)
            .Select(option => option.Key), StringComparer.Ordinal);
        current.RemoveAll(value => !validKeys.Contains(value));

        string query = search.text?.Trim() ?? string.Empty;
        List<ControlledTaskOption> filteredOptions = options
            .Where(option => option != null &&
                             (query.Length == 0 || option.SearchText.IndexOf(query,
                                 StringComparison.OrdinalIgnoreCase) >= 0))
            .ToList();
        visibleOptions.Clear();
        for (int i = 0; i < filteredOptions.Count; i++)
        {
            ControlledTaskOption option = filteredOptions[i];
            optionLabels[option.Key] = option.Label;
            visibleOptions[option.Key] = option;
        }

        listOptionPool.Clear();
        gridOptionPool.Clear();
        for (int i = 0; i < filteredOptions.Count; i++)
        {
            ControlledTaskOption option = filteredOptions[i];
            bool selected = current.Contains(option.Key);
            if (useGrid)
            {
                gridOptionPool.GetNext().Setup(option, selected);
            }
            else
            {
                float width = definition.Layout == ControlledTaskParameterLayout.CompactList
                    ? CompactOptionWidth
                    : FullWidthOptionWidth;
                listOptionPool.GetNext().Setup(option, selected, width);
            }
        }

        if (resetScroll)
        {
            if (useGrid) gridOptionsPane.ResetToTop();
            else listOptionsPane.ResetToTop();
        }
        UpdateFooter();
        UpdateSummary();
    }

    private void ToggleByKey(string key)
    {
        if (!visible || parameterIndex < 0 || parameterIndex >= parameters.Count ||
            !visibleOptions.TryGetValue(key, out ControlledTaskOption option)) return;
        Toggle(parameters[parameterIndex], option);
    }

    private void Toggle(ControlledTaskParameterDefinition definition, ControlledTaskOption option)
    {
        if (!option.Enabled) return;
        List<string> current = GetSelection(definition.Key);
        if (definition.Mode == ControlledTaskParameterMode.SingleChoice)
        {
            current.Clear();
            current.Add(option.Key);
        }
        else if (current.Contains(option.Key))
        {
            current.Remove(option.Key);
        }
        else if (current.Count < definition.MaxSelected)
        {
            current.Add(option.Key);
        }
        else
        {
            error.text = "Cultiway.ControlledTask.Reason.ParameterSelectionLimit".Localize();
            return;
        }

        error.text = string.Empty;
        RefreshOptions(false);
    }

    private void Previous()
    {
        if (parameterIndex <= 0) return;
        parameterIndex--;
        search.SetTextWithoutNotify(string.Empty);
        error.text = string.Empty;
        RefreshOptions(true);
    }

    private void Next()
    {
        if (!ValidateCurrentParameter()) return;
        if (parameterIndex + 1 < parameters.Count)
        {
            parameterIndex++;
            search.SetTextWithoutNotify(string.Empty);
            error.text = string.Empty;
            RefreshOptions(true);
            return;
        }

        ControlledTaskInvocation invocation = BuildInvocation();
        ControlledTaskAvailability availability;
        try
        {
            availability = command.ValidateInvocation(actor, invocation);
        }
        catch (Exception exception)
        {
            ModClass.LogError($"[ControlledTaskPicker] submit validation failed command={command.id}: {exception}");
            availability = ControlledTaskAvailability.Unavailable(
                "Cultiway.ControlledTask.Reason.InternalError");
        }
        if (!availability.Enabled)
        {
            error.text = availability.ReasonLocaleKey.Localize();
            return;
        }

        visible = false;
        modal.Hide();
        confirmed?.Invoke(invocation);
    }

    private bool ValidateCurrentParameter()
    {
        ControlledTaskParameterDefinition definition = parameters[parameterIndex];
        int count = GetSelection(definition.Key).Count;
        if (count >= definition.MinSelected && count <= definition.MaxSelected)
        {
            error.text = string.Empty;
            return true;
        }

        error.text = definition.Required
            ? "Cultiway.ControlledTask.Reason.ParameterSelectionRequired".Localize()
            : "Cultiway.ControlledTask.Reason.ParameterSelectionInvalid".Localize();
        return false;
    }

    private void UpdateFooter()
    {
        previousButton.interactable = parameterIndex > 0;
        UiStateStyle.ApplyVisual(previousButton,
            parameterIndex > 0 ? UiControlState.Normal : UiControlState.Disabled);
        bool last = parameterIndex + 1 >= parameters.Count;
        nextButtonLabel.text = last
            ? "Cultiway.ControlledTask.UI.ConfirmExecute".Localize()
            : "Cultiway.ControlledTask.UI.NextParameter".Localize();
        nextButton.interactable = true;
        UiStateStyle.ApplyVisual(nextButton, UiControlState.Normal);
    }

    private void UpdateSummary()
    {
        List<string> values = new();
        foreach (ControlledTaskParameterDefinition definition in parameters)
        {
            List<string> selected = GetSelection(definition.Key);
            if (selected.Count == 0) continue;
            string text = definition.Layout == ControlledTaskParameterLayout.ItemGrid
                ? string.Format("Cultiway.ControlledTask.UI.SelectedCount".Localize(), selected.Count)
                : string.Join(", ", selected.Select(value =>
                    optionLabels.TryGetValue(value, out string label) ? label : value));
            values.Add($"{definition.NameLocaleKey.Localize()}: {text}");
        }
        string invocationSummary = string.Empty;
        try
        {
            invocationSummary = command?.GetInvocationSummary(actor, BuildInvocation()) ?? string.Empty;
        }
        catch (Exception exception)
        {
            ModClass.LogError($"[ControlledTaskPicker] summary query failed command={command?.id}: {exception}");
        }
        if (!string.IsNullOrEmpty(invocationSummary)) values.Add(invocationSummary);
        summary.text = values.Count == 0
            ? "Cultiway.ControlledTask.UI.ParameterNoneSelected".Localize()
            : string.Join(" · ", values);
    }

    private List<string> GetSelection(string key)
    {
        if (!selections.TryGetValue(key, out List<string> values))
        {
            values = new List<string>();
            selections[key] = values;
        }
        return values;
    }

    private ControlledTaskInvocation BuildInvocation()
    {
        Dictionary<string, IReadOnlyList<string>> values = new(StringComparer.Ordinal);
        foreach (ControlledTaskParameterDefinition definition in parameters)
            values[definition.Key] = GetSelection(definition.Key).ToArray();
        return new ControlledTaskInvocation(ControlledTaskTarget.None, values);
    }

    private void Cancel()
    {
        if (!visible) return;
        visible = false;
        modal.Hide();
        cancelled?.Invoke();
    }
}
