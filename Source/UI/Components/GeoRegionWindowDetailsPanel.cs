using Cultiway.Utils;
using System;
using System.Collections.Generic;
using Cultiway.Core;
using LayoutGroupExt;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI.Components;

/// <summary>地区详情窗口的组成与关系区域，显示其中的国家城市，以及重叠和相邻地区。</summary>
internal class GeoRegionWindowDetailsPanel : MonoBehaviour
{
    // 窗口最多展示八个国家或城市，以及各四个重叠、相邻地区。
    private const int MaxCompositionItems = 8;
    private const int MaxRelationItems = 4;
    // 整体区域、组成区和关系区的固定高度。
    private const float PreferredHeight = 84f;
    private const float CompositionSectionHeight = 46f;
    private const float RelationSectionHeight = 24f;
    // 关系图标、组成旗帜的占位尺寸和旗帜显示比例。
    private const float IconCellSize = 18f;
    private const float BannerCellSize = 40f;
    private const float CompositionBannerScale = 0.85f;

    // 详情内容根节点，以及国家城市、重叠地区和相邻地区三个区域。
    private RectTransform _root;
    private Section _compositionSection;
    private Section _overlappingSection;
    private Section _adjacentSection;
    // 上次显示的地区及三类内容变化编号，用于只重画有变化的区域。
    private GeoRegion _lastRegion;
    private int _lastCompositionRevision;
    private int _lastOverlappingRevision;
    private int _lastAdjacentRevision;

    /// <summary>首次打开时创建国家城市、重叠地区和相邻地区三个展示区域。</summary>
    internal void Initialize()
    {
        if (_root != null) return;

        RemoveOriginalWindowContent();
        ConfigureHost();
        CreateRoot();
        _compositionSection = AddSection(
            _root,
            "GeoRegionCompositionSection",
            "Cultiway.GeoRegion.Window.Composition",
            CompositionSectionHeight,
            new Vector2(BannerCellSize, BannerCellSize));

        Transform relationsRow = CreateRelationsRow();
        _overlappingSection = AddSection(
            relationsRow,
            "GeoRegionOverlappingSection",
            "Cultiway.SelectedGeoRegion.Related",
            RelationSectionHeight,
            new Vector2(IconCellSize, IconCellSize));
        _adjacentSection = AddSection(
            relationsRow,
            "GeoRegionAdjacentSection",
            "Cultiway.SelectedGeoRegion.Adjacent",
            RelationSectionHeight,
            new Vector2(IconCellSize, IconCellSize));
    }

    /// <summary>
    /// 地区内容变化时分别更新受影响的区域；地区无效时清空全部条目。
    /// </summary>
    internal void Refresh(GeoRegion region)
    {
        Initialize();
        if (region == null || region.isRekt())
        {
            ClearSection(_compositionSection);
            ClearSection(_overlappingSection);
            ClearSection(_adjacentSection);
            _lastRegion = null;
            return;
        }

        bool regionChanged = !ReferenceEquals(_lastRegion, region);
        int compositionRevision = CombineRevisions(region.CrossLayerRevision, region.CompositionRevision);
        bool layoutDirty = false;

        if (regionChanged || _lastCompositionRevision != compositionRevision)
        {
            ClearSection(_compositionSection);
            FillComposition(_compositionSection.ContentRoot, region);
            _lastCompositionRevision = compositionRevision;
            layoutDirty = true;
        }
        if (regionChanged || _lastOverlappingRevision != region.CrossLayerRevision)
        {
            ClearSection(_overlappingSection);
            FillOverlappingRelations(_overlappingSection.ContentRoot, region);
            _lastOverlappingRevision = region.CrossLayerRevision;
            layoutDirty = true;
        }
        if (regionChanged || _lastAdjacentRevision != region.AdjacencyRevision)
        {
            ClearSection(_adjacentSection);
            FillAdjacentRelations(_adjacentSection.ContentRoot, region);
            _lastAdjacentRevision = region.AdjacencyRevision;
            layoutDirty = true;
        }

        _lastRegion = region;
        if (!layoutDirty) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(_root);
        LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
    }

    /// <summary>把两类变化编号合成一个值，用于判断国家城市展示是否需要更新。</summary>
    private static int CombineRevisions(int left, int right)
    {
        unchecked
        {
            return (left * 397) ^ right;
        }
    }

    /// <summary>先列出地区内的国家，再用城市补足空位，总数最多八个。</summary>
    private void FillComposition(Transform parent, GeoRegion region)
    {
        GeoRegionManager manager = WorldboxGame.I.GeoRegions;
        int added = 0;

        List<Kingdom> kingdoms = manager.GetKingdomsInRegion(region, MaxCompositionItems);
        for (int i = 0; i < kingdoms.Count && added < MaxCompositionItems; i++)
        {
            AddKingdomBanner(parent, kingdoms[i]);
            added++;
        }

        if (added < MaxCompositionItems)
        {
            List<City> cities = manager.GetCitiesInRegion(region, MaxCompositionItems - added);
            for (int i = 0; i < cities.Count && added < MaxCompositionItems; i++)
            {
                AddCityBanner(parent, cities[i]);
                added++;
            }
        }

        if (added == 0)
        {
            return;
        }
    }

    /// <summary>列出最多四个与当前地区范围重叠的其他地区。</summary>
    private void FillOverlappingRelations(Transform parent, GeoRegion region)
    {
        GeoRegionManager manager = WorldboxGame.I.GeoRegions;

        List<GeoRegion> overlappingRegions = manager.GetOverlappingRegions(region, MaxRelationItems);
        for (int i = 0; i < overlappingRegions.Count; i++)
        {
            AddRegionIcon(parent, overlappingRegions[i]);
        }
    }

    /// <summary>列出最多四个与当前地区同层相邻的其他地区。</summary>
    private void FillAdjacentRelations(Transform parent, GeoRegion region)
    {
        GeoRegionManager manager = WorldboxGame.I.GeoRegions;

        List<GeoRegion> adjacentRegions = manager.GetAdjacentRegions(region, region.data.Layer, MaxRelationItems);
        for (int i = 0; i < adjacentRegions.Count; i++)
        {
            AddRegionIcon(parent, adjacentRegions[i]);
        }
    }

    /// <summary>添加关系地区图标；点击打开目标详情，悬停在地图上突出显示。</summary>
    private static void AddRegionIcon(Transform parent, GeoRegion target)
    {
        GeoRegionSelectedInfoIcon icon = GeoRegionSelectedInfoIcon.Create(parent, "GeoRegionWindowRegionIcon", IconCellSize);
        icon.Setup(
            target.GetCategory().GetSpriteIcon(),
            "",
            "",
            null,
            () => AssetManager.meta_type_library.getAsset(target.meta_type).selectAndInspect(target, false, true, false));
        icon.SetGeoRegionTooltip(target);
        icon.SetHoverGeoRegion(target);
    }

    /// <summary>添加地区内国家的旗帜，点击可查看该国家。</summary>
    private static void AddKingdomBanner(Transform parent, Kingdom kingdom)
    {
        KingdomBanner prefab = Resources.Load<KingdomBanner>("ui/PrefabBannerKingdom");
        if (prefab == null)
        {
            throw new InvalidOperationException("找不到原版国家 banner prefab: ui/PrefabBannerKingdom");
        }

        KingdomBanner banner = Object.Instantiate(prefab, parent);
        SetupCompositionBanner(banner, kingdom);
    }

    /// <summary>添加地区内城市的旗帜，点击可查看该城市。</summary>
    private static void AddCityBanner(Transform parent, City city)
    {
        CityBanner prefab = Resources.Load<CityBanner>("ui/PrefabBannerCity");
        if (prefab == null)
        {
            throw new InvalidOperationException("找不到原版城市 banner prefab: ui/PrefabBannerCity");
        }

        CityBanner banner = Object.Instantiate(prefab, parent);
        SetupCompositionBanner(banner, city);
    }

    /// <summary>载入国家或城市旗帜，并允许玩家点击进入对应详情。</summary>
    private static void SetupCompositionBanner<TMeta, TData>(BannerGeneric<TMeta, TData> banner, TMeta meta)
        where TMeta : CoreSystemObject<TData>
        where TData : BaseSystemData
    {
        banner.gameObject.SetActive(true);
        banner.transform.localScale = Vector3.one * CompositionBannerScale;
        banner.enable_default_click = false;
        banner.enable_tab_show_click = true;
        banner.load(meta);
        SetupCompositionBannerLayout(banner.gameObject);
    }

    /// <summary>固定国家和城市旗帜的占位尺寸，使最多八个条目排列稳定。</summary>
    private static void SetupCompositionBannerLayout(GameObject bannerObject)
    {
        RectTransform rect = bannerObject.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(BannerCellSize, BannerCellSize);
        }

        LayoutElement layout = bannerObject.GetComponent<LayoutElement>() ?? bannerObject.AddComponent<LayoutElement>();
        layout.ignoreLayout = false;
        layout.minWidth = BannerCellSize;
        layout.preferredWidth = BannerCellSize;
        layout.minHeight = BannerCellSize;
        layout.preferredHeight = BannerCellSize;
    }

    /// <summary>创建一个带淡色标题和单行条目区的详情小节。</summary>
    private static Section AddSection(Transform parent, string name, string titleKey, float height, Vector2 cellSize)
    {
        GameObject sectionObject = new(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        sectionObject.transform.SetParent(parent, false);
        sectionObject.transform.localScale = Vector3.one;

        Image image = sectionObject.GetComponent<Image>();
        image.sprite = UiResources.GetSprite(UiResources.WindowInner);
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.raycastTarget = false;

        LayoutElement layout = sectionObject.GetComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
        layout.flexibleWidth = 1f;

        CreateBackgroundTitle(sectionObject.transform, titleKey);
        Transform content = CreateSectionContent(sectionObject.transform, cellSize);
        return new Section(content);
    }

    /// <summary>创建不会遮挡点击的本地化淡色背景标题。</summary>
    private static void CreateBackgroundTitle(Transform parent, string titleKey)
    {
        GameObject titleObject = new("GeoRegionWindowSectionTitle", typeof(RectTransform), typeof(Text), typeof(Shadow), typeof(LayoutElement));
        titleObject.transform.SetParent(parent, false);
        titleObject.transform.localScale = Vector3.one;

        RectTransform rect = titleObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        LayoutElement layout = titleObject.GetComponent<LayoutElement>();
        layout.ignoreLayout = true;

        Text text = titleObject.GetComponent<Text>();
        text.raycastTarget = false;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Cultiway.UI.UiTheme.Current.Font;
        text.fontSize = 10;
        text.fontStyle = FontStyle.Bold;
        text.color = new Color(1f, 0.60730225f, 0.1102941f, 0.18039216f);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 1;
        text.resizeTextMaxSize = 10;

        Shadow shadow = titleObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.18f);
        shadow.effectDistance = new Vector2(1f, -1f);

        LocalizedText localizedText = titleObject.AddComponent<LocalizedText>();
        localizedText.setKeyAndUpdate(titleKey);
    }

    /// <summary>创建单行条目位置，使图标或旗帜在可用宽度内均匀排列。</summary>
    private static Transform CreateSectionContent(Transform parent, Vector2 cellSize)
    {
        GameObject contentObject = new("GeoRegionWindowSectionContent", typeof(RectTransform), typeof(GridLayoutGroupExtended), typeof(FlexibleOneRowGrid));
        contentObject.transform.SetParent(parent, false);
        contentObject.transform.localScale = Vector3.one;

        RectTransform rect = contentObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(5f, 0f);
        rect.offsetMax = new Vector2(-5f, 0f);

        GridLayoutGroupExtended layout = contentObject.GetComponent<GridLayoutGroupExtended>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.startCorner = GridLayoutGroupExtended.Corner.UpperLeft;
        layout.startAxis = GridLayoutGroupExtended.Axis.Horizontal;
        layout.cellSize = cellSize;
        layout.spacing = Vector2.zero;
        layout.constraint = GridLayoutGroupExtended.Constraint.FixedRowCount;
        layout.constraintCount = 1;
        layout.moveDuration = 0.15f;
        layout.delayItems = 1;

        FlexibleOneRowGrid flexible = contentObject.GetComponent<FlexibleOneRowGrid>();
        flexible.bonus_spacing_x = 0;
        return contentObject.transform;
    }

    /// <summary>移除被改造成地区详情区域的原版内容和无关组件。</summary>
    private void RemoveOriginalWindowContent()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(transform.GetChild(i).gameObject);
        }

        Component[] components = GetComponents<Component>();
        for (int i = components.Length - 1; i >= 0; i--)
        {
            Component component = components[i];
            if (component == null ||
                component == this ||
                component is RectTransform ||
                component is CanvasRenderer ||
                component is Image ||
                component is LayoutElement)
            {
                continue;
            }

            Object.DestroyImmediate(component);
        }
    }

    /// <summary>设置详情区域的透明背景和固定高度，不阻挡窗口中的点击。</summary>
    private void ConfigureHost()
    {
        Image background = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        background.sprite = null;
        background.color = Color.clear;
        background.raycastTarget = false;

        LayoutElement layout = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
        layout.ignoreLayout = false;
        layout.minHeight = PreferredHeight;
        layout.preferredHeight = PreferredHeight;
        layout.flexibleWidth = 1f;
    }

    /// <summary>创建上下排列的详情内容根节点。</summary>
    private void CreateRoot()
    {
        GameObject rootObject = new("GeoRegionDetailsRoot", typeof(RectTransform), typeof(VerticalLayoutGroup));
        rootObject.transform.SetParent(transform, false);
        rootObject.transform.localScale = Vector3.one;

        _root = rootObject.GetComponent<RectTransform>();
        _root.anchorMin = Vector2.zero;
        _root.anchorMax = Vector2.one;
        _root.offsetMin = Vector2.zero;
        _root.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = rootObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(5, 5, 3, 3);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    /// <summary>创建横向关系行，让重叠地区和相邻地区并排显示。</summary>
    private Transform CreateRelationsRow()
    {
        GameObject rowObject = new("GeoRegionRelationsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowObject.transform.SetParent(_root, false);
        rowObject.transform.localScale = Vector3.one;

        RectTransform rect = rowObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        LayoutElement rowLayout = rowObject.GetComponent<LayoutElement>();
        rowLayout.minHeight = RelationSectionHeight;
        rowLayout.preferredHeight = RelationSectionHeight;
        rowLayout.flexibleWidth = 1f;

        HorizontalLayoutGroup layout = rowObject.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 5f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        return rowObject.transform;
    }

    /// <summary>删除某一小节的旧条目，为重新显示最新名单做准备。</summary>
    private static void ClearSection(Section section)
    {
        if (section?.ContentRoot == null) return;

        for (int i = section.ContentRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = section.ContentRoot.GetChild(i).gameObject;
            child.SetActive(false);
            Object.Destroy(child);
        }
    }

    /// <summary>保存一个详情小节实际放置图标或旗帜的位置。</summary>
    private class Section
    {
        /// <summary>小节中承载玩家可见条目的节点。</summary>
        internal readonly Transform ContentRoot;

        /// <summary>记录新建小节的条目位置。</summary>
        internal Section(Transform contentRoot)
        {
            ContentRoot = contentRoot;
        }
    }
}
