using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Const;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.UI.Components;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI;

public class SectWindow : WindowMetaGeneric<Sect, SectData>, ITraitWindow<SectTrait, SectTraitButton>, IAugmentationsWindow<ITraitsEditor<SectTrait>>
{
    private const string SectIconPath = "cultiway/icons/iconSect";
    private const float TabContentWidth = 214f;
    private const float TabTitleWidth = 214f;

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
            preserved_tabs: new[] { "Traits", "Interesting People", "Pyramid", "Statistics" },
            preserved_content: new[] { "content_traits_editor" },
            preserved_header: new[] { "header_traits" });
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
        content.DestroyIfPresent("content_sect_details");
        content.DestroyIfPresent("content_meta");

        Transform leaderContent = content.Find("content_sect_leader") ?? CloneKingdomContent("content_king", content, "content_sect_leader");
        SetupLeaderContent(leaderContent);
        Transform statsIconsContent = SetupStatsIconsContent(content);
        ReorderContent(content, leaderContent, statsIconsContent);
        SetupMainInfoTabContent(content, leaderContent, statsIconsContent);
        SetupSectCustomTabs(content);
        SectTraitsEditor.Setup(this, content);
        SetupPersistentHeader(headerTop);

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

    private static Transform CloneCityContent(string sourceName, Transform targetParent, string targetName)
    {
        GameObject prefab = Resources.Load<GameObject>("windows/city")
                            ?? throw new InvalidOperationException("找不到原版城市窗口 prefab: windows/city");
        Transform source = prefab.transform.Find($"Background/Scroll View/Viewport/Content/{sourceName}")
                           ?? throw new InvalidOperationException($"原版城市窗口缺少 {sourceName} 节点");

        GameObject clone = Object.Instantiate(source.gameObject, targetParent, false);
        clone.name = targetName;
        clone.transform.localScale = Vector3.one;
        return clone.transform;
    }

    private static Transform CloneUnitGenealogyContent(Transform targetParent, string targetName)
    {
        GameObject prefab = Resources.Load<GameObject>("windows/unit")
                            ?? throw new InvalidOperationException("找不到原版人物窗口 prefab: windows/unit");
        UnitGenealogyElement source = prefab.GetComponentInChildren<UnitGenealogyElement>(true)
                                      ?? throw new InvalidOperationException("原版人物窗口缺少 UnitGenealogyElement");

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

    private static void ReorderContent(Transform content, Transform leaderContent, Transform statsIconsContent)
    {
        Transform title = content.Find("tab_title_container_sect");
        ConfigureTabTitleContainer(title);
        Transform statsContent = content.Find("content_stats");

        int index = title != null ? title.GetSiblingIndex() + 1 : 0;
        leaderContent.SetSiblingIndex(index++);
        statsIconsContent?.SetSiblingIndex(index++);
        statsContent?.SetSiblingIndex(index++);
    }

    private void SetupMainInfoTabContent(Transform content, Transform leaderContent, Transform statsIconsContent)
    {
        WindowMetaTab mainTab = GetMainInfoTab();
        if (mainTab == null) return;

        mainTab.tab_elements.Clear();
        AddTabContent(mainTab, content.Find("tab_title_container_sect"));
        AddTabContent(mainTab, leaderContent);
        AddTabContent(mainTab, statsIconsContent);
        AddTabContent(mainTab, content.Find("content_stats"));
    }

    private static Transform SetupStatsIconsContent(Transform content)
    {
        Transform statsIconsContent = content.Find("content_more_icons") ?? CloneKingdomContent("content_more_icons", content, "content_more_icons");
        statsIconsContent.gameObject.SetActive(true);

        foreach (WindowMetaElementBase element in statsIconsContent.GetComponents<WindowMetaElementBase>())
        {
            Object.DestroyImmediate(element);
        }

        foreach (StatsIconContainer container in statsIconsContent.GetComponents<StatsIconContainer>())
        {
            Object.DestroyImmediate(container);
        }

        foreach (LocalizedText title in statsIconsContent.GetComponentsInChildren<LocalizedText>(true))
        {
            if (title.name == "title_tab")
            {
                title.setKeyAndUpdate("overview");
            }
        }

        SetupSectStatsIcons(statsIconsContent);
        statsIconsContent.gameObject.AddComponent<SectStatsElement>();
        return statsIconsContent;
    }

    private static void SetupSectStatsIcons(Transform statsIconsContent)
    {
        Transform iconsRoot = statsIconsContent.Find("Icons")
                              ?? throw new InvalidOperationException("SectWindow content_more_icons 缺少 Icons 节点");
        Transform template = iconsRoot.Find("i_buildings")
                             ?? throw new InvalidOperationException("SectWindow content_more_icons 缺少 i_buildings 模板图标");

        iconsRoot.DestroyIfPresent("i_food");
        CreateSectStatsIcon(template, iconsRoot, SectStatsElement.IconCultibooks, "ui/Icons/iconBooks", "Cultiway.Sect.Cultibooks");
        CreateSectStatsIcon(template, iconsRoot, SectStatsElement.IconElixirRecipes, "cultiway/icons/iconElixirCauldron", "Cultiway.Sect.ElixirRecipes");
        CreateSectStatsIcon(template, iconsRoot, SectStatsElement.IconSkillbooks, "cultiway/icons/cultilog/log_cat_skill", "Cultiway.Sect.Skillbooks");
    }

    private static void CreateSectStatsIcon(Transform template, Transform parent, string name, string iconPath, string titleKey)
    {
        parent.DestroyIfPresent(name);

        GameObject iconObject = Object.Instantiate(template.gameObject, parent, false);
        iconObject.name = name;
        iconObject.SetActive(false);

        StatsIcon icon = iconObject.GetComponent<StatsIcon>()
                         ?? throw new InvalidOperationException($"SectWindow 统计图标模板缺少 StatsIcon: {name}");
        icon.getIcon().sprite = SpriteTextureLoader.getSprite(iconPath);

        TipButton tipButton = iconObject.GetComponent<TipButton>()
                              ?? throw new InvalidOperationException($"SectWindow 统计图标模板缺少 TipButton: {name}");
        tipButton.textOnClick = titleKey;
        tipButton.textOnClickDescription = "";
    }

    private WindowMetaTab GetMainInfoTab()
    {
        WindowMetaTab defaultTab = scroll_window?.tabs?.tab_default;
        if (defaultTab != null && scroll_window.tabs._tabs.Contains(defaultTab))
        {
            return defaultTab;
        }

        return scroll_window?.tabs?._tabs
            .OrderBy(tab => tab.transform.GetSiblingIndex())
            .FirstOrDefault(tab => tab != null && tab.gameObject.activeSelf && tab.getState());
    }

    private void AddTabContent(WindowMetaTab tab, Transform content)
    {
        if (tab == null || content == null) return;

        scroll_window.tabs.addTabContent(tab, content);
    }

    private void SetupPersistentHeader(Transform headerTop)
    {
        Transform header = headerTop.parent;
        if (header != null)
        {
            header.gameObject.SetActive(true);
        }

        headerTop.gameObject.SetActive(true);
        foreach (WindowMetaTab tab in GetComponentsInChildren<WindowMetaTab>(true))
        {
            tab.tab_elements.RemoveAll(element => IsHeaderTabElement(element, header, headerTop));
        }

        scroll_window.tabs.refillTabsWithContent();
    }

    private static bool IsHeaderTabElement(Transform element, Transform header, Transform headerTop)
    {
        return element == null
               || element == header
               || element == headerTop
               || element.IsChildOf(headerTop);
    }

    private void SetupSectCustomTabs(Transform content)
    {
        Transform personnelContent = CreatePersonnelContent(content);
        Transform scriptureTypeTabs = CreateScriptureTypeTabContainer();
        Transform scriptureContent = CreateScriptureContent(content, scriptureTypeTabs);

        SetupCustomTab("Sect Personnel", "Cultiway.Sect.Personnel", "Cultiway.Sect.PersonnelDescription", "ui/icons/iconInterestingPeople", personnelContent);
        WindowMetaTab scriptureTab = SetupCustomTab("Sect Scripture", "Cultiway.Sect.ScripturePavilion", "Cultiway.Sect.ScripturePavilionDescription", "ui/icons/iconBooks", scriptureContent);
        AddTabContent(scriptureTab, scriptureTypeTabs);
        scriptureTypeTabs.gameObject.SetActive(false);
        scroll_window.tabs.refillTabsWithContent();
        ReorderSectCustomTabs();
    }

    private Transform CreatePersonnelContent(Transform content)
    {
        Transform existing = content.Find("content_sect_personnel");
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        Transform personnelContent = CloneUnitGenealogyContent(content, "content_sect_personnel");
        personnelContent.gameObject.SetActive(false);
        foreach (WindowMetaElementBase element in personnelContent.GetComponents<WindowMetaElementBase>())
        {
            Object.DestroyImmediate(element);
        }

        SectPersonnelElement personnelElement = personnelContent.gameObject.AddComponent<SectPersonnelElement>();
        personnelElement.Initialize();
        return personnelContent;
    }

    private Transform CreateScriptureContent(Transform content, Transform typeTabsContainer)
    {
        Transform existing = content.Find("content_sect_scripture");
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        Transform scriptureContent = CloneCityContent("content_books", content, "content_sect_scripture");
        scriptureContent.gameObject.SetActive(false);
        ConfigureTabContentRoot(scriptureContent);

        foreach (WindowMetaElementBase element in scriptureContent.GetComponentsInChildren<WindowMetaElementBase>(true))
        {
            Object.DestroyImmediate(element);
        }

        SectScriptureElement scriptureElement = scriptureContent.gameObject.AddComponent<SectScriptureElement>();
        scriptureElement.Initialize(typeTabsContainer);
        return scriptureContent;
    }

    private Transform CreateScriptureTypeTabContainer()
    {
        Transform header = transform.Find("Background/Scroll View/Viewport/Header")
                           ?? throw new InvalidOperationException("SectWindow 缺少 Header 节点");
        Transform existing = header.Find("header_type_tab_container");
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject container = new("header_type_tab_container", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement), typeof(TabTogglesGroup), typeof(Image));
        container.transform.SetParent(header, false);
        container.transform.localScale = Vector3.one;

        Transform headerTop = header.Find("header_top");
        if (headerTop != null)
        {
            container.transform.SetSiblingIndex(headerTop.GetSiblingIndex() + 1);
        }

        Image backgroundImage = container.GetComponent<Image>();
        backgroundImage.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        backgroundImage.type = Image.Type.Sliced;

        HorizontalLayoutGroup layout = container.GetComponent<HorizontalLayoutGroup>();
        layout.childControlHeight = true;
        layout.childControlWidth = false;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 2f;

        ContentSizeFitter fitter = container.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement layoutElement = container.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = TabTitleWidth;
        layoutElement.preferredHeight = 18f;

        container.SetActive(false);
        return container.transform;
    }

    private static Transform CloneTitleContainer(Transform sourceContent, Transform targetParent, string targetName, string titleKey, string iconPath)
    {
        Transform sourceTitle = sourceContent.Find("tab_title_container_sect")
                                ?? sourceContent.Find("tab_title_container_kingdom")
                                ?? throw new InvalidOperationException("SectWindow 缺少可复用的标签标题节点");
        Transform title = Object.Instantiate(sourceTitle.gameObject, targetParent, false).transform;
        title.name = targetName;
        title.localScale = Vector3.one;
        SetupTitleContainer(title, titleKey, iconPath);
        ConfigureTabTitleContainer(title);
        return title;
    }

    private static void SetupTitleContainer(Transform title, string titleKey, string iconPath)
    {
        title.Find("title_tab")?.GetComponent<LocalizedText>()?.setKeyAndUpdate(titleKey);
        Sprite icon = SpriteTextureLoader.getSprite(iconPath);
        SetImageSprite(title.Find("icon_left"), icon);
        SetImageSprite(title.Find("icon_right"), icon);
    }

    private static void SetImageSprite(Transform transform, Sprite sprite)
    {
        Image image = transform?.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = sprite;
        }
    }

    private static void ConfigureTabContentRoot(Transform root)
    {
        RectTransform rect = root.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(TabContentWidth, rect.sizeDelta.y);
        }

        LayoutElement layout = root.GetComponent<LayoutElement>() ?? root.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = TabContentWidth;
        layout.preferredWidth = TabContentWidth;
        layout.flexibleWidth = 0f;
    }

    private static void ConfigureTabTitleContainer(Transform title)
    {
        if (title == null) return;

        title.gameObject.SetActive(true);
        RectTransform rect = title.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(TabTitleWidth, rect.sizeDelta.y);
        }

        LayoutElement layout = title.GetComponent<LayoutElement>() ?? title.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = TabTitleWidth;
        layout.preferredWidth = TabTitleWidth;
        layout.flexibleWidth = 0f;
        layout.layoutPriority = 1;
    }

    private WindowMetaTab SetupCustomTab(string tabName, string titleKey, string descriptionKey, string iconPath, Transform content)
    {
        Transform tabsContainer = transform.Find("Background/Tabs")
                                  ?? throw new InvalidOperationException("SectWindow 缺少 Background/Tabs 节点");
        WindowMetaTab tab = tabsContainer.GetComponentsInChildren<WindowMetaTab>(true).FirstOrDefault(item => item.name == tabName);
        if (tab == null)
        {
            WindowMetaTab source = tabsContainer.Find("Interesting People")?.GetComponent<WindowMetaTab>()
                                   ?? tabsContainer.GetComponentInChildren<WindowMetaTab>(true)
                                   ?? throw new InvalidOperationException("SectWindow 缺少可复用的原版标签按钮");
            tab = Object.Instantiate(source, tabsContainer);
            tab.name = tabName;
            tab.container = scroll_window.tabs;
            scroll_window.tabs._tabs.Add(tab);
        }

        tab.tab_action = new WindowMetaTabEvent();
        tab.tab_action.AddListener(item => item.container.showTab(item));
        tab.tab_elements.Clear();
        scroll_window.tabs.addTabContent(tab, content);

        TipButton tipButton = tab.GetComponent<TipButton>() ?? tab.gameObject.AddComponent<TipButton>();
        tab._tip_button = tipButton;
        tipButton.textOnClick = titleKey;
        tipButton.textOnClickDescription = descriptionKey;
        tipButton.type = "tip";
        tipButton.hoverAction = null;
        tipButton.setHoverAction(new TooltipAction(tipButton.showTooltipDefault), true);

        tab._worldtip_text = tab.getWorldTipText();
        Transform iconTransform = tab.transform.Find("Icon");
        SetImageSprite(iconTransform, SpriteTextureLoader.getSprite(iconPath));
        content.gameObject.SetActive(false);
        scroll_window.tabs.refillTabsWithContent();
        return tab;
    }

    private void ReorderSectCustomTabs()
    {
        Transform tabsContainer = transform.Find("Background/Tabs");
        if (tabsContainer == null) return;

        WindowMetaTab mainTab = GetMainInfoTab();
        int insertIndex = mainTab != null ? mainTab.transform.GetSiblingIndex() + 1 : 1;

        Transform personnelTab = tabsContainer.Find("Sect Personnel");
        Transform scriptureTab = tabsContainer.Find("Sect Scripture");
        if (personnelTab != null)
        {
            personnelTab.SetSiblingIndex(insertIndex++);
        }

        if (scriptureTab != null)
        {
            scriptureTab.SetSiblingIndex(insertIndex);
        }

        scroll_window.tabs.refillTabsWithContent();
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

        string residenceName = sect.GetResidenceName();
        if (!string.IsNullOrEmpty(residenceName))
        {
            showStatRow("Cultiway.Sect.HomeCity", residenceName, MetaType.None, -1L, "iconCity");
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

    T IAugmentationsWindow<ITraitsEditor<SectTrait>>.GetComponentInChildren<T>(bool includeInactive)
    {
        return GetComponentInChildren<T>(includeInactive);
    }
}
