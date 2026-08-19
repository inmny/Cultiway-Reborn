using strings;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>附体操控期间显示在右上角的人物信息窗口入口。</summary>
internal sealed class ControlledPossessionInfoButtons : MonoBehaviour
{
    private const string RootName = "CultiwayControlledPossessionInfoButtons";
    private const string CultiwayInfoIcon = "cultiway/icons/iconCultivation";
    private const string OriginalTitleKey = "Cultiway.ControlledPossession.Info.Original";
    private const string OriginalDescriptionKey = "Cultiway.ControlledPossession.Info.Original Description";
    private const string CultiwayTitleKey = "Cultiway.ControlledPossession.Info.Cultiway";
    private const string CultiwayDescriptionKey = "Cultiway.ControlledPossession.Info.Cultiway Description";

    private static ControlledPossessionInfoButtons instance;

    private readonly Vector3[] cancelButtonCorners = new Vector3[4];
    private RectTransform rootRect;
    private RectTransform cancelButtonRect;
    private CanvasGroup canvasGroup;
    private Canvas canvas;
    private PossessionUI boundUi;
    private bool visible;

    internal static void Ensure()
    {
        if (instance != null) return;

        GameObject root = new(RootName, typeof(RectTransform), typeof(HorizontalLayoutGroup),
            typeof(CanvasGroup), typeof(ControlledPossessionInfoButtons));
        Transform parent = GetHudParent();
        if (parent != null) root.transform.SetParent(parent, false);
    }

    internal static bool ConsumesPointerInput()
    {
        if (instance == null || !instance.visible || instance.rootRect == null) return false;

        Camera eventCamera = instance.canvas != null && instance.canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? instance.canvas.worldCamera
            : null;
        return RectTransformUtility.RectangleContainsScreenPoint(
            instance.rootRect,
            Input.mousePosition,
            eventCamera);
    }

    private void Awake()
    {
        instance = this;
        rootRect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        BuildVisuals();
        ConfigureRootRect();
        SetVisible(false);
    }

    private void Update()
    {
        if (ScrollWindow.isWindowActive() || ControlledPossessionInputGate.BlocksPossessionActions ||
            !EnsureBound() || !cancelButtonRect.gameObject.activeInHierarchy || !TryGetControlledActor(out _))
        {
            SetVisible(false);
            return;
        }

        PositionBesideCancelButton();
        SetVisible(true);
    }

    private void BuildVisuals()
    {
        UiMetrics metrics = UiTheme.Current.Metrics;
        HorizontalLayoutGroup layout = GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperRight;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = metrics.SpacingSm;

        Button original = UiElements.CreateIconButton(transform, "OriginalInfo", UiIcons.Info,
            metrics.ControlLarge, metrics.ControlLarge, OpenOriginalInfo, metrics.SpacingXs);
        original.navigation = new Navigation { mode = Navigation.Mode.None };
        UiTooltip.Set(original.gameObject, OriginalTitleKey.Localize(), OriginalDescriptionKey.Localize());

        Button cultiway = UiElements.CreateIconButton(transform, "CultiwayInfo", CultiwayInfoIcon,
            metrics.ControlLarge, metrics.ControlLarge, OpenCultiwayInfo, metrics.SpacingXs);
        cultiway.navigation = new Navigation { mode = Navigation.Mode.None };
        UiTooltip.Set(cultiway.gameObject, CultiwayTitleKey.Localize(), CultiwayDescriptionKey.Localize());
    }

    private bool EnsureBound()
    {
        Transform parent = GetHudParent();
        PossessionUI ui = PossessionUI.instance;
        GameObject cancelButton = PowerButtonSelector.instance?.joy_control_cancel_button;
        RectTransform cancelRect = cancelButton == null ? null : cancelButton.GetComponent<RectTransform>();
        if (ui == null || parent == null || cancelRect == null) return false;
        if (boundUi == ui && transform.parent == parent && cancelButtonRect == cancelRect) return true;

        boundUi = ui;
        cancelButtonRect = cancelRect;
        transform.SetParent(parent, false);
        ConfigureRootRect();
        transform.SetAsLastSibling();
        canvas = GetComponentInParent<Canvas>();
        return true;
    }

    private void ConfigureRootRect()
    {
        if (rootRect == null) return;

        UiMetrics metrics = UiTheme.Current.Metrics;
        float buttonSize = metrics.ControlLarge;
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = Vector2.one;
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = new Vector2(buttonSize * 2f + metrics.SpacingSm, buttonSize);
        rootRect.localScale = Vector3.one;
    }

    private void PositionBesideCancelButton()
    {
        var parentRect = rootRect.parent as RectTransform;
        if (parentRect == null || cancelButtonRect == null) return;

        cancelButtonRect.GetWorldCorners(cancelButtonCorners);
        Vector3 topLeft = parentRect.InverseTransformPoint(cancelButtonCorners[1]);
        topLeft.x -= UiTheme.Current.Metrics.SpacingSm;
        rootRect.position = parentRect.TransformPoint(topLeft);
    }

    private static Transform GetHudParent()
    {
        return CanvasMain.instance?.canvas_ui?.transform.Find("CanvasParent");
    }

    private static void OpenOriginalInfo()
    {
        if (!TryGetControlledActor(out Actor actor)) return;
        ActionLibrary.openUnitWindow(actor);
    }

    private static void OpenCultiwayInfo()
    {
        if (!TryGetControlledActor(out Actor actor)) return;

        SelectedUnit.clear();
        SelectedUnit.select(actor);
        WindowNewCreatureInfo.Show();
    }

    private static bool TryGetControlledActor(out Actor actor)
    {
        actor = null;
        if (!ControllableUnit.isControllingUnit()) return false;

        actor = ControllableUnit.getControllableUnit();
        return actor != null && !actor.isRekt();
    }

    private void SetVisible(bool state)
    {
        visible = state;
        canvasGroup.alpha = state ? 1f : 0f;
        canvasGroup.interactable = state;
        canvasGroup.blocksRaycasts = state;
    }

    private void OnDestroy()
    {
        if (ReferenceEquals(instance, this)) instance = null;
    }
}
