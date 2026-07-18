using System;
using System.Collections.Generic;
using System.Globalization;
using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Core.Progression;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

/// <summary>展示角色某一修炼体系完整境界、进阶、专属数据和属性加成的 Tooltip。</summary>
public sealed class CultisysTooltip : APrefabPreview<CultisysTooltip>
{
    private const float ContentWidth = 109f;
    private const float StatBoxWidth = 104f;
    private const float StatCellWidth = 100f;
    private const float StatCellHeight = 10f;
    private const float StatRowSpacing = 0f;
    private static BaseCultisysAsset _pendingAsset;
    private static ActorExtend _pendingActor;

    private Tooltip _tooltip;
    private Image _icon;
    private Text _realm;
    private Text _meta;
    private Text _progression;
    private GameObject _progressSection;
    private LayoutElement _progressSectionLayout;
    private MonoObjPool<CultisysProgressEntry> _progressPool;
    private GameObject _statSection;
    private Text _statTitle;
    private LayoutElement _statSectionLayout;
    private RectTransform _statBox;
    private LayoutElement _statBoxLayout;
    private RectTransform _statGrid;
    private LayoutElement _statGridLayout;
    private MonoObjPool<CultisysStatBonusEntry> _statPool;
    private readonly List<CultisysDisplayLine> _detailLines = new();

    protected override void OnInit()
    {
        _tooltip = GetComponent<Tooltip>();
        _icon = transform.Find("CultisysSummary/Icon").GetComponent<Image>();
        _realm = transform.Find("CultisysSummary/Realm").GetComponent<Text>();
        _meta = transform.Find("CultisysSummary/Meta").GetComponent<Text>();
        _progression = transform.Find("CultisysSummary/Progression").GetComponent<Text>();
        _progressSection = transform.Find("ProgressSection").gameObject;
        _progressSectionLayout = _progressSection.GetComponent<LayoutElement>();
        _progressPool = new MonoObjPool<CultisysProgressEntry>(CultisysProgressEntry.Prefab,
            _progressSection.transform);
        _statSection = transform.Find("StatBonusSection").gameObject;
        _statTitle = transform.Find("StatBonusSection/Title").GetComponent<Text>();
        _statSectionLayout = _statSection.GetComponent<LayoutElement>();
        _statBox = transform.Find("StatBonusSection/Box").GetComponent<RectTransform>();
        _statBoxLayout = _statBox.GetComponent<LayoutElement>();
        _statGrid = transform.Find("StatBonusSection/Box/Grid").GetComponent<RectTransform>();
        _statGridLayout = _statGrid.GetComponent<LayoutElement>();
        _statPool = new MonoObjPool<CultisysStatBonusEntry>(CultisysStatBonusEntry.Prefab, _statGrid);
    }

    /// <summary>在指定来源控件旁展示角色所拥有的一个修炼体系。</summary>
    public static void Show(GameObject source, BaseCultisysAsset asset, ActorExtend actor)
    {
        if (source == null || asset == null || actor == null || !asset.IsOwnedBy(actor)) return;
        int level = asset.GetCurrentLevel(actor);
        if (level < 0 || level >= asset.LevelNumber) return;
        _pendingAsset = asset;
        _pendingActor = actor;
        try
        {
            Tooltip.show(source, WorldboxGame.Tooltips.Cultisys.id, new TooltipData());
        }
        finally
        {
            _pendingAsset = null;
            _pendingActor = null;
        }
    }

    /// <summary>由 TooltipAsset 回调读取当前同步展示上下文并刷新全部区域。</summary>
    internal void SetupPending()
    {
        Init();
        BaseCultisysAsset asset = _pendingAsset;
        ActorExtend actor = _pendingActor;
        if (asset == null || actor == null) return;

        int level = asset.GetCurrentLevel(actor);
        if (level < 0 || level >= asset.LevelNumber) return;
        ProgressionQuery query = asset.QueryProgression(actor);

        _tooltip.name.text = asset.GetName();
        _icon.sprite = SpriteTextureLoader.getSprite(asset.IconPath)
                       ?? SpriteTextureLoader.getSprite("cultiway/icons/iconCultivation");
        _realm.text = asset.GetLevelName(level);
        _meta.text = string.Format("Cultiway.CultisysTooltip.Format.LevelMeta".Localize(), level + 1,
            asset.LevelNumber, FormatNumber(asset.GetLevelPower(level)));
        _progression.text = CultisysPresentation.FormatProgression(query);
        _progression.color = CultisysPresentation.ResolveProgressionColor(query);
        _statTitle.text = "Cultiway.CultisysTooltip.Section.Attributes".Localize();

        if (asset.TryGetLevelDescription(level, out string description))
            _tooltip.setDescription(description);

        _detailLines.Clear();
        _progressPool.Clear();
        asset.AppendDisplayDetails(actor, _detailLines);
        int progressCount = 0;
        for (var i = 0; i < _detailLines.Count; i++)
        {
            CultisysDisplayLine line = _detailLines[i];
            if (line.HasProgress)
            {
                _progressPool.GetNext().Setup(line);
                progressCount++;
                continue;
            }
            _tooltip.addLineText(line.LabelKey.Localize(), line.Value, line.ColorHex, pLocalize: false,
                pLimitValue: 120);
        }
        RefreshProgressBars(progressCount);

        if (query.Available && query.Kind == ProgressionKind.Major && query.TargetLevel >= 0
                            && query.TargetLevel < asset.LevelNumber)
        {
            _tooltip.addLineText("Cultiway.CultisysTooltip.Progression.Target".Localize(),
                asset.GetLevelName(query.TargetLevel), pLocalize: false);
        }
        string reason = query.Available ? CultisysPresentation.ResolveProgressionReason(query) : null;
        if (!string.IsNullOrEmpty(reason))
        {
            _tooltip.addLineText("Cultiway.CultisysTooltip.Progression.Requirement".Localize(), reason,
                CultisysPresentation.ToHtml(UiTheme.Current.Palette.Warning), pLocalize: false,
                pLimitValue: 120);
        }

        RefreshStatBonuses(asset, level);
    }

    private void RefreshProgressBars(int count)
    {
        bool visible = count > 0;
        _progressSection.SetActive(visible);
        if (!visible) return;

        float height = count * CultisysProgressEntry.Height + Mathf.Max(0, count - 1) + 4f;
        _progressSectionLayout.minHeight = height;
        _progressSectionLayout.preferredHeight = height;
    }

    private void RefreshStatBonuses(BaseCultisysAsset asset, int level)
    {
        List<CultisysPresentation.StatBonus> stats =
            CultisysPresentation.BuildStatBonuses(asset.GetProvidedStats(level));
        _statPool.Clear();
        for (var i = 0; i < stats.Count; i++) _statPool.GetNext().Setup(stats[i]);

        bool visible = stats.Count > 0;
        _statSection.SetActive(visible);
        if (!visible) return;

        int rows = stats.Count;
        float gridHeight = rows * StatCellHeight + Mathf.Max(0, rows - 1) * StatRowSpacing;
        _statGrid.sizeDelta = new Vector2(_statGrid.sizeDelta.x, gridHeight);
        _statGridLayout.minHeight = gridHeight;
        _statGridLayout.preferredHeight = gridHeight;
        float boxHeight = gridHeight + 4f;
        _statBox.sizeDelta = new Vector2(_statBox.sizeDelta.x, boxHeight);
        _statBoxLayout.minHeight = boxHeight;
        _statBoxLayout.preferredHeight = boxHeight;
        float sectionHeight = 10f + boxHeight;
        _statSectionLayout.minHeight = sectionHeight;
        _statSectionLayout.preferredHeight = sectionHeight;
    }

    private static string FormatNumber(float value)
    {
        return value.ToString(Mathf.Approximately(value, Mathf.Round(value)) ? "0" : "0.##",
            CultureInfo.InvariantCulture);
    }

    private static void _init()
    {
        GameObject obj = Instantiate(Resources.Load<GameObject>(WorldboxGame.Tooltips.Tip.prefab_id),
            ModClass.I.PrefabLibrary);
        obj.name = "tooltip_cultiway_cultisys";
        CreateSummary(obj.transform);
        CreateProgressSection(obj.transform);
        CreateStatSection(obj.transform);
        Prefab = obj.AddComponent<CultisysTooltip>();
    }

    private static void CreateSummary(Transform parent)
    {
        GameObject summary = new("CultisysSummary", typeof(RectTransform), typeof(LayoutElement));
        summary.transform.SetParent(parent, false);
        summary.transform.SetSiblingIndex(1);
        UiLayout.SetSize(summary.transform, ContentWidth, 38f);

        GameObject icon = new("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(summary.transform, false);
        SetFixedRect(icon.GetComponent<RectTransform>(), new Vector2(26f, 26f), new Vector2(15f, 0f));
        icon.GetComponent<Image>().preserveAspect = true;

        CreateSummaryText(summary.transform, "Realm", 9f, 8, FontStyle.Bold);
        CreateSummaryText(summary.transform, "Meta", 0f, 6, FontStyle.Normal);
        CreateSummaryText(summary.transform, "Progression", -10f, 6, FontStyle.Normal);
    }

    private static Text CreateSummaryText(Transform parent, string name, float y, int size, FontStyle style)
    {
        GameObject obj = new(name, typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(31f, y);
        rect.sizeDelta = new Vector2(ContentWidth - 34f, 10f);
        Text text = obj.GetComponent<Text>();
        text.font = UiTheme.Current.Font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = UiTheme.Current.Palette.PrimaryText;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 4;
        text.resizeTextMaxSize = size;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static void CreateProgressSection(Transform parent)
    {
        GameObject section = new("ProgressSection", typeof(RectTransform), typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        section.transform.SetParent(parent, false);
        Transform stats = parent.Find("Stats");
        section.transform.SetSiblingIndex(stats == null ? 3 : stats.GetSiblingIndex());
        UiLayout.SetSize(section.transform, ContentWidth, 17f);
        VerticalLayoutGroup layout = section.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(2, 2, 2, 2);
        layout.spacing = 1f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    private static void CreateStatSection(Transform parent)
    {
        GameObject section = new("StatBonusSection", typeof(RectTransform), typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        section.transform.SetParent(parent, false);
        section.transform.SetSiblingIndex(5);
        UiLayout.SetSize(section.transform, ContentWidth, 20f);
        VerticalLayoutGroup sectionLayout = section.GetComponent<VerticalLayoutGroup>();
        sectionLayout.padding = new RectOffset();
        sectionLayout.spacing = 1f;
        sectionLayout.childAlignment = TextAnchor.UpperCenter;
        sectionLayout.childControlWidth = false;
        sectionLayout.childControlHeight = true;
        sectionLayout.childForceExpandWidth = false;
        sectionLayout.childForceExpandHeight = false;

        Text title = UiElements.CreateText(section.transform, "Title",
            string.Empty, StatBoxWidth, 9f, 6,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        title.color = UiTheme.Current.Palette.AccentText;

        GameObject box = new("Box", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        box.transform.SetParent(section.transform, false);
        UiLayout.SetSize(box.transform, StatBoxWidth, 5f);
        ApplyStatsSurface(box.GetComponent<Image>(), parent.Find("Stats")?.GetComponent<Image>());
        VerticalLayoutGroup boxLayout = box.GetComponent<VerticalLayoutGroup>();
        boxLayout.padding = new RectOffset(2, 2, 2, 2);
        boxLayout.childAlignment = TextAnchor.UpperCenter;
        boxLayout.childControlWidth = false;
        boxLayout.childControlHeight = false;
        boxLayout.childForceExpandWidth = false;
        boxLayout.childForceExpandHeight = false;

        GameObject grid = new("Grid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
        grid.transform.SetParent(box.transform, false);
        UiLayout.SetSize(grid.transform, StatCellWidth, 1f);
        GridLayoutGroup layout = grid.GetComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(StatCellWidth, StatCellHeight);
        layout.spacing = new Vector2(2f, StatRowSpacing);
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.startAxis = GridLayoutGroup.Axis.Horizontal;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 1;
    }

    private static void ApplyStatsSurface(Image target, Image source)
    {
        target.raycastTarget = false;
        if (source != null)
        {
            target.sprite = source.sprite;
            target.type = source.type;
            target.color = source.color;
            return;
        }

        UiResources.ApplySurface(target, UiSurface.WindowInner, new Color(0.371f, 0.371f, 0.371f, 0.588f));
    }

    private static void SetFixedRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }
}
