using Cultiway.Utils;
using System;
using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.UI.Components;
using Cultiway.Utils.Extension;
using NeoModLoader.utils;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI
{
    /// <summary>
    /// 地区详情窗口，集中显示地区图标、类别、位置、人口、城市国家以及相邻和重叠地区。
    /// </summary>
    public class GeoRegionWindow : WindowMetaGeneric<GeoRegion, GeoRegionData>
    {
        // 窗口标题使用的地区图标，以及统计概览标题节点名称。
        private const string GeoRegionTitleIconPath = "cultiway/icons/iconExtendGeoRegion";
        private const string StatsOverviewTitleName = "geo_region_overview_title";

        /// <summary>声明窗口展示的是地理区域。</summary>
        public override MetaType meta_type => MetaTypeExtend.GeoRegion.Back();
        /// <summary>当前由玩家选中并在窗口中查看的地区。</summary>
        public override GeoRegion meta_object => WorldboxGame.I.SelectedGeoRegion;
        // 原版窗口标题两处图标，会替换为当前地区类别图标。
        private Image _raceTopIcon1;
        private Image _raceTopIcon2;
        // 顶部概览与地区组成、关系区域。
        private GeoRegionWindowHeaderPanel _headerPanel;
        private GeoRegionWindowDetailsPanel _detailsPanel;
        // 上次显示的地区及各类变化编号，用于只更新真正发生变化的内容。
        private GeoRegion _lastRevisionRegion;
        private int _lastPresentationRevision = -1;
        private int _lastGeometryRevision = -1;
        private int _lastAdjacencyRevision = -1;
        private int _lastCrossLayerRevision = -1;
        private int _lastCompositionRevision = -1;
        private int _lastStatsDirtyVersion = -1;

        /// <summary>注册地区详情窗口，准备标题、顶部概览、组成关系和分析页。</summary>
        internal static void Init()
        {
            var metaTypeAsset = WorldboxGame.MetaTypes.GeoRegion;
            if (metaTypeAsset == null) return;

            var windowId = metaTypeAsset.window_name;

            // 需要 WindowAsset，避免 WindowToolbar 等逻辑对 null 解引用
            EnsureWindowAsset(windowId, metaTypeAsset);

            var meta_window = Manager.CreateMetaWindow<GeoRegionWindow, GeoRegion, GeoRegionData>(
                windowId,
                "Interesting People",
                "Pyramid",
                "Statistics");
            meta_window.SetDescendantsActiveByName(
                false,
                "Kingdom Icon",
                "Customization Icon");
            meta_window.SetupTabTitleContainer<GeoRegionWindow, GeoRegion, GeoRegionData>("tab_title_container_kingdom", "GeoRegion".Underscore(), GeoRegionTitleIconPath, GeoRegionTitleIconPath).name = "tab_title_container_geo_region";
            meta_window.SetupGeoRegionPanels();
        }

        /// <summary>玩家打开窗口时立即显示当前地区全部信息，并记住本次内容状态。</summary>
        public override void startShowingWindow()
        {
            base.startShowingWindow();
            RefreshGeoRegionPanels();
            CaptureRuntimeRevisions(meta_object);
        }

        /// <summary>窗口打开期间检查地区变化，必要时重画旗帜、详情和统计数字。</summary>
        private void Update()
        {
            GeoRegion region = meta_object;
            if (region == null || region.isRekt()) return;
            if (!HasRuntimeRevisionChanged(region)) return;

            bool bannerChanged = !ReferenceEquals(_lastRevisionRegion, region) ||
                                 _lastPresentationRevision != region.PresentationRevision ||
                                 _lastGeometryRevision != region.GeometryRevision;
            if (bannerChanged)
            {
                reloadBanner();
                showTopPartInformation();
            }

            RefreshGeoRegionPanels();
            updateStatsRows();
            CaptureRuntimeRevisions(region);
        }

        /// <summary>更新窗口标题区域，让两处图标都显示当前地区的类别。</summary>
        public override void showTopPartInformation()
        {
            base.showTopPartInformation();

            var region = meta_object;
            if (region == null || region.isRekt()) return;

            CacheRaceTopIcons();
            var typeIcon = region.GetCategory().GetSpriteIcon();
            if (_raceTopIcon1 != null) _raceTopIcon1.sprite = typeIcon;
            if (_raceTopIcon2 != null) _raceTopIcon2.sprite = typeIcon;
        }

        /// <summary>填充概览统计，玩家可看到类别、层级、面积、人口、城市国家、关系、中心和年龄。</summary>
        public override void showStatsRows()
        {
            var region = meta_object;
            if (region == null || region.isRekt()) return;

            GeoRegionManager manager = WorldboxGame.I.GeoRegions;
            GeoRegionAsset category = region.GetCategory();
            List<Kingdom> kingdoms = manager.GetKingdomsInRegion(region, int.MaxValue);
            List<City> cities = manager.GetCitiesInRegion(region, int.MaxValue);
            List<GeoRegion> overlapping = manager.GetOverlappingRegions(region, int.MaxValue);
            List<GeoRegion> adjacent = manager.GetAdjacentRegions(region, region.data.Layer, int.MaxValue);

            showStatRow("Cultiway.GeoRegion.Category", category.GetDisplayName(), MetaType.None, -1L, "iconWorldInfo");
            showStatRow("Cultiway.GeoRegion.Layer", GeoRegionSelectedTagsContainer.FormatLayer(region.data.Layer), MetaType.None, -1L, "iconWorldInfo");
            showStatRow("Cultiway.GeoRegion.Tiles", region.data.TileCount, MetaType.None, -1L, "iconZones");
            showStatRow("Cultiway.GeoRegion.Population", region.countUnits(), MetaType.None, -1L, "iconPopulation");
            showStatRow("Cultiway.GeoRegion.Kingdoms", kingdoms.Count, MetaType.None, -1L, "iconKingdomList");
            showStatRow("Cultiway.GeoRegion.Cities", cities.Count, MetaType.None, -1L, "iconCity");
            showStatRow("Cultiway.GeoRegion.Overlapping", overlapping.Count, MetaType.None, -1L, "iconZones");
            showStatRow("Cultiway.GeoRegion.Adjacent", adjacent.Count, MetaType.None, -1L, "iconAllianceZones");
            showStatRow("Cultiway.GeoRegion.Center", $"{region.data.CenterX}, {region.data.CenterY}", MetaType.None, -1L, "iconCityZones");
            showStatRow("Cultiway.GeoRegion.Age", region.getAge(), MetaType.None, -1L, "iconAge");
        }

        /// <summary>返回地区内的角色，供“有趣角色”页面列出。</summary>
        public override IEnumerable<Actor> getInterestingUnitsList()
        {
            var region = meta_object;
            if (region == null || region.isRekt()) return Array.Empty<Actor>();

            return region.getUnits();
        }

        /// <summary>把原版窗口内容整理为地区专用的顶部、详情、概览和分析区域。</summary>
        private void SetupGeoRegionPanels()
        {
            Transform headerTop = transform.Find("Background/Scroll View/Viewport/Header/header_top")
                                  ?? throw new InvalidOperationException("GeoRegionWindow 缺少原版 Header/header_top 节点");

            _headerPanel = headerTop.GetComponent<GeoRegionWindowHeaderPanel>() ?? headerTop.gameObject.AddComponent<GeoRegionWindowHeaderPanel>();
            _headerPanel.Initialize();

            Transform content = transform.Find("Background/Scroll View/Viewport/Content")
                                ?? throw new InvalidOperationException("GeoRegionWindow 缺少窗口 Content 节点");

            content.DestroyIfPresent("content_meta");
            content.DestroyIfPresent("content_relations");

            _detailsPanel = SetupDetailsPanel(content);
            SetupStatsOverviewTitle(content);

            Transform title = content.Find("tab_title_container_geo_region");
            int index = title != null ? title.GetSiblingIndex() + 1 : 0;
            _detailsPanel.transform.SetSiblingIndex(index);

            SetupAnalysisTabs();
        }

        /// <summary>取得或创建地区组成与关系区域，并放入窗口正文。</summary>
        private static GeoRegionWindowDetailsPanel SetupDetailsPanel(Transform content)
        {
            const string sourceName = "content_more_icons";
            const string panelName = "content_geo_region_details";

            Transform panelTransform = content.Find(panelName) ?? content.Find(sourceName);
            if (panelTransform == null)
            {
                GameObject panelObject = new(panelName, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                panelObject.transform.SetParent(content, false);
                panelObject.transform.localScale = Vector3.one;
                panelTransform = panelObject.transform;
            }

            panelTransform.name = panelName;
            GeoRegionWindowDetailsPanel panel = panelTransform.GetComponent<GeoRegionWindowDetailsPanel>() ?? panelTransform.gameObject.AddComponent<GeoRegionWindowDetailsPanel>();
            panel.Initialize();
            return panel;
        }

        /// <summary>把“概览”标题放到统计列表正上方，避免标题被包含在列表内部。</summary>
        private static void SetupStatsOverviewTitle(Transform content)
        {
            Transform statsContent = content.Find("content_stats")
                                     ?? throw new InvalidOperationException("GeoRegionWindow 缺少原版 content_stats 节点");
            RemoveStatsTabTitle(statsContent);
            RemoveStatsOverviewTitleChild(statsContent);

            Transform title = content.Find(StatsOverviewTitleName) ?? CreateStatsOverviewTitle(content);
            int statsIndex = statsContent.GetSiblingIndex();
            if (title.GetSiblingIndex() < statsIndex)
            {
                statsIndex--;
            }

            title.SetSiblingIndex(statsIndex);
            title.gameObject.SetActive(true);

            LocalizedText localizedTitle = title.GetComponent<LocalizedText>()
                                          ?? throw new InvalidOperationException("GeoRegionWindow 概览标题缺少 LocalizedText");
            localizedTitle.setKeyAndUpdate("overview");
        }

        /// <summary>移除旧版地区统计页遗留的重复标题。</summary>
        private static void RemoveStatsTabTitle(Transform statsContent)
        {
            Transform oldTitle = statsContent.Find("tab_title_container_geo_region_overview");
            if (oldTitle != null)
            {
                UnityEngine.Object.DestroyImmediate(oldTitle.gameObject);
            }
        }

        /// <summary>移除统计内容内部的旧概览标题，防止玩家看到两个标题。</summary>
        private static void RemoveStatsOverviewTitleChild(Transform statsContent)
        {
            Transform oldTitle = statsContent.Find(StatsOverviewTitleName);
            if (oldTitle != null)
            {
                UnityEngine.Object.DestroyImmediate(oldTitle.gameObject);
            }
        }

        /// <summary>创建位于统计列表上方的本地化“概览”标题。</summary>
        private static Transform CreateStatsOverviewTitle(Transform content)
        {
            GameObject titleObject = new(StatsOverviewTitleName, typeof(RectTransform), typeof(Text), typeof(Shadow), typeof(LocalizedText), typeof(LayoutElement));
            titleObject.transform.SetParent(content, false);
            titleObject.transform.localScale = Vector3.one;

            RectTransform rect = titleObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(192f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);

            Text text = titleObject.GetComponent<Text>();
            text.raycastTarget = false;
            text.font = Cultiway.UI.UiTheme.Current.Font;
            text.fontSize = 5;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 1;
            text.resizeTextMaxSize = 9;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;

            Shadow shadow = titleObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            shadow.effectDistance = new Vector2(0.5f, -0.5f);

            LayoutElement layout = titleObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 12f;
            layout.layoutPriority = 1;

            return titleObject.transform;
        }

        /// <summary>判断名称外观、边界关系、城市国家或统计数字是否在窗口打开期间发生变化。</summary>
        private bool HasRuntimeRevisionChanged(GeoRegion region)
        {
            return !ReferenceEquals(_lastRevisionRegion, region) ||
                   _lastPresentationRevision != region.PresentationRevision ||
                   _lastGeometryRevision != region.GeometryRevision ||
                   _lastAdjacencyRevision != region.AdjacencyRevision ||
                   _lastCrossLayerRevision != region.CrossLayerRevision ||
                   _lastCompositionRevision != region.CompositionRevision ||
                   _lastStatsDirtyVersion != region.getStatsDirtyVersion();
        }

        /// <summary>记住当前已显示的地区状态，供下一帧判断是否需要更新。</summary>
        private void CaptureRuntimeRevisions(GeoRegion region)
        {
            _lastRevisionRegion = region;
            if (region == null) return;

            _lastPresentationRevision = region.PresentationRevision;
            _lastGeometryRevision = region.GeometryRevision;
            _lastAdjacencyRevision = region.AdjacencyRevision;
            _lastCrossLayerRevision = region.CrossLayerRevision;
            _lastCompositionRevision = region.CompositionRevision;
            _lastStatsDirtyVersion = region.getStatsDirtyVersion();
        }

        /// <summary>用当前选中地区重新显示顶部概览和组成关系区域。</summary>
        private void RefreshGeoRegionPanels()
        {
            GeoRegion region = meta_object;
            _headerPanel?.Refresh(region);
            _detailsPanel?.Refresh(region);
        }

        /// <summary>找到原版窗口标题中的两处种族图标位置，后续用于显示地区类别。</summary>
        private void CacheRaceTopIcons()
        {
            _raceTopIcon1 ??= RequireRaceTopIcon("Background/RaceIcon");
            _raceTopIcon2 ??= RequireRaceTopIcon("Background/Container/RaceIcon");
        }

        /// <summary>按原版节点路径取得标题图标；窗口结构不完整时给出明确错误。</summary>
        private Image RequireRaceTopIcon(string path)
        {
            var iconTransform = transform.Find(path)
                                ?? throw new InvalidOperationException($"GeoRegionWindow 缺少原版种族图标节点: {path}");
            return iconTransform.GetComponent<Image>()
                   ?? throw new InvalidOperationException($"GeoRegionWindow 原版种族图标节点缺少 Image: {path}");
        }

        /// <summary>让有趣角色、人口金字塔、历史图表和统计列表都读取当前地区的数据。</summary>
        private void SetupAnalysisTabs()
        {
            foreach (InterestingPeopleTab tab in GetComponentsInChildren<InterestingPeopleTab>(true))
            {
                tab._interesting_people_window = this;
            }

            foreach (PopulationPyramidController controller in GetComponentsInChildren<PopulationPyramidController>(true))
            {
                controller._meta_type = MetaTypeExtend.GeoRegion.Back();
            }

            foreach (GraphController controller in GetComponentsInChildren<GraphController>(true))
            {
                controller._meta_type = MetaTypeExtend.GeoRegion.Back();
            }

            Transform statsContent = transform.Find("Background/Scroll View/Viewport/Content/content_stats");
            if (statsContent != null)
            {
                StatsRowsContainer statsRows = statsContent.GetComponent<StatsRowsContainer>();
                if (statsRows != null)
                {
                    statsRows.stats_window = this;
                }
            }
        }

        /// <summary>补齐地区窗口登记信息，使工具栏和返回按钮能正常打开、关闭该窗口。</summary>
        private static void EnsureWindowAsset(string windowId, MetaTypeAsset metaTypeAsset)
        {
            if (!AssetManager.window_library.has(windowId))
            {
                AssetManager.window_library.add(new WindowAsset
                {
                    id = windowId,
                    icon_path = "../../cultiway/icons/iconGeoRegion",
                    preload = false,
                    is_testable = false
                });
            }

            var windowAsset = AssetManager.window_library.get(windowId);
            if (windowAsset != null)
            {
                windowAsset.meta_type_asset = metaTypeAsset;
            }
        }
    }
}
