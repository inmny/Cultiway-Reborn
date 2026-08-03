using System;
using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Cultiway.UI;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.CreatureInfoPages;

/// <summary>由固定主体和两层实例强调色组成的境界纹章。</summary>
internal sealed class RealmEmblemView : MonoBehaviour
{
    private Image baseLayer;
    private Image primaryLayer;
    private Image secondaryLayer;
    private Color primaryColor;
    private Color secondaryColor;

    /// <summary>创建固定尺寸的三层纹章。</summary>
    public static RealmEmblemView Create(Transform parent, float size)
    {
        var obj = new GameObject("Realm Emblem", typeof(RectTransform), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        UiLayout.SetSize(obj.transform, size, size);

        Image baseLayer = CreateLayer(obj.transform, "Base");
        Image primaryLayer = CreateLayer(obj.transform, "Primary");
        Image secondaryLayer = CreateLayer(obj.transform, "Secondary");

        var view = obj.AddComponent<RealmEmblemView>();
        view.baseLayer = baseLayer;
        view.primaryLayer = primaryLayer;
        view.secondaryLayer = secondaryLayer;
        return view;
    }

    /// <summary>刷新纹章三层贴图和实例强调色。</summary>
    public void SetPresentation(RealmEmblemPresentation presentation)
    {
        baseLayer.sprite = SpriteTextureLoader.getSprite(presentation.BasePath);
        primaryLayer.sprite = SpriteTextureLoader.getSprite(presentation.PrimaryPath);
        secondaryLayer.sprite = SpriteTextureLoader.getSprite(presentation.SecondaryPath);
        primaryColor = presentation.PrimaryColor;
        secondaryColor = presentation.SecondaryColor;
        ApplyAnimatedColors(1f);
    }

    private void Update()
    {
        float time = Time.unscaledTime;
        float pulse = 0.5f + Mathf.Sin(time * 2.2f) * 0.5f;
        transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.03f, pulse);
        primaryLayer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, time * 2.1f);
        secondaryLayer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -time * 2.8f);
        ApplyAnimatedColors(pulse);
    }

    private void OnDisable()
    {
        transform.localScale = Vector3.one;
        primaryLayer.rectTransform.localRotation = Quaternion.identity;
        secondaryLayer.rectTransform.localRotation = Quaternion.identity;
    }

    private void ApplyAnimatedColors(float pulse)
    {
        primaryLayer.color = WithAlpha(primaryColor, Mathf.Lerp(0.78f, 1f, pulse));
        secondaryLayer.color = WithAlpha(secondaryColor, Mathf.Lerp(0.7f, 0.94f, 1f - pulse));
    }

    private static Image CreateLayer(Transform parent, string name)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parent, false);
        UiLayout.Stretch(obj.GetComponent<RectTransform>(), 2f, 2f, 2f, 2f);
        var image = obj.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a *= alpha;
        return color;
    }
}

/// <summary>三个修仙境界页共用的纹章、名称和摘要。</summary>
internal sealed class XianRealmHeaderView
{
    private readonly RealmEmblemView emblem;
    private readonly Text name;
    private readonly Text primary;
    private readonly Text secondary;

    private XianRealmHeaderView(RealmEmblemView emblem, Text name, Text primary, Text secondary)
    {
        this.emblem = emblem;
        this.name = name;
        this.primary = primary;
        this.secondary = secondary;
    }

    /// <summary>创建 52 像素高的公共境界页头部。</summary>
    public static XianRealmHeaderView Create(Transform parent)
    {
        GameObject root = UiLayout.Create(parent, "Realm Header", true,
            XianRealmPagePresentation.PageWidth, 52f, 4f, TextAnchor.MiddleLeft);
        RealmEmblemView emblem = RealmEmblemView.Create(root.transform, 48f);

        GameObject textColumn = UiLayout.Create(root.transform, "Summary", false, 170f, 50f, 1f);
        Text name = UiElements.CreateText(textColumn.transform, "Name", string.Empty, 170f, 22f, 9,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        name.color = UiTheme.Current.Palette.AccentText;
        ConfigureBestFit(name, 6, 9);
        Text primary = UiElements.CreateText(textColumn.transform, "Primary", string.Empty, 170f, 13f, 7,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        ConfigureBestFit(primary, 5, 7);
        Text secondary = UiElements.CreateText(textColumn.transform, "Secondary", string.Empty, 170f, 13f, 7,
            TextAnchor.MiddleLeft);
        secondary.color = UiTheme.Current.Palette.MutedText;
        ConfigureBestFit(secondary, 5, 7);

        return new XianRealmHeaderView(emblem, name, primary, secondary);
    }

    /// <summary>刷新头部文字和纹章。</summary>
    public void Set(
        RealmEmblemPresentation emblemPresentation,
        string displayName,
        string primaryText,
        string secondaryText)
    {
        emblem.SetPresentation(emblemPresentation);
        name.text = displayName;
        primary.text = primaryText;
        secondary.text = secondaryText;
        secondary.gameObject.SetActive(!string.IsNullOrEmpty(secondaryText));
    }

    private static void ConfigureBestFit(Text text, int minSize, int maxSize)
    {
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = minSize;
        text.resizeTextMaxSize = maxSize;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }
}

/// <summary>筑基三花或五气区域中的单个图标数值。</summary>
internal sealed class FoundationMetricCell
{
    private const float Width = 44f;
    private const float Height = 22f;

    private readonly CharacterPanelIconValue value;
    private readonly string iconPath;
    private readonly string label;
    private readonly Color activeColor;

    private FoundationMetricCell(
        CharacterPanelIconValue value,
        string iconPath,
        string label,
        Color activeColor)
    {
        this.value = value;
        this.iconPath = iconPath;
        this.label = label;
        this.activeColor = activeColor;
    }

    /// <summary>复用原版 ui/IconValue 创建带 windowInnerSliced 表面的品阶单元。</summary>
    public static FoundationMetricCell Create(
        Transform parent,
        string name,
        string iconPath,
        string label,
        Color activeColor)
    {
        CharacterPanelIconValue value = CharacterPanelIconValue.Instantiate(parent, false, name);
        UiLayout.SetSize(value.transform, Width, Height);
        return new FoundationMetricCell(value, iconPath, label, activeColor);
    }

    /// <summary>刷新品阶文本，并在 Tooltip 中保留精确的原始强度。</summary>
    public void SetValue(float amount)
    {
        bool active = XianRealmPagePresentation.IsPositive(amount);
        string levelName = active
            ? StrengthLevelFormatter.GetLevelName(amount, Cultisyses.Xian.DisplayStyle)
            : "Cultiway.RealmPage.Foundation.Unformed".Localize();
        string detail = active
            ? string.Format("Cultiway.RealmPage.Foundation.RawStrength".Localize(),
                XianRealmPagePresentation.FormatNumber(amount))
            : string.Empty;
        value.Setup(new CharacterPanelIconValueState(
            levelName,
            iconPath,
            label,
            detail,
            textColor: active
                ? activeColor
                : UiTheme.Current.Palette.Disabled));
    }
}

/// <summary>筑基页的一组归一化比例条和品阶单元。</summary>
internal sealed class FoundationMetricGroup
{
    private readonly UiWeightedSegmentBar bar;
    private readonly FoundationMetricCell[] cells;

    private FoundationMetricGroup(UiWeightedSegmentBar bar, FoundationMetricCell[] cells)
    {
        this.bar = bar;
        this.cells = cells;
    }

    /// <summary>创建三花或五气指标组。</summary>
    public static FoundationMetricGroup Create(
        Transform parent,
        string objectName,
        string title,
        string[] iconPaths,
        string[] labels,
        Color[] colors)
    {
        GameObject root = UiLayout.Create(parent, objectName, false,
            XianRealmPagePresentation.PageWidth, 60f, 3f);
        Text heading = UiElements.CreateText(root.transform, "Title", title,
            XianRealmPagePresentation.PageWidth, 13f, 8, TextAnchor.MiddleLeft, FontStyle.Bold);
        heading.color = UiTheme.Current.Palette.AccentText;
        UiWeightedSegmentBar bar = UiWeightedSegmentBar.Create(root.transform, "Composition", 246f, 8f);

        GameObject row = UiLayout.Create(root.transform, "Metrics", true,
            XianRealmPagePresentation.PageWidth, 33f, 5f, TextAnchor.MiddleCenter);
        var cells = new FoundationMetricCell[labels.Length];
        for (var i = 0; i < cells.Length; i++)
        {
            cells[i] = FoundationMetricCell.Create(row.transform, $"Metric {i}",
                iconPaths[i], labels[i], colors[i]);
        }
        return new FoundationMetricGroup(bar, cells);
    }

    /// <summary>刷新比例条和各项品阶。</summary>
    public void SetValues(float[] values, Color[] colors)
    {
        bar.SetSegments(values, colors);
        for (var i = 0; i < cells.Length; i++) cells[i].SetValue(values[i]);
    }
}

/// <summary>八元素比例条下方的单个图例。</summary>
internal sealed class ElementLegendEntry
{
    private readonly GameObject root;
    private readonly Image icon;
    private readonly Text value;
    private readonly int elementIndex;

    private ElementLegendEntry(GameObject root, Image icon, Text value, int elementIndex)
    {
        this.root = root;
        this.icon = icon;
        this.value = value;
        this.elementIndex = elementIndex;
    }

    /// <summary>创建一个紧凑元素图例。</summary>
    public static ElementLegendEntry Create(Transform parent, int elementIndex)
    {
        GameObject root = UiLayout.Create(parent, $"Element {elementIndex}", true, 29f, 13f, 1f,
            TextAnchor.MiddleLeft);
        var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(LayoutElement));
        iconObject.transform.SetParent(root.transform, false);
        UiLayout.SetSize(iconObject.transform, 9f, 9f);
        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = SpriteTextureLoader.getSprite(XianRealmPagePresentation.ElementIconPaths[elementIndex]);
        icon.preserveAspect = true;

        Text value = UiElements.CreateText(root.transform, "Value", string.Empty, 19f, 13f, 5,
            TextAnchor.MiddleRight, FontStyle.Bold);
        return new ElementLegendEntry(root, icon, value, elementIndex);
    }

    /// <summary>按实际占比刷新图例并隐藏零占比元素。</summary>
    public void SetValue(float ratio)
    {
        bool visible = XianRealmPagePresentation.IsPositive(ratio);
        root.SetActive(visible);
        if (!visible) return;
        value.text = ratio.ToString("P0");
        value.color = XianRealmPagePresentation.GetElementColor(elementIndex);
        UiTooltip.Set(icon.gameObject, ElementIndex.ElementNames[elementIndex].Localize(), value.text);
    }
}

/// <summary>构成区中一个已经显化的原子条目。</summary>
internal sealed class CoreFormationAtomEntry
{
    private readonly GameObject root;
    private readonly Image icon;
    private readonly Text label;
    private CoreFormationEffectTooltipModel tooltipModel;

    private CoreFormationAtomEntry(GameObject root, Image icon, Text label)
    {
        this.root = root;
        this.icon = icon;
        this.label = label;
    }

    /// <summary>创建 78×18 的无边框原子条目。</summary>
    public static CoreFormationAtomEntry Create(Transform parent, int index)
    {
        GameObject root = UiLayout.Create(parent, $"Atom {index}", true, 78f, 18f, 2f,
            TextAnchor.MiddleLeft);
        Image hitArea = root.AddComponent<Image>();
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;

        var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(LayoutElement));
        iconObject.transform.SetParent(root.transform, false);
        UiLayout.SetSize(iconObject.transform, 14f, 14f);
        Image icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        Text label = UiElements.CreateText(root.transform, "Name", string.Empty, 62f, 18f, 6,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 4;
        label.resizeTextMaxSize = 6;
        var entry = new CoreFormationAtomEntry(root, icon, label);
        UiTooltip.Set(root, entry.ShowTooltip);
        return entry;
    }

    /// <summary>刷新原子图标、名称、继承来源和该原子提供的特殊效果提示。</summary>
    public void SetValue(
        CoreFormationAtomPresentation atom,
        CoreFormationRealm realm,
        ActorExtend actor,
        IList<CoreFormationResolvedEffect> resolvedEffects,
        bool includeRuntimeState)
    {
        root.SetActive(true);
        icon.sprite = SpriteTextureLoader.getSprite(atom.Asset.icon_path);
        label.text = atom.Asset.GetName();

        bool inherited = atom.State.inherited;
        Color realmColor = XianRealmPagePresentation.GetRealmColor(realm);
        label.color = inherited
            ? Color.Lerp(
                XianRealmPagePresentation.GetInheritedRealmColor(realm),
                UiTheme.Current.Palette.MutedText,
                0.35f)
            : realmColor;
        string origin = inherited
            ? "Cultiway.RealmPage.Atom.Inherited".Localize()
            : "Cultiway.RealmPage.Atom.Manifested".Localize();
        tooltipModel = CoreFormationEffectPresentation.BuildAtomTooltip(
            actor,
            atom.Asset,
            origin,
            resolvedEffects,
            includeRuntimeState);
    }

    /// <summary>隐藏当前未使用的固定槽位。</summary>
    public void Hide()
    {
        root.SetActive(false);
    }

    /// <summary>在当前条目上打开已经绑定的核心构成特殊效果 Tooltip。</summary>
    private void ShowTooltip()
    {
        CoreFormationEffectTooltip.Show(root, tooltipModel);
    }
}

/// <summary>代表法术的首帧预览和名称行。</summary>
internal sealed class RepresentativeSkillView
{
    private readonly GameObject iconObject;
    private readonly Image icon;
    private readonly Text name;

    private RepresentativeSkillView(GameObject iconObject, Image icon, Text name)
    {
        this.iconObject = iconObject;
        this.icon = icon;
        this.name = name;
    }

    /// <summary>创建固定高度的代表法术行。</summary>
    public static RepresentativeSkillView Create(Transform parent)
    {
        GameObject root = UiLayout.Create(parent, "Representative Skill", true,
            XianRealmPagePresentation.PageWidth, 20f, 3f, TextAnchor.MiddleLeft);
        Text title = UiElements.CreateText(root.transform, "Title",
            "Cultiway.RealmPage.Skill.Title".Localize(), 58f, 20f, 7,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        title.color = UiTheme.Current.Palette.AccentText;

        var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(LayoutElement));
        iconObject.transform.SetParent(root.transform, false);
        UiLayout.SetSize(iconObject.transform, 18f, 18f);
        Image icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;

        Text name = UiElements.CreateText(root.transform, "Name", string.Empty, 164f, 20f, 7,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        name.resizeTextForBestFit = true;
        name.resizeTextMinSize = 5;
        name.resizeTextMaxSize = 7;
        return new RepresentativeSkillView(iconObject, icon, name);
    }

    /// <summary>刷新法术首帧、名称和现有编辑器说明。</summary>
    public void SetSkill(string skillId)
    {
        SkillEntityAsset asset = XianRealmPagePresentation.ResolveRepresentativeSkill(skillId);
        name.text = XianRealmPagePresentation.ResolveSkillName(asset);
        iconObject.SetActive(asset != null);
        if (asset == null) return;

        icon.sprite = XianRealmPagePresentation.ResolveSkillPreview(asset);
        UiTooltip.Set(iconObject,
            XianRealmPagePresentation.ResolveSkillName(asset),
            XianRealmPagePresentation.ResolveSkillDescription(asset));
    }
}
