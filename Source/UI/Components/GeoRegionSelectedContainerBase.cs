using System.Collections.Generic;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using DG.Tweening;
using LayoutGroupExt;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI.Components;

internal abstract class GeoRegionSelectedContainerBase : MonoBehaviour
{
    private const float BannerScale = 0.75f;
    private const float DefaultBannerWidth = 34f;
    private const float DefaultBannerHeight = 44f;

    private readonly List<GameObject> _spawnedObjects = new();
    private RectTransform _hostRect;
    private RectTransform _tabElementRect;
    private float _baseHostWidth;
    private float _baseTabElementWidth;
    private int _itemsCount;
    private GeoRegion _lastRefreshRegion;
    private string _lastRefreshKey;
    private bool _hasRefreshKey;
    private bool _gridIsHost;
    private Transform _originalContentRoot;
    private Text _backgroundTitle;
    private LocalizedText _backgroundTitleLocalization;
    protected Transform Grid { get; private set; }

    protected virtual float LeftPadding => 0f;
    protected virtual float RightPadding => 0f;
    protected virtual float TopPadding => 0f;
    protected virtual float BottomPadding => 0f;
    protected virtual float MinimumHeight => 30f;
    protected virtual float MinimumWidth => 0f;
    protected virtual int ConstraintCount => 2;
    protected virtual GridLayoutGroupExtended.Constraint ConstraintType => GridLayoutGroupExtended.Constraint.FixedRowCount;
    protected virtual GridLayoutGroupExtended.Axis StartAxis => GridLayoutGroupExtended.Axis.Horizontal;
    protected virtual TextAnchor ChildAlignment => TextAnchor.MiddleLeft;
    protected virtual Vector2 CellSize => new(GeoRegionSelectedInfoIcon.DefaultSize, GeoRegionSelectedInfoIcon.DefaultSize);
    protected virtual Vector2 GridSpacing => new(3f, 3f);
    protected virtual bool KeepVisibleWhenEmpty => false;
    protected virtual bool AnchorGridToTop => false;
    protected virtual bool UseHostAsGrid => false;
    protected virtual bool UseFlexibleOneRowSpacing => ConstraintType == GridLayoutGroupExtended.Constraint.FixedRowCount && ConstraintCount == 1;
    protected virtual int FlexibleBonusSpacingX => Mathf.RoundToInt(GridSpacing.x);
    protected virtual string BackgroundTitle => null;
    protected virtual string BackgroundTitleKey => null;
    protected virtual int BackgroundTitleFontSize => 20;
    protected virtual Color BackgroundTitleColor => new(0.34f, 0.25f, 0.13f, 0.58f);

    internal void SetOriginalContentRoot(Transform contentRoot)
    {
        _originalContentRoot = contentRoot;
    }

    internal void Initialize()
    {
        if (Grid != null) return;

        _hostRect = GetComponent<RectTransform>();
        _tabElementRect = FindTabElementRect();
        _baseHostWidth = GetRectWidth(_hostRect);
        _baseTabElementWidth = GetRectWidth(_tabElementRect);

        Transform titleRoot = FindOriginalTitleRoot(_originalContentRoot);
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform childTransform = transform.GetChild(i);
            if (ShouldKeepOriginalChild(childTransform, titleRoot, _originalContentRoot))
            {
                childTransform.gameObject.SetActive(true);
                continue;
            }

            GameObject child = childTransform.gameObject;
            child.SetActive(false);
            if (child.TryGetComponent(out LayoutElement layoutElement))
            {
                layoutElement.ignoreLayout = true;
            }
        }

        SetBackgroundTitle(BackgroundTitleKey, BackgroundTitle);

        GridLayoutGroupExtended layout;
        if (UseHostAsGrid)
        {
            Grid = transform;
            _gridIsHost = true;
            layout = GetComponent<GridLayoutGroupExtended>() ?? gameObject.AddComponent<GridLayoutGroupExtended>();
        }
        else if (_originalContentRoot != null)
        {
            Grid = _originalContentRoot;
            Grid.gameObject.SetActive(true);
            ClearOriginalContentRoot(Grid);
            layout = Grid.GetComponent<GridLayoutGroupExtended>() ?? Grid.gameObject.AddComponent<GridLayoutGroupExtended>();
        }
        else
        {
            GameObject gridObject = new("GeoRegionItems", typeof(RectTransform), typeof(GridLayoutGroupExtended));
            gridObject.transform.SetParent(transform);
            gridObject.transform.localScale = Vector3.one;

            RectTransform rect = gridObject.GetComponent<RectTransform>();
            SetGridAnchor(rect);
            rect.sizeDelta = new Vector2(Mathf.Max(MinimumWidth, _baseHostWidth), MinimumHeight);
            rect.anchoredPosition = GetGridAnchoredPosition();

            Grid = gridObject.transform;
            layout = gridObject.GetComponent<GridLayoutGroupExtended>();
        }

        layout.cellSize = CellSize;
        layout.spacing = GridSpacing;
        layout.padding = UseHostAsGrid
            ? new RectOffset(Mathf.RoundToInt(LeftPadding), Mathf.RoundToInt(RightPadding), Mathf.RoundToInt(TopPadding), Mathf.RoundToInt(BottomPadding))
            : new RectOffset();
        layout.startCorner = GridLayoutGroupExtended.Corner.UpperLeft;
        layout.startAxis = StartAxis;
        layout.childAlignment = ChildAlignment;
        layout.constraint = ConstraintType;
        layout.constraintCount = Mathf.Max(1, ConstraintCount);
        layout.moveDuration = 0.12f;
        layout.delayItems = 8;

        if (UseFlexibleOneRowSpacing)
        {
            FlexibleOneRowGrid flexible = Grid.GetComponent<FlexibleOneRowGrid>() ?? Grid.gameObject.AddComponent<FlexibleOneRowGrid>();
            flexible.bonus_spacing_x = FlexibleBonusSpacingX;
        }

        ApplyLayoutSize(Vector2.zero);
    }

    protected void SetBackgroundTitle(string titleKey, string fallbackTitle)
    {
        if (string.IsNullOrWhiteSpace(titleKey) && string.IsNullOrWhiteSpace(fallbackTitle))
        {
            if (_backgroundTitle != null)
            {
                _backgroundTitle.gameObject.SetActive(false);
            }

            return;
        }

        _backgroundTitle ??= CreateBackgroundTitle();
        if (_backgroundTitleLocalization != null && !string.IsNullOrWhiteSpace(titleKey))
        {
            _backgroundTitleLocalization.setKeyAndUpdate(titleKey);
        }
        else
        {
            _backgroundTitle.text = string.IsNullOrWhiteSpace(fallbackTitle)
                ? LMTools.GetOrKey(titleKey)
                : fallbackTitle;
        }

        _backgroundTitle.gameObject.SetActive(true);
        if (_backgroundTitleLocalization == null)
        {
            _backgroundTitle.fontSize = BackgroundTitleFontSize;
            _backgroundTitle.color = BackgroundTitleColor;
            _backgroundTitle.transform.SetAsFirstSibling();
        }
    }

    internal void Refresh(GeoRegion region)
    {
        Initialize();

        string refreshKey = GetRefreshKey(region);
        if (_hasRefreshKey && ReferenceEquals(_lastRefreshRegion, region) && _lastRefreshKey == refreshKey)
        {
            RefreshExisting(region);
            gameObject.SetActive(_spawnedObjects.Count > 0 || KeepVisibleWhenEmpty);
            return;
        }

        ClearSpawned(false);
        Build(region);
        _lastRefreshRegion = region;
        _lastRefreshKey = refreshKey;
        _hasRefreshKey = true;
        ApplyLayoutSize(GetContentSize());
        gameObject.SetActive(_spawnedObjects.Count > 0 || KeepVisibleWhenEmpty);
    }

    protected abstract void Build(GeoRegion region);

    protected virtual void RefreshExisting(GeoRegion region)
    {
    }

    protected virtual string GetRefreshKey(GeoRegion region)
    {
        if (region == null) throw new System.InvalidOperationException("GeoRegion 为空");
        if (region.data == null) throw new System.InvalidOperationException($"GeoRegion 数据为空: id={region.getID()}");

        GeoRegionData data = region.data;
        return $"{region.getID()}|{data.name}|{data.CategoryId}|{(int)data.Layer}|{data.TileCount}|{data.CenterX}|{data.CenterY}|{data.color_id}|{data.BannerBackgroundIndex}|{data.BannerIconIndex}";
    }

    protected GeoRegionSelectedInfoIcon AddIcon(Sprite sprite, string title, string description, Color? color = null, UnityEngine.Events.UnityAction clickAction = null)
    {
        GeoRegionSelectedInfoIcon icon = GeoRegionSelectedInfoIcon.Create(Grid, "GeoRegionInfoIcon", CellSize.x);
        icon.Setup(sprite, title, description, color, clickAction);
        Track(icon.gameObject);
        return icon;
    }

    protected GeoRegionBanner AddGeoRegionBanner(GeoRegion region)
    {
        GeoRegionBanner banner = Object.Instantiate(GeoRegionBanner.Prefab, Grid);
        banner.gameObject.SetActive(true);
        banner.transform.localScale = Vector3.one * BannerScale;
        banner.enable_default_click = false;
        banner.enable_tab_show_click = true;
        banner.load(region);
        SetupBannerLayout(banner.gameObject);
        Track(banner.gameObject);
        return banner;
    }

    protected CityBanner AddCityBanner(City city)
    {
        CityBanner prefab = Resources.Load<CityBanner>("ui/PrefabBannerCity");
        if (prefab == null)
        {
            throw new System.InvalidOperationException("找不到原版城市 banner prefab: ui/PrefabBannerCity");
        }

        CityBanner banner = Object.Instantiate(prefab, Grid);
        banner.gameObject.SetActive(true);
        banner.transform.localScale = Vector3.one * 0.75f;
        banner.enable_default_click = false;
        banner.enable_tab_show_click = true;
        banner.load(city);
        SetupBannerLayout(banner.gameObject);
        Track(banner.gameObject);
        return banner;
    }

    protected static Sprite LoadSprite(string path)
    {
        Sprite sprite = string.IsNullOrEmpty(path) ? null : SpriteTextureLoader.getSprite(path);
        return sprite != null ? sprite : SpriteTextureLoader.getSprite(GeoRegionAsset.DefaultIconPath);
    }

    protected static Color RegionColor(GeoRegion region)
    {
        Color32 color = region.getColor().getColorMain32();
        return new Color(color.r / 255f, color.g / 255f, color.b / 255f, 0.82f);
    }

    protected static void SelectGeoRegion(GeoRegion region)
    {
        AssetManager.meta_type_library.getAsset(region.meta_type).selectAndInspect(region, false, true, false);
    }

    private void Track(GameObject obj)
    {
        _spawnedObjects.Add(obj);
        _itemsCount++;
    }

    private Text CreateBackgroundTitle()
    {
        GameObject titleObject = new("GeoRegionContainerTitle", typeof(RectTransform), typeof(Text), typeof(Shadow), typeof(LayoutElement));
        titleObject.transform.SetParent(transform, false);
        titleObject.transform.localScale = Vector3.one;

        LayoutElement layoutElement = titleObject.GetComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;

        RectTransform rect = titleObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        Text text = titleObject.GetComponent<Text>();
        text.raycastTarget = false;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontStyle = FontStyle.Bold;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 12;
        text.resizeTextMaxSize = BackgroundTitleFontSize;

        Shadow shadow = titleObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.22f);
        shadow.effectDistance = new Vector2(1f, -1f);

        return text;
    }

    private Transform FindOriginalTitleRoot(Transform contentRoot)
    {
        _backgroundTitle = null;
        _backgroundTitleLocalization = null;

        LocalizedText[] localizedTexts = GetComponentsInChildren<LocalizedText>(true);
        for (int i = 0; i < localizedTexts.Length; i++)
        {
            LocalizedText localizedText = localizedTexts[i];
            if (!IsTitleCandidate(localizedText.transform, contentRoot)) continue;

            Text text = localizedText.GetComponent<Text>();
            if (text == null) continue;

            _backgroundTitle = text;
            _backgroundTitleLocalization = localizedText;
            EnsureIgnoredByLayout(text.gameObject);
            return GetImmediateChildUnderSelf(text.transform);
        }

        Text[] texts = GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (!IsTitleCandidate(text.transform, contentRoot)) continue;

            _backgroundTitle = text;
            _backgroundTitleLocalization = text.GetComponent<LocalizedText>();
            EnsureIgnoredByLayout(text.gameObject);
            return GetImmediateChildUnderSelf(text.transform);
        }

        return null;
    }

    private bool IsTitleCandidate(Transform candidate, Transform contentRoot)
    {
        if (candidate == null || candidate == transform) return false;
        if (contentRoot != null && (candidate == contentRoot || candidate.IsChildOf(contentRoot))) return false;

        string lowerName = candidate.name.ToLowerInvariant();
        if (lowerName.Contains("title") || lowerName.Contains("text") || lowerName.Contains("label"))
        {
            return true;
        }

        LocalizedText localizedText = candidate.GetComponent<LocalizedText>();
        return localizedText != null && !string.IsNullOrEmpty(localizedText.key) && localizedText.key != LocalizedText.DEFAULT_KEY;
    }

    private Transform GetImmediateChildUnderSelf(Transform child)
    {
        Transform current = child;
        while (current != null && current.parent != transform)
        {
            current = current.parent;
        }

        return current;
    }

    private bool ShouldKeepOriginalChild(Transform child, Transform titleRoot, Transform contentRoot)
    {
        if (child == null) return false;
        if (titleRoot != null && (child == titleRoot || titleRoot.IsChildOf(child))) return true;
        return contentRoot != null && (child == contentRoot || contentRoot.IsChildOf(child));
    }

    private static void ClearOriginalContentRoot(Transform contentRoot)
    {
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = contentRoot.GetChild(i).gameObject;
            child.SetActive(false);
            Object.Destroy(child);
        }
    }

    private static void EnsureIgnoredByLayout(GameObject obj)
    {
        LayoutElement layoutElement = obj.GetComponent<LayoutElement>() ?? obj.AddComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;
    }

    private void ClearSpawned(bool invalidateRefreshKey = true)
    {
        KillLayoutTweens(Grid);

        for (int i = 0; i < _spawnedObjects.Count; i++)
        {
            GameObject obj = _spawnedObjects[i];
            if (obj == null) continue;

            KillLayoutTweens(obj.transform);
            obj.SetActive(false);
            Object.Destroy(obj);
        }

        _spawnedObjects.Clear();
        _itemsCount = 0;

        if (!invalidateRefreshKey) return;
        _lastRefreshRegion = null;
        _lastRefreshKey = null;
        _hasRefreshKey = false;
    }

    private void OnDisable()
    {
        ClearSpawned();
    }

    private void OnDestroy()
    {
        ClearSpawned();
    }

    private static void KillLayoutTweens(Transform root)
    {
        if (root == null) return;

        RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect == null) continue;
            rect.DOKill(false);
        }
    }

    private Vector2 GetContentSize()
    {
        if (_itemsCount == 0) return Vector2.zero;

        int rows;
        int columns;
        int constraintCount = Mathf.Max(1, ConstraintCount);
        if (ConstraintType == GridLayoutGroupExtended.Constraint.FixedColumnCount)
        {
            columns = Mathf.Min(constraintCount, _itemsCount);
            rows = Mathf.CeilToInt(_itemsCount / (float)constraintCount);
        }
        else if (ConstraintType == GridLayoutGroupExtended.Constraint.FixedRowCount)
        {
            rows = Mathf.Min(constraintCount, _itemsCount);
            columns = Mathf.CeilToInt(_itemsCount / (float)constraintCount);
        }
        else
        {
            float availableWidth = Mathf.Max(MinimumWidth, _baseHostWidth) - LeftPadding - RightPadding;
            columns = Mathf.Max(1, Mathf.FloorToInt((availableWidth + GridSpacing.x) / (CellSize.x + GridSpacing.x)));
            columns = Mathf.Min(columns, _itemsCount);
            rows = Mathf.CeilToInt(_itemsCount / (float)columns);
        }

        float width = LeftPadding + RightPadding + columns * CellSize.x + Mathf.Max(0, columns - 1) * GridSpacing.x;
        float height = TopPadding + BottomPadding + rows * CellSize.y + Mathf.Max(0, rows - 1) * GridSpacing.y;
        return new Vector2(width, height);
    }

    private void ApplyLayoutSize(Vector2 contentSize)
    {
        float width = Mathf.Max(MinimumWidth, _baseHostWidth, contentSize.x);
        float height = Mathf.Max(MinimumHeight, contentSize.y);
        SetRectSize(_hostRect, width, height);

        RectTransform gridRect = (RectTransform)Grid;
        if (!_gridIsHost)
        {
            SetGridAnchor(gridRect);
            gridRect.sizeDelta = new Vector2(Mathf.Max(0f, width - LeftPadding - RightPadding), Mathf.Max(0f, height - TopPadding - BottomPadding));
            gridRect.anchoredPosition = GetGridAnchoredPosition();
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(gridRect);

        if (_tabElementRect != null && _tabElementRect != _hostRect)
        {
            float tabWidth = Mathf.Max(_baseTabElementWidth, GetChildRightEdge(_tabElementRect, _hostRect));
            SetRectSize(_tabElementRect, tabWidth, Mathf.Max(_tabElementRect.sizeDelta.y, height));
            LayoutRebuilder.MarkLayoutForRebuild(_tabElementRect);
        }

        LayoutRebuilder.MarkLayoutForRebuild(_hostRect);
    }

    private float SetupBannerLayout(GameObject bannerObject)
    {
        RectTransform rect = bannerObject.GetComponent<RectTransform>();
        float width = CellSize.x > 0f ? CellSize.x : DefaultBannerWidth;
        float height = CellSize.y > 0f ? CellSize.y : DefaultBannerHeight;
        if (rect != null)
        {
            float rectWidth = rect.sizeDelta.x > 0f ? rect.sizeDelta.x : rect.rect.width;
            float rectHeight = rect.sizeDelta.y > 0f ? rect.sizeDelta.y : rect.rect.height;
            if (rectWidth > 0f)
            {
                width = Mathf.Min(width, Mathf.Clamp(rectWidth * Mathf.Abs(bannerObject.transform.localScale.x), 28f, 48f));
            }

            if (rectHeight > 0f)
            {
                height = Mathf.Min(height, Mathf.Clamp(rectHeight * Mathf.Abs(bannerObject.transform.localScale.y), 28f, 52f));
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        LayoutElement layout = bannerObject.GetComponent<LayoutElement>() ?? bannerObject.AddComponent<LayoutElement>();
        layout.ignoreLayout = false;
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.minHeight = height;
        layout.preferredHeight = height;
        return width;
    }

    private void SetGridAnchor(RectTransform rect)
    {
        if (rect == null) return;

        if (AnchorGridToTop)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            return;
        }

        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
    }

    private Vector2 GetGridAnchoredPosition()
    {
        return AnchorGridToTop
            ? new Vector2(LeftPadding, -TopPadding)
            : new Vector2(LeftPadding, (BottomPadding - TopPadding) * 0.5f);
    }

    private RectTransform FindTabElementRect()
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.parent != null && current.parent.GetComponent<PowersTab>() != null)
            {
                return current.GetComponent<RectTransform>();
            }

            current = current.parent;
        }

        return _hostRect;
    }

    private static float GetRectWidth(RectTransform rect)
    {
        if (rect == null) return 0f;
        float width = rect.rect.width;
        if (width > 0f) return width;
        return Mathf.Max(0f, rect.sizeDelta.x);
    }

    private static void SetRectSize(RectTransform rect, float width, float height)
    {
        if (rect == null) return;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

        float currentHeight = Mathf.Max(rect.rect.height, rect.sizeDelta.y);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(currentHeight, height));
    }

    private static float GetChildRightEdge(RectTransform parent, RectTransform child)
    {
        if (parent == null || child == null) return 0f;
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, child);
        return bounds.max.x - parent.rect.xMin;
    }
}
