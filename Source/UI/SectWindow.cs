using System;
using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.UI.Components;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI;

public class SectWindow : WindowMetaGeneric<Sect, SectData>
{
    private const string SectIconPath = "cultiway/icons/iconSect";
    private const string StatsOverviewTitleName = "sect_overview_title";

    public override MetaType meta_type => MetaTypeExtend.Sect.Back();
    public override Sect meta_object => WorldboxGame.I.SelectedSect;

    private Image _raceTopIcon1;
    private Image _raceTopIcon2;
    private SectBanner _banner;
    private SectLeaderElement _leaderElement;

    internal static void Init()
    {
        MetaTypeAsset metaTypeAsset = WorldboxGame.MetaTypes.Sect;
        if (metaTypeAsset == null) return;

        string windowId = metaTypeAsset.window_name;
        EnsureWindowAsset(windowId, metaTypeAsset);

        SectWindow metaWindow = Manager.CreateMetaWindow<SectWindow, Sect, SectData>(
            windowId,
            "Interesting People",
            "Pyramid",
            "Statistics");
        metaWindow.SetDescendantsActiveByName(
            false,
            "Kingdom Icon",
            "Customization Icon");
        metaWindow.SetupTabTitleContainer<SectWindow, Sect, SectData>("tab_title_container_kingdom", "Sect".Underscore(), SectIconPath, SectIconPath).name = "tab_title_container_sect";
        metaWindow.SetupSectPanels();
    }

    public override void startShowingWindow()
    {
        base.startShowingWindow();
        RefreshSectBanner();
        _leaderElement?.refresh();
    }

    public override void showTopPartInformation()
    {
        base.showTopPartInformation();

        Sect sect = meta_object;
        if (sect == null || sect.isRekt()) return;

        CacheRaceTopIcons();
        Sprite icon = SpriteTextureLoader.getSprite(SectIconPath);
        if (species_icon != null) species_icon.sprite = icon;
        if (_raceTopIcon1 != null) _raceTopIcon1.sprite = icon;
        if (_raceTopIcon2 != null) _raceTopIcon2.sprite = icon;
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
        showStatRow("Cultiway.Sect.Territory", sect.GetTerritoryCount(), MetaType.None, -1L, "iconZones");
        showStatRow("adults", sect.countAdults(), MetaType.None, -1L, "iconAdults");
        showStatRow("children", sect.countChildren(), MetaType.None, -1L, "iconChildren");
        showStatRow("Cultiway.Sect.Cultibooks", sect.data.CultibookCount, MetaType.None, -1L, "iconBooks");
        showStatRow("Cultiway.Sect.ElixirRecipes", sect.data.ElixirRecipeCount, MetaType.None, -1L, "iconKnowledge");
        showStatRow("Cultiway.Sect.Skillbooks", sect.data.SkillbookCount, MetaType.None, -1L, "iconKnowledge");
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

    private void SetupSectPanels()
    {
        Transform headerTop = transform.Find("Background/Scroll View/Viewport/Header/header_top")
                              ?? throw new InvalidOperationException("SectWindow 缺少原版 Header/header_top 节点");

        SetupSectHeader(headerTop);

        Transform content = transform.Find("Background/Scroll View/Viewport/Content")
                            ?? throw new InvalidOperationException("SectWindow 缺少窗口 Content 节点");

        content.DestroyIfPresent("content_relations");
        content.DestroyIfPresent("content_more_icons");
        content.DestroyIfPresent("content_sect_details");
        content.DestroyIfPresent("content_meta");

        Transform leaderContent = content.Find("content_sect_leader") ?? CloneKingdomContent("content_king", content, "content_sect_leader");
        SetupLeaderContent(leaderContent);
        SetupStatsOverviewTitle(content);
        ReorderContent(content, leaderContent);

        SetupAnalysisTabs();
    }

    private void SetupSectHeader(Transform headerTop)
    {
        HideHeaderStats(headerTop);

        Transform bannerTransform = headerTop.Find("BannerBackground/Container/Main Banner")
                                    ?? throw new InvalidOperationException("SectWindow 原版 Header 缺少 Main Banner 节点");
        bannerTransform.gameObject.SetActive(true);

        _banner = bannerTransform.GetComponent<SectBanner>();
        if (_banner == null)
        {
            KingdomBanner kingdomBanner = bannerTransform.GetComponent<KingdomBanner>();
            _banner = bannerTransform.gameObject.AddComponent<SectBanner>();
            if (kingdomBanner != null)
            {
                kingdomBanner.CopyCompatibleSerializedFieldsTo(_banner);
                Object.DestroyImmediate(kingdomBanner);
            }
        }

        _banner.enable_default_click = false;
        _banner.enable_tab_show_click = true;
        SectBanner.HideVanillaBannerDecorations(bannerTransform);
        headerTop.Find("BannerBackground/Container/easter_egg_container")?.gameObject.SetActive(false);
    }

    private static void HideHeaderStats(Transform headerTop)
    {
        headerTop.Find("content_info_left")?.gameObject.SetActive(false);
        headerTop.Find("content_info_right")?.gameObject.SetActive(false);
    }

    private static Transform CloneKingdomContent(string sourceName, Transform targetParent, string targetName)
    {
        GameObject prefab = Resources.Load<GameObject>("windows/kingdom")
                            ?? throw new InvalidOperationException("找不到原版国家窗口 prefab: windows/kingdom");
        Transform source = prefab.transform.Find($"Background/Scroll View/Viewport/Content/{sourceName}")
                           ?? throw new InvalidOperationException($"原版国家窗口缺少 {sourceName} 节点");

        GameObject clone = Object.Instantiate(source.gameObject, targetParent, false);
        clone.name = targetName;
        clone.transform.localScale = Vector3.one;
        return clone.transform;
    }

    private void SetupLeaderContent(Transform leaderContent)
    {
        foreach (WindowMetaElementBase element in leaderContent.GetComponents<WindowMetaElementBase>())
        {
            Object.DestroyImmediate(element);
        }

        GameObject titleElement = leaderContent.Find("Title")?.gameObject
                                  ?? throw new InvalidOperationException("SectWindow 掌门栏缺少 Title 节点");
        PrefabUnitElement unitElement = leaderContent.GetComponentInChildren<PrefabUnitElement>(true)
                                        ?? throw new InvalidOperationException("SectWindow 掌门栏缺少 PrefabUnitElement");

        _leaderElement = leaderContent.GetComponent<SectLeaderElement>() ?? leaderContent.gameObject.AddComponent<SectLeaderElement>();
        _leaderElement.Initialize(titleElement, unitElement);
    }

    private static void ReorderContent(Transform content, Transform leaderContent)
    {
        Transform title = content.Find("tab_title_container_sect");
        Transform overviewTitle = content.Find(StatsOverviewTitleName);
        Transform statsContent = content.Find("content_stats");

        int index = title != null ? title.GetSiblingIndex() + 1 : 0;
        leaderContent.SetSiblingIndex(index++);
        overviewTitle?.SetSiblingIndex(index++);
        statsContent?.SetSiblingIndex(index);
    }

    private static void SetupStatsOverviewTitle(Transform content)
    {
        Transform statsContent = content.Find("content_stats")
                                 ?? throw new InvalidOperationException("SectWindow 缺少原版 content_stats 节点");
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
                                      ?? throw new InvalidOperationException("SectWindow 概览标题缺少 LocalizedText");
        localizedTitle.setKeyAndUpdate("overview");
    }

    private static void RemoveStatsTabTitle(Transform statsContent)
    {
        Transform oldTitle = statsContent.Find("tab_title_container_sect_overview");
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
        text.font = GetCurrentFont();
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

    private static Font GetCurrentFont()
    {
        return WorldboxGame.I?.CurrentFont ?? LocalizedTextManager.current_font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private void RefreshSectBanner()
    {
        Sect sect = meta_object;
        if (sect == null || sect.isRekt())
        {
            return;
        }

        if (_banner == null)
        {
            Transform bannerTransform = transform.Find("Background/Scroll View/Viewport/Header/header_top/BannerBackground/Container/Main Banner");
            _banner = bannerTransform != null ? bannerTransform.GetComponent<SectBanner>() : null;
        }

        _banner?.load(sect);
    }

    private void CacheRaceTopIcons()
    {
        _raceTopIcon1 ??= RequireRaceTopIcon("Background/RaceIcon");
        _raceTopIcon2 ??= RequireRaceTopIcon("Background/Container/RaceIcon");
    }

    private Image RequireRaceTopIcon(string path)
    {
        Transform iconTransform = transform.Find(path)
                                  ?? throw new InvalidOperationException($"SectWindow 缺少原版种族图标节点: {path}");
        return iconTransform.GetComponent<Image>()
               ?? throw new InvalidOperationException($"SectWindow 原版种族图标节点缺少 Image: {path}");
    }

    private void SetupAnalysisTabs()
    {
        foreach (InterestingPeopleTab tab in GetComponentsInChildren<InterestingPeopleTab>(true))
        {
            tab._interesting_people_window = this;
        }

        foreach (PopulationPyramidController controller in GetComponentsInChildren<PopulationPyramidController>(true))
        {
            controller._meta_type = MetaTypeExtend.Sect.Back();
        }

        foreach (GraphController controller in GetComponentsInChildren<GraphController>(true))
        {
            controller._meta_type = MetaTypeExtend.Sect.Back();
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

    private void ShowFounderInfo(Sect sect)
    {
        if (sect.data.FounderActorID > 0)
        {
            Actor founder = GetFounderActor(sect);
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
        City city = sect.GetHomeCity();
        if (city != null && !city.isRekt())
        {
            tryToShowMetaCity("Cultiway.Sect.HomeCity", -1L, null, city);
            return;
        }

        if (!string.IsNullOrEmpty(sect.data.HomeCityName))
        {
            showStatRow("Cultiway.Sect.HomeCity", sect.data.HomeCityName, MetaType.None, -1L, "iconCity");
        }
    }

    private void ShowDoctrineInfo(Sect sect)
    {
        CultibookAsset doctrine = sect.GetDoctrineCultibook();
        string doctrineName = doctrine != null
            ? doctrine.Name
            : string.IsNullOrEmpty(sect.data.DoctrineCultibookName)
                ? "-"
                : sect.data.DoctrineCultibookName;
        showStatRow("Cultiway.Sect.DoctrineCultibook", doctrineName, MetaType.None, -1L, "iconBooks");
    }

    private static Actor GetFounderActor(Sect sect)
    {
        if (sect.data.FounderActorID <= 0) return null;

        Actor actor = World.world.units.get(sect.data.FounderActorID);
        return actor.isRekt() ? null : actor;
    }
}
