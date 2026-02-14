using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine.UI;

namespace Cultiway.UI.Components
{
    public class GeoRegionListComponent
        : ComponentListBase<GeoRegionListElement, GeoRegion, GeoRegionData, GeoRegionListComponent>
    {
        public override MetaType meta_type => MetaTypeExtend.GeoRegion.Back();

        internal static void Init()
        {
            var windowId = WorldboxGame.ListWindows.GeoRegionList.id;

            // 需要 WindowAsset，避免 WindowToolbar 等逻辑对 null 解引用
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

        private static int sortByArea(GeoRegion a, GeoRegion b)
        {
            return b.E
                .GetIncomingLinks<BelongToRelation>()
                .Count()
                .CompareTo(a.E.GetIncomingLinks<BelongToRelation>().Count());
        }
    }
}
