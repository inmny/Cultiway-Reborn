using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>在附体人物面板上方展示全部当前可选主动能力，并允许点击直选。</summary>
internal sealed class ControlledActiveAbilityBar : MonoBehaviour
{
    private const string RootName = "CultiwayControlledActiveAbilityBar";
    private const string HeaderFormatKey = "cultiway_control_ability_bar_format";
    private const string TooltipSummaryKey = "cultiway_control_ability_tooltip_summary";
    private const string TooltipDetailKey = "cultiway_control_ability_tooltip_detail";
    private const string UnknownAbilityKey = "cultiway_control_ability_unknown";
    private const int MaxColumns = 10;
    private const float MinBarWidth = 230f;
    private const float HeaderHeight = 14f;
    private const float CharacterPanelGap = 6f;
    private const float SafeHorizontalMargin = 12f;
    private const float FallbackBottom = 44f;

    private static ControlledActiveAbilityBar _instance;

    private readonly List<ActiveAbilityHandle> _abilities = new();
    private readonly Vector3[] _panelCorners = new Vector3[4];
    private readonly Vector3[] _hintsCorners = new Vector3[4];
    private PossessionUI _boundUi;
    private Actor _actor;
    private RectTransform _rootRect;
    private RectTransform _characterPanel;
    private RectTransform _actionHintsPanel;
    private RectTransform _gridRect;
    private RectTransform _headerRect;
    private GridLayoutGroup _gridLayout;
    private Text _header;
    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private MonoObjPool<ControlledActiveAbilitySlot> _slotPool;
    private int _layoutAbilityCount = -1;
    private int _layoutColumns = -1;
    private float _layoutParentWidth = -1f;
    private bool _visualsBuilt;
    private bool _visible;

    internal static void Ensure()
    {
        if (_instance != null) return;

        var root = new GameObject(RootName, typeof(RectTransform), typeof(CanvasGroup),
            typeof(ControlledActiveAbilityBar));
        Transform parent = CanvasMain.instance?.canvas_ui?.transform;
        if (parent != null) root.transform.SetParent(parent, false);
    }

    internal static bool ConsumesPointerInput()
    {
        if (_instance == null || !_instance._visible || _instance._rootRect == null) return false;
        Camera eventCamera = _instance._canvas != null &&
                             _instance._canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _instance._canvas.worldCamera
            : null;
        return RectTransformUtility.RectangleContainsScreenPoint(
            _instance._rootRect,
            Input.mousePosition,
            eventCamera);
    }

    private void Awake()
    {
        _instance = this;
        _rootRect = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
        ConfigureRootRect();
        SetVisible(false);
    }

    private void Update()
    {
        if (ControlledPossessionInputGate.BlocksPossessionActions || !EnsureBound() ||
            !ControlledCultivatorSkillControls.TryGetControlledActor(out Actor actor))
        {
            Hide();
            return;
        }

        int selectedIndex = ControlledCultivatorSkillControls.CollectSelectableAbilities(actor, _abilities);
        if (selectedIndex < 0)
        {
            Hide();
            return;
        }

        _actor = actor;
        ActorExtend caster = actor.GetExtend();
        ApplyLayout(_abilities.Count);
        PositionAboveCharacterPanel();

        _slotPool.ResetToStart();
        ActiveAbilityDescriptor selectedDescriptor = default;
        for (int i = 0; i < _abilities.Count; i++)
        {
            ActiveAbilityHandle handle = _abilities[i];
            ActiveAbilityDescriptor descriptor = ActiveAbilityService.Describe(caster, handle);
            ActiveAbilityControlState controlState = ActiveAbilityService.ResolveControlState(caster, handle);
            string shortcut = ResolveShortcutLabel(i);
            if (i == selectedIndex) selectedDescriptor = descriptor;
            ControlledActiveAbilitySlot slot = _slotPool.GetNext();
            slot.transform.SetSiblingIndex(i + 1);
            slot.Setup(
                handle,
                descriptor,
                controlState,
                shortcut,
                i == selectedIndex,
                BuildTooltipSummary(caster, handle, descriptor),
                BuildTooltipDetail(descriptor, controlState, shortcut));
        }
        _slotPool.ClearUnsed();

        string selectedName = ResolveAbilityName(selectedDescriptor);
        _header.text = string.Format(HeaderFormatKey.Localize(), selectedIndex + 1, _abilities.Count, selectedName);
        SetVisible(true);
    }

    private bool EnsureBound()
    {
        PossessionUI ui = PossessionUI.instance;
        if (ui == null) return false;
        if (_boundUi == ui && transform.parent != null && _characterPanel != null)
        {
            if (!_visualsBuilt) BuildVisuals();
            return true;
        }

        Transform parent = ui.transform.Find("Inner") ?? ui.transform.FindRecursive("Inner") ?? ui.transform;
        transform.SetParent(parent, false);
        transform.SetAsLastSibling();
        _boundUi = ui;
        _canvas = GetComponentInParent<Canvas>();
        _characterPanel = (parent.Find("Character Panel") ?? parent.FindRecursive("Character Panel"))
            ?.GetComponent<RectTransform>();
        _actionHintsPanel = (parent.Find("Right") ?? parent.FindRecursive("Right"))?.GetComponent<RectTransform>();
        ConfigureRootRect();
        if (!_visualsBuilt) BuildVisuals();
        _layoutAbilityCount = -1;
        return _characterPanel != null;
    }

    private void BuildVisuals()
    {
        GameObject headerObject = new("CurrentAbility", typeof(RectTransform), typeof(Text));
        headerObject.transform.SetParent(transform, false);
        _headerRect = headerObject.GetComponent<RectTransform>();
        _header = headerObject.GetComponent<Text>();
        _header.font = UiTheme.Current.Font;
        _header.fontSize = 7;
        _header.fontStyle = FontStyle.Bold;
        _header.alignment = TextAnchor.MiddleCenter;
        _header.color = UiTheme.Current.Palette.PrimaryText;
        _header.horizontalOverflow = HorizontalWrapMode.Wrap;
        _header.verticalOverflow = VerticalWrapMode.Truncate;
        _header.resizeTextForBestFit = true;
        _header.resizeTextMinSize = 5;
        _header.resizeTextMaxSize = 7;
        _header.raycastTarget = false;

        GameObject gridObject = new("Abilities", typeof(RectTransform), typeof(GridLayoutGroup));
        gridObject.transform.SetParent(transform, false);
        _gridRect = gridObject.GetComponent<RectTransform>();
        _gridLayout = gridObject.GetComponent<GridLayoutGroup>();
        _gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        _gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        _gridLayout.childAlignment = TextAnchor.UpperCenter;
        _gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;

        ControlledActiveAbilitySlot template = ControlledActiveAbilitySlot.CreateTemplate(_gridRect);
        template.gameObject.SetActive(false);
        _slotPool = new MonoObjPool<ControlledActiveAbilitySlot>(
            template,
            _gridRect,
            slot => slot.Initialize(SelectAbility),
            null,
            slot => slot.Clear());
        _visualsBuilt = true;
    }

    private void ApplyLayout(int abilityCount)
    {
        RectTransform parentRect = transform.parent as RectTransform;
        float parentWidth = parentRect == null ? MinBarWidth : Mathf.Max(1f, parentRect.rect.width);
        float slotSize = UiTheme.Current.Metrics.ControlLarge;
        float spacing = UiTheme.Current.Metrics.SpacingXs;
        float padding = UiTheme.Current.Metrics.SpacingSm;
        float availableWidth = Mathf.Max(slotSize + padding * 2f, parentWidth - SafeHorizontalMargin * 2f);
        int widthColumns = Mathf.Max(1,
            Mathf.FloorToInt((availableWidth - padding * 2f + spacing) / (slotSize + spacing)));
        int columns = Mathf.Clamp(Mathf.Min(abilityCount, MaxColumns), 1, widthColumns);
        if (_layoutAbilityCount == abilityCount && _layoutColumns == columns &&
            Mathf.Approximately(_layoutParentWidth, parentWidth)) return;

        _layoutAbilityCount = abilityCount;
        _layoutColumns = columns;
        _layoutParentWidth = parentWidth;

        int rows = Mathf.CeilToInt(abilityCount / (float)columns);
        float gridWidth = columns * slotSize + (columns - 1) * spacing;
        float gridHeight = rows * slotSize + (rows - 1) * spacing;
        float barWidth = Mathf.Min(availableWidth, Mathf.Max(MinBarWidth, gridWidth + padding * 2f));
        float barHeight = padding * 2f + gridHeight + UiTheme.Current.Metrics.SpacingXs + HeaderHeight;

        _rootRect.sizeDelta = new Vector2(barWidth, barHeight);
        _gridRect.anchorMin = _gridRect.anchorMax = new Vector2(0.5f, 0f);
        _gridRect.pivot = new Vector2(0.5f, 0f);
        _gridRect.anchoredPosition = new Vector2(0f, padding);
        _gridRect.sizeDelta = new Vector2(gridWidth, gridHeight);
        _gridLayout.cellSize = new Vector2(slotSize, slotSize);
        _gridLayout.spacing = new Vector2(spacing, spacing);
        _gridLayout.constraintCount = columns;

        _headerRect.anchorMin = new Vector2(0f, 0f);
        _headerRect.anchorMax = new Vector2(1f, 0f);
        _headerRect.pivot = new Vector2(0.5f, 0f);
        _headerRect.anchoredPosition = new Vector2(0f, padding + gridHeight + UiTheme.Current.Metrics.SpacingXs);
        _headerRect.sizeDelta = new Vector2(-padding * 2f, HeaderHeight);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_gridRect);
    }

    private void PositionAboveCharacterPanel()
    {
        RectTransform parentRect = transform.parent as RectTransform;
        float barBottom = FallbackBottom;
        if (parentRect == null)
        {
            _rootRect.anchoredPosition = new Vector2(0f, barBottom);
            return;
        }

        if (_characterPanel != null)
        {
            _characterPanel.GetWorldCorners(_panelCorners);
            Vector3 localTop = parentRect.InverseTransformPoint(_panelCorners[1]);
            barBottom = localTop.y - parentRect.rect.yMin + CharacterPanelGap;
        }

        if (_actionHintsPanel != null && _actionHintsPanel.gameObject.activeInHierarchy)
        {
            _actionHintsPanel.GetWorldCorners(_hintsCorners);
            Vector3 hintsBottomLeft = parentRect.InverseTransformPoint(_hintsCorners[0]);
            Vector3 hintsTopRight = parentRect.InverseTransformPoint(_hintsCorners[2]);
            float rootCenterX = Mathf.Lerp(parentRect.rect.xMin, parentRect.rect.xMax, 0.5f);
            float rootLeft = rootCenterX - _rootRect.rect.width * _rootRect.pivot.x;
            float rootRight = rootCenterX + _rootRect.rect.width * (1f - _rootRect.pivot.x);
            float rootBottom = parentRect.rect.yMin + barBottom;
            float rootTop = rootBottom + _rootRect.rect.height;
            bool overlapsHorizontally = rootLeft < hintsTopRight.x && rootRight > hintsBottomLeft.x;
            bool overlapsVertically = rootBottom < hintsTopRight.y && rootTop > hintsBottomLeft.y;
            if (overlapsHorizontally && overlapsVertically)
            {
                barBottom = hintsTopRight.y - parentRect.rect.yMin + CharacterPanelGap;
            }
        }

        _rootRect.anchoredPosition = new Vector2(0f, barBottom);
    }

    private void SelectAbility(ActiveAbilityHandle handle)
    {
        if (_actor == null || !ControlledCultivatorSkillControls.SelectAbility(_actor, handle)) return;
        PositionAboveCharacterPanel();
    }

    private static string BuildTooltipSummary(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        ActiveAbilityDescriptor descriptor)
    {
        string relation = ResolveTargetRelation(descriptor.TargetRelation);
        string targetMode = ResolveTargetMode(descriptor.TargetMode);
        float range = ActiveAbilityService.ResolveRange(caster, handle);
        string rangeText = descriptor.TargetRelation == SkillUseTargetRelation.Self
            ? "cultiway_control_ability_range_self".Localize()
            : range > 0f
                ? string.Format("cultiway_control_ability_range_format".Localize(), range)
                : "cultiway_control_ability_range_unlimited".Localize();
        return string.Format(TooltipSummaryKey.Localize(), relation, targetMode, rangeText);
    }

    private static string BuildTooltipDetail(
        ActiveAbilityDescriptor descriptor,
        ActiveAbilityControlState controlState,
        string shortcut)
    {
        string castHotkey = WorldboxGame.Hotkeys.GetHotkeyText(WorldboxGame.Hotkeys.CastControlledSkill, "R");
        string cycleHotkey = WorldboxGame.Hotkeys.GetHotkeyText(WorldboxGame.Hotkeys.CycleControlledSkill, "E");
        string detail = string.Format(TooltipDetailKey.Localize(), ResolveActivationMode(descriptor.ActivationMode),
            castHotkey, cycleHotkey);
        detail += "\n" + string.Format(
            "cultiway_control_ability_tooltip_status".Localize(),
            ResolveControlState(controlState));
        if (!string.IsNullOrEmpty(shortcut))
        {
            detail += " · " + string.Format(
                "cultiway_control_ability_tooltip_shortcut".Localize(),
                shortcut);
        }
        return detail;
    }

    private static string ResolveShortcutLabel(int index)
    {
        return index switch
        {
            >= 0 and < 9 => (index + 1).ToString(),
            9 => "0",
            _ => string.Empty,
        };
    }

    private static string ResolveControlState(ActiveAbilityControlState state)
    {
        if (state.IsActive) return "cultiway_control_ability_state_active".Localize();
        return state.BlockReason switch
        {
            ActiveAbilityControlBlockReason.Cooldown => string.Format(
                "cultiway_control_ability_state_cooldown".Localize(),
                Mathf.CeilToInt(state.CooldownRemaining)),
            ActiveAbilityControlBlockReason.InsufficientResource =>
                "cultiway_control_ability_state_resource".Localize(),
            ActiveAbilityControlBlockReason.Unavailable =>
                "cultiway_control_ability_state_unavailable".Localize(),
            _ => "cultiway_control_ability_state_ready".Localize(),
        };
    }

    private static string ResolveAbilityName(ActiveAbilityDescriptor descriptor)
    {
        return string.IsNullOrWhiteSpace(descriptor.Name) ? UnknownAbilityKey.Localize() : descriptor.Name;
    }

    private static string ResolveTargetRelation(SkillUseTargetRelation relation)
    {
        return relation switch
        {
            SkillUseTargetRelation.Friendly => "cultiway_control_ability_relation_friendly".Localize(),
            SkillUseTargetRelation.Self => "cultiway_control_ability_relation_self".Localize(),
            SkillUseTargetRelation.WorldTile => "cultiway_control_ability_relation_world".Localize(),
            _ => "cultiway_control_ability_relation_hostile".Localize(),
        };
    }

    private static string ResolveTargetMode(ActiveAbilityTargetMode mode)
    {
        return mode switch
        {
            ActiveAbilityTargetMode.Self => "cultiway_control_ability_target_self".Localize(),
            ActiveAbilityTargetMode.Object => "cultiway_control_ability_target_object".Localize(),
            ActiveAbilityTargetMode.Point => "cultiway_control_ability_target_point".Localize(),
            ActiveAbilityTargetMode.ObjectOrPoint => "cultiway_control_ability_target_object_or_point".Localize(),
            ActiveAbilityTargetMode.Area => "cultiway_control_ability_target_area".Localize(),
            _ => "cultiway_control_ability_target_none".Localize(),
        };
    }

    private static string ResolveActivationMode(ActiveAbilityActivationMode mode)
    {
        return mode switch
        {
            ActiveAbilityActivationMode.Sustained => "cultiway_control_ability_activation_sustained".Localize(),
            ActiveAbilityActivationMode.Toggle => "cultiway_control_ability_activation_toggle".Localize(),
            _ => "cultiway_control_ability_activation_instant".Localize(),
        };
    }

    private void ConfigureRootRect()
    {
        if (_rootRect == null) return;
        _rootRect.anchorMin = _rootRect.anchorMax = new Vector2(0.5f, 0f);
        _rootRect.pivot = new Vector2(0.5f, 0f);
        _rootRect.anchoredPosition = new Vector2(0f, FallbackBottom);
        _rootRect.sizeDelta = new Vector2(MinBarWidth, HeaderHeight + UiTheme.Current.Metrics.ControlLarge);
    }

    private void Hide()
    {
        _actor = null;
        _abilities.Clear();
        if (_visible) _slotPool?.Clear();
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        _visible = visible;
        if (_canvasGroup == null) return;
        _canvasGroup.alpha = visible ? 1f : 0f;
        _canvasGroup.interactable = visible;
        _canvasGroup.blocksRaycasts = visible;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}

/// <summary>能力带中的固定尺寸图标槽。</summary>
internal sealed class ControlledActiveAbilitySlot : MonoBehaviour
{
    private const float SelectedRotation = 15f;

    private ActiveAbilityHandle _handle;
    private Action<ActiveAbilityHandle> _select;
    private Button _button;
    private Image _icon;
    private Image _cooldownOverlay;
    private Text _cooldownText;
    private Image _activeMarker;
    private Text _resourceMarker;
    private Text _shortcutLabel;
    private Sprite _displayedIcon;
    private string _tooltipTitle;
    private string _tooltipSummary;
    private string _tooltipDetail;
    private bool _initialized;
    private bool _selected;

    internal static ControlledActiveAbilitySlot CreateTemplate(Transform parent)
    {
        GameObject root = new("AbilitySlotTemplate", typeof(RectTransform), typeof(Image), typeof(Button),
            typeof(TipButton), typeof(ControlledActiveAbilitySlot));
        root.transform.SetParent(parent, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        UiMetrics metrics = UiTheme.Current.Metrics;
        float size = metrics.ControlLarge;
        rootRect.sizeDelta = new Vector2(size, size);

        Image background = root.GetComponent<Image>();
        UiResources.ApplySurface(background, UiSurface.Button);
        Button button = root.GetComponent<Button>();
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        GameObject iconObject = new("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(root.transform, false);
        Image icon = iconObject.GetComponent<Image>();
        UiLayout.Stretch(icon.rectTransform, UiTheme.Current.Metrics.SpacingSm,
            UiTheme.Current.Metrics.SpacingSm, UiTheme.Current.Metrics.SpacingSm,
            UiTheme.Current.Metrics.SpacingSm);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        GameObject cooldownOverlayObject = new("CooldownOverlay", typeof(RectTransform), typeof(Image));
        cooldownOverlayObject.transform.SetParent(root.transform, false);
        Image cooldownOverlay = cooldownOverlayObject.GetComponent<Image>();
        UiLayout.Stretch(cooldownOverlay.rectTransform, metrics.SpacingXs, metrics.SpacingXs,
            metrics.SpacingXs, metrics.SpacingXs);
        cooldownOverlay.color = new Color(0f, 0f, 0f, 0.58f);
        cooldownOverlay.raycastTarget = false;
        cooldownOverlayObject.SetActive(false);

        Text cooldownText = CreateOverlayText(root.transform, "CooldownText", 9, TextAnchor.MiddleCenter);
        UiLayout.Stretch(cooldownText.rectTransform);
        cooldownText.gameObject.SetActive(false);

        GameObject activeMarkerObject = new("ActiveMarker", typeof(RectTransform), typeof(Image));
        activeMarkerObject.transform.SetParent(root.transform, false);
        Image activeMarker = activeMarkerObject.GetComponent<Image>();
        RectTransform activeMarkerRect = activeMarker.rectTransform;
        activeMarkerRect.anchorMin = activeMarkerRect.anchorMax = Vector2.one;
        activeMarkerRect.pivot = Vector2.one;
        activeMarkerRect.anchoredPosition = new Vector2(-1f, -1f);
        activeMarkerRect.sizeDelta = new Vector2(9f, 9f);
        UiResources.SetImage(activeMarker, UiIcons.ToggleOn);
        activeMarker.color = UiTheme.Current.Palette.Success;
        activeMarker.raycastTarget = false;
        activeMarkerObject.SetActive(false);

        Text resourceMarker = CreateOverlayText(root.transform, "ResourceMarker", 10, TextAnchor.MiddleCenter);
        RectTransform resourceMarkerRect = resourceMarker.rectTransform;
        resourceMarkerRect.anchorMin = resourceMarkerRect.anchorMax = Vector2.one;
        resourceMarkerRect.pivot = Vector2.one;
        resourceMarkerRect.anchoredPosition = new Vector2(-1f, -1f);
        resourceMarkerRect.sizeDelta = new Vector2(9f, 9f);
        resourceMarker.text = "!";
        resourceMarker.color = UiTheme.Current.Palette.Error;
        resourceMarker.gameObject.SetActive(false);

        Text shortcutLabel = CreateOverlayText(root.transform, "Shortcut", 7, TextAnchor.MiddleCenter);
        RectTransform shortcutRect = shortcutLabel.rectTransform;
        shortcutRect.anchorMin = shortcutRect.anchorMax = Vector2.zero;
        shortcutRect.pivot = Vector2.zero;
        shortcutRect.anchoredPosition = new Vector2(1f, 1f);
        shortcutRect.sizeDelta = new Vector2(9f, 9f);
        shortcutLabel.color = UiTheme.Current.Palette.AccentText;
        Outline shortcutOutline = shortcutLabel.gameObject.AddComponent<Outline>();
        shortcutOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shortcutOutline.effectDistance = new Vector2(0.6f, -0.6f);

        return root.GetComponent<ControlledActiveAbilitySlot>();
    }

    private static Text CreateOverlayText(
        Transform parent,
        string name,
        int fontSize,
        TextAnchor alignment)
    {
        GameObject textObject = new(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = UiTheme.Current.Font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = UiTheme.Current.Palette.PrimaryText;
        text.raycastTarget = false;
        return text;
    }

    internal void Initialize(Action<ActiveAbilityHandle> select)
    {
        _select = select;
        if (_initialized) return;
        _button = GetComponent<Button>();
        _icon = transform.Find("Icon").GetComponent<Image>();
        _cooldownOverlay = transform.Find("CooldownOverlay").GetComponent<Image>();
        _cooldownText = transform.Find("CooldownText").GetComponent<Text>();
        _activeMarker = transform.Find("ActiveMarker").GetComponent<Image>();
        _resourceMarker = transform.Find("ResourceMarker").GetComponent<Text>();
        _shortcutLabel = transform.Find("Shortcut").GetComponent<Text>();
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(Select);
        _initialized = true;
    }

    internal void Setup(
        ActiveAbilityHandle handle,
        ActiveAbilityDescriptor descriptor,
        ActiveAbilityControlState controlState,
        string shortcut,
        bool selected,
        string tooltipSummary,
        string tooltipDetail)
    {
        _handle = handle;
        Sprite icon = descriptor.Icon ?? LoadFallbackIcon();
        if (_displayedIcon != icon)
        {
            _displayedIcon = icon;
            _icon.sprite = icon;
            _icon.overrideSprite = icon;
        }

        bool cooldown = !controlState.IsActive &&
                        controlState.BlockReason == ActiveAbilityControlBlockReason.Cooldown &&
                        controlState.CooldownRemaining > 0f;
        bool resourceMissing = !controlState.IsActive &&
                               controlState.BlockReason == ActiveAbilityControlBlockReason.InsufficientResource;
        bool unavailable = !controlState.IsActive &&
                           controlState.BlockReason == ActiveAbilityControlBlockReason.Unavailable;
        _icon.color = resourceMissing || unavailable
            ? UiTheme.Current.Palette.Disabled
            : UiTheme.Current.Palette.PrimaryText;
        _cooldownOverlay.gameObject.SetActive(cooldown);
        _cooldownText.gameObject.SetActive(cooldown);
        if (cooldown) _cooldownText.text = Mathf.CeilToInt(controlState.CooldownRemaining).ToString();
        _activeMarker.gameObject.SetActive(controlState.IsActive);
        _resourceMarker.gameObject.SetActive(resourceMissing);
        _shortcutLabel.text = shortcut;
        _shortcutLabel.gameObject.SetActive(!string.IsNullOrEmpty(shortcut));

        string title = string.IsNullOrWhiteSpace(descriptor.Name)
            ? "cultiway_control_ability_unknown".Localize()
            : descriptor.Name;
        if (_tooltipTitle != title || _tooltipSummary != tooltipSummary || _tooltipDetail != tooltipDetail)
        {
            _tooltipTitle = title;
            _tooltipSummary = tooltipSummary;
            _tooltipDetail = tooltipDetail;
            UiTooltip.Set(gameObject, title, tooltipSummary, tooltipDetail);
        }

        if (_selected == selected) return;
        _selected = selected;
        transform.localRotation = Quaternion.Euler(0f, 0f, selected ? SelectedRotation : 0f);
    }

    internal void Clear()
    {
        _handle = default;
        if (!_initialized) return;
        _cooldownOverlay.gameObject.SetActive(false);
        _cooldownText.gameObject.SetActive(false);
        _activeMarker.gameObject.SetActive(false);
        _resourceMarker.gameObject.SetActive(false);
        _shortcutLabel.gameObject.SetActive(false);
        if (!_selected) return;
        _selected = false;
        transform.localRotation = Quaternion.identity;
    }

    private void Select()
    {
        _select?.Invoke(_handle);
    }

    private static Sprite LoadFallbackIcon()
    {
        return SpriteTextureLoader.getSprite("ui/icons/iconDamage")
               ?? SpriteTextureLoader.getSprite("ui/icons/iconMana");
    }
}
