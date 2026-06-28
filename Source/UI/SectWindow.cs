using System;
using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

public class SectWindow : WindowMetaGeneric<Sect, SectData>
{
    private const string SectIconPath = "cultiway/icons/iconSect";

    public override MetaType meta_type => MetaTypeExtend.Sect.Back();
    public override Sect meta_object => WorldboxGame.I.SelectedSect;

    private SectBanner _banner;

    internal static void Init()
    {
        MetaTypeAsset metaTypeAsset = WorldboxGame.MetaTypes.Sect;
        if (metaTypeAsset == null) return;

        string windowId = metaTypeAsset.window_name;
        EnsureWindowAsset(windowId, metaTypeAsset);

        SectWindow metaWindow = Manager.CreateMetaWindow<SectWindow, Sect, SectData>(
            windowId,
            "Interesting People",
            "Statistics");
        metaWindow.SetDescendantsActiveByName(
            false,
            "Kingdom Icon",
            "Customization Icon");
        metaWindow.SetupTabTitleContainer<SectWindow, Sect, SectData>("tab_title_container_kingdom", "Sect".Underscore(), SectIconPath, SectIconPath).name = "tab_title_container_sect";
        metaWindow.SetupSectBanner();
    }

    public override void startShowingWindow()
    {
        base.startShowingWindow();
        RefreshSectBanner();
    }

    public override void showTopPartInformation()
    {
        base.showTopPartInformation();

        Sect sect = meta_object;
        if (sect == null || sect.isRekt()) return;

        if (species_icon != null)
        {
            species_icon.sprite = SpriteTextureLoader.getSprite(SectIconPath);
        }
    }

    public override void showStatsRows()
    {
        Sect sect = meta_object;
        if (sect == null || sect.isRekt()) return;

        tryShowPastNames();
        showStatRow("Cultiway.Sect.Founded", sect.getFoundedDate(), MetaType.None, -1L, "iconAge");
        showStatRow("Cultiway.Sect.Level", sect.data.Level, MetaType.None, -1L, "iconWorldInfo");
        showStatRow("Cultiway.Sect.Reputation", sect.data.Reputation, MetaType.None, -1L, "iconRenown");
        showStatRow("Cultiway.Sect.Members", sect.countUnits(), MetaType.None, -1L, "iconPopulation");
        showStatRow("adults", sect.countAdults(), MetaType.None, -1L, "iconMature");
        showStatRow("children", sect.countChildren(), MetaType.None, -1L, "iconChildren");
        showStatRow("Cultiway.Sect.Cultibooks", sect.data.CultibookCount, MetaType.None, -1L, "iconBooks");
        showStatRow("Cultiway.Sect.ElixirRecipes", sect.data.ElixirRecipeCount, MetaType.None, -1L, "iconKnowledge");
        showStatRow("Cultiway.Sect.Skillbooks", sect.data.SkillbookCount, MetaType.None, -1L, "iconKnowledge");
        ShowLeaderInfo(sect);
        ShowFounderInfo(sect);
        ShowHomeCityInfo(sect);
        ShowDoctrineInfo(sect);
    }

    public override IEnumerable<Actor> getInterestingUnitsList()
    {
        Sect sect = meta_object;
        return sect == null || sect.isRekt() ? Array.Empty<Actor>() : sect.getUnits();
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

    private void SetupSectBanner()
    {
        Transform bannerTransform = transform.Find("Background/Scroll View/Viewport/Header/header_top/BannerBackground/Container/Main Banner")
                                    ?? throw new InvalidOperationException("SectWindow 原版 Header 缺少 Main Banner 节点");

        _banner = bannerTransform.GetComponent<SectBanner>();
        if (_banner == null)
        {
            _banner = bannerTransform.gameObject.AddComponent<SectBanner>();
        }

        KingdomBanner oldBanner = bannerTransform.GetComponent<KingdomBanner>();
        if (oldBanner != null)
        {
            DestroyImmediate(oldBanner);
        }
    }

    private void RefreshSectBanner()
    {
        Sect sect = meta_object;
        if (_banner == null || sect == null || sect.isRekt()) return;

        _banner.load(sect);
    }

    private void ShowLeaderInfo(Sect sect)
    {
        Actor leader = sect.GetLeaderActor();
        if (leader != null && !leader.isRekt())
        {
            tryToShowActor("Cultiway.Sect.Leader", -1L, null, leader, "iconKings");
            return;
        }

        if (!string.IsNullOrEmpty(sect.data.LeaderActorName))
        {
            showStatRow("Cultiway.Sect.Leader", sect.data.LeaderActorName, MetaType.None, -1L, "iconKings");
        }
    }

    private void ShowFounderInfo(Sect sect)
    {
        if (sect.data.FounderActorID > 0)
        {
            Actor founder = World.world.units.get(sect.data.FounderActorID);
            if (founder != null && !founder.isRekt())
            {
                tryToShowActor("Cultiway.Sect.Founder", -1L, null, founder, "iconKings");
                return;
            }
        }

        if (!string.IsNullOrEmpty(sect.data.FounderActorName))
        {
            showStatRow("Cultiway.Sect.Founder", sect.data.FounderActorName, MetaType.None, -1L, "iconKings");
        }
    }

    private void ShowHomeCityInfo(Sect sect)
    {
        if (sect.data.HomeCityID > 0)
        {
            City city = World.world.cities.get(sect.data.HomeCityID);
            if (city != null && !city.isRekt())
            {
                tryToShowMetaCity("Cultiway.Sect.HomeCity", -1L, null, city);
                return;
            }
        }

        if (!string.IsNullOrEmpty(sect.data.HomeCityName))
        {
            showStatRow("Cultiway.Sect.HomeCity", sect.data.HomeCityName, MetaType.None, -1L, "iconCity");
        }
    }

    private void ShowDoctrineInfo(Sect sect)
    {
        string doctrineName = string.IsNullOrEmpty(sect.data.DoctrineCultibookName)
            ? "-"
            : sect.data.DoctrineCultibookName;
        showStatRow("Cultiway.Sect.DoctrineCultibook", doctrineName, MetaType.None, -1L, "iconBooks");
    }
}
