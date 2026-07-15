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
    public class GeoRegionWindow : WindowMetaGeneric<GeoRegion, GeoRegionData>
    {
        private const string GeoRegionTitleIconPath = "cultiway/icons/iconExtendGeoRegion";
        private const string StatsOverviewTitleName = "geo_region_overview_title";

        public override MetaType meta_type => MetaTypeExtend.GeoRegion.Back();
        public override GeoRegion meta_object => WorldboxGame.I.SelectedGeoRegion;
        private Image _raceTopIcon1;
        private Image _raceTopIcon2;
        private GeoRegionWindowHeaderPanel _headerPanel;
        private GeoRegionWindowDetailsPanel _detailsPanel;

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

        public override void startShowingWindow()
        {
            base.startShowingWindow();
            RefreshGeoRegionPanels();
        }

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

        public override IEnumerable<Actor> getInterestingUnitsList()
        {
            var region = meta_object;
            if (region == null || region.isRekt()) return Array.Empty<Actor>();

            return region.getUnits();
        }

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

        private static void RemoveStatsTabTitle(Transform statsContent)
        {
            Transform oldTitle = statsContent.Find("tab_title_container_geo_region_overview");
            if (oldTitle != null)
            {
                UnityEngine.Object.DestroyImmediate(oldTitle.gameObject);
            }
        }

        private static void RemoveStatsOverviewTitleChild(Transform statsContent)
        {
            Transform oldTitle = statsContent.Find(StatsOverviewTitleName);
            if (oldTitle != null)
            {
                UnityEngine.Object.DestroyImmediate(oldTitle.gameObject);
            }
        }

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

        private void RefreshGeoRegionPanels()
        {
            GeoRegion region = meta_object;
            _headerPanel?.Refresh(region);
            _detailsPanel?.Refresh(region);
        }

        private void CacheRaceTopIcons()
        {
            _raceTopIcon1 ??= RequireRaceTopIcon("Background/RaceIcon");
            _raceTopIcon2 ??= RequireRaceTopIcon("Background/Container/RaceIcon");
        }

        private Image RequireRaceTopIcon(string path)
        {
            var iconTransform = transform.Find(path)
                                ?? throw new InvalidOperationException($"GeoRegionWindow 缺少原版种族图标节点: {path}");
            return iconTransform.GetComponent<Image>()
                   ?? throw new InvalidOperationException($"GeoRegionWindow 原版种族图标节点缺少 Image: {path}");
        }

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
