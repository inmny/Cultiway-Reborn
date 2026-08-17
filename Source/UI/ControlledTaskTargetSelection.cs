using Cultiway.Const;
using Cultiway.Core.ControlledTasks;
using strings;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>为世界地块命令提供独立预览、同步提交校验和取消恢复。</summary>
internal sealed class ControlledTaskTargetSelection : MonoBehaviour
{
    private const string SelectionEffectPath = "effects/PrefabUnitSelectionEffect";
    private const string SelectionFramesPath = "effects/unit_selected_effect";

    private static ControlledTaskCommandLibrary CommandLibrary => ModClass.L.ControlledTaskCommandLibrary;
    private static ControlledTaskTargetSelection instance;

    private RectTransform statusRect;
    private Text statusText;
    private GameObject marker;
    private SpriteRenderer markerRenderer;
    private SpriteAnimation markerAnimation;
    private long actorId;
    private string commandId;
    private WorldTile hoveredTile;
    private ControlledTaskAvailability hoveredAvailability;
    private bool active;

    internal static bool IsActive => instance != null && instance.active;

    internal static void Ensure()
    {
        if (instance != null) return;
        var root = new GameObject("CultiwayControlledTaskTargetSelection", typeof(RectTransform),
            typeof(ControlledTaskTargetSelection));
        Transform parent = CanvasMain.instance?.canvas_ui?.transform;
        if (parent != null) root.transform.SetParent(parent, false);
    }

    internal static void Begin(long targetActorId, string targetCommandId)
    {
        Ensure();
        if (instance == null) return;
        instance.StartSelection(targetActorId, targetCommandId);
    }

    internal static void CancelToPalette()
    {
        if (instance == null || !instance.active) return;
        string returnCommandId = instance.commandId;
        instance.StopSelection();
        ControlledTaskCommandPalette.ReturnFromTargetSelection(returnCommandId);
    }

    internal static void ClearWorldState()
    {
        if (instance == null) return;
        instance.StopSelection();
    }

    internal static bool ConsumesPointerInput()
    {
        if (instance == null || !instance.active || instance.statusRect == null) return false;
        Canvas canvas = instance.statusRect.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        return RectTransformUtility.RectangleContainsScreenPoint(
            instance.statusRect, Input.mousePosition, eventCamera);
    }

    private void Awake()
    {
        instance = this;
        RectTransform rect = GetComponent<RectTransform>();
        UiLayout.Stretch(rect);
        BuildStatus();
    }

    private void BuildStatus()
    {
        var status = new GameObject("TargetStatus", typeof(RectTransform), typeof(Image));
        status.transform.SetParent(transform, false);
        statusRect = status.GetComponent<RectTransform>();
        statusRect.anchorMin = statusRect.anchorMax = new Vector2(0.5f, 1f);
        statusRect.pivot = new Vector2(0.5f, 1f);
        statusRect.anchoredPosition = new Vector2(0f, -8f);
        statusRect.sizeDelta = new Vector2(260f, 30f);
        UiResources.ApplySurface(status.GetComponent<Image>(), UiSurface.WindowInner,
            UiTheme.Current.Palette.InnerPanelTint);

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(status.transform, false);
        RectTransform iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(6f, 0f);
        iconRect.sizeDelta = new Vector2(18f, 18f);
        UiResources.SetImage(icon.GetComponent<Image>(), "ui/icons/iconArrowDestination");
        icon.GetComponent<Image>().raycastTarget = false;

        statusText = UiElements.CreateText(status.transform, "Text", string.Empty, 200f, 28f, 7,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        statusText.rectTransform.anchorMin = Vector2.zero;
        statusText.rectTransform.anchorMax = Vector2.one;
        statusText.rectTransform.offsetMin = new Vector2(29f, 1f);
        statusText.rectTransform.offsetMax = new Vector2(-31f, -1f);
        statusText.GetComponent<LayoutElement>().ignoreLayout = true;
        statusText.raycastTarget = false;
        statusText.resizeTextForBestFit = true;
        statusText.resizeTextMinSize = 5;
        statusText.resizeTextMaxSize = 7;

        Button cancel = UiElements.CreateIconButton(status.transform, "Cancel", UiIcons.Cancel, 24f, 22f,
            CancelToPalette, 4f);
        RectTransform cancelRect = cancel.GetComponent<RectTransform>();
        cancelRect.anchorMin = cancelRect.anchorMax = new Vector2(1f, 0.5f);
        cancelRect.pivot = new Vector2(1f, 0.5f);
        cancelRect.anchoredPosition = new Vector2(-4f, 0f);
        status.SetActive(false);
    }

    private void StartSelection(long targetActorId, string targetCommandId)
    {
        if (!CommandLibrary.TryGet(targetCommandId, out ControlledTaskCommandAsset command) ||
            command.TargetMode != ControlledTaskTargetMode.WorldTile)
        {
            ControlledTaskCommandPalette.ReturnFromTargetSelection(
                targetCommandId, "Cultiway.ControlledTask.Reason.CommandMissing");
            return;
        }
        actorId = targetActorId;
        commandId = targetCommandId;
        active = true;
        statusRect.gameObject.SetActive(true);
        EnsureMarker();
        if (marker != null) marker.SetActive(true);
    }

    private void Update()
    {
        if (!active) return;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            CancelToPalette();
            return;
        }

        if (!TryResolveContext(out Actor actor, out ControlledTaskCommandAsset command))
        {
            StopSelection();
            ControlledTaskCommandPalette.CompleteHandoff();
            return;
        }

        hoveredTile = ResolveMouseTile();
        try
        {
            ControlledTaskAvailability availability = command.Evaluate(actor);
            hoveredAvailability = availability.Enabled
                ? command.ValidateTarget(actor, hoveredTile)
                : availability;
        }
        catch (System.Exception exception)
        {
            ModClass.LogError($"[ControlledTaskTarget] validation failed command={command.id}: {exception}");
            hoveredAvailability = ControlledTaskAvailability.Unavailable(
                "Cultiway.ControlledTask.Reason.InternalError");
        }
        UpdatePreview(command);

        if (!World.world.isOverUI() && Input.GetMouseButtonDown(0) && hoveredAvailability.Enabled &&
            hoveredTile != null)
            Submit(command);
    }

    private void UpdatePreview(ControlledTaskCommandAsset command)
    {
        string commandName = command.NameLocaleKey.Localize();
        if (hoveredAvailability.Enabled)
        {
            statusText.text = string.Format("Cultiway.ControlledTask.UI.TargetValid".Localize(), commandName);
            statusText.color = UiTheme.Current.Palette.Success;
        }
        else
        {
            string reason = string.IsNullOrEmpty(hoveredAvailability.ReasonLocaleKey)
                ? "Cultiway.ControlledTask.Reason.TargetMissing".Localize()
                : hoveredAvailability.ReasonLocaleKey.Localize();
            statusText.text = string.Format("Cultiway.ControlledTask.UI.TargetInvalid".Localize(), reason);
            statusText.color = UiTheme.Current.Palette.Error;
        }

        EnsureMarker();
        if (marker == null) return;
        if (hoveredTile == null)
        {
            marker.SetActive(false);
            return;
        }
        marker.SetActive(true);
        marker.transform.position = hoveredTile.posV3;
        marker.transform.localScale = Vector3.one * 0.1f;
        if (markerRenderer != null)
            markerRenderer.color = hoveredAvailability.Enabled
                ? UiTheme.Current.Palette.Success
                : UiTheme.Current.Palette.Error;
        markerAnimation?.update(Time.unscaledDeltaTime);
    }

    private void Submit(ControlledTaskCommandAsset command)
    {
        ControlledTaskTarget target = ControlledTaskTarget.ForTile(hoveredTile);
        ControlledTaskStartResult result = ControlledTaskOrderService.TryBegin(actorId, command.id, target);
        string returnCommandId = commandId;
        StopSelection();
        if (result.Success)
            ControlledTaskCommandPalette.CompleteHandoff();
        else
            ControlledTaskCommandPalette.ReturnFromTargetSelection(returnCommandId, result.ReasonLocaleKey);
    }

    private bool TryResolveContext(out Actor actor, out ControlledTaskCommandAsset command)
    {
        actor = actorId > 0 && World.world?.units != null ? World.world.units.get(actorId) : null;
        if (!CommandLibrary.TryGet(commandId, out command) || actor == null || actor.isRekt())
            return false;
        return ControllableUnit.isControllingUnit() && ControllableUnit.count() == 1 &&
               ReferenceEquals(ControllableUnit.getControllableUnit(), actor) &&
               ControllableUnit.isControllingUnit(actor);
    }

    private static WorldTile ResolveMouseTile()
    {
        if (World.world == null) return null;
        Vector3 position = World.world.getMousePos();
        int x = Mathf.FloorToInt(position.x);
        int y = Mathf.FloorToInt(position.y);
        return x >= 0 && y >= 0 && x < MapBox.width && y < MapBox.height
            ? World.world.GetTileSimple(x, y)
            : null;
    }

    private void EnsureMarker()
    {
        if (marker != null) return;
        GameObject original = Resources.Load<GameObject>(SelectionEffectPath);
        marker = original != null
            ? Instantiate(original)
            : new GameObject("CultiwayControlledTaskTargetMarker", typeof(SpriteRenderer), typeof(SpriteAnimation));
        marker.name = "CultiwayControlledTaskTargetMarker";
        if (World.world != null) marker.transform.SetParent(World.world.transform, true);
        if (marker.TryGetComponent<UnitSelectionEffect>(out UnitSelectionEffect selectionEffect))
            selectionEffect.enabled = false;
        markerRenderer = marker.GetComponent<SpriteRenderer>() ?? marker.AddComponent<SpriteRenderer>();
        markerRenderer.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        markerRenderer.sortingOrder = 55;
        if (LibraryMaterials.instance != null) markerRenderer.sharedMaterial = LibraryMaterials.instance.mat_world_object;
        markerAnimation = marker.GetComponent<SpriteAnimation>() ?? marker.AddComponent<SpriteAnimation>();
        if (markerAnimation.frames == null || markerAnimation.frames.Length == 0)
            markerAnimation.frames = SpriteTextureLoader.getSpriteList(SelectionFramesPath, true) ?? new Sprite[0];
        markerAnimation.create();
        markerAnimation.resetAnim();
    }

    private void StopSelection()
    {
        active = false;
        actorId = 0;
        commandId = null;
        hoveredTile = null;
        if (statusRect != null) statusRect.gameObject.SetActive(false);
        if (marker != null)
        {
            marker.SetActive(false);
            marker.transform.position = Globals.POINT_IN_VOID;
        }
    }

    private void OnDestroy()
    {
        if (marker != null) Destroy(marker);
        if (ReferenceEquals(instance, this)) instance = null;
    }
}
