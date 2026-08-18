using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine.UI;

namespace Cultiway.UI.Components
{
    /// <summary>地区列表窗口控制器，列出全部地区并提供年龄、声望、人口和面积排序。</summary>
    public class GeoRegionListComponent
        : ComponentListBase<GeoRegionListElement, GeoRegion, GeoRegionData, GeoRegionListComponent>
    {
        /// <summary>声明列表中的每一项都是地理区域。</summary>
        public override MetaType meta_type => MetaTypeExtend.GeoRegion.Back();

        /// <summary>注册地区列表窗口，并为顶部插图补充悬停说明。</summary>
        internal static void Init()
        {
            var windowId = WorldboxGame.ListWindows.GeoRegionList.id;

            // 补齐窗口登记信息，确保玩家能正常使用工具栏和返回按钮。
            EnsureWindowAsset(windowId, MetaTypeExtend.GeoRegion.Back().getAsset());

            var meta_window = Manager.CreateListMetaWindow(windowId, MetaTypeExtend.GeoRegion);
            var art_main = meta_window.transform.Find("Background/Scroll View/Viewport/Header/Illustration Background/Mask Illustration/Art Main");
            if (art_main.GetComponent<Button>() == null)
            {
                art_main.AddComponent<Button>();
            }
            art_main.GetComponent<Button>().OnHover(() =>
            {
                Tooltip.show(art_main.gameObject, WorldboxGame.Tooltips.RawTip.id, new TooltipData()
                {
                    tip_name = "AIGenerated"
                });
            });
        }

        /// <summary>补齐地区列表的窗口登记信息，使列表可以从菜单正常打开和返回。</summary>
        private static void EnsureWindowAsset(string windowId, MetaTypeAsset metaTypeAsset)
        {
            if (!AssetManager.window_library.has(windowId))
            {
                AssetManager.window_library.add(
                    new WindowAsset
                    {
                        id = windowId,
                        icon_path = "../../cultiway/icons/iconGeoRegion",
                        preload = false,
                        is_testable = false
                    }
                );
            }

            var windowAsset = AssetManager.window_library.get(windowId);
            if (windowAsset != null)
            {
                windowAsset.meta_type_asset = metaTypeAsset;
            }
        }

        /// <summary>创建排序按钮；玩家可按年龄、声望、人口或地区面积排列列表。</summary>
        public override void setupSortingTabs()
        {
            genericMetaSortByAge(new Comparison<GeoRegion>(sortByAge));
            genericMetaSortByRenown(new Comparison<GeoRegion>(sortByRenown));
            genericMetaSortByPopulation(
                new Comparison<GeoRegion>(
                    sortByPopulation
                )
            );

            _ = sorting_tab.tryAddButton(
                "ui/Icons/iconZones",
                "sort_by_area",
                new SortButtonAction(show),
                delegate
                {
                    current_sort = new Comparison<GeoRegion>(
                        sortByArea
                    );
                }
            );
        }

        /// <summary>按地块数量从大到小排列地区。</summary>
        private static int sortByArea(GeoRegion a, GeoRegion b)
        {
            return b.data.TileCount.CompareTo(a.data.TileCount);
        }
    }
}
