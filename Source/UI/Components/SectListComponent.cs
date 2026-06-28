using System;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Components;

public class SectListComponent : ComponentListBase<SectListElement, Sect, SectData, SectListComponent>
{
    public override MetaType meta_type => MetaTypeExtend.Sect.Back();

    internal static void Init()
    {
        string windowId = WorldboxGame.ListWindows.SectList.id;

        EnsureWindowAsset(windowId, MetaTypeExtend.Sect.Back().getAsset());

        ListWindow metaWindow = Manager.CreateListMetaWindow(windowId, MetaTypeExtend.Sect);
        Transform artMain = metaWindow.transform.Find("Background/Scroll View/Viewport/Header/Illustration Background/Mask Illustration/Art Main");
        if (artMain.GetComponent<Button>() == null)
        {
            artMain.AddComponent<Button>();
        }

        artMain.GetComponent<Button>().OnHover(() =>
        {
            Tooltip.show(artMain.gameObject, WorldboxGame.Tooltips.RawTip.id, new TooltipData
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
                    icon_path = "../../cultiway/icons/iconSect",
                    preload = false,
                    is_testable = false
                }
            );
        }

        WindowAsset windowAsset = AssetManager.window_library.get(windowId);
        if (windowAsset != null)
        {
            windowAsset.meta_type_asset = metaTypeAsset;
        }
    }

    public override void setupSortingTabs()
    {
        genericMetaSortByAge(new Comparison<Sect>(sortByAge));
        genericMetaSortByRenown(new Comparison<Sect>(SortByReputation));
        genericMetaSortByPopulation(new Comparison<Sect>(sortByPopulation));

        _ = sorting_tab.tryAddButton(
            "ui/Icons/iconBooks",
            "Cultiway.Sect.SortByCultibooks",
            new SortButtonAction(show),
            delegate
            {
                current_sort = new Comparison<Sect>(SortByCultibooks);
            }
        );
    }

    private static int SortByReputation(Sect left, Sect right)
    {
        return right.data.Reputation.CompareTo(left.data.Reputation);
    }

    private static int SortByCultibooks(Sect left, Sect right)
    {
        return right.data.CultibookCount.CompareTo(left.data.CultibookCount);
    }
}
