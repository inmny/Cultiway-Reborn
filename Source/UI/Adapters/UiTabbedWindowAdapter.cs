using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI;

/// <summary>可放入原版多标签窗口壳的页面契约。</summary>
public interface IUiTabbedPage
{
    string Id { get; }
    string TitleKey { get; }
    string DescriptionKey { get; }
    string IconPath { get; }

    Transform CreateContent(Transform parent, Transform titleTemplate, float width);
}

/// <summary>原版多标签窗口壳的稳定配置。</summary>
internal sealed class UiTabbedWindowOptions
{
    public string WindowId { get; }
    public string TitleKey { get; }
    public string WindowIconPath { get; }
    public string DiagnosticName { get; }
    public float ContentWidth { get; set; } = 214f;
    public float ScrollViewWidth { get; set; } = 224f;
    public bool HideHeader { get; set; }
    public Action<Transform> ConfigureHeader { get; set; }

    public UiTabbedWindowOptions(string windowId, string titleKey, string windowIconPath,
        string diagnosticName)
    {
        WindowId = windowId;
        TitleKey = titleKey;
        WindowIconPath = windowIconPath;
        DiagnosticName = diagnosticName;
    }
}

/// <summary>适配原版 settings prefab，只负责固定层级、Tab 与 ScrollWindow 生命周期。</summary>
internal static class UiTabbedWindowAdapter
{
    private const string PrefabPath = "windows/settings";

    public static void Create<TWindow>(UiTabbedWindowOptions options, IReadOnlyList<IUiTabbedPage> pages)
        where TWindow : TabbedWindow
    {
        if (ScrollWindow.windowLoaded(options.WindowId)) return;

        RegisterWindowAsset(options);
        GameObject prefab = Resources.Load<GameObject>(PrefabPath) ??
                            throw new InvalidOperationException($"找不到原版多标签窗口 prefab: {PrefabPath}");
        ScrollWindow prefabWindow = RequireComponent<ScrollWindow>(prefab, options, PrefabPath);
        ListPool<GameObject> disabledTabs = ScrollWindow.disableTabsInPrefab(prefabWindow);
        GameObject window = Object.Instantiate(prefab, ModClass.I.PrefabLibrary);
        ScrollWindow.enableTabsInPrefab(disabledTabs);

        window.SetActive(false);
        window.transform.SetParent(CanvasMain.instance.transformWindows);
        window.transform.localScale = Vector3.one;
        window.name = options.WindowId;

        ScrollWindow scrollWindow = RequireComponent<ScrollWindow>(window, options, options.WindowId);
        foreach (TabbedWindow component in window.GetComponents<TabbedWindow>())
            Object.DestroyImmediate(component);

        ConfigureWindow(window.transform, scrollWindow, options);
        ConfigurePages(window.transform, scrollWindow, options, pages);
        window.AddComponent<TWindow>();

        ScrollWindow._all_windows.Add(options.WindowId, scrollWindow);
        scrollWindow.screen_id = options.WindowId;
        scrollWindow.name = options.WindowId;
        scrollWindow.init();
        scrollWindow.create(true);
    }

    private static void RegisterWindowAsset(UiTabbedWindowOptions options)
    {
        if (AssetManager.window_library.has(options.WindowId)) return;
        AssetManager.window_library.add(new WindowAsset
        {
            id = options.WindowId,
            icon_path = options.WindowIconPath,
            preload = false,
            is_testable = false,
        });
    }

    private static void ConfigureWindow(Transform root, ScrollWindow scrollWindow,
        UiTabbedWindowOptions options)
    {
        Require(root, "Tabs Right", options).gameObject.SetActive(false);
        RequireComponent<LocalizedText>(Require(root, "Background/Title", options).gameObject, options,
            "Background/Title").setKeyAndUpdate(options.TitleKey);

        Transform header = Require(root, "Background/Scroll View/Viewport/Header", options);
        if (options.HideHeader)
        {
            LayoutElement headerLayout = header.GetComponent<LayoutElement>();
            headerLayout.minHeight = 0f;
            headerLayout.preferredHeight = 0f;
            header.gameObject.SetActive(false);
        }
        else
        {
            options.ConfigureHeader?.Invoke(header);
        }

        RectTransform scrollView = Require(root, "Background/Scroll View", options).GetComponent<RectTransform>();
        scrollView.sizeDelta = new Vector2(options.ScrollViewWidth, scrollView.sizeDelta.y);
        scrollWindow.historyActionEnabled = true;
    }

    private static void ConfigurePages(Transform root, ScrollWindow scrollWindow,
        UiTabbedWindowOptions options, IReadOnlyList<IUiTabbedPage> pages)
    {
        Transform content = Require(root, "Background/Scroll View/Viewport/Content", options);
        Transform tabsContainer = Require(root, "Background/Tabs", options);
        scrollWindow.tabs.init();
        scrollWindow.tabs._scroll_window = scrollWindow;

        WindowMetaTab sourceTab = tabsContainer.GetComponentInChildren<WindowMetaTab>(true) ??
                                  throw Missing(options, "Background/Tabs/<WindowMetaTab>");
        GameObject tabTemplateObject = Object.Instantiate(sourceTab.gameObject, ModClass.I.PrefabLibrary, false);
        WindowMetaTab tabTemplate = tabTemplateObject.GetComponent<WindowMetaTab>();
        Transform sourceTitle = content.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(item => item.name.StartsWith("tab_title_container", StringComparison.Ordinal)) ??
                                throw Missing(options, "Background/Scroll View/Viewport/Content/tab_title_container*");
        GameObject titleTemplateObject = Object.Instantiate(sourceTitle.gameObject, ModClass.I.PrefabLibrary, false);

        UiLayout.ClearChildren(content, true, child => child.name == "runes_parent");
        ConfigureContentLayout(content);
        UiLayout.ClearChildren(tabsContainer, true);
        ConfigureTabsLayout(tabsContainer, options);
        scrollWindow.tabs._tabs.Clear();
        scrollWindow.tabs.tab_default = null;

        for (int i = 0; i < pages.Count; i++)
        {
            IUiTabbedPage page = pages[i];
            Transform pageContent = page.CreateContent(content, titleTemplateObject.transform,
                options.ContentWidth);
            WindowMetaTab tab = CreateTab(tabsContainer, scrollWindow, tabTemplate, page, pageContent,
                options);
            if (i == 0) scrollWindow.tabs.tab_default = tab;
        }

        Object.DestroyImmediate(titleTemplateObject);
        Object.DestroyImmediate(tabTemplateObject);
        scrollWindow.tabs._scroll_window = scrollWindow;
        scrollWindow.tabs.refillTabsWithContent();
    }

    private static WindowMetaTab CreateTab(Transform parent, ScrollWindow scrollWindow, WindowMetaTab template,
        IUiTabbedPage page, Transform pageContent, UiTabbedWindowOptions options)
    {
        WindowMetaTab tab = Object.Instantiate(template.gameObject, parent, false).GetComponent<WindowMetaTab>();
        tab.name = page.Id;
        tab.container = scrollWindow.tabs;
        tab.tab_action = new WindowMetaTabEvent();
        tab.tab_action.AddListener(item => item.container.showTab(item));
        tab.tab_elements = new List<Transform>();
        scrollWindow.tabs._tabs.Add(tab);
        scrollWindow.tabs.addTabContent(tab, pageContent);

        TipButton tip = tab.GetComponent<TipButton>() ?? tab.gameObject.AddComponent<TipButton>();
        tab._tip_button = tip;
        tip.textOnClick = page.TitleKey;
        tip.textOnClickDescription = page.DescriptionKey;
        tip.type = "tip";
        tip.setHoverAction(new TooltipAction(tip.showTooltipDefault), true);
        tab._worldtip_text = tab.getWorldTipText();

        Image icon = Require(tab.transform, "Icon", options).GetComponent<Image>();
        UiResources.SetImage(icon, page.IconPath);
        pageContent.gameObject.SetActive(false);
        tab.gameObject.SetActive(true);
        tab.toggleActive(true);
        return tab;
    }

    private static void ConfigureTabsLayout(Transform tabsContainer, UiTabbedWindowOptions options)
    {
        GridLayoutGroup layout = RequireComponent<GridLayoutGroup>(tabsContainer.gameObject, options,
            "Background/Tabs");
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = new Vector2(0f, UiTheme.Current.Metrics.SpacingXs);
        layout.childAlignment = TextAnchor.UpperCenter;
    }

    private static void ConfigureContentLayout(Transform content)
    {
        foreach (WindowMetaElementBase element in content.GetComponents<WindowMetaElementBase>())
            Object.DestroyImmediate(element);

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = UiTheme.Current.Metrics.SpacingSm;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static Transform Require(Transform root, string path, UiTabbedWindowOptions options)
    {
        return root.Find(path) ?? throw Missing(options, path);
    }

    private static T RequireComponent<T>(GameObject gameObject, UiTabbedWindowOptions options, string path)
        where T : Component
    {
        return gameObject.GetComponent<T>() ??
               throw new InvalidOperationException($"{options.DiagnosticName} 的 {path} 缺少 {typeof(T).Name} 组件");
    }

    private static InvalidOperationException Missing(UiTabbedWindowOptions options, string path)
    {
        return new InvalidOperationException($"{options.DiagnosticName} 缺少原版节点: {path}");
    }
}
