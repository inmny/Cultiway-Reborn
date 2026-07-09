using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Localization;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using NeoModLoader.General;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI;

public class WindowRealmNames : TabbedWindow
{
    public const string WindowId = "Cultiway.UI.WindowRealmNames";

    private const float ContentWidth = 214f;
    private const float InnerWidth = ContentWidth - 14f;
    private const float RowHeight = 18f;
    private const float LabelWidth = 58f;
    private const float InputWidth = InnerWidth - LabelWidth - 4f;

    private static readonly RealmNamePage[] Pages =
    [
        new XianRealmNamePage(),
        new MagicRealmNamePage()
    ];

    internal static void Init()
    {
        if (ScrollWindow.windowLoaded(WindowId)) return;

        RegisterWindowAssetIfMissing();

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
                                    ?? throw new InvalidOperationException("境界名称窗口缺少 ScrollWindow 组件");
        RemoveOriginalWindowBehaviour(window);
        ConfigureWindowBase(window, scrollWindow);
        ConfigureTabsAndContent(window, scrollWindow);

        window.AddComponent<WindowRealmNames>();

        ScrollWindow._all_windows.Add(WindowId, scrollWindow);
        scrollWindow.screen_id = WindowId;
        scrollWindow.name = WindowId;
        scrollWindow.init();
        scrollWindow.create(true);
    }

    private static void RegisterWindowAssetIfMissing()
    {
        if (AssetManager.window_library.has(WindowId)) return;

        AssetManager.window_library.add(new WindowAsset
        {
            id = WindowId,
            icon_path = "../../cultiway/icons/iconCultivation",
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
        title?.GetComponent<LocalizedText>()?.setKeyAndUpdate("Cultiway.UI.WindowRealmNames Title");

        Transform header = window.transform.Find("Background/Scroll View/Viewport/Header");
        if (header != null)
        {
            LayoutElement headerLayout = header.GetComponent<LayoutElement>();
            if (headerLayout != null)
            {
                headerLayout.minHeight = 0f;
                headerLayout.preferredHeight = 0f;
            }

            header.gameObject.SetActive(false);
        }

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
                            ?? throw new InvalidOperationException("境界名称窗口缺少 Content 节点");
        Transform tabsContainer = window.transform.Find("Background/Tabs")
                                  ?? throw new InvalidOperationException("境界名称窗口缺少 Background/Tabs 节点");
        scrollWindow.tabs.init();
        BindTabsToWindow(scrollWindow);

        WindowMetaTab sourceTab = tabsContainer.GetComponentInChildren<WindowMetaTab>(true)
                                  ?? throw new InvalidOperationException("境界名称窗口缺少可复用的原版 tab 按钮");
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

        foreach (RealmNamePage page in Pages)
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

    private static WindowMetaTab CreateTab(Transform tabsContainer, ScrollWindow scrollWindow, WindowMetaTab sourceTab, RealmNamePage page, Transform pageContent)
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

    private abstract class RealmNamePage
    {
        private const string SectionId = "cultisys_level_names";

        private readonly List<InputField> _inputs = new();

        public abstract string Id { get; }
        public abstract string TitleKey { get; }
        public abstract string DescriptionKey { get; }
        public abstract string IconPath { get; }
        protected abstract string SystemId { get; }
        protected abstract int LevelCount { get; }

        public Transform CreateContent(Transform parent, Transform titleTemplate, float width)
        {
            _inputs.Clear();

            GameObject root = new($"content_{Id}", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one;
            SetLayoutWidth(root.transform, width);

            VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = root.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Transform title = CreateTitleContainer(root.transform, titleTemplate, width, TitleKey);
            SetLayoutWidth(title, width, false);

            BuildContent(root.transform, width);

            root.SetActive(false);
            return root.transform;
        }

        protected virtual string GetDefaultLevelName(int level)
        {
            return LM.Get(GetDefaultLocaleKey(level));
        }

        private void BuildContent(Transform root, float width)
        {
            Transform card = CreateCard(root, "Realm Name Settings", width);
            AddText(card,
                "Description",
                LMTools.GetOrKey("Cultiway.UI.WindowRealmNames Description"),
                6,
                FontStyle.Normal,
                TextAnchor.UpperLeft,
                new Color(0.82f, 0.79f, 0.68f, 1f),
                InnerWidth);

            Transform actions = CreatePlainGroup(card, "Actions", InnerWidth, true, 4f, TextAnchor.MiddleLeft, false);
            SetLayout(actions, InnerWidth, RowHeight);
            AddActionButton(actions,
                "Reset",
                LMTools.GetOrKey("Cultiway.UI.WindowRealmNames.Reset"),
                () =>
                {
                    ModifiableLocalizationManager.ResetCultiLevelGroup(SectionId, SystemId);
                    RefreshInputs();
                });

            AddDivider(card, InnerWidth);

            for (int i = 0; i < LevelCount; i++)
            {
                AddRealmNameRow(card, i);
            }
        }

        private void AddRealmNameRow(Transform parent, int level)
        {
            Transform row = CreatePlainGroup(parent, $"Level {level}", InnerWidth, true, 4f, TextAnchor.MiddleLeft, false);
            SetLayout(row, InnerWidth, RowHeight);

            Text label = CreateText(row, $"Level {level} Label", 6, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.95f, 0.81f, 0.45f, 1f));
            label.text = $"第{level + 1:00}境";
            SetLayout(label.transform, LabelWidth, RowHeight);

            InputField input = CreateInput(row, $"Level {level} Input", level);
            _inputs.Add(input);
        }

        private InputField CreateInput(Transform parent, string name, int level)
        {
            GameObject obj = new(name, typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            obj.transform.localScale = Vector3.one;
            SetLayout(obj.transform, InputWidth, RowHeight);

            Image background = obj.GetComponent<Image>();
            background.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
            background.type = Image.Type.Sliced;
            background.color = new Color(0.13f, 0.15f, 0.12f, 0.95f);

            Text text = CreateInputText(obj.transform, "Text", FontStyle.Normal, Color.white);
            Text placeholder = CreateInputText(obj.transform, "Placeholder", FontStyle.Italic, new Color(0.6f, 0.58f, 0.5f, 0.72f));
            placeholder.text = GetDefaultLevelName(level);

            InputField input = obj.GetComponent<InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 16;
            input.text = GetConfigText(level);
            input.onEndEdit.AddListener(new UnityAction<string>(value =>
            {
                ModifiableLocalizationManager.UpdateText(SectionId, GetConfigKey(level), value);
                input.text = GetConfigText(level);
            }));
            return input;
        }

        private Text CreateInputText(Transform parent, string name, FontStyle style, Color color)
        {
            Text text = CreateText(parent, name, 7, style, TextAnchor.MiddleLeft, color);
            RectTransform rect = text.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(4f, 1f);
            rect.offsetMax = new Vector2(-4f, -1f);
            return text;
        }

        private void RefreshInputs()
        {
            for (int i = 0; i < _inputs.Count; i++)
            {
                InputField input = _inputs[i];
                if (input == null) continue;

                input.text = GetConfigText(i);
            }
        }

        private string GetConfigText(int level)
        {
            return ModifiableLocalizationManager.GetText(SectionId, GetConfigKey(level));
        }

        private string GetConfigKey(int level)
        {
            return $"{SystemId}.{level}";
        }

        private string GetDefaultLocaleKey(int level)
        {
            return $"cultisys_{SystemId}_{level}";
        }

        private static Transform CreateTitleContainer(Transform parent, Transform template, float width, string titleKey)
        {
            if (template != null)
            {
                Transform title = Object.Instantiate(template.gameObject, parent, false).transform;
                title.name = "tab_title_container_realm_names";

                Text text = title.GetComponentInChildren<Text>(true);
                if (text != null)
                {
                    text.text = LMTools.GetOrKey(titleKey);
                }

                LocalizedText localizedText = title.GetComponentInChildren<LocalizedText>(true);
                localizedText?.setKeyAndUpdate(titleKey);
                return title;
            }

            GameObject obj = new("tab_title_container_realm_names", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            obj.transform.localScale = Vector3.one;
            Text fallback = obj.GetComponent<Text>();
            fallback.font = UIUtils.GetCurrentFont();
            fallback.fontSize = 9;
            fallback.fontStyle = FontStyle.Bold;
            fallback.alignment = TextAnchor.MiddleCenter;
            fallback.color = new Color(1f, 0.64f, 0.16f, 1f);
            fallback.text = LMTools.GetOrKey(titleKey);
            SetLayout(obj.transform, width, 18f);
            return obj.transform;
        }

        private static Transform CreateCard(Transform parent, string name, float width)
        {
            GameObject card = new(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
            card.transform.SetParent(parent, false);
            card.transform.localScale = Vector3.one;
            SetLayoutWidth(card.transform, width);

            Image background = card.GetComponent<Image>();
            background.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
            background.type = Image.Type.Sliced;
            background.color = Color.white;

            VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(7, 7, 6, 6);
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = card.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return card.transform;
        }

        private static Transform CreatePlainGroup(Transform parent, string name, float width, bool horizontal, float spacing, TextAnchor alignment, bool fitHeight)
        {
            Type layoutType = horizontal ? typeof(HorizontalLayoutGroup) : typeof(VerticalLayoutGroup);
            GameObject group = new(name, typeof(RectTransform), layoutType, typeof(LayoutElement));
            group.transform.SetParent(parent, false);
            group.transform.localScale = Vector3.one;
            SetLayoutWidth(group.transform, width);
            if (fitHeight)
            {
                ContentSizeFitter fitter = group.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            if (horizontal)
            {
                HorizontalLayoutGroup layout = group.GetComponent<HorizontalLayoutGroup>();
                layout.spacing = spacing;
                layout.childAlignment = alignment;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
            }
            else
            {
                VerticalLayoutGroup layout = group.GetComponent<VerticalLayoutGroup>();
                layout.spacing = spacing;
                layout.childAlignment = alignment;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
            }

            return group.transform;
        }

        private static Text AddText(Transform parent, string name, string value, int fontSize, FontStyle style, TextAnchor alignment, Color color, float width)
        {
            Text text = CreateText(parent, name, fontSize, style, alignment, color);
            text.text = value;
            int lineCount = Math.Max(1, Mathf.CeilToInt(value.Length / 26f));
            SetLayout(text.transform, width, lineCount * (fontSize + 4f) + 2f);
            return text;
        }

        private static Text CreateText(Transform parent, string name, int fontSize, FontStyle style, TextAnchor alignment, Color color)
        {
            GameObject obj = new(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            obj.transform.localScale = Vector3.one;

            Text text = obj.GetComponent<Text>();
            text.font = UIUtils.GetCurrentFont();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void AddActionButton(Transform parent, string name, string value, UnityAction action)
        {
            GameObject obj = new(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            obj.transform.localScale = Vector3.one;
            SetLayout(obj.transform, 56f, RowHeight);

            Image background = obj.GetComponent<Image>();
            background.sprite = SpriteTextureLoader.getSprite("ui/button");
            background.type = Image.Type.Sliced;
            background.color = Color.white;

            Button button = obj.GetComponent<Button>();
            button.onClick.AddListener(action);

            Text label = CreateText(obj.transform, "Text", 6, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            label.text = value;
            RectTransform rect = label.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AddDivider(Transform parent, float width)
        {
            GameObject obj = new("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            obj.transform.localScale = Vector3.one;
            SetLayout(obj.transform, width, 1f);

            Image image = obj.GetComponent<Image>();
            image.color = new Color(0.12f, 0.1f, 0.08f, 0.45f);
        }

        private static void SetLayoutWidth(Transform transform, float width, bool resetHeight = true)
        {
            LayoutElement layout = transform.GetComponent<LayoutElement>() ?? transform.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.flexibleWidth = 0f;

            RectTransform rect = transform.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(width, resetHeight ? 0f : rect.sizeDelta.y);
            }
        }

        private static void SetLayout(Transform transform, float width, float height)
        {
            RectTransform rect = transform.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(width, height);
            }

            LayoutElement layout = transform.GetComponent<LayoutElement>() ?? transform.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.flexibleWidth = 0f;
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleHeight = 0f;
        }
    }

    private sealed class XianRealmNamePage : RealmNamePage
    {
        public override string Id => "Xian";
        public override string TitleKey => "Cultiway.UI.WindowRealmNames.Tab.Xian";
        public override string DescriptionKey => "Cultiway.UI.WindowRealmNames.Tab.Xian Description";
        public override string IconPath => "cultiway/icons/iconCultivation";
        protected override string SystemId => "Xian";
        protected override int LevelCount => 20;
    }

    private sealed class MagicRealmNamePage : RealmNamePage
    {
        public override string Id => "Magic";
        public override string TitleKey => "Cultiway.UI.WindowRealmNames.Tab.Magic";
        public override string DescriptionKey => "Cultiway.UI.WindowRealmNames.Tab.Magic Description";
        public override string IconPath => "cultiway/icons/iconMagic";
        protected override string SystemId => "Magic";
        protected override int LevelCount => 10;
    }
}
