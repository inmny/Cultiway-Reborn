using Cultiway.Utils;
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

/// <summary>
/// 地区选中底栏各内容区域的共同基础，负责创建图标或旗帜、按内容调整尺寸并复用暂时隐藏的条目。
/// </summary>
internal abstract class GeoRegionSelectedContainerBase : MonoBehaviour
{
    // 地区和城市旗帜在选中底栏中的显示比例与默认占位尺寸。
    private const float BannerScale = 0.75f;
    private const float DefaultBannerWidth = 34f;
    private const float DefaultBannerHeight = 44f;

    // 当前显示的条目，以及暂时隐藏、可在下次刷新继续使用的图标和旗帜。
    private readonly List<GameObject> _spawnedObjects = new();
    private readonly Stack<GeoRegionSelectedInfoIcon> _iconPool = new();
    private readonly Stack<GeoRegionBanner> _geoRegionBannerPool = new();
    private readonly Stack<CityBanner> _cityBannerPool = new();
    // 当前内容区域和整个底栏条目的尺寸基准，以及实际显示的条目数量。
    private RectTransform _hostRect;
    private RectTransform _tabElementRect;
    private float _baseHostWidth;
    private float _baseTabElementWidth;
    private int _itemsCount;
    // 上次显示的地区和内容标记，用于名单未变时只更新文字与图案。
    private GeoRegion _lastRefreshRegion;
    private string _lastRefreshKey;
    private bool _hasRefreshKey;
    // 记录条目是否直接放在原版区域、原版内容位置和背景标题。
    private bool _gridIsHost;
    private Transform _originalContentRoot;
    private Text _backgroundTitle;
    private LocalizedText _backgroundTitleLocalization;
    /// <summary>实际承载地区图标或旗帜的排列区域。</summary>
    protected Transform Grid { get; private set; }

    // 子类可调整四周留白、最小尺寸、行列数量和条目排列方向。
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
    // 每个条目的固定尺寸与间距。
    protected virtual Vector2 CellSize => new(GeoRegionSelectedInfoIcon.DefaultSize, GeoRegionSelectedInfoIcon.DefaultSize);
    protected virtual Vector2 GridSpacing => new(3f, 3f);
    // 控制空内容是否保留区域、条目放置方式和单行间距。
    protected virtual bool KeepVisibleWhenEmpty => false;
    protected virtual bool AnchorGridToTop => false;
    protected virtual bool UseHostAsGrid => false;
    protected virtual bool UseFlexibleOneRowSpacing => ConstraintType == GridLayoutGroupExtended.Constraint.FixedRowCount && ConstraintCount == 1;
    protected virtual int FlexibleBonusSpacingX => Mathf.RoundToInt(GridSpacing.x);
    // 条目换位动画时长和依次开始动画的数量限制。
    protected virtual float LayoutMoveDuration => 0.12f;
    protected virtual int LayoutDelayItems => 8;
    // 背景标题的备用文字、本地化文本编号、字号和颜色。
    protected virtual string BackgroundTitle => null;
    protected virtual string BackgroundTitleKey => null;
    protected virtual int BackgroundTitleFontSize => 20;
    protected virtual Color BackgroundTitleColor => new(0.34f, 0.25f, 0.13f, 0.58f);

    /// <summary>记录原版区域放置条目的位置，替换内容时尽量保留原有框架和标题。</summary>
    internal void SetOriginalContentRoot(Transform contentRoot)
    {
        _originalContentRoot = contentRoot;
    }

    /// <summary>首次使用时整理原版节点，创建条目排列区域并应用子类指定的尺寸和间距。</summary>
    internal void Initialize()
    {
        if (Grid != null) return;

        _hostRect = GetComponent<RectTransform>();
        _tabElementRect = FindTabElementRect();
        _baseHostWidth = GetRectWidth(_hostRect);
        _baseTabElementWidth = GetRectWidth(_tabElementRect);

        Transform titleRoot = FindOriginalTitleRoot(_originalContentRoot);
        Transform contentRootToKeep = UseHostAsGrid ? null : _originalContentRoot;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform childTransform = transform.GetChild(i);
            if (ShouldKeepOriginalChild(childTransform, titleRoot, contentRootToKeep))
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

        if (UseHostAsGrid)
        {
            HideOriginalContentRoot(_originalContentRoot);
        }

        CleanupOriginalChildren();
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
        layout.moveDuration = LayoutMoveDuration;
        layout.delayItems = LayoutDelayItems;

        if (UseFlexibleOneRowSpacing)
        {
            FlexibleOneRowGrid flexible = Grid.GetComponent<FlexibleOneRowGrid>() ?? Grid.gameObject.AddComponent<FlexibleOneRowGrid>();
            flexible.bonus_spacing_x = FlexibleBonusSpacingX;
        }

        ApplyLayoutSize(Vector2.zero);
    }

    /// <summary>设置区域背后的淡色标题；没有标题文字时隐藏它。</summary>
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

    /// <summary>
    /// 更新区域内容。名单变化时重新摆放条目，只是数值变化时保留当前条目；返回是否需要重算底栏大小。
    /// </summary>
    internal bool Refresh(GeoRegion region)
    {
        Initialize();

        string refreshKey = GetRefreshKey(region);
        if (_hasRefreshKey && ReferenceEquals(_lastRefreshRegion, region) && _lastRefreshKey == refreshKey)
        {
            RefreshContent(region);
            return false;
        }

        ClearSpawned(false);
        Build(region);
        _lastRefreshRegion = region;
        _lastRefreshKey = refreshKey;
        _hasRefreshKey = true;
        ApplyLayoutSize(GetContentSize());
        SetContainerActive(_spawnedObjects.Count > 0 || KeepVisibleWhenEmpty);
        return true;
    }

    /// <summary>由子类为当前地区添加玩家可见的图标或旗帜。</summary>
    protected abstract void Build(GeoRegion region);

    /// <summary>名单不变时由子类更新现有条目的文字、图案或数值。</summary>
    protected virtual void RefreshContent(GeoRegion region)
    {
    }

    /// <summary>返回代表当前显示名单的文字；结果变化时会重新创建和排列条目。</summary>
    protected abstract string GetRefreshKey(GeoRegion region);

    /// <summary>子类可在首次准备区域时移除会干扰地区内容的原版组件。</summary>
    protected virtual void CleanupOriginalChildren()
    {
    }

    /// <summary>添加一个带说明和可选点击动作的信息图标，优先复用之前隐藏的图标。</summary>
    protected GeoRegionSelectedInfoIcon AddIcon(Sprite sprite, string title, string description, Color? color = null, UnityEngine.Events.UnityAction clickAction = null)
    {
        GeoRegionSelectedInfoIcon icon = _iconPool.Count > 0
            ? _iconPool.Pop()
            : GeoRegionSelectedInfoIcon.Create(Grid, "GeoRegionInfoIcon", CellSize.x);
        icon.transform.SetParent(Grid, false);
        icon.gameObject.SetActive(true);
        icon.Setup(sprite, title, description, color, clickAction);
        Track(icon.gameObject);
        return icon;
    }

    /// <summary>添加可点击进入详情的地区旗帜，优先复用之前隐藏的旗帜。</summary>
    protected GeoRegionBanner AddGeoRegionBanner(GeoRegion region)
    {
        GeoRegionBanner banner = _geoRegionBannerPool.Count > 0
            ? _geoRegionBannerPool.Pop()
            : Object.Instantiate(GeoRegionBanner.Prefab, Grid);
        banner.transform.SetParent(Grid, false);
        banner.gameObject.SetActive(true);
        banner.transform.localScale = Vector3.one * BannerScale;
        banner.enable_default_click = false;
        banner.enable_tab_show_click = true;
        banner.load(region);
        SetupBannerLayout(banner.gameObject);
        Track(banner.gameObject);
        return banner;
    }

    /// <summary>添加可点击进入详情的城市旗帜，优先复用之前隐藏的旗帜。</summary>
    protected CityBanner AddCityBanner(City city)
    {
        CityBanner prefab = Resources.Load<CityBanner>("ui/PrefabBannerCity");
        if (prefab == null)
        {
            throw new System.InvalidOperationException("找不到原版城市 banner prefab: ui/PrefabBannerCity");
        }

        CityBanner banner = _cityBannerPool.Count > 0
            ? _cityBannerPool.Pop()
            : Object.Instantiate(prefab, Grid);
        banner.transform.SetParent(Grid, false);
        banner.gameObject.SetActive(true);
        banner.transform.localScale = Vector3.one * 0.75f;
        banner.enable_default_click = false;
        banner.enable_tab_show_click = true;
        banner.load(city);
        SetupBannerLayout(banner.gameObject);
        Track(banner.gameObject);
        return banner;
    }

    /// <summary>载入指定图标；资源不存在时显示默认地区图标。</summary>
    protected static Sprite LoadSprite(string path)
    {
        Sprite sprite = string.IsNullOrEmpty(path) ? null : SpriteTextureLoader.getSprite(path);
        return sprite != null ? sprite : SpriteTextureLoader.getSprite(GeoRegionAsset.DefaultIconPath);
    }

    /// <summary>取得适合覆盖在信息图标上的半透明地区颜色。</summary>
    protected static Color RegionColor(GeoRegion region)
    {
        Color32 color = region.getColor().getColorMain32();
        return new Color(color.r / 255f, color.g / 255f, color.b / 255f, 0.82f);
    }

    /// <summary>将目标设为玩家当前选中地区并打开其详情。</summary>
    protected static void SelectGeoRegion(GeoRegion region)
    {
        AssetManager.meta_type_library.getAsset(region.meta_type).selectAndInspect(region, false, true, false);
    }

    /// <summary>记录一个当前正在显示的条目，并增加布局使用的数量。</summary>
    private void Track(GameObject obj)
    {
        _spawnedObjects.Add(obj);
        _itemsCount++;
    }

    /// <summary>原版区域没有可用标题时，创建不会挡住点击的淡色背景标题。</summary>
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
        text.font = Cultiway.UI.UiTheme.Current.Font;
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

    /// <summary>在原版区域中寻找可沿用的标题，同时避开原本放置图标的内容节点。</summary>
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

    /// <summary>判断一个文字节点是否像区域标题，而不是条目内容中的文字。</summary>
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

    /// <summary>从深层标题向上找到本区域的直接子节点，便于整体保留标题装饰。</summary>
    private Transform GetImmediateChildUnderSelf(Transform child)
    {
        Transform current = child;
        while (current != null && current.parent != transform)
        {
            current = current.parent;
        }

        return current;
    }

    /// <summary>判断整理原版区域时是否应保留标题或原内容位置。</summary>
    private bool ShouldKeepOriginalChild(Transform child, Transform titleRoot, Transform contentRoot)
    {
        if (child == null) return false;
        if (titleRoot != null && (child == titleRoot || titleRoot.IsChildOf(child))) return true;
        return contentRoot != null && (child == contentRoot || contentRoot.IsChildOf(child));
    }

    /// <summary>清空原版条目，防止国家、盟友或战争内容与地区内容重叠。</summary>
    private static void ClearOriginalContentRoot(Transform contentRoot)
    {
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = contentRoot.GetChild(i).gameObject;
            child.SetActive(false);
            Object.Destroy(child);
        }
    }

    /// <summary>条目直接放在区域根节点时，隐藏不再使用的原版内容位置。</summary>
    private void HideOriginalContentRoot(Transform contentRoot)
    {
        if (contentRoot == null || contentRoot == transform) return;

        contentRoot.gameObject.SetActive(false);
        EnsureIgnoredByLayout(contentRoot.gameObject);
    }

    /// <summary>让保留的标题或隐藏节点不占用地区条目的排列空间。</summary>
    private static void EnsureIgnoredByLayout(GameObject obj)
    {
        LayoutElement layoutElement = obj.GetComponent<LayoutElement>() ?? obj.AddComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;
    }

    /// <summary>隐藏当前条目并按类型收好以便复用，同时可选择忘记上次显示名单。</summary>
    private void ClearSpawned(bool invalidateRefreshKey = true)
    {
        KillLayoutTweens(Grid);

        for (int i = 0; i < _spawnedObjects.Count; i++)
        {
            GameObject obj = _spawnedObjects[i];
            if (obj == null) continue;

            KillLayoutTweens(obj.transform);
            obj.SetActive(false);
            if (obj.TryGetComponent(out GeoRegionSelectedInfoIcon icon))
            {
                _iconPool.Push(icon);
            }
            else if (obj.TryGetComponent(out GeoRegionBanner geoRegionBanner))
            {
                _geoRegionBannerPool.Push(geoRegionBanner);
            }
            else if (obj.TryGetComponent(out CityBanner cityBanner))
            {
                _cityBannerPool.Push(cityBanner);
            }
            else
            {
                Object.Destroy(obj);
            }
        }

        _spawnedObjects.Clear();
        _itemsCount = 0;

        if (!invalidateRefreshKey) return;
        _lastRefreshRegion = null;
        _lastRefreshKey = null;
        _hasRefreshKey = false;
    }

    /// <summary>按是否有内容显示或隐藏整个区域。</summary>
    private void SetContainerActive(bool active)
    {
        if (gameObject.activeSelf != active) gameObject.SetActive(active);
    }

    /// <summary>底栏销毁时清理当前条目和所有暂存的图标、旗帜。</summary>
    private void OnDestroy()
    {
        ClearSpawned();
        DestroyPool(_iconPool);
        DestroyPool(_geoRegionBannerPool);
        DestroyPool(_cityBannerPool);
    }

    /// <summary>销毁一类不再使用的暂存条目。</summary>
    private static void DestroyPool<T>(Stack<T> pool) where T : Component
    {
        while (pool.Count > 0)
        {
            T component = pool.Pop();
            if (component != null) Object.Destroy(component.gameObject);
        }
    }

    /// <summary>停止条目尚未结束的移动动画，避免刷新后继续滑向旧位置。</summary>
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

    /// <summary>根据条目数量、行列和间距计算内容完整显示所需的宽高。</summary>
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

    /// <summary>将内容所需尺寸应用到当前区域和整个底栏，避免图标被裁切。</summary>
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

    /// <summary>为旗帜设置固定占位尺寸和居中位置，使多枚旗帜排列整齐。</summary>
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

    /// <summary>按子类要求把条目区域固定在左侧中部或左上角。</summary>
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

    /// <summary>根据四周留白计算条目区域的位置。</summary>
    private Vector2 GetGridAnchoredPosition()
    {
        return AnchorGridToTop
            ? new Vector2(LeftPadding, -TopPadding)
            : new Vector2(LeftPadding, (BottomPadding - TopPadding) * 0.5f);
    }

    /// <summary>向上寻找整个选中底栏条目，以便内容变宽或变高时同步扩展外层。</summary>
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

    /// <summary>读取一个界面区域当前可用宽度。</summary>
    private static float GetRectWidth(RectTransform rect)
    {
        if (rect == null) return 0f;
        float width = rect.rect.width;
        if (width > 0f) return width;
        return Mathf.Max(0f, rect.sizeDelta.x);
    }

    /// <summary>扩展界面区域到所需宽高，不缩小原版已有高度。</summary>
    private static void SetRectSize(RectTransform rect, float width, float height)
    {
        if (rect == null) return;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

        float currentHeight = Mathf.Max(rect.rect.height, rect.sizeDelta.y);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(currentHeight, height));
    }

    /// <summary>计算子区域最右侧相对外层的位置，用于确定底栏总宽度。</summary>
    private static float GetChildRightEdge(RectTransform parent, RectTransform child)
    {
        if (parent == null || child == null) return 0f;
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, child);
        return bounds.max.x - parent.rect.xMin;
    }
}
