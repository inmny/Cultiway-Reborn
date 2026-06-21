using System;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
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

        internal static void Init()
        {
            var metaTypeAsset = WorldboxGame.MetaTypes.GeoRegion;
            if (metaTypeAsset == null) return;

            var windowId = metaTypeAsset.window_name;

            // 需要 WindowAsset，避免 WindowToolbar 等逻辑对 null 解引用
            EnsureWindowAsset(windowId, metaTypeAsset);

            var meta_window = Manager.CreateMetaWindow<GeoRegionWindow, GeoRegion, GeoRegionData>(windowId);
            meta_window.SetupTabTitleContainer<GeoRegionWindow, GeoRegion, GeoRegionData>("tab_title_container_kingdom", "GeoRegion".Underscore(), "cultiway/icons/iconExtendGeoRegion", "cultiway/icons/iconExtendGeoRegion").name = "tab_title_container_geo_region";
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
            // 这里必须 override，否则 StatsRowsContainer 会调用基类实现并抛 NotImplementedException
            var region = meta_object;
            if (region == null || region.isRekt()) return;

            var tilesCount = 0;
            if (!region.E.IsNull)
            {
                tilesCount = region.E.GetIncomingLinks<BelongToRelation>().Count;
            }

            // key 先占位，后续在 Locales 里补文本
            showStatRow("Cultiway.GeoRegion.Tiles".Underscore(), tilesCount, MetaType.None, -1L, null, null, null);
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
