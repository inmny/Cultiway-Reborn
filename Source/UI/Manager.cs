using System.Collections.Generic;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using NeoModLoader.General;
using NeoModLoader.General.UI.Tab;
using NeoModLoader.utils;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

public enum TabButtonType
{
    INFO,
    OVERWORLD,
    WORLD,
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
    public static           PowersTab                            powers_tab;
    private static readonly Dictionary<TabButtonType, Transform> button_groups = new();
    private static          RectTransform                        top_container;

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
            "Controller", "Container"
        });
        powers_tab.PutElement("Container", top_container, new Vector2(-4, -19));

        ConstructTabContainer(TabButtonType.INFO,     SpriteTextureLoader.getSprite("ui/icons/iconAbout"));
        ConstructTabContainer(TabButtonType.WORLD,    SpriteTextureLoader.getSprite("ui/icons/iconWorldInfo"));
        ConstructTabContainer(TabButtonType.RACE,     SpriteTextureLoader.getSprite("ui/icons/iconHumans"));
        ConstructTabContainer(TabButtonType.CREATURE, SpriteTextureLoader.getSprite("ui/icons/iconSheep"));
        ConstructTabContainer(TabButtonType.BUILDING, SpriteTextureLoader.getSprite("ui/icons/iconBuildings"));
        ConstructTabContainer(TabButtonType.DROP,     SpriteTextureLoader.getSprite("ui/icons/iconRain"));
        ConstructTabContainer(TabButtonType.DEBUG,    SpriteTextureLoader.getSprite("ui/icons/iconDebug"));


        AddButtonsForDebug();

        powers_tab.UpdateLayout();

        SwitchTab(TabButtonType.INFO);
    }
    private static string[] kingdom_window_content_to_remove = [
      "TopElements", "content_meta", "content_relations", "content_king", "content_more_icons", "content_capital", "content_villages", "content_traits_editor"
    ];
    private static string[] kingdom_window_header_to_remove = [
        "header_top", "header_traits"
    ];
    public static TWindow CreateMetaWindow<TWindow, TMeta, TMetaData>(string window_id) 
    where TWindow : WindowMetaGeneric<TMeta, TMetaData> 
    where TMeta : CoreSystemObject<TMetaData> 
    where TMetaData : BaseSystemData
    {
        var prefab = Resources.Load<GameObject>("windows/kingdom");
        ListPool<GameObject> tTabsObjects = ScrollWindow.disableTabsInPrefab(prefab.GetComponent<ScrollWindow>());
        var window = Object.Instantiate(prefab, ModClass.I.PrefabLibrary);

        var kingdom_window = window.GetComponent<KingdomWindow>();

        kingdom_window.DeleteTab("Villages");
        kingdom_window.DeleteTab("Traits");
        kingdom_window.DeleteTab("Families");
        kingdom_window.DeleteTab("Interesting People");
        kingdom_window.DeleteTab("Pyramid");
        kingdom_window.DeleteTab("Statistics");
        
        Object.DestroyImmediate(kingdom_window);
        foreach (var content_name in kingdom_window_content_to_remove)
        {
            var content = window.transform.Find($"Background/Scroll View/Viewport/Content/{content_name}");
            if (content == null) continue;
            ModClass.LogInfo($"[{nameof(Manager)}] content: {content_name}");
            Object.DestroyImmediate(content.gameObject);
        }
        foreach (var header_name in kingdom_window_header_to_remove)
        {
            var header = window.transform.Find($"Background/Scroll View/Viewport/Header/{header_name}");
            if (header == null) continue;
            ModClass.LogInfo($"[{nameof(Manager)}] header: {header_name}");
            Object.DestroyImmediate(header.gameObject);
        }
        window.transform.SetParent(CanvasMain.instance.transformWindows);
        window.transform.localScale = Vector2.one;


        ScrollWindow.enableTabsInPrefab(tTabsObjects);
        window.SetActive(false);

        foreach (var tab in window.GetComponentsInChildren<WindowMetaTab>(true))
        {
            ModClass.LogInfo($"[{nameof(Manager)}] tab: {tab.name}");
            tab.tab_action = new();
            tab.tab_action.AddListener((t) =>
            {
                ModClass.LogInfo($"[{nameof(Manager)}] show tab: {t}");
                t.container.showTab(t);
            });
        }

        var meta_window = window.AddComponent<TWindow>();
        var scroll_window = window.GetComponent<ScrollWindow>();

        ScrollWindow._all_windows.Add(window_id, scroll_window);

        meta_window.scroll_window = scroll_window;
        scroll_window.screen_id = window_id;
        scroll_window.name = window_id; 
        scroll_window.init();
        scroll_window.create(true);


        return meta_window;
        bool IsWindowMetaGeneric(System.Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition().Name == "WindowMetaGeneric`2")
                {
                    return true;
                }
            }

            return false;
        }
    }

    private void AddButtonsForDebug()
    {
        AddButton(TabButtonType.DEBUG, PowerButtonCreator.CreateSimpleButton("Cultiway.UI.Buttons.LogPerf", () => { ModClass.I.LogPerf(true);}, null));
    }

    public static void AddButton(TabButtonType type, PowerButton button)
    {
        if (!button_groups.TryGetValue(type, out Transform group))
        {
            ConstructTabContainer(type, null);
            group = button_groups[type];
        }

        button.transform.SetParent(group);
        button.transform.localScale = Vector3.one;
    }

    private static void ConstructTabContainer(TabButtonType type, Sprite icon)
    {
        powers_tab.AddPowerButton("Controller",
            PowerButtonCreator.CreateSimpleButton(type.ToString(), () => { SwitchTab(type); },
                icon));
        Transform transform = new GameObject(type.ToString(), typeof(GridLayoutGroup), typeof(ContentSizeFitter))
            .transform;
        transform.SetParent(top_container);
        transform.localPosition = Vector3.zero;
        transform.localScale = Vector3.one;
        ((RectTransform)transform).pivot = new Vector2(0, 0.5f);

        var layout = transform.GetComponent<GridLayoutGroup>();
        layout.spacing = new Vector2(4, 4);
        layout.startAxis = GridLayoutGroup.Axis.Vertical;
        layout.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        layout.constraintCount = 2;
        layout.cellSize = new Vector2(32, 32);

        var fitter = transform.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        button_groups[type] = transform;
    }

    private static void SwitchTab(TabButtonType type)
    {
        foreach (var pair in button_groups)
        {
            pair.Value.gameObject.SetActive(pair.Key == type);
            if (pair.Key == type)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(pair.Value.GetComponent<RectTransform>());
            }
        }
        
        if (powers_tab.parentObj == null) return;
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(top_container);
        powers_tab.setNewWidth();
    }

    public void PostInit()
    {
        WindowNewCreatureInfo.CreateAndInit("Cultiway.UI.WindowNewCreatureInfo");
        GeoRegionWindow.Init();
    }
}