using System;
using System.Collections.Generic;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI;

public static class CharacterPanelExtensions
{
    private const string HostName = "CultiwayCharacterPanelExtensionsHost";
    private static readonly List<CharacterPanelRegistration> Registrations = new();
    private static CharacterPanelExtensionHost _host;

    internal static IReadOnlyList<CharacterPanelRegistration> RegisteredEntries => Registrations;

    public static void RegisterIconValue(string id, Func<Actor, bool> visible,
        Func<Actor, CharacterPanelIconValueState> read)
    {
        Register(CharacterPanelRegistration.IconValue(id, visible, read));
    }

    public static void RegisterIconValue(string id, Func<Actor, CharacterPanelIconValueState> read)
    {
        RegisterIconValue(id, null, read);
    }

    public static void RegisterProgressBar(string id, Func<Actor, bool> visible,
        Func<Actor, CharacterPanelProgressBarState> read)
    {
        Register(CharacterPanelRegistration.ProgressBar(id, visible, read));
    }

    public static void RegisterProgressBar(string id, Func<Actor, CharacterPanelProgressBarState> read)
    {
        RegisterProgressBar(id, null, read);
    }

    private static void Register(CharacterPanelRegistration registration)
    {
        var index = Registrations.FindIndex(x => x.Id == registration.Id);
        if (index >= 0)
        {
            Registrations[index] = registration;
        }
        else
        {
            Registrations.Add(registration);
        }

        EnsureHost();
    }

    private static void EnsureHost()
    {
        if (_host != null) return;

        foreach (var host in Resources.FindObjectsOfTypeAll<CharacterPanelExtensionHost>())
        {
            if (host == null) continue;
            _host = host;
            return;
        }

        var obj = new GameObject(HostName, typeof(CharacterPanelExtensionHost));
        Object.DontDestroyOnLoad(obj);
        _host = obj.GetComponent<CharacterPanelExtensionHost>();
    }
}

public readonly struct CharacterPanelIconValueState
{
    public readonly string Text;
    public readonly string IconPath;
    public readonly string TooltipTitle;
    public readonly string TooltipDescription;
    public readonly string TooltipDetail;
    public readonly Color? TextColor;

    public CharacterPanelIconValueState(string text, string iconPath, string tooltipTitle,
        string tooltipDescription = "", string tooltipDetail = "", Color? textColor = null)
    {
        Text = text ?? string.Empty;
        IconPath = iconPath ?? string.Empty;
        TooltipTitle = tooltipTitle ?? string.Empty;
        TooltipDescription = tooltipDescription ?? string.Empty;
        TooltipDetail = tooltipDetail ?? string.Empty;
        TextColor = textColor;
    }
}

public readonly struct CharacterPanelProgressBarState
{
    public readonly float Value;
    public readonly float Max;
    public readonly string IconPath;
    public readonly string TooltipTitle;
    public readonly string TooltipDescription;
    public readonly string TooltipDetail;
    public readonly Color? FillColor;

    public CharacterPanelProgressBarState(float value, float max, string iconPath, string tooltipTitle,
        string tooltipDescription = "", string tooltipDetail = "", Color? fillColor = null)
    {
        Value = value;
        Max = max;
        IconPath = iconPath ?? string.Empty;
        TooltipTitle = tooltipTitle ?? string.Empty;
        TooltipDescription = tooltipDescription ?? string.Empty;
        TooltipDetail = tooltipDetail ?? string.Empty;
        FillColor = fillColor;
    }
}

internal readonly struct CharacterPanelRegistration
{
    private readonly Func<Actor, bool> _visible;
    private readonly Func<Actor, CharacterPanelIconValueState> _readIconValue;
    private readonly Func<Actor, CharacterPanelProgressBarState> _readProgressBar;

    public string Id { get; }
    public CharacterPanelEntryKind Kind { get; }

    private CharacterPanelRegistration(string id, CharacterPanelEntryKind kind, Func<Actor, bool> visible,
        Func<Actor, CharacterPanelIconValueState> readIconValue,
        Func<Actor, CharacterPanelProgressBarState> readProgressBar)
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentException("Character panel entry id is empty", nameof(id));

        Id = id;
        Kind = kind;
        _visible = visible;
        _readIconValue = readIconValue;
        _readProgressBar = readProgressBar;
    }

    public static CharacterPanelRegistration IconValue(string id, Func<Actor, bool> visible,
        Func<Actor, CharacterPanelIconValueState> read)
    {
        if (read == null) throw new ArgumentNullException(nameof(read));
        return new CharacterPanelRegistration(id, CharacterPanelEntryKind.IconValue, visible, read, null);
    }

    public static CharacterPanelRegistration ProgressBar(string id, Func<Actor, bool> visible,
        Func<Actor, CharacterPanelProgressBarState> read)
    {
        if (read == null) throw new ArgumentNullException(nameof(read));
        return new CharacterPanelRegistration(id, CharacterPanelEntryKind.ProgressBar, visible, null, read);
    }

    public bool IsVisible(Actor actor)
    {
        return _visible?.Invoke(actor) ?? true;
    }

    public CharacterPanelIconValueState ReadIconValue(Actor actor)
    {
        return _readIconValue(actor);
    }

    public CharacterPanelProgressBarState ReadProgressBar(Actor actor)
    {
        return _readProgressBar(actor);
    }
}

internal enum CharacterPanelEntryKind
{
    IconValue,
    ProgressBar
}

internal sealed class CharacterPanelExtensionHost : MonoBehaviour
{
    private const string OldEntryPrefix = "cultiway_character_panel_value_";
    private const string EntryNamePrefix = "cultiway_character_panel_entry_";
    private const string RowNamePrefix = "cultiway_character_panel_row_";
    private const int OriginalRowCount = 2;
    private const float MinRowHeight = 15f;
    private const float ExtensionRowContentYOffset = 1f;
    private const int RowBackgroundCornerRadius = 4;
    private const float RowBackgroundHorizontalOverlap = 1.1f;
    private const float RowBackgroundVerticalOverlap = 2f;

    private static readonly Color FallbackTopRowColor = new(0.16f, 0.18f, 0.15f, 0.92f);
    private static readonly Color FallbackBottomRowColor = new(0.10f, 0.11f, 0.095f, 0.92f);

    private readonly Dictionary<string, RuntimeEntry> _entries = new();
    private readonly HashSet<string> _loggedErrors = new();
    private readonly List<OriginalPanelRow> _originalRows = new();
    private readonly List<ExtensionRow> _extensionRows = new();
    private readonly List<RuntimeEntry> _visibleEntries = new();
    private PossessionUnitInfo _panel;
    private RectTransform _panelRect;
    private RectTransform _iconsRect;
    private HorizontalLayoutGroup _iconsLayout;
    private ResizablePanelImage _frameBackground;
    private AlternatingRowsBackground _rowsBackground;
    private Vector2 _basePanelSize;
    private Vector2 _basePanelPosition;
    private Vector2 _baseIconsSize;
    private float _rowHeight;
    private int _appliedExtensionRowCount = -1;

    private void Update()
    {
        if (!EnsureBound())
        {
            HideEntries();
            return;
        }

        Actor actor = GetControlledActor();
        _visibleEntries.Clear();
        IReadOnlyList<CharacterPanelRegistration> registrations = CharacterPanelExtensions.RegisteredEntries;
        for (int i = 0; i < registrations.Count; i++)
        {
            CharacterPanelRegistration registration = registrations[i];
            RuntimeEntry entry = EnsureEntry(registration);
            if (actor == null || !TryUpdateEntry(registration, actor, entry))
            {
                entry.SetVisible(false);
                continue;
            }

            entry.SetVisible(true);
            _visibleEntries.Add(entry);
        }

        int extensionRowCount = LayoutVisibleEntries(_visibleEntries);
        ApplyPanelRows(extensionRowCount);
    }

    private bool EnsureBound()
    {
        if (_panel != null && _iconsRect != null && _iconsLayout != null) return true;

        _entries.Clear();
        _originalRows.Clear();
        _extensionRows.Clear();
        _visibleEntries.Clear();
        _panel = null;
        _panelRect = null;
        _iconsRect = null;
        _iconsLayout = null;
        _frameBackground = null;
        _rowsBackground?.Dispose();
        _rowsBackground = null;
        _appliedExtensionRowCount = -1;

        foreach (PossessionUnitInfo panel in Resources.FindObjectsOfTypeAll<PossessionUnitInfo>())
        {
            if (panel == null || !panel.gameObject.scene.IsValid()) continue;

            Transform icons = panel.transform.Find("Icons") ?? panel.transform.FindRecursive("Icons");
            if (icons == null) continue;

            var iconsLayout = icons.GetComponent<HorizontalLayoutGroup>();
            if (iconsLayout == null) continue;

            Bind(panel, icons, iconsLayout);
            return true;
        }

        return false;
    }

    private void Bind(PossessionUnitInfo panel, Transform icons, HorizontalLayoutGroup iconsLayout)
    {
        RemoveOldExtensionObjects(panel.transform, icons);

        Transform iconSex = panel.transform.Find("Icon Sex");
        Transform iconSpecies = panel.transform.Find("Icon Species");
        Transform name = panel.transform.Find("Name");
        Transform banner = panel.transform.Find("BannerPrefab L");
        List<Image> backgrounds = FindDirectBackgrounds(panel.transform);

        _panel = panel;
        _panelRect = panel.GetComponent<RectTransform>();
        _iconsRect = icons.GetComponent<RectTransform>();
        _iconsLayout = iconsLayout;
        _basePanelSize = _panelRect.sizeDelta;
        _basePanelPosition = _panelRect.anchoredPosition;
        _baseIconsSize = _iconsRect.sizeDelta;
        _rowHeight = Mathf.Max(MinRowHeight, _basePanelSize.y / OriginalRowCount);
        _rowsBackground = backgrounds.Count > 0 ? new AlternatingRowsBackground(backgrounds[0]) : null;
        _frameBackground = backgrounds.Count > 1 ? new ResizablePanelImage(backgrounds[1]) : null;

        var topRow = new OriginalPanelRow();
        topRow.Add(iconSex);
        topRow.Add(iconSpecies);
        topRow.Add(name);
        topRow.Add(banner);
        _originalRows.Add(topRow);

        var iconsRow = new OriginalPanelRow();
        iconsRow.Add(icons);
        _originalRows.Add(iconsRow);

        ApplyPanelRows(0);
    }

    private static List<Image> FindDirectBackgrounds(Transform panel)
    {
        var result = new List<Image>();
        foreach (Transform child in panel)
        {
            if (child.name != "Background") continue;

            Image image = child.GetComponent<Image>();
            if (image != null) result.Add(image);
        }

        return result;
    }

    private RuntimeEntry EnsureEntry(CharacterPanelRegistration registration)
    {
        if (_entries.TryGetValue(registration.Id, out RuntimeEntry entry) && entry.Kind == registration.Kind &&
            entry.Component != null)
        {
            return entry;
        }

        if (entry.Component != null)
        {
            Object.Destroy(entry.Component.gameObject);
        }

        string entryName = EntryNamePrefix + registration.Id;
        Transform existing = _panelRect.transform.Find(entryName);
        if (existing != null)
        {
            Object.Destroy(existing.gameObject);
        }

        entry = registration.Kind switch
        {
            CharacterPanelEntryKind.IconValue => new RuntimeEntry(
                registration.Kind,
                Object.Instantiate(CharacterPanelIconValue.Prefab, _panelRect.transform, false)),
            CharacterPanelEntryKind.ProgressBar => new RuntimeEntry(
                registration.Kind,
                Object.Instantiate(CharacterPanelProgressBar.Prefab, _panelRect.transform, false)),
            _ => throw new ArgumentOutOfRangeException()
        };
        entry.Component.gameObject.name = entryName;
        entry.SetVisible(false);
        _entries[registration.Id] = entry;
        return entry;
    }

    private bool TryUpdateEntry(CharacterPanelRegistration registration, Actor actor, RuntimeEntry entry)
    {
        try
        {
            if (!registration.IsVisible(actor)) return false;

            switch (registration.Kind)
            {
                case CharacterPanelEntryKind.IconValue:
                {
                    CharacterPanelIconValueState state = registration.ReadIconValue(actor);
                    if (string.IsNullOrEmpty(state.Text)) return false;
                    ((CharacterPanelIconValue)entry.Component).Setup(state);
                    return true;
                }
                case CharacterPanelEntryKind.ProgressBar:
                {
                    CharacterPanelProgressBarState state = registration.ReadProgressBar(actor);
                    if (state.Max <= 0f) return false;
                    ((CharacterPanelProgressBar)entry.Component).Setup(state);
                    return true;
                }
                default:
                    return false;
            }
        }
        catch (Exception e)
        {
            if (_loggedErrors.Add(registration.Id))
            {
                ModClass.LogError($"[{nameof(CharacterPanelExtensions)}] failed to update {registration.Id}\n{e}");
            }

            return false;
        }
    }

    private int LayoutVisibleEntries(List<RuntimeEntry> entries)
    {
        float availableWidth = _baseIconsSize.x - _iconsLayout.padding.left - _iconsLayout.padding.right;
        int rowIndex = 0;
        ExtensionRow row = null;

        for (int i = 0; i < entries.Count; i++)
        {
            RuntimeEntry entry = entries[i];
            if (row == null)
            {
                row = EnsureExtensionRow(rowIndex);
                row.Begin(availableWidth, _iconsLayout.spacing);
            }
            else if (!row.HasSpaceFor(entry.Width))
            {
                rowIndex++;
                row = EnsureExtensionRow(rowIndex);
                row.Begin(availableWidth, _iconsLayout.spacing);
            }

            row.Add(entry);
        }

        int rowCount = entries.Count == 0 ? 0 : rowIndex + 1;
        for (int i = rowCount; i < _extensionRows.Count; i++)
        {
            _extensionRows[i].SetVisible(false);
        }

        return rowCount;
    }

    private ExtensionRow EnsureExtensionRow(int index)
    {
        while (_extensionRows.Count <= index)
        {
            _extensionRows.Add(ExtensionRow.Create(_panelRect.transform, _iconsLayout));
        }

        return _extensionRows[index];
    }

    private void ApplyPanelRows(int extensionRowCount)
    {
        extensionRowCount = Mathf.Max(0, extensionRowCount);
        bool sameRowCount = _appliedExtensionRowCount == extensionRowCount;
        _appliedExtensionRowCount = extensionRowCount;

        int totalRows = OriginalRowCount + extensionRowCount;
        float extraHeight = extensionRowCount * _rowHeight;
        float offset = extraHeight * 0.5f;

        if (!sameRowCount)
        {
            _panelRect.sizeDelta = new Vector2(_basePanelSize.x, _basePanelSize.y + extraHeight);
            _panelRect.anchoredPosition = _basePanelPosition + new Vector2(0f, offset);
            _frameBackground?.ApplyHeightDelta(extraHeight);
            _rowsBackground?.Apply(totalRows, extraHeight, Mathf.RoundToInt(_rowHeight));

            for (int i = 0; i < _originalRows.Count; i++)
            {
                _originalRows[i].ApplyShift(extraHeight, extraHeight);
            }
        }

        for (int i = 0; i < _extensionRows.Count; i++)
        {
            bool visible = i < extensionRowCount;
            _extensionRows[i].SetVisible(visible);
            if (!visible) continue;

            int rowIndex = OriginalRowCount + i;
            _extensionRows[i].SetLayout(_baseIconsSize.x, _rowHeight, -rowIndex * _rowHeight,
                ExtensionRowContentYOffset);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(_iconsRect);
    }

    private void HideEntries()
    {
        foreach (RuntimeEntry entry in _entries.Values)
        {
            entry.SetVisible(false);
        }

        if (_panelRect != null)
        {
            ApplyPanelRows(0);
        }
    }

    private static Actor GetControlledActor()
    {
        if (!ControllableUnit.isControllingUnit()) return null;

        Actor actor = ControllableUnit.getControllableUnit();
        if (actor == null || actor.isRekt()) return null;
        return actor;
    }

    private static void RemoveOldExtensionObjects(Transform panel, Transform icons)
    {
        for (int i = icons.childCount - 1; i >= 0; i--)
        {
            Transform child = icons.GetChild(i);
            if (!child.name.StartsWith(OldEntryPrefix) && !child.name.StartsWith(EntryNamePrefix)) continue;
            Object.Destroy(child.gameObject);
        }

        for (int i = panel.childCount - 1; i >= 0; i--)
        {
            Transform child = panel.GetChild(i);
            if (!child.name.StartsWith(RowNamePrefix) && !child.name.StartsWith(EntryNamePrefix)) continue;
            Object.Destroy(child.gameObject);
        }
    }

    private readonly struct RuntimeEntry
    {
        public readonly CharacterPanelEntryKind Kind;
        public readonly MonoBehaviour Component;

        public float Width => Component.GetComponent<RectTransform>().sizeDelta.x;

        public RuntimeEntry(CharacterPanelEntryKind kind, MonoBehaviour component)
        {
            Kind = kind;
            Component = component;
        }

        public void SetVisible(bool visible)
        {
            if (Component == null || Component.gameObject.activeSelf == visible) return;
            Component.gameObject.SetActive(visible);
        }

        public void SetParent(Transform parent)
        {
            if (Component == null || Component.transform.parent == parent) return;
            Component.transform.SetParent(parent, false);
        }
    }

    private readonly struct OriginalPanelRow
    {
        private readonly List<PanelRectSnapshot> _items;

        public OriginalPanelRow()
        {
            _items = new List<PanelRectSnapshot>();
        }

        public void Add(Transform transform)
        {
            if (transform == null) return;

            RectTransform rect = transform.GetComponent<RectTransform>();
            if (rect == null) return;
            _items.Add(new PanelRectSnapshot(rect));
        }

        public void ApplyShift(float parentHeightDelta, float desiredWorldShift)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                _items[i].ApplyWorldYOffset(parentHeightDelta, desiredWorldShift);
            }
        }
    }

    private readonly struct PanelRectSnapshot
    {
        private readonly RectTransform _rect;
        private readonly Vector2 _anchorMin;
        private readonly Vector2 _anchorMax;
        private readonly Vector2 _size;
        private readonly Vector2 _position;

        public PanelRectSnapshot(RectTransform rect)
        {
            _rect = rect;
            _anchorMin = rect.anchorMin;
            _anchorMax = rect.anchorMax;
            _size = rect.sizeDelta;
            _position = rect.anchoredPosition;
        }

        public void ApplyWorldYOffset(float parentHeightDelta, float desiredWorldShift)
        {
            if (_rect == null) return;

            float parentCenterShift = parentHeightDelta * 0.5f;
            float anchorCenterY = (_anchorMin.y + _anchorMax.y) * 0.5f;
            float anchorReferenceShift = parentCenterShift + (anchorCenterY - 0.5f) * parentHeightDelta;
            float localShift = desiredWorldShift - anchorReferenceShift;
            float anchorRangeY = _anchorMax.y - _anchorMin.y;
            _rect.anchorMin = _anchorMin;
            _rect.anchorMax = _anchorMax;
            _rect.sizeDelta = new Vector2(_size.x, _size.y - anchorRangeY * parentHeightDelta);
            _rect.anchoredPosition = new Vector2(_position.x, _position.y + localShift);
        }
    }

    private sealed class ResizablePanelImage
    {
        private readonly RectTransform _rect;
        private readonly Vector2 _size;
        private readonly Vector2 _position;

        public ResizablePanelImage(Image image)
        {
            _rect = image.GetComponent<RectTransform>();
            _size = _rect.sizeDelta;
            _position = _rect.anchoredPosition;
        }

        public void ApplyHeightDelta(float heightDelta)
        {
            ApplyHeightDelta(heightDelta, 0f, 0f);
        }

        public void ApplyHeightDelta(float heightDelta, float horizontalOverlap, float verticalOverlap)
        {
            if (_rect == null) return;

            _rect.sizeDelta = new Vector2(_size.x + horizontalOverlap * 2f,
                _size.y + heightDelta + verticalOverlap * 2f);
            _rect.anchoredPosition = _position;
        }
    }

    private sealed class AlternatingRowsBackground
    {
        private readonly Image _image;
        private readonly ResizablePanelImage _layout;
        private readonly Sprite _originalSprite;
        private readonly Image.Type _originalType;
        private readonly bool _originalPreserveAspect;
        private readonly bool _originalFillCenter;
        private readonly Color _topColor;
        private readonly Color _bottomColor;
        private Texture2D _texture;
        private Sprite _sprite;
        private int _rows = -1;

        public AlternatingRowsBackground(Image image)
        {
            _image = image;
            _layout = new ResizablePanelImage(image);
            _originalSprite = image.sprite;
            _originalType = image.type;
            _originalPreserveAspect = image.preserveAspect;
            _originalFillCenter = image.fillCenter;
            if (!TryReadRowColors(image, out _topColor, out _bottomColor))
            {
                _topColor = FallbackTopRowColor;
                _bottomColor = FallbackBottomRowColor;
            }
        }

        public void Apply(int rows, float heightDelta, int rowPixels)
        {
            if (_image == null) return;

            if (rows <= OriginalRowCount && Mathf.Approximately(heightDelta, 0f))
            {
                _layout.ApplyHeightDelta(0f);
                RestoreOriginal();
                return;
            }

            _layout.ApplyHeightDelta(heightDelta, RowBackgroundHorizontalOverlap, RowBackgroundVerticalOverlap);
            rows = Mathf.Max(OriginalRowCount, rows);
            if (_rows != rows)
            {
                Rebuild(rows, Mathf.Max(1, rowPixels));
            }

            _image.sprite = _sprite ?? _originalSprite;
            _image.type = _sprite == null ? _originalType : Image.Type.Simple;
            _image.preserveAspect = _sprite == null && _originalPreserveAspect;
            _image.fillCenter = _sprite == null ? _originalFillCenter : true;
            _image.raycastTarget = false;
        }

        public void Dispose()
        {
            DestroyGenerated();
            _rows = -1;
        }

        private void RestoreOriginal()
        {
            DestroyGenerated();
            _rows = -1;
            _image.sprite = _originalSprite;
            _image.type = _originalType;
            _image.preserveAspect = _originalPreserveAspect;
            _image.fillCenter = _originalFillCenter;
            _image.raycastTarget = false;
        }

        private void Rebuild(int rows, int rowPixels)
        {
            DestroyGenerated();
            int height = rows * rowPixels;
            float currentWidth = Mathf.Max(_image.rectTransform.rect.width, _image.rectTransform.sizeDelta.x);
            if (currentWidth <= 0f && _originalSprite != null)
            {
                currentWidth = _originalSprite.rect.width;
            }

            int width = Mathf.Max(16, Mathf.RoundToInt(currentWidth));
            _texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            for (int row = 0; row < rows; row++)
            {
                Color color = row % 2 == 0 ? _topColor : _bottomColor;
                int yStart = height - (row + 1) * rowPixels;
                for (int y = 0; y < rowPixels; y++)
                {
                    int py = yStart + y;
                    for (int x = 0; x < width; x++)
                    {
                        Color pixel = color;
                        pixel.a *= GetRoundedRectAlpha(x, py, width, height, RowBackgroundCornerRadius);
                        _texture.SetPixel(x, py, pixel);
                    }
                }
            }

            _texture.Apply(false, true);
            _sprite = Sprite.Create(_texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 1f);
            _sprite.hideFlags = HideFlags.DontSave;
            _rows = rows;
        }

        private static float GetRoundedRectAlpha(int x, int y, int width, int height, int radius)
        {
            if (radius <= 0) return 1f;

            int left = radius;
            int right = width - radius - 1;
            int bottom = radius;
            int top = height - radius - 1;
            float cx = x < left ? left : x > right ? right : x;
            float cy = y < bottom ? bottom : y > top ? top : y;
            float dx = x - cx;
            float dy = y - cy;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            if (distance <= radius - 1f) return 1f;
            if (distance >= radius) return 0f;
            return radius - distance;
        }

        private void DestroyGenerated()
        {
            if (_sprite != null) Object.Destroy(_sprite);
            if (_texture != null) Object.Destroy(_texture);
            _sprite = null;
            _texture = null;
        }

        private static bool TryReadRowColors(Image image, out Color top, out Color bottom)
        {
            top = FallbackTopRowColor;
            bottom = FallbackBottomRowColor;
            Sprite sprite = image.sprite;
            if (sprite == null || sprite.texture == null) return false;

            try
            {
                Rect rect = sprite.textureRect;
                int x = Mathf.Clamp(Mathf.RoundToInt(rect.x + rect.width * 0.5f), 0, sprite.texture.width - 1);
                int topY = Mathf.Clamp(Mathf.RoundToInt(rect.y + rect.height * 0.75f), 0, sprite.texture.height - 1);
                int bottomY = Mathf.Clamp(Mathf.RoundToInt(rect.y + rect.height * 0.25f), 0,
                    sprite.texture.height - 1);
                top = Multiply(sprite.texture.GetPixel(x, topY), image.color);
                bottom = Multiply(sprite.texture.GetPixel(x, bottomY), image.color);
                if (Mathf.Abs(GetLuminance(top) - GetLuminance(bottom)) < 0.02f)
                {
                    return false;
                }

                return top.a > 0.05f && bottom.a > 0.05f;
            }
            catch (UnityException)
            {
                return false;
            }
        }

        private static Color Multiply(Color a, Color b)
        {
            return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
        }

        private static float GetLuminance(Color color)
        {
            return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
        }
    }

    private sealed class ExtensionRow
    {
        private readonly RectTransform _root;
        private readonly RectTransform _content;
        private float _availableWidth;
        private float _usedWidth;
        private float _spacing;
        private int _itemCount;

        private ExtensionRow(RectTransform root, RectTransform content)
        {
            _root = root;
            _content = content;
        }

        public static ExtensionRow Create(Transform parent, HorizontalLayoutGroup template)
        {
            GameObject root = new(RowNamePrefix + parent.childCount, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);

            GameObject content = new("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            content.transform.SetParent(root.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            HorizontalLayoutGroup layout = content.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(template.padding.left, template.padding.right, template.padding.top,
                template.padding.bottom);
            layout.childAlignment = template.childAlignment;
            layout.spacing = template.spacing;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            return new ExtensionRow(rootRect, contentRect);
        }

        public void Begin(float availableWidth, float spacing)
        {
            _availableWidth = availableWidth;
            _usedWidth = 0f;
            _spacing = spacing;
            _itemCount = 0;
            SetVisible(true);
        }

        public bool HasSpaceFor(float width)
        {
            return _itemCount == 0 || _usedWidth + _spacing + width <= _availableWidth;
        }

        public void Add(RuntimeEntry entry)
        {
            entry.SetParent(_content);
            entry.SetVisible(true);
            _usedWidth = _itemCount == 0 ? entry.Width : _usedWidth + _spacing + entry.Width;
            _itemCount++;
        }

        public void SetLayout(float width, float height, float y, float contentYOffset)
        {
            _root.sizeDelta = new Vector2(width, height);
            _root.anchoredPosition = new Vector2(0f, y);
            _content.offsetMin = new Vector2(0f, contentYOffset);
            _content.offsetMax = new Vector2(0f, contentYOffset);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        public void SetVisible(bool visible)
        {
            if (_root.gameObject.activeSelf == visible) return;
            _root.gameObject.SetActive(visible);
        }
    }
}
