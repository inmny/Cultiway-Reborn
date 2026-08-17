using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.UI.Prefab;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.UI;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.Content.UI.CreatureInfoPages;

/// <summary>功法页顶部的主修功法摘要行。</summary>
internal sealed class CultibookSummaryRow : APrefabPreview<CultibookSummaryRow>
{
    private Image icon;
    private Text nameLabel;
    private Text levelLabel;
    private Text masteryLabel;
    private Text affinityLabel;
    private CultibookMasteryStrip masteryStrip;

    protected override void OnInit()
    {
        icon = transform.Find("Icon").GetComponent<Image>();
        nameLabel = transform.Find("Content/Heading/Name").GetComponent<Text>();
        levelLabel = transform.Find("Content/Heading/Level").GetComponent<Text>();
        masteryLabel = transform.Find("Content/Meta/Mastery").GetComponent<Text>();
        affinityLabel = transform.Find("Content/Meta/Affinity").GetComponent<Text>();
        masteryStrip = new CultibookMasteryStrip(transform.Find("Content/MasteryStrip/Fill") as RectTransform);
    }

    /// <summary>绑定人物当前主修功法及人物上下文 Tooltip。</summary>
    public void Setup(CultibookEntryModel model, ActorExtend actor)
    {
        Init();
        icon.sprite = SpriteTextureLoader.getSprite(model.CoverPath);
        nameLabel.text = model.Asset.Name;
        levelLabel.text = model.Asset.Level.GetName();
        masteryLabel.text = string.Format(
            "Cultiway.CultibookPage.Format.Mastery".Localize(),
            model.Mastery);
        affinityLabel.text = model.HasAffinity
            ? string.Format("Cultiway.CultibookPage.Format.Affinity".Localize(),
                CultibookPagePresentation.FormatPercent(model.Affinity))
            : "Cultiway.CultibookPage.Value.NoRoot".Localize();
        affinityLabel.color = !model.HasAffinity
            ? UiTheme.Current.Palette.Disabled
            : model.Affinity >= model.Asset.ElementAffinityThreshold
                ? UiTheme.Current.Palette.Success
                : UiTheme.Current.Palette.Warning;
        masteryStrip.Set(model.Mastery / 100f);
        UiTooltip.Set(icon.gameObject,
            () => CultibookTooltip.Show(icon.gameObject, model.Asset, actor, model.Mastery));
    }

    internal void ClearWorldBinding()
    {
        if (!Initialized) return;
        UiTooltip.Clear(icon.gameObject);
    }

    private static void _init()
    {
        GameObject root = UiLayout.Create(ModClass.I.PrefabLibrary, nameof(CultibookSummaryRow), true,
            234f, 50f, 5f, TextAnchor.MiddleLeft);
        UiListRowChrome.Attach(root, false);

        GameObject icon = new("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        icon.transform.SetParent(root.transform, false);
        UiLayout.SetSize(icon.transform, 40f, 40f);
        icon.GetComponent<Image>().preserveAspect = true;

        GameObject content = UiLayout.Create(root.transform, "Content", false, 183f, 44f, 1f);
        GameObject heading = UiLayout.Create(content.transform, "Heading", true, 183f, 18f, 2f);
        Text name = UiElements.CreateText(heading.transform, "Name", string.Empty, 126f, 18f, 8,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        Text level = UiElements.CreateText(heading.transform, "Level", string.Empty, 55f, 18f, 6,
            TextAnchor.MiddleRight, FontStyle.Bold);
        level.color = UiTheme.Current.Palette.AccentText;

        GameObject meta = UiLayout.Create(content.transform, "Meta", true, 183f, 16f, 2f);
        Text mastery = UiElements.CreateText(meta.transform, "Mastery", string.Empty, 88f, 16f, 6);
        Text affinity = UiElements.CreateText(meta.transform, "Affinity", string.Empty, 93f, 16f, 6,
            TextAnchor.MiddleRight);
        CreateMasteryStrip(content.transform, 183f);
        ConfigureBestFit(name, 5, 8);
        ConfigureBestFit(level, 4, 6);
        ConfigureBestFit(mastery, 4, 6);
        ConfigureBestFit(affinity, 4, 6);
        Prefab = root.AddComponent<CultibookSummaryRow>();
    }

    internal static GameObject CreateMasteryStrip(Transform parent, float width)
    {
        GameObject track = new("MasteryStrip", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        track.transform.SetParent(parent, false);
        UiLayout.SetSize(track.transform, width, 4f);
        Image trackImage = track.GetComponent<Image>();
        trackImage.color = UiTheme.Current.Palette.SegmentTrack;
        trackImage.raycastTarget = false;

        GameObject fill = new("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(track.transform, false);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImage = fill.GetComponent<Image>();
        fillImage.color = UiTheme.Current.Palette.Success;
        fillImage.raycastTarget = false;
        return track;
    }

    internal static void ConfigureBestFit(Text text, int minimum, int maximum)
    {
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = minimum;
        text.resizeTextMaxSize = maximum;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }
}

/// <summary>已知但非主修功法的紧凑列表行。</summary>
internal sealed class CultibookKnownRow : APrefabPreview<CultibookKnownRow>
{
    private Image icon;
    private Text nameLabel;
    private Text levelLabel;
    private Text knowledgeLabel;
    private CultibookMasteryStrip masteryStrip;

    protected override void OnInit()
    {
        icon = transform.Find("Icon").GetComponent<Image>();
        nameLabel = transform.Find("Content/Heading/Name").GetComponent<Text>();
        levelLabel = transform.Find("Content/Heading/Level").GetComponent<Text>();
        knowledgeLabel = transform.Find("Content/Knowledge").GetComponent<Text>();
        masteryStrip = new CultibookMasteryStrip(transform.Find("Content/MasteryStrip/Fill") as RectTransform);
    }

    /// <summary>绑定一部已知功法及其人物了解度。</summary>
    public void Setup(CultibookEntryModel model, ActorExtend actor)
    {
        Init();
        icon.sprite = SpriteTextureLoader.getSprite(model.CoverPath);
        nameLabel.text = model.Asset.Name;
        levelLabel.text = model.Asset.Level.GetName();
        knowledgeLabel.text = string.Format(
            "Cultiway.CultibookPage.Format.Knowledge".Localize(),
            model.Mastery);
        masteryStrip.Set(model.Mastery / 100f);
        UiTooltip.Set(icon.gameObject,
            () => CultibookTooltip.Show(icon.gameObject, model.Asset, actor, model.Mastery));
    }

    internal void ClearWorldBinding()
    {
        UiTooltip.Clear(icon.gameObject);
    }

    private static void _init()
    {
        GameObject root = UiLayout.Create(ModClass.I.PrefabLibrary, nameof(CultibookKnownRow), true,
            234f, 38f, 4f, TextAnchor.MiddleLeft);
        UiListRowChrome.Attach(root, false);
        GameObject icon = new("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        icon.transform.SetParent(root.transform, false);
        UiLayout.SetSize(icon.transform, 32f, 32f);
        icon.GetComponent<Image>().preserveAspect = true;

        GameObject content = UiLayout.Create(root.transform, "Content", false, 192f, 33f, 1f);
        GameObject heading = UiLayout.Create(content.transform, "Heading", true, 192f, 16f, 2f);
        Text name = UiElements.CreateText(heading.transform, "Name", string.Empty, 134f, 16f, 7,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        Text level = UiElements.CreateText(heading.transform, "Level", string.Empty, 56f, 16f, 5,
            TextAnchor.MiddleRight, FontStyle.Bold);
        level.color = UiTheme.Current.Palette.AccentText;
        Text knowledge = UiElements.CreateText(content.transform, "Knowledge", string.Empty, 192f, 12f, 5);
        CultibookSummaryRow.CreateMasteryStrip(content.transform, 192f);
        CultibookSummaryRow.ConfigureBestFit(name, 4, 7);
        CultibookSummaryRow.ConfigureBestFit(level, 4, 5);
        CultibookSummaryRow.ConfigureBestFit(knowledge, 4, 5);
        Prefab = root.AddComponent<CultibookKnownRow>();
    }
}

/// <summary>一种修炼方式的有效实践列表行。</summary>
internal sealed class CultivationPracticeRow : APrefabPreview<CultivationPracticeRow>
{
    private Text nameLabel;
    private Text valueLabel;

    protected override void OnInit()
    {
        nameLabel = transform.Find("Name").GetComponent<Text>();
        valueLabel = transform.Find("Value").GetComponent<Text>();
    }

    public void Setup(CultivationPracticeEntryModel model)
    {
        Init();
        nameLabel.text = model.MethodName;
        valueLabel.text = string.Format(
            "Cultiway.CultibookPage.Format.Practice".Localize(),
            CultibookPagePresentation.FormatNumber(model.EffectiveMonths),
            CultibookPagePresentation.FormatPercent(model.Share));
    }

    private static void _init()
    {
        GameObject root = UiLayout.Create(ModClass.I.PrefabLibrary, nameof(CultivationPracticeRow), true,
            234f, 18f, 2f, TextAnchor.MiddleLeft);
        Text name = UiElements.CreateText(root.transform, "Name", string.Empty, 136f, 18f, 6);
        Text value = UiElements.CreateText(root.transform, "Value", string.Empty, 96f, 18f, 6,
            TextAnchor.MiddleRight, FontStyle.Bold);
        name.color = UiTheme.Current.Palette.MutedText;
        CultibookSummaryRow.ConfigureBestFit(name, 4, 6);
        CultibookSummaryRow.ConfigureBestFit(value, 4, 6);
        Prefab = root.AddComponent<CultivationPracticeRow>();
    }
}

/// <summary>当前可见修炼资源的文本或进度条行。</summary>
internal sealed class CultivationResourceRow : APrefabPreview<CultivationResourceRow>
{
    private Text nameLabel;
    private Text valueLabel;
    private CultisysProgressEntry progress;

    protected override void OnInit()
    {
        nameLabel = transform.Find("Name").GetComponent<Text>();
        valueLabel = transform.Find("Value").GetComponent<Text>();
        progress = transform.Find("Progress").GetComponent<CultisysProgressEntry>();
    }

    public void Setup(CultivationResourceEntryModel model)
    {
        Init();
        nameLabel.text = model.Name;
        progress.gameObject.SetActive(model.HasCapacity);
        valueLabel.gameObject.SetActive(!model.HasCapacity);
        if (model.HasCapacity)
        {
            progress.Setup(CultisysDisplayLine.CreateProgress(
                string.Empty,
                model.Value,
                model.Capacity,
                model.IconPath));
        }
        else
        {
            valueLabel.text = CultibookPagePresentation.FormatNumber(model.Value);
        }
    }

    private static void _init()
    {
        GameObject root = UiLayout.Create(ModClass.I.PrefabLibrary, nameof(CultivationResourceRow), true,
            234f, 22f, 4f, TextAnchor.MiddleLeft);
        Text name = UiElements.CreateText(root.transform, "Name", string.Empty, 134f, 22f, 6);
        Text value = UiElements.CreateText(root.transform, "Value", string.Empty, 92f, 22f, 6,
            TextAnchor.MiddleRight, FontStyle.Bold);
        name.color = UiTheme.Current.Palette.MutedText;
        CultisysProgressEntry progress = Object.Instantiate(CultisysProgressEntry.Prefab, root.transform, false);
        progress.name = "Progress";
        UiLayout.SetSize(progress.transform, CultisysProgressEntry.Width, CultisysProgressEntry.Height);
        CultibookSummaryRow.ConfigureBestFit(name, 4, 6);
        CultibookSummaryRow.ConfigureBestFit(value, 4, 6);
        Prefab = root.AddComponent<CultivationResourceRow>();
    }
}

/// <summary>实践区中的八元素暴露比例条和两行图例。</summary>
internal sealed class CultivationElementExposureView
{
    private readonly GameObject root;
    private readonly UiWeightedSegmentBar bar;
    private readonly CultivationElementLegendCell[] cells;

    private CultivationElementExposureView(
        GameObject root,
        UiWeightedSegmentBar bar,
        CultivationElementLegendCell[] cells)
    {
        this.root = root;
        this.bar = bar;
        this.cells = cells;
    }

    public GameObject Root => root;

    public static CultivationElementExposureView Create(Transform parent)
    {
        GameObject root = UiLayout.Create(parent, "ElementExposure", false, 234f, 60f, 2f);
        Text title = UiElements.CreateText(root.transform, "Title",
            "Cultiway.CultibookPage.Label.ElementExposure".Localize(), 234f, 12f, 6,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        title.color = UiTheme.Current.Palette.MutedText;
        UiWeightedSegmentBar bar = UiWeightedSegmentBar.Create(root.transform, "Segments", 234f, 7f);
        GameObject five = UiLayout.Create(root.transform, "FiveElements", true, 234f, 18f, 3f,
            TextAnchor.MiddleCenter);
        GameObject other = UiLayout.Create(root.transform, "OtherElements", true, 234f, 18f, 4f,
            TextAnchor.MiddleCenter);
        var cells = new CultivationElementLegendCell[ElementIndex.Count];
        for (var i = 0; i < 5; i++) cells[i] = CultivationElementLegendCell.Create(five.transform, i, 44f);
        for (var i = 5; i < ElementIndex.Count; i++)
            cells[i] = CultivationElementLegendCell.Create(other.transform, i, 74f);
        return new CultivationElementExposureView(root, bar, cells);
    }

    public void SetValues(float[] values)
    {
        var colors = new Color[ElementIndex.Count];
        for (var i = 0; i < ElementIndex.Count; i++)
        {
            colors[i] = XianRealmPagePresentation.GetElementColor(i);
            cells[i].SetValue(values[i]);
        }
        bar.SetSegments(values, colors);
    }
}

/// <summary>元素暴露图例中的一个图标和百分比。</summary>
internal sealed class CultivationElementLegendCell
{
    private readonly int elementIndex;
    private readonly Image icon;
    private readonly Text value;

    private CultivationElementLegendCell(int elementIndex, Image icon, Text value)
    {
        this.elementIndex = elementIndex;
        this.icon = icon;
        this.value = value;
    }

    public static CultivationElementLegendCell Create(Transform parent, int elementIndex, float width)
    {
        GameObject root = UiLayout.Create(parent, $"Element {elementIndex}", true, width, 18f, 1f,
            TextAnchor.MiddleCenter);
        GameObject iconObject = new("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconObject.transform.SetParent(root.transform, false);
        UiLayout.SetSize(iconObject.transform, 13f, 13f);
        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = SpriteTextureLoader.getSprite(XianRealmPagePresentation.ElementIconPaths[elementIndex]);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        Text value = UiElements.CreateText(root.transform, "Value", string.Empty, width - 14f, 18f, 5,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        CultibookSummaryRow.ConfigureBestFit(value, 4, 5);
        return new CultivationElementLegendCell(elementIndex, icon, value);
    }

    public void SetValue(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        value.text = CultibookPagePresentation.FormatPercent(ratio);
        Color color = ratio > 0f
            ? XianRealmPagePresentation.GetElementColor(elementIndex)
            : UiTheme.Current.Palette.Disabled;
        value.color = color;
        icon.color = ratio > 0f ? Color.white : UiTheme.Current.Palette.Disabled;
    }
}

/// <summary>用锚点宽度表示 0..1 掌握度的轻量进度条。</summary>
internal sealed class CultibookMasteryStrip
{
    private readonly RectTransform fill;

    public CultibookMasteryStrip(RectTransform fill)
    {
        this.fill = fill;
    }

    public void Set(float ratio)
    {
        fill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        fill.offsetMax = Vector2.zero;
    }
}
