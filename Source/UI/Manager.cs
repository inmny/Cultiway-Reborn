using System;
using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Core.Logging;
using Cultiway.UI.Components;
using Cultiway.Utils.Extension;
using NeoModLoader.General;
using NeoModLoader.General.UI.Tab;
using NeoModLoader.utils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI;

public enum TabButtonType
{
    INFO,
    OVERWORLD,
    WORLD,
    BIOME,
    RACE,
    CREATURE,
    BUILDING,
    BOSS,
    DROP,
    OTHERS,
    DEBUG
}

public class Manager
{
    private const string DebugIconRoot       = "cultiway/icons/cultilog/";

    public static           PowersTab                                      powers_tab;
    private static readonly Dictionary<TabButtonType, PowerTabGroupLayout> button_groups = new();
    private static          RectTransform                                  top_container;

    public void Init()
    {
        top_container = new GameObject("TopContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
        top_container.pivot = new Vector2(0, 0.5f);
        var fitter = top_container.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        var layout_group = top_container.GetComponent<HorizontalLayoutGroup>();
        layout_group.childControlHeight = false;
        layout_group.childControlWidth = false;
        layout_group.childForceExpandWidth = false;
        layout_group.childForceExpandHeight = false;

        powers_tab = TabManager.CreateTab("Cultiway", "Cultiway", "Cultiway Description",
            SpriteTextureLoader.getSprite("cultiway/icons/iconTab"));
        powers_tab.SetLayout(new List<string>
        {
            "Controller"
        });
        powers_tab.PutElement("Controller", top_container, new Vector2(140, -16), true);

        ConstructTabContainer(TabButtonType.INFO,     SpriteTextureLoader.getSprite("ui/icons/iconAbout"));
        ConstructTabContainer(TabButtonType.WORLD,    SpriteTextureLoader.getSprite("ui/icons/iconWorldInfo"));
        ConstructTabContainer(TabButtonType.BIOME,    SpriteTextureLoader.getSprite("cultiway/icons/biomes/Bamboo"));
        ConstructTabContainer(TabButtonType.RACE,     SpriteTextureLoader.getSprite("ui/icons/iconHumans"));
        ConstructTabContainer(TabButtonType.CREATURE, SpriteTextureLoader.getSprite("ui/icons/iconSheep"));
        ConstructTabContainer(TabButtonType.BUILDING, SpriteTextureLoader.getSprite("ui/icons/iconBuildings"));
        ConstructTabContainer(TabButtonType.DROP,     SpriteTextureLoader.getSprite("ui/icons/iconRain"));
        ConstructTabContainer(TabButtonType.DEBUG,    SpriteTextureLoader.getSprite("ui/icons/iconDebug"));

        RegisterTabSections();
        AddButtonsForDebug();

        powers_tab.UpdateLayout();

        SwitchTab(TabButtonType.INFO);
    }

    private static void RegisterTabSections()
    {
        AddSection(TabButtonType.INFO, PowerTabSections.InfoMain, 100);

        AddSection(TabButtonType.WORLD, PowerTabSections.WorldInfo, 100);
        AddSection(TabButtonType.WORLD, PowerTabSections.WorldGeography, 200);
        AddSection(TabButtonType.WORLD, PowerTabSections.WorldPavilions, 300);
        AddSection(TabButtonType.WORLD, PowerTabSections.WorldRains, 400);
        AddSeparator(TabButtonType.WORLD, PowerTabSections.WorldGeography, 0, "world.geography.separator");
        AddSeparator(TabButtonType.WORLD, PowerTabSections.WorldPavilions, 0, "world.pavilions.separator");
        AddSeparator(TabButtonType.WORLD, PowerTabSections.WorldRains, 0, "world.rains.separator");

        AddSection(TabButtonType.BIOME, PowerTabSections.BiomeMain, 100);
        AddSection(TabButtonType.RACE, PowerTabSections.RaceMain, 100);
        AddSection(TabButtonType.CREATURE, PowerTabSections.CreatureMain, 100);
        AddSection(TabButtonType.BUILDING, PowerTabSections.BuildingMain, 100);
        AddSection(TabButtonType.DROP, PowerTabSections.DropMain, 100);
        AddSection(TabButtonType.DEBUG, PowerTabSections.DebugMain, 100);
    }

    private static string[] kingdom_window_content_to_remove = [
      "TopElements", "content_motto", "content_meta_needs", "content_king", "content_capital", "content_villages", "content_traits_editor"
    ];
    private static string[] kingdom_window_header_to_remove = [
        "header_traits"
    ];
    public static ListWindow CreateListMetaWindow(string window_id, MetaTypeExtend meta_type)
    {
        var prefab = Resources.Load<GameObject>("windows/list_kingdoms");
        ListPool<GameObject> tTabsObjects = ScrollWindow.disableTabsInPrefab(prefab.GetComponent<ScrollWindow>());

        var window = Object.Instantiate(prefab, ModClass.I.PrefabLibrary);
        ScrollWindow.enableTabsInPrefab(tTabsObjects);
        window.SetActive(false);
        
        window.transform.SetParent(CanvasMain.instance.transformWindows);
        window.transform.localScale = Vector2.one;


        var list_window = window.GetComponent<ListWindow>();
        list_window._meta_type = meta_type.Back();
        list_window._list_element_prefab = GetListElementPrefab(meta_type);
        
        list_window.transform.Find("Background/Title").GetComponent<LocalizedText>().setKeyAndUpdate(meta_type.ToString().Underscore() + "s");
        list_window.transform.Find("Background/Scroll View/Viewport/Content/content_list/title_list/title_tab").GetComponent<LocalizedText>().setKeyAndUpdate("tab_all_" + meta_type.ToString().Underscore() + "s");

        var scroll_window = window.GetComponent<ScrollWindow>();

        ScrollWindow._all_windows.Add(window_id, scroll_window);
        scroll_window.screen_id = window_id;
        scroll_window.name = window_id; 
        scroll_window.init();
        scroll_window.create(true);

        return list_window;
    }

    private static GameObject GetListElementPrefab(MetaTypeExtend meta_type)
    {
        return meta_type switch
        {
            MetaTypeExtend.GeoRegion => GeoRegionListElement.Prefab.gameObject,
            MetaTypeExtend.Sect => SectListElement.Prefab.gameObject,
            _ => throw new NotSupportedException($"未注册列表元素预制体: {meta_type}")
        };
    }

    public static TWindow CreateMetaWindow<TWindow, TMeta, TMetaData>(string window_id, params string[] preserved_tabs)
    where TWindow : WindowMetaGeneric<TMeta, TMetaData> 
    where TMeta : CoreSystemObject<TMetaData> 
    where TMetaData : BaseSystemData
    {
        return CreateMetaWindow<TWindow, TMeta, TMetaData>(window_id, preserved_tabs, null, null);
    }

    public static TWindow CreateMetaWindow<TWindow, TMeta, TMetaData>(
        string window_id,
        IEnumerable<string> preserved_tabs,
        IEnumerable<string> preserved_content,
        IEnumerable<string> preserved_header)
        where TWindow : WindowMetaGeneric<TMeta, TMetaData>
        where TMeta : CoreSystemObject<TMetaData>
        where TMetaData : BaseSystemData
    {
        var prefab = Resources.Load<GameObject>("windows/kingdom");
        ListPool<GameObject> tTabsObjects = ScrollWindow.disableTabsInPrefab(prefab.GetComponent<ScrollWindow>());
        var window = Object.Instantiate(prefab, ModClass.I.PrefabLibrary);

        var kingdom_window = window.GetComponent<KingdomWindow>();

        var preservedTabs = ToPreservedSet(preserved_tabs);
        var preservedContent = ToPreservedSet(preserved_content);
        var preservedHeader = ToPreservedSet(preserved_header);
        DeleteTabUnlessPreserved(kingdom_window, preservedTabs, "Villages");
        DeleteTabUnlessPreserved(kingdom_window, preservedTabs, "Traits");
        DeleteTabUnlessPreserved(kingdom_window, preservedTabs, "Families");
        DeleteTabUnlessPreserved(kingdom_window, preservedTabs, "Interesting People");
        DeleteTabUnlessPreserved(kingdom_window, preservedTabs, "Pyramid");
        DeleteTabUnlessPreserved(kingdom_window, preservedTabs, "Statistics");

        RemoveLegacyMetaWindowElements(window, preservedTabs);
        
        Object.DestroyImmediate(kingdom_window);
        foreach (var content_name in kingdom_window_content_to_remove)
        {
            if (preservedContent.Contains(content_name)) continue;

            var content = window.transform.Find($"Background/Scroll View/Viewport/Content/{content_name}");
            if (content == null) continue;
            ModClass.LogInfo($"[{nameof(Manager)}] content: {content_name}");
            Object.DestroyImmediate(content.gameObject);
        }
        foreach (var header_name in kingdom_window_header_to_remove)
        {
            if (preservedHeader.Contains(header_name)) continue;

            var header = window.transform.Find($"Background/Scroll View/Viewport/Header/{header_name}");
            if (header == null) continue;
            ModClass.LogInfo($"[{nameof(Manager)}] header: {header_name}");
            Object.DestroyImmediate(header.gameObject);
        }
        window.transform.SetParent(CanvasMain.instance.transformWindows);
        window.transform.localScale = Vector2.one;


        ScrollWindow.enableTabsInPrefab(tTabsObjects);
        window.SetActive(false);

        var meta_window = window.AddComponent<TWindow>();
        var scroll_window = window.GetComponent<ScrollWindow>();
        meta_window.scroll_window = scroll_window;

        ScrollWindow._all_windows.Add(window_id, scroll_window);

        foreach (var tab in window.GetComponentsInChildren<WindowMetaTab>(true))
        {
            var persistent_count = tab.tab_action.GetPersistentEventCount();

            bool is_tab_switcher = false;
            if (persistent_count > 0)
            {
                for (int i = 0; i < persistent_count; i++)
                {
                    var method_name = tab.tab_action.GetPersistentMethodName(i);
                    if (method_name == nameof(WindowMetaTabButtonsContainer.showTab))
                    {
                        is_tab_switcher = true;
                        break;
                    }
                }
                tab.tab_action = new();
            }

            if (is_tab_switcher)
            {
                tab.tab_action.AddListener((t) =>
                {
                    t.container.showTab(t);
                });
            }
        }
        meta_window.transform.Find("Tabs Right/Favorite").GetComponent<WindowMetaTab>().tab_action.AddListener(_ =>
        {
            meta_window.pressFavorite();
        });
        meta_window._favorite_icon = meta_window.transform.Find("Tabs Right/Favorite/Icon").GetComponent<Image>();

        var list_window_asset = AssetManager.list_window_library.getByMetaType(meta_window.meta_type);
        meta_window.transform.Find("Tabs Right/Kingdoms").GetComponent<Button>().onClick = new();
        meta_window.transform.Find("Tabs Right/Kingdoms").GetComponent<Button>().onClick.AddListener(() =>
        {
            ScrollWindow.showWindow(list_window_asset.id);
        });
        meta_window.transform.Find("Tabs Right/Kingdoms").GetComponent<TipButton>().textOnClick = (typeof(TMeta).Name + "s").Underscore();
        meta_window.transform.Find("Tabs Right/Kingdoms").GetComponent<TipButton>().textOnClickDescription = (typeof(TMeta).Name + "sDescription").Underscore();
        meta_window.transform.Find("Tabs Right/Kingdoms/Icon").GetComponent<Image>().sprite = SpriteTextureLoader.getSprite(list_window_asset.icon_path);
        meta_window.transform.Find("Tabs Right/Kingdoms").name = typeof(TMeta).Name + "s";


        scroll_window.screen_id = window_id;
        scroll_window.name = window_id; 
        scroll_window.init();
        scroll_window.create(true);


        return meta_window;
    }

    private static HashSet<string> ToPreservedSet(IEnumerable<string> names)
    {
        return new HashSet<string>(names ?? Array.Empty<string>());
    }

    private static void DeleteTabUnlessPreserved(TabbedWindow window, ISet<string> preserved_tabs, string tab_name)
    {
        if (preserved_tabs.Contains(tab_name)) return;
        window.DeleteTab(tab_name);
    }

    private static void RemoveLegacyMetaWindowElements(GameObject window, ISet<string> preserved_tabs)
    {
        var preservedRoots = new List<Transform>();
        if (preserved_tabs.Count > 0)
        {
            foreach (var tab in window.GetComponentsInChildren<WindowMetaTab>(true))
            {
                if (!preserved_tabs.Contains(tab.name)) continue;

                for (int i = 0; i < tab.tab_elements.Count; i++)
                {
                    var element = tab.tab_elements[i];
                    if (element == null || preservedRoots.Contains(element)) continue;
                    preservedRoots.Add(element);
                }
            }
        }

        foreach (var element in window.GetComponentsInChildren<WindowMetaElementBase>(true))
        {
            if (ShouldPreserveMetaElement(element, preservedRoots)) continue;
            Object.DestroyImmediate(element);
        }
    }

    private static bool ShouldPreserveMetaElement(WindowMetaElementBase element, IReadOnlyList<Transform> preserved_roots)
    {
        if (!IsUnderAnyRoot(element.transform, preserved_roots)) return false;
        return element is InterestingPeopleTab;
    }

    private static bool IsUnderAnyRoot(Transform transform, IReadOnlyList<Transform> roots)
    {
        for (int i = 0; i < roots.Count; i++)
        {
            Transform root = roots[i];
            if (root == null) continue;
            if (transform == root || transform.IsChildOf(root)) return true;
        }

        return false;
    }

    public static TTab CreateSelectedMetaTab<TTab, TMeta, TMetaData>(string tab_id)
    where TTab : SelectedMeta<TMeta, TMetaData>
    where TMeta : MetaObject<TMetaData>, IFavoriteable 
    where TMetaData : MetaObjectData
    {
        var prefab = CanvasMain.instance.canvas_ui.transform.Find("CanvasBottom/BottomElements/BottomElementsMover/CanvasScrollView/Scroll View/Viewport/Content/Power Tabs/selected_kingdom").gameObject;
        
        var tab_obj = Object.Instantiate(prefab, ModClass.I.PrefabLibrary);
        tab_obj.name = tab_id;
        tab_obj.SetActive(false);

        var source_tab = tab_obj.GetComponent<SelectedKingdom>() 
                         ?? throw new InvalidOperationException("创建自定义选中底栏失败：找不到原版 selected_kingdom 的 SelectedKingdom 组件");
        var tab = tab_obj.AddComponent<TTab>();
        source_tab.CopyCompatibleSerializedFieldsTo(tab);
        source_tab.DisableSerializedObjectsMissingFrom(tab);
        RequireCopiedSelectedTabFields(tab, tab_id);

        Object.DestroyImmediate(source_tab);

        tab_obj.transform.SetParent(prefab.transform.parent);
        tab_obj.transform.localScale = Vector3.one;
        tab_obj.SetActive(true);
        
        return tab;
    }

    private static void RequireCopiedSelectedTabFields(Component target, string tab_id)
    {
        var context = $"创建自定义选中底栏失败：{tab_id}";
        target.RequireCopiedSerializedField("name_field", context);
        target.RequireCopiedSerializedField("icon_right", context);
        target.RequireCopiedSerializedField("stats_icons", context, true);
    }

    public static void InsertButtonForMeta(MetaTypeExtend meta_type)
    {
        var toolbar_container_transform = CanvasMain.instance.canvas_windows.transform.Find("WindowToolbarContainer/WindowToolbar/content/Scroll View/Viewport/Content/Metas Group 3");
        var prefab = toolbar_container_transform.Find("clans_list").gameObject;
        var inserted_list_button = Object.Instantiate(prefab, toolbar_container_transform);
        inserted_list_button.name = meta_type.ToString().Underscore() + "s_list";
        var image = inserted_list_button.transform.Find("Icon").GetComponent<Image>();
        var button = inserted_list_button.GetComponent<Button>();
        var tip_button = inserted_list_button.GetComponent<TipButton>();
        var power_button = inserted_list_button.GetComponent<PowerButton>();
        var meta_switcher = inserted_list_button.GetComponent<MetaSpriteSwitcher>();
        var list_window_asset = AssetManager.list_window_library.getByMetaType(meta_type.Back());
        meta_switcher.meta_type = meta_type.Back();
        power_button.open_window_id = list_window_asset.id;
        power_button.block_same_window = false;
        image.sprite = SpriteTextureLoader.getSprite(list_window_asset.icon_path);
        tip_button.textOnClick = inserted_list_button.name;
        tip_button.textOnClickDescription = inserted_list_button.name + "_description";
        tip_button.type = WorldboxGame.Tooltips.GetMetaListTooltipAsset(meta_type).id;

        var bottom_container_transform = CanvasMain.instance.canvas_ui.transform.Find("CanvasBottom/BottomElements/BottomElementsMover/CanvasScrollView/Scroll View/Viewport/Content/Power Tabs/noosphere");
        var anchor_obj = bottom_container_transform.Find("_line (4)");
        inserted_list_button = Object.Instantiate(inserted_list_button, bottom_container_transform);
        inserted_list_button.name = meta_type.ToString().Underscore() + "s_list";
        inserted_list_button.transform.SetSiblingIndex(anchor_obj.GetSiblingIndex());
        var custom_map_mode = ModClass.L.CustomMapModeLibrary.get(meta_type.ToString().Underscore());
        prefab = bottom_container_transform.Find(CustomMapModeLibrary.GetMapModeButtonPrefabName(custom_map_mode)).gameObject;
        bool was_prefab_active = prefab.activeSelf;
        if (was_prefab_active)
        {
            prefab.SetActive(false);
        }

        var inserted_layer_button = Object.Instantiate(prefab, bottom_container_transform);
        if (was_prefab_active)
        {
            prefab.SetActive(true);
        }

        inserted_layer_button.name = meta_type.ToString().Underscore() + "_layer";
        inserted_layer_button.transform.SetSiblingIndex(anchor_obj.GetSiblingIndex());

        image = inserted_layer_button.transform.Find("Icon").GetComponent<Image>();
        button = inserted_layer_button.GetComponent<Button>();
        tip_button = inserted_layer_button.GetComponent<TipButton>() ?? inserted_layer_button.AddComponent<TipButton>();
        var layer_power_button = inserted_layer_button.GetComponent<PowerButton>();
        Sprite layer_icon = SpriteTextureLoader.getSprite(custom_map_mode.icon_path);
        image.sprite = layer_icon;
        image.overrideSprite = layer_icon;
        tip_button.textOnClick = custom_map_mode.toggle_name;
        tip_button.textOnClickDescription = custom_map_mode.toggle_name + "_description";
        tip_button.text_description_2 = "hotkey_tip_zone_switch";
        tip_button.type = "tip_zone_mode";
        tip_button.showOnClick = false;
        inserted_layer_button.SetActive(true);
        layer_power_button.checkToggleIcon();
    }

    public static void InsertWallButton(GodPower power, string icon_path)
    {
        Transform creation_tab_transform = CanvasMain.instance.canvas_ui.transform.Find("CanvasBottom/BottomElements/BottomElementsMover/CanvasScrollView/Scroll View/Viewport/Content/Power Tabs/creation");
        PowersTab creation_tab = creation_tab_transform.GetComponent<PowersTab>();
        Transform template = creation_tab_transform.Find("wall_light");
        int insert_index = template.GetSiblingIndex() + 1;
        Object.DestroyImmediate(creation_tab_transform.GetChild(insert_index).gameObject);

        GameObject template_obj = template.gameObject;
        bool template_active = template_obj.activeSelf;
        template_obj.SetActive(false);

        GameObject button_obj = Object.Instantiate(template_obj, creation_tab_transform);
        template_obj.SetActive(template_active);

        button_obj.name = power.id;
        button_obj.transform.SetSiblingIndex(insert_index);

        PowerButton button = button_obj.GetComponent<PowerButton>();
        button.godPower = power;
        button.rect_transform = button_obj.GetComponent<RectTransform>();

        Sprite icon = SpriteTextureLoader.getSprite(icon_path);
        button.icon.sprite = icon;
        button.icon.overrideSprite = icon;

        button_obj.SetActive(true);

        if (creation_tab._asset == null) return;

        creation_tab._power_buttons.Add(button);
        creation_tab.findNeighbours(true);
        creation_tab.sortButtons();
        creation_tab.recalc();
    }

    private void AddButtonsForDebug()
    {
        AddDebugButton(100, "Cultiway.UI.Buttons.LogPerf", () => { ModClass.I.LogPerf(true); }, "log_action_perf");
        AddDebugToggleButton(200, "Cultiway.UI.Buttons.ToggleCultiLog", CultiLogPlayerOptions.Enabled, () => { ModClass.I.OnCultiLogEnabledToggled(); }, "log_toggle");
        AddDebugToggleButton(300, "Cultiway.UI.Buttons.ToggleCultiLogDisk", CultiLogPlayerOptions.DiskEnabled, () => { ModClass.I.OnCultiLogDiskToggled(); }, "log_disk");
        AddDebugButton(400, "Cultiway.UI.Buttons.CycleCultiLogLevel", () => { ModClass.I.CycleCultiLogMinLevel(); }, "log_level");

        AddDebugLogCategoryButton(500, "Cultiway.UI.Buttons.ToggleCultiLogGeneral", CultiLogCategory.General, "log_cat_general");
        AddDebugLogCategoryButton(600, "Cultiway.UI.Buttons.ToggleCultiLogCombat", CultiLogCategory.Combat, "log_cat_combat");
        AddDebugLogCategoryButton(700, "Cultiway.UI.Buttons.ToggleCultiLogSect", CultiLogCategory.Sect, "log_cat_sect");
        AddDebugLogCategoryButton(800, "Cultiway.UI.Buttons.ToggleCultiLogCultivation", CultiLogCategory.Cultivation, "log_cat_cultivation");
        AddDebugLogCategoryButton(900, "Cultiway.UI.Buttons.ToggleCultiLogBook", CultiLogCategory.Book, "log_cat_book");
        AddDebugLogCategoryButton(1000, "Cultiway.UI.Buttons.ToggleCultiLogSkill", CultiLogCategory.Skill, "log_cat_skill");
        AddDebugLogCategoryButton(1100, "Cultiway.UI.Buttons.ToggleCultiLogPathfinding", CultiLogCategory.Pathfinding, "log_cat_pathfinding");
        AddDebugLogCategoryButton(1200, "Cultiway.UI.Buttons.ToggleCultiLogItem", CultiLogCategory.Item, "log_cat_item");
        AddDebugLogCategoryButton(1300, "Cultiway.UI.Buttons.ToggleCultiLogTrain", CultiLogCategory.Train, "log_cat_train");
        AddDebugLogCategoryButton(1400, "Cultiway.UI.Buttons.ToggleCultiLogGeo", CultiLogCategory.Geo, "log_cat_geo");
        AddDebugLogCategoryButton(1500, "Cultiway.UI.Buttons.ToggleCultiLogAI", CultiLogCategory.AI, "log_cat_ai");
        AddDebugLogCategoryButton(1600, "Cultiway.UI.Buttons.ToggleCultiLogUI", CultiLogCategory.UI, "log_cat_ui");
        AddDebugLogCategoryButton(1700, "Cultiway.UI.Buttons.ToggleCultiLogPerf", CultiLogCategory.Perf, "log_cat_perf");
        AddDebugLogCategoryButton(1800, "Cultiway.UI.Buttons.ToggleCultiLogAIGC", CultiLogCategory.AIGC, "log_cat_aigc");
        AddDebugLogCategoryButton(1900, "Cultiway.UI.Buttons.ToggleCultiLogError", CultiLogCategory.Error, "log_cat_error");

        AddDebugButton(2000, "Cultiway.UI.Buttons.ExportCultiLog", () => { ModClass.I.ExportCultiLog(); }, "log_export");
        AddDebugButton(2100, "Cultiway.UI.Buttons.ClearCultiLog", () => { ModClass.I.ClearCultiLog(); }, "log_clear");
        AddDebugButton(2200, "Cultiway.UI.Buttons.CultiLogStats", () => { ModClass.I.LogCultiLogStats(); }, "log_stats");
    }

    private static void AddDebugLogCategoryButton(int order, string key, CultiLogCategory category, string iconName)
    {
        AddDebugToggleButton(order, key, CultiLogPlayerOptions.GetCategoryOptionId(category), () => { ModClass.I.OnCultiLogCategoryToggled(category); }, iconName);
    }

    private static void AddDebugButton(int order, string key, UnityAction action, string iconName)
    {
        Sprite icon = SpriteTextureLoader.getSprite(DebugIconRoot + iconName);
        AddButton(TabButtonType.DEBUG, PowerTabSections.DebugMain, order, key,
            PowerButtonCreator.CreateSimpleButton(key, action, icon));
    }

    private static void AddDebugToggleButton(int order, string key, string optionId, UnityAction action, string iconName)
    {
        Sprite icon = SpriteTextureLoader.getSprite(DebugIconRoot + iconName);
        RegisterDebugTogglePower(key, optionId);
        PowerButton button = PowerButtonCreator.CreateToggleButton(key, icon);
        GodPower power = AssetManager.powers.get(key);
        if (power != null)
        {
            power.toggle_action += _ => action();
        }

        TipButton tip = button.GetComponent<TipButton>() ?? button.gameObject.AddComponent<TipButton>();
        tip.textOnClick = key;
        tip.type = "normal";
        tip.setHoverAction(() =>
        {
            tip.textOnClickDescription = key + " Description";
            tip.text_description_2 = IsPlayerOptionEnabled(optionId)
                ? "Cultiway.UI.Buttons.ToggleStatus.Enabled"
                : "Cultiway.UI.Buttons.ToggleStatus.Disabled";
            tip.showTooltipDefault();
        });
        AddButton(TabButtonType.DEBUG, PowerTabSections.DebugMain, order, key, button);
    }

    private static void RegisterDebugTogglePower(string key, string optionId)
    {
        if (AssetManager.powers.get(key) != null) return;

        AssetManager.powers.add(new GodPower
        {
            id = key,
            name = key,
            unselect_when_window = true,
            toggle_name = optionId
        });
    }

    private static bool IsPlayerOptionEnabled(string optionId)
    {
        return PlayerConfig.dict != null &&
               PlayerConfig.dict.TryGetValue(optionId, out PlayerOptionData data) &&
               data.boolVal;
    }

    public static void AddSection(TabButtonType type, string sectionId, int order)
    {
        PowerTabGroupLayout group = GetButtonGroup(type);
        group.AddSection(sectionId, order);
        RefreshTabLayout();
    }

    public static void AddButton(TabButtonType type, string sectionId, int order, string stableId,
        PowerButton button)
    {
        PowerTabGroupLayout group = GetButtonGroup(type);
        group.AddButton(sectionId, order, stableId, button);
        RefreshTabLayout();
    }

    public static void AddButtonPair(TabButtonType type, string sectionId, int order, string stableId,
        PowerButton topButton, PowerButton bottomButton)
    {
        PowerTabGroupLayout group = GetButtonGroup(type);
        group.AddButtonPair(sectionId, order, stableId, topButton, bottomButton);
        RefreshTabLayout();
    }

    public static void AddSeparator(TabButtonType type, string sectionId, int order, string stableId)
    {
        PowerTabGroupLayout group = GetButtonGroup(type);
        group.AddSeparator(sectionId, order, stableId);
        RefreshTabLayout();
    }

    private static PowerTabGroupLayout GetButtonGroup(TabButtonType type)
    {
        if (!button_groups.TryGetValue(type, out PowerTabGroupLayout group))
        {
            throw new InvalidOperationException($"神力 Tab 分类未注册: {type}");
        }

        return group;
    }

    private static void ConstructTabContainer(TabButtonType type, Sprite icon)
    {
        powers_tab.AddPowerButton("Controller",
            PowerButtonCreator.CreateSimpleButton(type.ToString(), () => { SwitchTab(type); },
                icon));
        button_groups[type] = new PowerTabGroupLayout(type.ToString(), top_container);
    }

    private static void SwitchTab(TabButtonType type)
    {
        foreach (var pair in button_groups)
        {
            pair.Value.SetActive(pair.Key == type);
        }

        RefreshTabLayout();
    }

    private static void RefreshTabLayout()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(top_container);
        powers_tab.UpdateLayout();
        RefreshTabNavigation();
        if (powers_tab.parentObj != null)
        {
            powers_tab.setNewWidth();
        }
    }

    private static void RefreshTabNavigation()
    {
        List<PowerButton> buttons = powers_tab._power_buttons;
        var positions = new Dictionary<PowerButton, Vector2>(buttons.Count);
        for (int i = 0; i < buttons.Count; i++)
        {
            PowerButton button = buttons[i];
            button.left = null;
            button.right = null;
            button.up = null;
            button.down = null;
            Vector3 worldCenter = button.rect_transform.TransformPoint(button.rect_transform.rect.center);
            positions[button] = powers_tab.transform.InverseTransformPoint(worldCenter);
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            PowerButton button = buttons[i];
            Vector2 position = positions[button];
            float leftDistance = float.MaxValue;
            float rightDistance = float.MaxValue;
            float upDistance = float.MaxValue;
            float downDistance = float.MaxValue;

            for (int j = 0; j < buttons.Count; j++)
            {
                if (i == j) continue;

                PowerButton candidate = buttons[j];
                Vector2 candidatePosition = positions[candidate];
                float deltaX = candidatePosition.x - position.x;
                float deltaY = candidatePosition.y - position.y;
                if (Mathf.Abs(deltaY) < 0.1f)
                {
                    if (deltaX < 0f && -deltaX < leftDistance)
                    {
                        button.left = candidate;
                        leftDistance = -deltaX;
                    }
                    else if (deltaX > 0f && deltaX < rightDistance)
                    {
                        button.right = candidate;
                        rightDistance = deltaX;
                    }
                }

                if (Mathf.Abs(deltaX) < 0.1f)
                {
                    if (deltaY > 0f && deltaY < upDistance)
                    {
                        button.up = candidate;
                        upDistance = deltaY;
                    }
                    else if (deltaY < 0f && -deltaY < downDistance)
                    {
                        button.down = candidate;
                        downDistance = -deltaY;
                    }
                }
            }

            if (button.left == null)
            {
                button.left = FindHorizontalWrap(button, positions, findRightmost: true);
            }

            if (button.right == null)
            {
                button.right = FindHorizontalWrap(button, positions, findRightmost: false);
            }
        }
    }

    private static PowerButton FindHorizontalWrap(PowerButton source,
        IReadOnlyDictionary<PowerButton, Vector2> positions, bool findRightmost)
    {
        Vector2 sourcePosition = positions[source];
        PowerButton result = null;
        float selectedX = findRightmost ? float.MinValue : float.MaxValue;
        foreach (var pair in positions)
        {
            if (pair.Key == source || Mathf.Abs(pair.Value.y - sourcePosition.y) >= 0.1f) continue;
            if (findRightmost ? pair.Value.x <= selectedX : pair.Value.x >= selectedX) continue;

            result = pair.Key;
            selectedX = pair.Value.x;
        }

        return result;
    }

    public void PostInit()
    {
        WindowModInfo.Init();
        AddButton(TabButtonType.INFO, PowerTabSections.InfoMain, 100, WindowModInfo.WindowId,
            PowerButtonCreator.CreateWindowButton(
                "Cultiway.UI.WindowModInfo Title",
                WindowModInfo.WindowId,
                SpriteTextureLoader.getSprite("cultiway/icons/iconTab")
            )
        );
        WindowRealmNames.Init();
        AddButton(TabButtonType.INFO, PowerTabSections.InfoMain, 200, WindowRealmNames.WindowId,
            PowerButtonCreator.CreateWindowButton(
                "Cultiway.UI.WindowRealmNames Title",
                WindowRealmNames.WindowId,
                SpriteTextureLoader.getSprite("cultiway/icons/iconCultivation")
            )
        );
        WindowSourcelessDamageLevelConfig.CreateAndInit(WindowSourcelessDamageLevelConfig.Id);
        AddButton(TabButtonType.INFO, PowerTabSections.InfoMain, 300, WindowSourcelessDamageLevelConfig.Id,
            PowerButtonCreator.CreateWindowButton(
                $"{WindowSourcelessDamageLevelConfig.Id} Title",
                WindowSourcelessDamageLevelConfig.Id,
                SpriteTextureLoader.getSprite("ui/icons/iconDamage")
            )
        );
        WindowNewCreatureInfo.CreateAndInit("Cultiway.UI.WindowNewCreatureInfo");
        GeoRegionWindow.Init();
        SectWindow.Init();
        SelectedGeoRegionTab.Init();
        GeoRegionListComponent.Init();
        SectListComponent.Init();
        InsertButtonForMeta(MetaTypeExtend.GeoRegion);
        InsertButtonForMeta(MetaTypeExtend.Sect);
    }
}
