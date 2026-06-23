using System;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.UI.Components;
using Cultiway.Utils.Extension;
using NeoModLoader.utils;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI
{
    public class GeoRegionWindow : WindowMetaGeneric<GeoRegion, GeoRegionData>
    {
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

            var meta_window = Manager.CreateMetaWindow<GeoRegionWindow, GeoRegion, GeoRegionData>(windowId);
            meta_window.SetDescendantsActiveByName(
                false,
                "Kingdom Icon",
                "Customization Icon");
            meta_window.SetupTabTitleContainer<GeoRegionWindow, GeoRegion, GeoRegionData>("tab_title_container_kingdom", "GeoRegion".Underscore(), "cultiway/icons/iconExtendGeoRegion", "cultiway/icons/iconExtendGeoRegion").name = "tab_title_container_geo_region";
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
            // 基础数据放在 Header 两侧，这里只保留空实现避免原版基类抛异常。
        }

        private void SetupGeoRegionPanels()
        {
            Transform headerTop = transform.Find("Background/Scroll View/Viewport/Header/header_top")
                                  ?? throw new InvalidOperationException("GeoRegionWindow 缺少原版 Header/header_top 节点");

            _headerPanel = headerTop.GetComponent<GeoRegionWindowHeaderPanel>() ?? headerTop.gameObject.AddComponent<GeoRegionWindowHeaderPanel>();
            _headerPanel.Initialize();

            Transform content = transform.Find("Background/Scroll View/Viewport/Content")
                                ?? throw new InvalidOperationException("GeoRegionWindow 缺少窗口 Content 节点");

            DestroyContentIfPresent(content, "content_meta");
            DestroyContentIfPresent(content, "content_geo_region_composition");
            DestroyContentIfPresent(content, "content_relations");
            DestroyContentIfPresent(content, "content_geo_region_relations");
            DestroyContentIfPresent(content, "content_geo_region_top");
            CollapseContentIfPresent(content, "content_stats");

            _detailsPanel = SetupDetailsPanel(content);

            Transform title = content.Find("tab_title_container_geo_region");
            int index = title != null ? title.GetSiblingIndex() + 1 : 0;
            _detailsPanel.transform.SetSiblingIndex(index);
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

        private static void DestroyContentIfPresent(Transform content, string name)
        {
            Transform child = content.Find(name);
            if (child == null) return;
            DestroyImmediate(child.gameObject);
        }

        private static void CollapseContentIfPresent(Transform content, string name)
        {
            Transform child = content.Find(name);
            if (child == null) return;

            child.gameObject.SetActive(false);
            LayoutElement layout = child.GetComponent<LayoutElement>() ?? child.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            layout.minHeight = 0f;
            layout.preferredHeight = 0f;
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
