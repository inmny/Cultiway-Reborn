using System;
using System.Collections.Generic;
using Cultiway.Core.Localization;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using NeoModLoader.General;
using UnityEngine;
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

    private static readonly IUiTabbedPage[] Pages =
    [
        new XianRealmNamePage(),
        new MagicRealmNamePage(),
    ];

    internal static void Init()
    {
        UiTabbedWindowAdapter.Create<WindowRealmNames>(new UiTabbedWindowOptions(
            WindowId,
            "Cultiway.UI.WindowRealmNames Title",
            "../../cultiway/icons/iconCultivation",
            "境界名称窗口")
        {
            ContentWidth = ContentWidth,
            HideHeader = true,
        }, Pages);
    }

    private abstract class RealmNamePage : IUiTabbedPage
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
            GameObject root = CreateAutoGroup(parent, $"content_{Id}", width, false,
                UiTheme.Current.Metrics.SpacingSm, TextAnchor.UpperCenter);
            CreateTitle(root.transform, titleTemplate, width);
            BuildContent(root.transform);
            root.SetActive(false);
            return root.transform;
        }

        private void BuildContent(Transform root)
        {
            Transform card = CreateCard(root, "Realm Name Settings", ContentWidth);
            Text description = CreateAutoText(card, "Description",
                LMTools.GetOrKey("Cultiway.UI.WindowRealmNames Description"), 6);
            description.color = UiTheme.Current.Palette.MutedText;

            GameObject actions = UiLayout.Create(card, "Actions", true, InnerWidth, RowHeight,
                UiTheme.Current.Metrics.SpacingSm, TextAnchor.MiddleLeft);
            UiElements.CreateButton(actions.transform, "Reset",
                LMTools.GetOrKey("Cultiway.UI.WindowRealmNames.Reset"), 56f, RowHeight, () =>
                {
                    ModifiableLocalizationManager.ResetCultiLevelGroup(SectionId, SystemId);
                    RefreshInputs();
                });

            GameObject divider = new("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            divider.transform.SetParent(card, false);
            UiLayout.SetSize(divider.transform, InnerWidth, 1f);
            divider.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.16f);

            for (int level = 0; level < LevelCount; level++) AddRealmNameRow(card, level);
        }

        private void AddRealmNameRow(Transform parent, int level)
        {
            GameObject row = UiLayout.Create(parent, $"Level {level}", true, InnerWidth, RowHeight,
                UiTheme.Current.Metrics.SpacingSm, TextAnchor.MiddleLeft);
            Text label = UiElements.CreateText(row.transform, $"Level {level} Label", $"第{level + 1:00}境",
                LabelWidth, RowHeight, 6, TextAnchor.MiddleLeft, FontStyle.Bold);
            label.color = UiTheme.Current.Palette.AccentText;

            InputField input = UiElements.CreateInput(row.transform, $"Level {level} Input",
                GetConfigText(level), GetDefaultLevelName(level), InputWidth, RowHeight);
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 16;
            input.onEndEdit.AddListener(value =>
            {
                ModifiableLocalizationManager.UpdateText(SectionId, GetConfigKey(level), value);
                input.text = GetConfigText(level);
            });
            _inputs.Add(input);
        }

        private void RefreshInputs()
        {
            for (int level = 0; level < _inputs.Count; level++)
                _inputs[level].text = GetConfigText(level);
        }

        private string GetConfigText(int level)
        {
            return ModifiableLocalizationManager.GetText(SectionId, GetConfigKey(level));
        }

        private string GetConfigKey(int level)
        {
            return $"{SystemId}.{level}";
        }

        private string GetDefaultLevelName(int level)
        {
            return LM.Get($"cultisys_{SystemId}_{level}");
        }

        private void CreateTitle(Transform parent, Transform template, float width)
        {
            Transform title = Object.Instantiate(template.gameObject, parent, false).transform;
            title.name = $"tab_title_container_{Id}";
            title.GetComponentInChildren<Text>(true).text = LMTools.GetOrKey(TitleKey);
            title.GetComponentInChildren<LocalizedText>(true)?.setKeyAndUpdate(TitleKey);
            UiLayout.SetWidth(title, width, false);
        }

        private static Transform CreateCard(Transform parent, string name, float width)
        {
            GameObject card = CreateAutoGroup(parent, name, width, false,
                UiTheme.Current.Metrics.SpacingXs + 1f, TextAnchor.UpperLeft);
            Image background = card.AddComponent<Image>();
            UiResources.ApplySurface(background, UiSurface.WindowInner);
            card.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(7, 7, 6, 6);
            return card.transform;
        }

        private static GameObject CreateAutoGroup(Transform parent, string name, float width, bool horizontal,
            float spacing, TextAnchor alignment)
        {
            Type layoutType = horizontal ? typeof(HorizontalLayoutGroup) : typeof(VerticalLayoutGroup);
            GameObject group = new(name, typeof(RectTransform), layoutType, typeof(LayoutElement),
                typeof(ContentSizeFitter));
            group.transform.SetParent(parent, false);
            UiLayout.SetWidth(group.transform, width);
            group.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            HorizontalOrVerticalLayoutGroup layout = (HorizontalOrVerticalLayoutGroup)group.GetComponent(layoutType);
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = !horizontal;
            layout.childForceExpandHeight = false;
            return group;
        }

        private static Text CreateAutoText(Transform parent, string name, string value, int fontSize)
        {
            Text text = UiElements.CreateText(parent, name, value, InnerWidth, 0f, fontSize,
                TextAnchor.UpperLeft, FontStyle.Normal, VerticalWrapMode.Overflow);
            LayoutElement layout = text.GetComponent<LayoutElement>();
            layout.minHeight = 0f;
            layout.preferredHeight = -1f;
            return text;
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
