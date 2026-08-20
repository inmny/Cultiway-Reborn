using System;
using Cultiway.Const;
using Cultiway.Content.SpiritVeins;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.SpiritVeins;

/// <summary>灵脉总览列表，提供规模和布局数量排序。</summary>
public sealed class SpiritVeinListComponent
    : ComponentListBase<SpiritVeinListElement, SpiritVein, SpiritVeinData, SpiritVeinListComponent>
{
    public override MetaType meta_type => MetaTypeExtend.SpiritVein.Back();

    internal static void Init()
    {
        string windowId = WorldboxGame.ListWindows.SpiritVeinList.id;
        EnsureWindowAsset(windowId, MetaTypeExtend.SpiritVein.Back().getAsset());

        ListWindow window = Cultiway.UI.Manager.CreateListMetaWindow(windowId, MetaTypeExtend.SpiritVein);
        Transform artMain = window.transform.Find(
            "Background/Scroll View/Viewport/Header/Illustration Background/Mask Illustration/Art Main");
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

    public override void setupSortingTabs()
    {
        sorting_tab.tryAddButton(
            "ui/Icons/iconTileMountains",
            "Cultiway.SpiritVein.SortByScale",
            new SortButtonAction(show),
            delegate { current_sort = new Comparison<SpiritVein>(SortByScale); });
        sorting_tab.tryAddButton(
            "ui/Icons/iconZones",
            "Cultiway.SpiritVein.SortBySections",
            new SortButtonAction(show),
            delegate { current_sort = new Comparison<SpiritVein>(SortBySections); });
        sorting_tab.tryAddButton(
            "ui/Icons/iconCityZones",
            "Cultiway.SpiritVein.SortByGrounds",
            new SortButtonAction(show),
            delegate { current_sort = new Comparison<SpiritVein>(SortByGrounds); });
        sorting_tab.tryAddButton(
            "ui/Icons/iconForbiddenKnowledgeBlackholeEyeOpen",
            "Cultiway.SpiritVein.SortByEyes",
            new SortButtonAction(show),
            delegate { current_sort = new Comparison<SpiritVein>(SortByEyes); });
    }

    private static void EnsureWindowAsset(string windowId, MetaTypeAsset metaTypeAsset)
    {
        if (!AssetManager.window_library.has(windowId))
        {
            AssetManager.window_library.add(new WindowAsset
            {
                id = windowId,
                icon_path = "../../cultiway/icons/iconSpiritVein",
                preload = false,
                is_testable = false
            });
        }

        WindowAsset asset = AssetManager.window_library.get(windowId);
        if (asset != null)
        {
            asset.meta_type_asset = metaTypeAsset;
        }
    }

    private static int SortByScale(SpiritVein left, SpiritVein right)
    {
        return right.Scale.CompareTo(left.Scale);
    }

    private static int SortBySections(SpiritVein left, SpiritVein right)
    {
        return right.SectionIds.Count.CompareTo(left.SectionIds.Count);
    }

    private static int SortByGrounds(SpiritVein left, SpiritVein right)
    {
        return right.GroundIds.Count.CompareTo(left.GroundIds.Count);
    }

    private static int SortByEyes(SpiritVein left, SpiritVein right)
    {
        return right.EyeIds.Count.CompareTo(left.EyeIds.Count);
    }
}
