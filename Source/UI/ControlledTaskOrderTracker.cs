using System.Collections.Generic;
using Cultiway.Core.ControlledTasks;
using strings;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>在附体结束后持续展示玩家任务订单，并提供定位和安全取消。</summary>
internal sealed class ControlledTaskOrderTracker : MonoBehaviour
{
    private static ControlledTaskOrderTracker instance;

    private readonly List<ControlledTaskOrderView> orders = new();
    private RectTransform panelRect;
    private Image commandIcon;
    private Text summaryText;
    private Text counterText;
    private Button previousButton;
    private Button nextButton;
    private Button locateButton;
    private Button cancelButton;
    private long selectedOrderId;
    private float nextRefreshAt;

    internal static void Ensure()
    {
        if (instance != null) return;
        var root = new GameObject("CultiwayControlledTaskOrderTracker", typeof(RectTransform),
            typeof(ControlledTaskOrderTracker));
        Transform parent = CanvasMain.instance?.canvas_ui?.transform;
        if (parent != null) root.transform.SetParent(parent, false);
    }

    internal static bool ConsumesPointerInput()
    {
        if (instance == null || instance.panelRect == null || !instance.panelRect.gameObject.activeInHierarchy)
            return false;
        return ContainsPointer(instance.commandIcon?.rectTransform) || ContainsPointer(instance.previousButton) ||
               ContainsPointer(instance.nextButton) || ContainsPointer(instance.locateButton) ||
               ContainsPointer(instance.cancelButton);
    }

    private static bool ContainsPointer(Button button)
    {
        return button != null && button.gameObject.activeInHierarchy &&
               ContainsPointer(button.GetComponent<RectTransform>());
    }

    private static bool ContainsPointer(RectTransform rect)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy) return false;
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, eventCamera);
    }

    private void Awake()
    {
        instance = this;
        RectTransform root = GetComponent<RectTransform>();
        UiLayout.Stretch(root);
        BuildVisuals();
    }

    private void BuildVisuals()
    {
        var panel = new GameObject("OrderTracker", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(transform, false);
        panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -8f);
        panelRect.sizeDelta = new Vector2(344f, 34f);
        UiResources.ApplySurface(panel.GetComponent<Image>(), UiSurface.WindowInner,
            UiTheme.Current.Palette.InnerPanelTint);
        panel.GetComponent<Image>().raycastTarget = false;

        var icon = new GameObject("CommandIcon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(panel.transform, false);
        RectTransform iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(6f, 0f);
        iconRect.sizeDelta = new Vector2(20f, 20f);
        commandIcon = icon.GetComponent<Image>();
        commandIcon.raycastTarget = false;

        summaryText = UiElements.CreateText(panel.transform, "Summary", string.Empty, 180f, 30f, 7,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        RectTransform summaryRect = summaryText.rectTransform;
        summaryRect.anchorMin = summaryRect.anchorMax = new Vector2(0f, 0.5f);
        summaryRect.pivot = new Vector2(0f, 0.5f);
        summaryRect.anchoredPosition = new Vector2(31f, 0f);
        summaryText.resizeTextForBestFit = true;
        summaryText.resizeTextMinSize = 5;
        summaryText.resizeTextMaxSize = 7;
        summaryText.raycastTarget = false;

        counterText = UiElements.CreateText(panel.transform, "Counter", string.Empty, 27f, 30f, 6,
            TextAnchor.MiddleCenter);
        RectTransform counterRect = counterText.rectTransform;
        counterRect.anchorMin = counterRect.anchorMax = new Vector2(0f, 0.5f);
        counterRect.pivot = new Vector2(0f, 0.5f);
        counterRect.anchoredPosition = new Vector2(211f, 0f);
        counterText.color = UiTheme.Current.Palette.MutedText;
        counterText.raycastTarget = false;

        previousButton = CreateTrackerButton(panel.transform, "Previous", UiIcons.Previous, 239f, ShowPrevious);
        nextButton = CreateTrackerButton(panel.transform, "Next", UiIcons.Next, 263f, ShowNext);
        locateButton = CreateTrackerButton(panel.transform, "Locate", "ui/icons/iconArrowDestination", 287f,
            LocateSelected);
        cancelButton = CreateTrackerButton(panel.transform, "Cancel", UiIcons.Cancel, 315f, CancelSelected);
        panel.SetActive(false);
    }

    private static Button CreateTrackerButton(Transform parent, string name, string iconPath, float x,
        UnityEngine.Events.UnityAction action)
    {
        Button button = UiElements.CreateIconButton(parent, name, iconPath, 24f, 24f, action, 4f);
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        return button;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshAt) return;
        nextRefreshAt = Time.unscaledTime + 0.15f;
        Refresh();
    }

    private void Refresh()
    {
        ControlledTaskOrderService.CopyVisibleOrders(orders);
        if (orders.Count == 0 || ControlledPossessionInputGate.BlocksPossessionActions)
        {
            if (orders.Count == 0) selectedOrderId = 0;
            panelRect.gameObject.SetActive(false);
            return;
        }

        int index = FindSelectedIndex();
        if (index < 0)
        {
            index = 0;
            selectedOrderId = orders[0].OrderId;
        }
        ControlledTaskOrderView order = orders[index];
        panelRect.gameObject.SetActive(true);
        UiResources.SetImage(commandIcon, order.IconPath);
        string commandName = order.CommandNameLocaleKey.Localize();
        string state = GetStateLocaleKey(order.State).Localize();
        summaryText.text = string.Format("Cultiway.ControlledTask.UI.TrackerSummary".Localize(),
            order.ActorName, commandName, state);
        summaryText.color = GetStateColor(order.State);
        counterText.text = $"{index + 1}/{orders.Count}";

        bool multiple = orders.Count > 1;
        previousButton.gameObject.SetActive(multiple);
        nextButton.gameObject.SetActive(multiple);
        locateButton.interactable = order.CanLocate;
        cancelButton.interactable = order.CanCancel;
        UiStateStyle.ApplyVisual(locateButton, order.CanLocate ? UiControlState.Normal : UiControlState.Disabled);
        UiStateStyle.ApplyVisual(cancelButton,
            order.CanCancel ? UiControlState.Destructive : UiControlState.Disabled);
        RefreshControlTooltips();
        string detail = string.IsNullOrEmpty(order.ReasonLocaleKey)
            ? state
            : order.ReasonLocaleKey.Localize();
        UiTooltip.Set(commandIcon.gameObject, commandName, detail);
    }

    private void RefreshControlTooltips()
    {
        UiTooltip.Set(previousButton.gameObject, "Cultiway.ControlledTask.UI.Previous".Localize(), string.Empty);
        UiTooltip.Set(nextButton.gameObject, "Cultiway.ControlledTask.UI.Next".Localize(), string.Empty);
        UiTooltip.Set(locateButton.gameObject, "Cultiway.ControlledTask.UI.Locate".Localize(), string.Empty);
        UiTooltip.Set(cancelButton.gameObject, "Cultiway.ControlledTask.UI.Cancel".Localize(),
            "Cultiway.ControlledTask.UI.CancelDescription".Localize());
    }

    private int FindSelectedIndex()
    {
        for (var i = 0; i < orders.Count; i++)
            if (orders[i].OrderId == selectedOrderId) return i;
        return -1;
    }

    private void ShowPrevious()
    {
        if (orders.Count < 2) return;
        int index = FindSelectedIndex();
        index = (index <= 0 ? orders.Count : index) - 1;
        selectedOrderId = orders[index].OrderId;
        Refresh();
    }

    private void ShowNext()
    {
        if (orders.Count < 2) return;
        int index = FindSelectedIndex();
        index = (index + 1) % orders.Count;
        selectedOrderId = orders[index].OrderId;
        Refresh();
    }

    private void LocateSelected()
    {
        if (!TryGetSelected(out ControlledTaskOrderView order)) return;
        Actor actor = ControlledTaskOrderService.ResolveOrderActor(order.ActorId);
        if (actor != null && !actor.isRekt()) World.world.locatePosition(actor.current_position);
    }

    private void CancelSelected()
    {
        if (!TryGetSelected(out ControlledTaskOrderView order) || !order.CanCancel) return;
        ControlledTaskOrderService.TryCancel(order.OrderId);
        Refresh();
    }

    private bool TryGetSelected(out ControlledTaskOrderView order)
    {
        int index = FindSelectedIndex();
        if (index >= 0)
        {
            order = orders[index];
            return true;
        }
        order = default;
        return false;
    }

    private static string GetStateLocaleKey(ControlledTaskOrderState state)
    {
        return $"Cultiway.ControlledTask.State.{state}";
    }

    private static Color GetStateColor(ControlledTaskOrderState state)
    {
        return state switch
        {
            ControlledTaskOrderState.Running => UiTheme.Current.Palette.PrimaryText,
            ControlledTaskOrderState.Completed => UiTheme.Current.Palette.Success,
            ControlledTaskOrderState.Cancelled => UiTheme.Current.Palette.MutedText,
            _ => UiTheme.Current.Palette.Warning
        };
    }

    private void OnDestroy()
    {
        if (ReferenceEquals(instance, this)) instance = null;
    }
}
