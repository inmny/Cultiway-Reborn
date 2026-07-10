using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.UI.ModInfoPages;
using Cultiway.Utils;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI;

public class WindowModInfo : TabbedWindow
{
    public const string WindowId = "Cultiway.UI.WindowModInfo";
    private const float ContentWidth = 214f;
    private const float HeaderContentHeight = 30f;
    private const float HeaderIconSize = 48f;
    private const float HeaderTextWidth = 176f;
    private const string HeaderContentName = "Cultiway Header Content";
    private static Sprite _modIconSprite;

    private static readonly ModInfoPage[] Pages =
    [
        new OverviewPage(),
        new CultivationPage(),
        new SectPage(),
        new SkillPage(),
        new ItemsPage(),
        new WorldPage(),
        new WarhammerPage(),
        new AIGCPage()
    ];

    internal static void Init()
    {
        if (ScrollWindow.windowLoaded(WindowId)) return;

        EnsureWindowAsset();

        GameObject prefab = Resources.Load<GameObject>("windows/settings")
                            ?? throw new InvalidOperationException("找不到原版多标签窗口 prefab: windows/settings");
        ScrollWindow prefabScrollWindow = prefab.GetComponent<ScrollWindow>()
                                        ?? throw new InvalidOperationException("原版 settings 缺少 ScrollWindow 组件");
        ListPool<GameObject> tabObjects = ScrollWindow.disableTabsInPrefab(prefabScrollWindow);
        GameObject window = Object.Instantiate(prefab, ModClass.I.PrefabLibrary);
        ScrollWindow.enableTabsInPrefab(tabObjects);

        window.SetActive(false);
        window.transform.SetParent(CanvasMain.instance.transformWindows);
        window.transform.localScale = Vector3.one;
        window.name = WindowId;

        ScrollWindow scrollWindow = window.GetComponent<ScrollWindow>()
                                    ?? throw new InvalidOperationException("模组介绍窗口缺少 ScrollWindow 组件");
        RemoveOriginalWindowBehaviour(window);
        ConfigureWindowBase(window, scrollWindow);
        ConfigureTabsAndContent(window, scrollWindow);

        window.AddComponent<WindowModInfo>();

        ScrollWindow._all_windows.Add(WindowId, scrollWindow);
        scrollWindow.screen_id = WindowId;
        scrollWindow.name = WindowId;
        scrollWindow.init();
        scrollWindow.create(true);
    }

    private static void EnsureWindowAsset()
    {
        if (AssetManager.window_library.has(WindowId)) return;

        AssetManager.window_library.add(new WindowAsset
        {
            id = WindowId,
            icon_path = "../../cultiway/icons/iconTab",
            preload = false,
            is_testable = false
        });
    }

    private static void RemoveOriginalWindowBehaviour(GameObject window)
    {
        foreach (TabbedWindow component in window.GetComponents<TabbedWindow>())
        {
            Object.DestroyImmediate(component);
        }
    }

    private static void ConfigureWindowBase(GameObject window, ScrollWindow scrollWindow)
    {
        Transform tabsRight = window.transform.Find("Tabs Right");
        if (tabsRight != null)
        {
            tabsRight.gameObject.SetActive(false);
        }

        Transform title = window.transform.Find("Background/Title");
        title?.GetComponent<LocalizedText>()?.setKeyAndUpdate("Cultiway.UI.WindowModInfo Title");
        ConfigureHeader(window);

        Transform scrollView = window.transform.Find("Background/Scroll View");
        if (scrollView != null)
        {
            RectTransform rect = scrollView.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(224f, rect.sizeDelta.y);
        }

        scrollWindow.historyActionEnabled = true;
    }

    private static void ConfigureTabsAndContent(GameObject window, ScrollWindow scrollWindow)
    {
        Transform content = window.transform.Find("Background/Scroll View/Viewport/Content")
                            ?? throw new InvalidOperationException("模组介绍窗口缺少 Content 节点");
        Transform tabsContainer = window.transform.Find("Background/Tabs")
                                  ?? throw new InvalidOperationException("模组介绍窗口缺少 Background/Tabs 节点");
        scrollWindow.tabs.init();
        BindTabsToWindow(scrollWindow);

        WindowMetaTab sourceTab = tabsContainer.GetComponentInChildren<WindowMetaTab>(true)
                                  ?? throw new InvalidOperationException("模组介绍窗口缺少可复用的原版 tab 按钮");
        GameObject sourceTabObject = Object.Instantiate(sourceTab.gameObject, ModClass.I.PrefabLibrary, false);
        WindowMetaTab sourceTabTemplate = sourceTabObject.GetComponent<WindowMetaTab>();
        Transform sourceTitleTemplate = content.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(item => item.name.StartsWith("tab_title_container", StringComparison.Ordinal));
        GameObject titleTemplateObject = sourceTitleTemplate == null
            ? null
            : Object.Instantiate(sourceTitleTemplate.gameObject, ModClass.I.PrefabLibrary, false);
        Transform titleTemplate = titleTemplateObject == null ? null : titleTemplateObject.transform;

        ClearContentChildren(content);
        ConfigureContentLayout(content);
        ClearChildren(tabsContainer);
        ConfigureTabsLayout(tabsContainer);

        scrollWindow.tabs._tabs.Clear();
        scrollWindow.tabs.tab_default = null;

        foreach (ModInfoPage page in Pages)
        {
            Transform pageContent = page.CreateContent(content, titleTemplate, ContentWidth);
            WindowMetaTab tab = CreateTab(tabsContainer, scrollWindow, sourceTabTemplate, page, pageContent);
            if (scrollWindow.tabs.tab_default == null)
            {
                scrollWindow.tabs.tab_default = tab;
            }
        }

        SanitizeTabs(scrollWindow.tabs);

        if (titleTemplateObject != null)
        {
            Object.DestroyImmediate(titleTemplateObject);
        }
        Object.DestroyImmediate(sourceTabObject);

        BindTabsToWindow(scrollWindow);
        scrollWindow.tabs.refillTabsWithContent();
    }

    private static void BindTabsToWindow(ScrollWindow scrollWindow)
    {
        if (scrollWindow?.tabs == null) return;

        scrollWindow.tabs._scroll_window = scrollWindow;
    }

    private static void SanitizeTabs(WindowMetaTabButtonsContainer tabs)
    {
        tabs._tabs.RemoveAll(tab => tab == null);
        foreach (WindowMetaTab tab in tabs._tabs)
        {
            tab.tab_elements ??= new List<Transform>();
            tab.tab_elements.RemoveAll(element => element == null);
        }
    }

    private static void ConfigureHeader(GameObject window)
    {
        Transform header = window.transform.Find("Background/Scroll View/Viewport/Header");
        if (header == null) return;

        header.gameObject.SetActive(true);

        Transform oldContent = header.Find(HeaderContentName);
        if (oldContent != null)
        {
            Object.DestroyImmediate(oldContent.gameObject);
        }

        RectTransform headerRect = header.GetComponent<RectTransform>();
        if (headerRect != null)
        {
            headerRect.sizeDelta = new Vector2(228f, HeaderContentHeight);
        }

        ConfigureHeaderRootLayout(header);

        GameObject content = new(HeaderContentName, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        content.transform.SetParent(header, false);
        content.transform.localScale = Vector3.one;
        AddLayout(content, ContentWidth, HeaderContentHeight);

        HorizontalLayoutGroup layout = content.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(4, 4, 2, 2);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        GameObject iconObj = new("Mod Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconObj.transform.SetParent(content.transform, false);
        iconObj.transform.localScale = Vector3.one;
        LayoutElement iconLayout = iconObj.GetComponent<LayoutElement>();
        iconLayout.minWidth = HeaderIconSize;
        iconLayout.preferredWidth = HeaderIconSize;
        iconLayout.minHeight = HeaderIconSize;
        iconLayout.preferredHeight = HeaderIconSize;
        Image iconImage = iconObj.GetComponent<Image>();
        iconImage.preserveAspect = true;
        SetImage(iconObj.transform, GetModIconSprite());

        GameObject infoObj = new("Mod Info", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        infoObj.transform.SetParent(content.transform, false);
        infoObj.transform.localScale = Vector3.one;
        LayoutElement infoLayout = infoObj.GetComponent<LayoutElement>();
        infoLayout.minWidth = HeaderTextWidth;
        infoLayout.preferredWidth = HeaderTextWidth;
        infoLayout.minHeight = 24f;
        infoLayout.preferredHeight = 24f;

        VerticalLayoutGroup infoGroup = infoObj.GetComponent<VerticalLayoutGroup>();
        infoGroup.spacing = 0f;
        infoGroup.childAlignment = TextAnchor.MiddleLeft;
        infoGroup.childControlWidth = true;
        infoGroup.childControlHeight = true;
        infoGroup.childForceExpandWidth = false;
        infoGroup.childForceExpandHeight = false;

        var declaration = ModClass.I.GetDeclaration();
        Text title = CreateText(infoObj.transform, "Name Version", 8, FontStyle.Bold, TextAnchor.MiddleLeft);
        title.text = $"{declaration.Name}  v{declaration.Version}";
        title.resizeTextForBestFit = true;
        title.resizeTextMinSize = 5;
        title.resizeTextMaxSize = 8;
        AddLayout(title.gameObject, HeaderTextWidth, 13f);

        Text authors = CreateText(infoObj.transform, "Authors", 6, FontStyle.Normal, TextAnchor.MiddleLeft);
        authors.color = new Color(0.82f, 0.78f, 0.68f, 1f);
        authors.text = $"作者: {declaration.Author}";
        authors.resizeTextForBestFit = true;
        authors.resizeTextMinSize = 4;
        authors.resizeTextMaxSize = 6;
        AddLayout(authors.gameObject, HeaderTextWidth, 11f);

        if (headerRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(headerRect);
        }
    }

    private static void ConfigureHeaderRootLayout(Transform header)
    {
        VerticalLayoutGroup layout = header.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.padding = new RectOffset(0, 0, 3, 3);
            layout.spacing = 0f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        LayoutElement headerLayout = header.GetComponent<LayoutElement>();
        if (headerLayout != null)
        {
            headerLayout.minHeight = HeaderContentHeight + 6f;
            headerLayout.preferredHeight = HeaderContentHeight + 6f;
            headerLayout.flexibleHeight = 0f;
        }
    }

    private static void ConfigureTabsLayout(Transform tabsContainer)
    {
        VerticalLayoutGroup layout = tabsContainer.GetComponent<VerticalLayoutGroup>();
        if (layout == null) return;

        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 2f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    private static void ConfigureContentLayout(Transform content)
    {
        foreach (WindowMetaElementBase element in content.GetComponents<WindowMetaElementBase>())
        {
            Object.DestroyImmediate(element);
        }

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static WindowMetaTab CreateTab(Transform tabsContainer, ScrollWindow scrollWindow, WindowMetaTab sourceTab, ModInfoPage page, Transform pageContent)
    {
        WindowMetaTab tab = Object.Instantiate(sourceTab.gameObject, tabsContainer, false).GetComponent<WindowMetaTab>();
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

        Transform iconTransform = tab.transform.Find("Icon");
        Image icon = iconTransform?.GetComponent<Image>();
        if (icon != null)
        {
            Sprite sprite = SpriteTextureLoader.getSprite(page.IconPath);
            icon.sprite = sprite;
            icon.overrideSprite = sprite;
        }

        pageContent.gameObject.SetActive(false);
        tab.gameObject.SetActive(true);
        tab.toggleActive(true);
        return tab;
    }

    private static Text CreateText(Transform parent, string name, int fontSize, FontStyle style, TextAnchor alignment)
    {
        GameObject obj = new(name, typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);
        obj.transform.localScale = Vector3.one;

        Text text = obj.GetComponent<Text>();
        text.font = UIUtils.GetCurrentFont();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static void AddLayout(GameObject obj, float width, float height)
    {
        RectTransform rect = obj.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(width, height);
        }

        LayoutElement layout = obj.GetComponent<LayoutElement>() ?? obj.AddComponent<LayoutElement>();
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.flexibleWidth = 0f;
        layout.minHeight = height;
        layout.preferredHeight = height;
        layout.flexibleHeight = 0f;
    }

    private static Sprite GetModIconSprite()
    {
        if (_modIconSprite != null) return _modIconSprite;

        var declaration = ModClass.I.GetDeclaration();
        string iconPath = string.IsNullOrWhiteSpace(declaration.IconPath) ? "icon.png" : declaration.IconPath;
        string fullPath = Path.Combine(declaration.FolderPath, iconPath);
        if (File.Exists(fullPath))
        {
            Texture2D texture = new(2, 2)
            {
                filterMode = FilterMode.Point
            };
            if (texture.LoadImage(File.ReadAllBytes(fullPath)))
            {
                _modIconSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    1f);
            }
        }

        return _modIconSprite ?? SpriteTextureLoader.getSprite("cultiway/icons/iconTab");
    }

    private static void SetImage(Transform transform, Sprite sprite)
    {
        Image image = transform?.GetComponent<Image>();
        if (image == null) return;

        image.sprite = sprite;
        image.overrideSprite = sprite;
    }

    private static void ClearChildren(Transform transform)
    {
        List<GameObject> children = new();
        foreach (Transform child in transform)
        {
            children.Add(child.gameObject);
        }

        foreach (GameObject child in children)
        {
            Object.DestroyImmediate(child);
        }
    }

    private static void ClearContentChildren(Transform content)
    {
        List<GameObject> children = new();
        foreach (Transform child in content)
        {
            if (child.name == "runes_parent") continue;
            children.Add(child.gameObject);
        }

        foreach (GameObject child in children)
        {
            Object.DestroyImmediate(child);
        }
    }

}
