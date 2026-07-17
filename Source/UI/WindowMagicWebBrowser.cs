using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.Semantics;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI;

/// <summary>
/// 以纯查询方式浏览当前世界魔网。该窗口不会调用 Touch，也不提供学习、编辑或上传操作。
/// </summary>
public sealed class WindowMagicWebBrowser : AbstractWideWindow<WindowMagicWebBrowser>
{
    private enum GroupMode
    {
        Entity,
        Element,
        Ring,
        Source,
        Trajectory
    }

    private enum SortMode
    {
        Name,
        ItemLevel,
        ManaDemand,
        ModifierCount,
        Lifetime
    }

    private enum SourceFilter
    {
        All,
        Default,
        Uploaded
    }

    private enum SemanticFilterState
    {
        Off,
        Required,
        Excluded
    }

    private sealed class EntryModel
    {
        public MagicWebEntryView Entry;
        public string Name;
        public string EntityId;
        public string EntityName;
        public int AnimationIndex;
        public string TrajectoryId;
        public string TrajectoryName;
        public string ItemLevelName;
        public int ItemLevelValue;
        public float ManaDemand;
        public string[] ModifierNames;
        public SemanticAsset[] Semantics;
        public string SearchText;
        public Sprite[] Frames;
        public float FrameInterval;
    }

    private sealed class GroupModel
    {
        public string Key;
        public string Name;
        public string Order;
        public List<EntryModel> Entries;
    }

    private sealed class MageContext
    {
        public ActorExtend Actor;
        public ElementRoot Root;
        public int MaxRing;
        public int Capacity;
        public int KnownCount;
        public HashSet<string> KnownFamilies;
    }

    private readonly struct Compatibility
    {
        public readonly float Affinity;
        public readonly bool RingAllowed;
        public readonly bool AffinityAllowed;
        public readonly bool KnownFamily;
        public readonly bool CapacityFull;

        public bool Understandable => RingAllowed && AffinityAllowed;

        public Compatibility(float affinity, bool ringAllowed, bool affinityAllowed, bool knownFamily,
            bool capacityFull)
        {
            Affinity = affinity;
            RingAllowed = ringAllowed;
            AffinityAllowed = affinityAllowed;
            KnownFamily = knownFamily;
            CapacityFull = capacityFull;
        }
    }

    public const string Id = "Cultiway.UI.WindowMagicWebBrowser";
    public static readonly Vector2 WindowSize = new(600f, 360f);

    private const float RootWidth = 520f;
    private const float RootHeight = 318f;
    private const float FilterHeight = 110f;
    private const float BodyCollapsedHeight = 290f;
    private const float BodyExpandedHeight = 176f;
    private const float LeftWidth = 258f;
    private const float RightWidth = 258f;
    private const int PageSize = 50;
    private const int MaxItemLevel = 35;

    private static readonly SkillBlueprintExporter Exporter = new();
    private static readonly SemanticAsset[] ElementSemantics =
    {
        SkillSemantics.Element.Iron, SkillSemantics.Element.Wood, SkillSemantics.Element.Water,
        SkillSemantics.Element.Fire, SkillSemantics.Element.Earth, SkillSemantics.Element.Neg,
        SkillSemantics.Element.Pos, SkillSemantics.Element.Entropy, SkillSemantics.Element.Generic
    };

    private readonly List<EntryModel> _entries = new();
    private readonly HashSet<SemanticAsset> _elementFilters = new();
    private readonly Dictionary<SemanticAsset, SemanticFilterState> _semanticFilters = new();
    private MonoObjPool<MagicWebBrowserRow> _rowPool;
    private InputField _search;
    private Button _groupButton;
    private Button _sortButton;
    private Button _directionButton;
    private Button _filterButton;
    private Text _resultCount;
    private GameObject _filterRoot;
    private Transform _filterContent;
    private GameObject _dynamicFilterRoot;
    private Text _minLevelText;
    private Text _maxLevelText;
    private Button _sourceFilterButton;
    private Button _entityFilterButton;
    private Button _trajectoryFilterButton;
    private Toggle _understandableOnlyToggle;
    private Toggle _hideKnownToggle;
    private GameObject _body;
    private GameObject _leftScroll;
    private GameObject _rightScroll;
    private Image _detailIcon;
    private Text _detailName;
    private Text _detailSummary;
    private Text _detailSource;
    private Text _detailSemantics;
    private Text _detailModifiers;
    private Text _detailCompatibility;
    private Sprite[] _detailFrames = Array.Empty<Sprite>();
    private int _detailFrameIndex;
    private float _detailFrameTimer;
    private float _detailFrameInterval = 0.1f;
    private GroupMode _groupMode;
    private SortMode _sortMode;
    private SourceFilter _sourceFilter;
    private bool _sortDescending;
    private bool _filterExpanded;
    private bool _understandableOnly;
    private bool _hideKnown;
    private int _minItemLevel;
    private int _maxItemLevel = MaxItemLevel;
    private string _entityFilter;
    private string _trajectoryFilter;
    private string[] _entityIds = Array.Empty<string>();
    private string[] _trajectoryIds = Array.Empty<string>();
    private string _expandedGroupKey;
    private bool _groupCollapsed;
    private int _page;
    private int _selectedEntityId = -1;
    private int _selectionVersion;
    private int _displayedWorldYear = int.MinValue;
    private bool _dirty = true;

    protected override void Init()
    {
        UiWindowContext.Bind(BackgroundTransform);
        var root = UiLayout.Create(BackgroundTransform, "MagicWebRoot", false, RootWidth, RootHeight,
            4f);
        root.transform.localPosition = new Vector3(0f, -8f);

        CreateToolbar(root.transform);
        CreateFilterBand(root.transform);
        CreateBody(root.transform);

        var manager = MagicWebManager.Instance;
        if (manager != null) manager.Changed += HandleMagicWebChanged;
        _selectionVersion = SelectedUnit.getSelectionVersion();
        SetFilterExpanded(false);
    }

    public override void OnNormalEnable()
    {
        ReloadEntries();
    }

    private void OnDestroy()
    {
        var manager = MagicWebManager.Instance;
        if (manager != null) manager.Changed -= HandleMagicWebChanged;
    }

    private void Update()
    {
        if (!gameObject.activeInHierarchy) return;

        var selectionVersion = SelectedUnit.getSelectionVersion();
        if (_selectionVersion != selectionVersion)
        {
            _selectionVersion = selectionVersion;
            Refresh();
        }

        var worldYear = GetWorldYear();
        if (_displayedWorldYear != worldYear)
        {
            _displayedWorldYear = worldYear;
            RefreshList();
        }

        if (_detailFrames.Length < 2) return;
        _detailFrameTimer += Time.unscaledDeltaTime;
        if (_detailFrameTimer < _detailFrameInterval) return;
        _detailFrameTimer = 0f;
        _detailFrameIndex = (_detailFrameIndex + 1) % _detailFrames.Length;
        _detailIcon.sprite = _detailFrames[_detailFrameIndex];
    }

    private void CreateToolbar(Transform parent)
    {
        var toolbar = UiLayout.Create(parent, "Toolbar", true, RootWidth, 24f, 4f);
        _search = UiSearchField.Create(toolbar.transform, "Search", string.Empty,
            "Cultiway.MagicWeb.UI.Placeholder.Search".Localize(), 110f, 22f).Input;
        UiTooltip.Set(_search, "Cultiway.MagicWeb.UI.Tooltip.Search.Title",
            "Cultiway.MagicWeb.UI.Tooltip.Search");
        _search.onValueChanged.AddListener(_ => ApplyFilterChange());

        _groupButton = UiElements.CreateIconTextButton(toolbar.transform, "Group", WanfaUiIcons.Entity,
            string.Empty, 88f, 22f, CycleGroupMode);
        UiTooltip.Set(_groupButton.gameObject, "Cultiway.MagicWeb.UI.Tooltip.Group.Title",
            "Cultiway.MagicWeb.UI.Tooltip.Group");
        _sortButton = UiElements.CreateIconTextButton(toolbar.transform, "Sort", UiIcons.Sort,
            string.Empty, 80f, 22f, CycleSortMode);
        UiTooltip.Set(_sortButton.gameObject, "Cultiway.MagicWeb.UI.Tooltip.Sort.Title",
            "Cultiway.MagicWeb.UI.Tooltip.Sort");
        _directionButton = UiElements.CreateIconButton(toolbar.transform, "Direction", UiIcons.MoveUp,
            24f, 22f, ToggleSortDirection);
        UiTooltip.Set(_directionButton.gameObject, "Cultiway.MagicWeb.UI.Tooltip.Direction.Title",
            "Cultiway.MagicWeb.UI.Tooltip.Direction");
        _filterButton = UiElements.CreateIconTextButton(toolbar.transform, "Filters", UiIcons.Options,
            string.Empty, 60f, 22f, () => SetFilterExpanded(!_filterExpanded));
        UiTooltip.Set(_filterButton.gameObject, "Cultiway.MagicWeb.UI.Tooltip.Filters.Title",
            "Cultiway.MagicWeb.UI.Tooltip.Filters");
        var reset = UiElements.CreateIconButton(toolbar.transform, "Reset", UiIcons.Reset, 24f, 22f,
            ResetView);
        UiTooltip.Set(reset.gameObject, "Cultiway.MagicWeb.UI.Tooltip.Reset.Title",
            "Cultiway.MagicWeb.UI.Tooltip.Reset");
        _resultCount = UiElements.CreateText(toolbar.transform, "ResultCount", string.Empty, 82f, 22f, 6,
            TextAnchor.MiddleRight);
    }

    private void CreateFilterBand(Transform parent)
    {
        UiScrollPane filters = UiScrollPane.CreateVertical(parent, "FilterBand", RootWidth, FilterHeight);
        filters.SetSurface(UiSurface.WindowInner, UiTheme.Current.Metrics.SpacingXs, false);
        _filterContent = filters.Content;
        _filterRoot = filters.Root.gameObject;

        var rangeRow = UiLayout.Create(_filterContent, "LevelAndAssetFilters", true, RootWidth - 8f,
            22f, 3f);
        UiElements.CreateText(rangeRow.transform, "LevelLabel",
            "Cultiway.MagicWeb.UI.Filter.ItemLevel".Localize(), 54f, 22f, 7);
        UiElements.CreateIconButton(rangeRow.transform, "MinDown", UiIcons.Remove, 22f, 20f,
            () => AdjustItemLevel(true, -1), 5f);
        _minLevelText = UiElements.CreateText(rangeRow.transform, "MinLevel", string.Empty, 48f, 22f, 7,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        UiElements.CreateIconButton(rangeRow.transform, "MinUp", UiIcons.Add, 22f, 20f,
            () => AdjustItemLevel(true, 1), 5f);
        UiElements.CreateText(rangeRow.transform, "Separator", "-", 8f, 22f, 7, TextAnchor.MiddleCenter);
        UiElements.CreateIconButton(rangeRow.transform, "MaxDown", UiIcons.Remove, 22f, 20f,
            () => AdjustItemLevel(false, -1), 5f);
        _maxLevelText = UiElements.CreateText(rangeRow.transform, "MaxLevel", string.Empty, 48f,
            22f, 7, TextAnchor.MiddleCenter, FontStyle.Bold);
        UiElements.CreateIconButton(rangeRow.transform, "MaxUp", UiIcons.Add, 22f, 20f,
            () => AdjustItemLevel(false, 1), 5f);
        _sourceFilterButton = UiElements.CreateButton(rangeRow.transform, "Source", string.Empty, 92f, 20f,
            CycleSourceFilter);

        var assetRow = UiLayout.Create(_filterContent, "AssetFilters", true, RootWidth - 8f, 22f,
            3f);
        _entityFilterButton = UiElements.CreateButton(assetRow.transform, "Entity", string.Empty, 170f, 20f,
            CycleEntityFilter);
        _trajectoryFilterButton = UiElements.CreateButton(assetRow.transform, "Trajectory", string.Empty,
            170f, 20f, CycleTrajectoryFilter);

        var contextRow = UiLayout.Create(_filterContent, "MageFilters", true, RootWidth - 8f, 22f,
            4f);
        _understandableOnlyToggle = UiElements.CreateToggle(contextRow.transform, "UnderstandableOnly",
            "Cultiway.MagicWeb.UI.Filter.UnderstandableOnly".Localize(), false, 136f, 20f);
        _understandableOnlyToggle.onValueChanged.AddListener(value =>
        {
            _understandableOnly = value;
            ApplyFilterChange();
        });
        _hideKnownToggle = UiElements.CreateToggle(contextRow.transform, "HideKnown",
            "Cultiway.MagicWeb.UI.Filter.HideKnown".Localize(), false, 116f, 20f);
        _hideKnownToggle.onValueChanged.AddListener(value =>
        {
            _hideKnown = value;
            ApplyFilterChange();
        });
        UiElements.CreateText(contextRow.transform, "ContextHint",
            "Cultiway.MagicWeb.UI.Filter.SelectedMage".Localize(), 240f, 20f, 6);
    }

    private void CreateBody(Transform parent)
    {
        _body = UiLayout.Create(parent, "Body", true, RootWidth, BodyCollapsedHeight, 4f);
        UiScrollPane left = UiScrollPane.CreateVertical(_body.transform, "GroupedEntries", LeftWidth,
            BodyCollapsedHeight);
        left.SetSurface(UiSurface.WindowEmpty, UiTheme.Current.Metrics.SpacingXs, false);
        _leftScroll = left.Root.gameObject;
        _rowPool = new MonoObjPool<MagicWebBrowserRow>(MagicWebBrowserRow.Prefab, left.Content);

        UiScrollPane right = UiScrollPane.CreateVertical(_body.transform, "EntryDetail", RightWidth,
            BodyCollapsedHeight);
        right.SetSurface(UiSurface.WindowInner, UiTheme.Current.Metrics.SpacingXs, false);
        _rightScroll = right.Root.gameObject;
        var header = UiLayout.Create(right.Content, "Header", true, RightWidth - 8f, 58f, 4f,
            TextAnchor.UpperLeft);
        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        icon.transform.SetParent(header.transform, false);
        UiLayout.SetSize(icon.transform, 54f, 54f);
        _detailIcon = icon.GetComponent<Image>();
        _detailIcon.preserveAspect = true;
        var heading = UiLayout.Create(header.transform, "Heading", false, RightWidth - 66f, 54f, 2f);
        _detailName = UiElements.CreateText(heading.transform, "Name", string.Empty, RightWidth - 66f, 22f,
            9, TextAnchor.MiddleLeft, FontStyle.Bold);
        _detailSummary = UiElements.CreateText(heading.transform, "Summary", string.Empty,
            RightWidth - 66f, 30f, 6);
        _detailSource = CreateDetailText(right.Content, "Source", 54f);
        _detailSemantics = CreateDetailText(right.Content, "Semantics", 66f);
        _detailModifiers = CreateDetailText(right.Content, "Modifiers", 100f);
        _detailCompatibility = CreateDetailText(right.Content, "Compatibility", 110f);
    }

    private static Text CreateDetailText(Transform parent, string name, float height)
    {
        var text = UiElements.CreateText(parent, name, string.Empty, RightWidth - 8f, height, 7,
            TextAnchor.UpperLeft);
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private void HandleMagicWebChanged()
    {
        _dirty = true;
        if (gameObject.activeInHierarchy) ReloadEntries();
    }

    private void ReloadEntries()
    {
        _entries.Clear();
        var manager = MagicWebManager.Instance;
        if (manager != null)
        {
            var query = new MagicWebQuery { MaxRing = 12, MaxResults = int.MaxValue };
            foreach (var entry in manager.Query(query))
            {
                if (entry.Container.IsNull || !entry.Container.HasComponent<SkillContainer>()) continue;
                _entries.Add(BuildEntryModel(entry));
            }
        }

        RefreshFilterAssets();
        RefreshDynamicFilters();
        _dirty = false;
        _displayedWorldYear = GetWorldYear();
        Refresh();
    }

    private static EntryModel BuildEntryModel(MagicWebEntryView entry)
    {
        var container = entry.Container;
        var skill = container.GetComponent<SkillContainer>();
        var entityId = skill.SkillEntityAssetID;
        var trajectoryId = SkillBlueprintTrajectory.ResolveEffectiveId(container);
        var name = container.HasName ? container.Name.value : string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            var exported = Exporter.Export(container, new SkillBlueprintExportOptions
            {
                PreserveContainerNameAsCustom = true
            });
            name = exported.Blueprint == null
                ? entityId.Localize()
                : Core.SkillLibV3.Wanfa.WanfaPavilionService.Instance.GetDisplayName(exported.Blueprint);
        }

        var itemLevel = container.TryGetComponent(out ItemLevel resolvedLevel)
            ? resolvedLevel
            : ItemLevel.FromValue(0);
        var modifierNames = container.GetComponentTypes()
            .Where(type => typeof(IModifier).IsAssignableFrom(type) && type != typeof(Trajectory))
            .Select(type => (IModifier)container.GetComponent(type))
            .Where(modifier => modifier.ModifierAsset != null && !modifier.ModifierAsset.EditorDerived)
            .OrderBy(modifier => modifier.ModifierAsset.id, StringComparer.Ordinal)
            .Select(modifier => string.IsNullOrWhiteSpace(modifier.GetValue())
                ? modifier.GetKey()
                : $"{modifier.GetKey()}: {modifier.GetValue()}")
            .ToArray();
        var semantics = entry.Semantics.OrderBy(semantic => semantic.id, StringComparer.Ordinal).ToArray();
        var animation = skill.Asset.IsAnimationIndexValid(skill.AnimationIndex)
            ? skill.Asset.GetAnimation(skill.AnimationIndex)
            : null;
        var frames = animation?.Frames ?? Array.Empty<Sprite>();
        var frameInterval = animation?.Settings.ResolveFrameInterval(skill.MotionProfile.FrameInterval) ?? 0.1f;
        var entityName = entityId.Localize();
        var trajectoryName = trajectoryId.Localize();
        var searchParts = new List<string>
        {
            name, entityId, entityName, trajectoryId, trajectoryName
        };
        searchParts.AddRange(modifierNames);
        searchParts.AddRange(semantics.Select(semantic => semantic.id));
        searchParts.AddRange(semantics.Select(semantic => semantic.GetName()));

        return new EntryModel
        {
            Entry = entry,
            Name = name,
            EntityId = entityId,
            EntityName = entityName,
            AnimationIndex = skill.AnimationIndex,
            TrajectoryId = trajectoryId,
            TrajectoryName = trajectoryName,
            ItemLevelName = FormatMagicItemLevel(itemLevel),
            ItemLevelValue = itemLevel,
            ManaDemand = SkillCastCost.CalculateStepDemand(container),
            ModifierNames = modifierNames,
            Semantics = semantics,
            SearchText = string.Join("\n", searchParts),
            Frames = frames,
            FrameInterval = frameInterval
        };
    }

    private void Refresh()
    {
        if (_dirty)
        {
            ReloadEntries();
            return;
        }

        UpdateControls();
        RefreshList();
    }

    private void RefreshList()
    {
        if (_rowPool == null) return;
        _rowPool.Clear();
        var mage = BuildMageContext();
        _understandableOnlyToggle.interactable = mage != null;
        _hideKnownToggle.interactable = mage != null;

        var filtered = _entries.Where(entry => MatchesFilters(entry, mage)).ToList();
        var groups = BuildGroups(filtered);
        if (groups.Count == 0)
        {
            _expandedGroupKey = null;
            _groupCollapsed = false;
            _selectedEntityId = -1;
            _resultCount.text = string.Format("Cultiway.MagicWeb.UI.Format.ResultCount".Localize(), 0,
                _entries.Count);
            ShowEmptyDetail();
            UpdateFilterButton();
            return;
        }

        var expanded = groups.FirstOrDefault(group => group.Key == _expandedGroupKey);
        if (expanded == null)
        {
            expanded = groups[0];
            _expandedGroupKey = expanded.Key;
            _groupCollapsed = false;
            _page = 0;
        }

        var sortedExpanded = SortEntries(expanded.Entries).ToList();
        var selected = filtered.FirstOrDefault(entry => entry.Entry.Container.Id == _selectedEntityId);
        if (selected == null)
        {
            selected = sortedExpanded[0];
            _selectedEntityId = selected.Entry.Container.Id;
        }

        foreach (var group in groups)
        {
            var currentGroup = group;
            var isExpanded = currentGroup.Key == _expandedGroupKey && !_groupCollapsed;
            _rowPool.GetNext().SetupGroup(currentGroup.Name, currentGroup.Entries.Count, isExpanded, () =>
            {
                if (_expandedGroupKey == currentGroup.Key)
                {
                    _groupCollapsed = !_groupCollapsed;
                }
                else
                {
                    _expandedGroupKey = currentGroup.Key;
                    _groupCollapsed = false;
                }
                _page = 0;
                RefreshList();
            });
            if (!isExpanded) continue;

            var sorted = SortEntries(currentGroup.Entries).ToList();
            var pageCount = Math.Max(1, (sorted.Count + PageSize - 1) / PageSize);
            _page = Math.Clamp(_page, 0, pageCount - 1);
            var pageEntries = sorted.Skip(_page * PageSize).Take(PageSize).ToArray();
            for (var index = 0; index < pageEntries.Length; index++)
            {
                var entry = pageEntries[index];
                var currentEntry = entry;
                var detail = string.Format("Cultiway.MagicWeb.UI.Format.EntrySummary".Localize(),
                    currentEntry.ManaDemand.ToString("0.##", CultureInfo.InvariantCulture),
                    SkillCastResources.Mana.id.Localize(),
                    currentEntry.Entry.IsDefault
                        ? "Cultiway.MagicWeb.UI.Source.Native".Localize()
                        : "Cultiway.MagicWeb.UI.Source.Uploaded".Localize());
                var icon = currentEntry.Frames.Length == 0 ? null : currentEntry.Frames[0];
                _rowPool.GetNext().SetupEntry(currentEntry.Entry.Container, icon, currentEntry.Name, detail,
                    currentEntry.ItemLevelName,
                    currentEntry.Entry.Container.Id == _selectedEntityId,
                    pageCount == 1 && index == pageEntries.Length - 1, () =>
                    {
                        _selectedEntityId = currentEntry.Entry.Container.Id;
                        RefreshList();
                    });
            }

            if (pageCount > 1)
            {
                _rowPool.GetNext().SetupPager(_page, pageCount, () =>
                {
                    _page--;
                    RefreshList();
                }, () =>
                {
                    _page++;
                    RefreshList();
                });
            }
        }

        _resultCount.text = string.Format("Cultiway.MagicWeb.UI.Format.ResultCount".Localize(), filtered.Count,
            _entries.Count);
        UpdateFilterButton();
        UpdateDetail(selected, mage);
    }

    private bool MatchesFilters(EntryModel entry, MageContext mage)
    {
        var search = _search.text.Trim();
        if (search.Length > 0 && entry.SearchText.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
            return false;
        if (entry.ItemLevelValue < _minItemLevel || entry.ItemLevelValue > _maxItemLevel) return false;
        if (_sourceFilter == SourceFilter.Default && !entry.Entry.IsDefault) return false;
        if (_sourceFilter == SourceFilter.Uploaded && entry.Entry.IsDefault) return false;
        if (_entityFilter != null && entry.EntityId != _entityFilter) return false;
        if (_trajectoryFilter != null && entry.TrajectoryId != _trajectoryFilter) return false;
        if (_elementFilters.Count > 0 && !_elementFilters.Any(entry.Semantics.Contains)) return false;
        foreach (var (semantic, state) in _semanticFilters)
        {
            if (state == SemanticFilterState.Required && !entry.Semantics.Contains(semantic)) return false;
            if (state == SemanticFilterState.Excluded && entry.Semantics.Contains(semantic)) return false;
        }

        if (mage == null) return true;
        var compatibility = ResolveCompatibility(entry, mage);
        if (_understandableOnly && !compatibility.Understandable) return false;
        if (_hideKnown && compatibility.KnownFamily) return false;
        return true;
    }

    private List<GroupModel> BuildGroups(List<EntryModel> filtered)
    {
        return filtered.GroupBy(GetGroupKey)
            .Select(group =>
            {
                var sample = group.First();
                return new GroupModel
                {
                    Key = group.Key,
                    Name = GetGroupName(sample),
                    Order = GetGroupOrder(sample),
                    Entries = group.ToList()
                };
            })
            .OrderBy(group => group.Order, StringComparer.Ordinal)
            .ThenBy(group => group.Name, StringComparer.Ordinal)
            .ToList();
    }

    private string GetGroupKey(EntryModel entry)
    {
        return _groupMode switch
        {
            GroupMode.Entity => $"entity:{entry.EntityId}:{entry.AnimationIndex}",
            GroupMode.Element => $"element:{entry.Entry.Profile.PrimaryElement.id}",
            GroupMode.Ring => $"ring:{entry.Entry.Profile.Ring}",
            GroupMode.Source => entry.Entry.IsDefault ? "source:default" : "source:uploaded",
            GroupMode.Trajectory => $"trajectory:{entry.TrajectoryId}",
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private string GetGroupName(EntryModel entry)
    {
        return _groupMode switch
        {
            GroupMode.Entity => string.Format("Cultiway.MagicWeb.UI.Format.EntityGroup".Localize(),
                entry.EntityName, entry.AnimationIndex + 1),
            GroupMode.Element => entry.Entry.Profile.PrimaryElement.GetName(),
            GroupMode.Ring => string.Format("Cultiway.MagicWeb.UI.Format.Ring".Localize(), entry.Entry.Profile.Ring),
            GroupMode.Source => entry.Entry.IsDefault
                ? "Cultiway.MagicWeb.UI.Source.Native".Localize()
                : "Cultiway.MagicWeb.UI.Source.Uploaded".Localize(),
            GroupMode.Trajectory => entry.TrajectoryName,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private string GetGroupOrder(EntryModel entry)
    {
        return _groupMode switch
        {
            GroupMode.Entity => $"{entry.EntityName}\u0001{entry.AnimationIndex:D3}",
            GroupMode.Element => Array.IndexOf(ElementSemantics, entry.Entry.Profile.PrimaryElement).ToString("D2"),
            GroupMode.Ring => entry.Entry.Profile.Ring.ToString("D2"),
            GroupMode.Source => entry.Entry.IsDefault ? "00" : "01",
            GroupMode.Trajectory => entry.TrajectoryName,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private IEnumerable<EntryModel> SortEntries(IEnumerable<EntryModel> entries)
    {
        IOrderedEnumerable<EntryModel> ordered = _sortMode switch
        {
            SortMode.Name => _sortDescending
                ? entries.OrderByDescending(entry => entry.Name, StringComparer.Ordinal)
                : entries.OrderBy(entry => entry.Name, StringComparer.Ordinal),
            SortMode.ItemLevel => _sortDescending
                ? entries.OrderByDescending(entry => entry.ItemLevelValue)
                : entries.OrderBy(entry => entry.ItemLevelValue),
            SortMode.ManaDemand => _sortDescending
                ? entries.OrderByDescending(entry => entry.ManaDemand)
                : entries.OrderBy(entry => entry.ManaDemand),
            SortMode.ModifierCount => _sortDescending
                ? entries.OrderByDescending(entry => entry.ModifierNames.Length)
                : entries.OrderBy(entry => entry.ModifierNames.Length),
            SortMode.Lifetime => _sortDescending
                ? entries.OrderByDescending(GetRemainingYears)
                : entries.OrderBy(GetRemainingYears),
            _ => throw new ArgumentOutOfRangeException()
        };
        return ordered.ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .ThenBy(entry => entry.Entry.Container.Id);
    }

    private void UpdateDetail(EntryModel entry, MageContext mage)
    {
        _detailName.text = entry.Name;
        _detailSummary.text = string.Format("Cultiway.MagicWeb.UI.Format.DetailSummary".Localize(),
            entry.ItemLevelName, SkillCastResources.Mana.id.Localize(),
            entry.ManaDemand.ToString("0.##", CultureInfo.InvariantCulture), entry.EntityName,
            entry.TrajectoryName);
        _detailFrames = entry.Frames;
        _detailFrameIndex = 0;
        _detailFrameTimer = 0f;
        _detailFrameInterval = entry.FrameInterval;
        _detailIcon.sprite = _detailFrames.Length == 0 ? null : _detailFrames[0];
        UiTooltip.Set(_detailIcon.gameObject,
            () => SkillTooltip.Show(_detailIcon.gameObject, entry.Entry.Container));

        var lifetime = entry.Entry.IsDefault
            ? "Cultiway.MagicWeb.UI.Lifetime.Permanent".Localize()
            : string.Format("Cultiway.MagicWeb.UI.Format.RemainingYears".Localize(),
                GetRemainingYears(entry).ToString("0.0", CultureInfo.InvariantCulture));
        if (entry.Entry.IsDefault)
        {
            _detailSource.text = string.Format("Cultiway.MagicWeb.UI.Format.NativeSource".Localize(), lifetime);
        }
        else
        {
            var publisher = string.IsNullOrWhiteSpace(entry.Entry.PublisherName)
                ? "Cultiway.MagicWeb.UI.Source.UnknownPublisher".Localize()
                : entry.Entry.PublisherName;
            var uploadYear = Mathf.FloorToInt((float)(entry.Entry.PublishedWorldTime / TimeScales.SecPerYear));
            _detailSource.text = string.Format("Cultiway.MagicWeb.UI.Format.UploadedSource".Localize(), publisher,
                entry.Entry.PublisherActorId, uploadYear, lifetime);
        }

        _detailSemantics.text = string.Format("Cultiway.MagicWeb.UI.Format.Tags".Localize(),
            string.Join("、", entry.Semantics.Select(semantic => semantic.GetName())));
        _detailModifiers.text = string.Format("Cultiway.MagicWeb.UI.Format.Modifiers".Localize(),
            entry.ModifierNames.Length == 0
                ? "Cultiway.MagicWeb.UI.State.None".Localize()
                : string.Join("\n", entry.ModifierNames));
        if (mage == null)
        {
            _detailCompatibility.text = "Cultiway.MagicWeb.UI.Compatibility.NoMage".Localize();
            return;
        }

        var compatibility = ResolveCompatibility(entry, mage);
        var yes = "Cultiway.MagicWeb.UI.State.Yes".Localize();
        var no = "Cultiway.MagicWeb.UI.State.No".Localize();
        _detailCompatibility.text = string.Format("Cultiway.MagicWeb.UI.Format.Compatibility".Localize(),
            mage.Actor.Base.getName(), mage.Actor.Base.data.id,
            compatibility.Affinity.ToString("0.000", CultureInfo.InvariantCulture),
            MagicSetting.MagicStudyAffinityThreshold.ToString("0.000", CultureInfo.InvariantCulture),
            compatibility.AffinityAllowed ? yes : no,
            entry.Entry.Profile.Ring, mage.MaxRing, compatibility.RingAllowed ? yes : no,
            compatibility.KnownFamily ? yes : no,
            mage.KnownCount, mage.Capacity, compatibility.CapacityFull ? yes : no,
            compatibility.Understandable
                ? "Cultiway.MagicWeb.UI.Compatibility.Understandable".Localize()
                : "Cultiway.MagicWeb.UI.Compatibility.NotUnderstandable".Localize());
    }

    private void ShowEmptyDetail()
    {
        _detailFrames = Array.Empty<Sprite>();
        _detailIcon.sprite = null;
        _detailName.text = "Cultiway.MagicWeb.UI.State.NoResults".Localize();
        _detailSummary.text = string.Empty;
        _detailSource.text = string.Empty;
        _detailSemantics.text = string.Empty;
        _detailModifiers.text = string.Empty;
        _detailCompatibility.text = string.Empty;
    }

    private static MageContext BuildMageContext()
    {
        if (!SelectedUnit.isSet()) return null;
        var actor = SelectedUnit.unit;
        if (actor.isRekt()) return null;
        var actorExtend = actor.GetExtend();
        if (!actorExtend.HasCultisys<Magic>() || !actorExtend.HasElementRoot()) return null;

        ref var magic = ref actorExtend.GetCultisys<Magic>();
        var context = new MageContext
        {
            Actor = actorExtend,
            Root = actorExtend.GetElementRoot(),
            MaxRing = Cultisyses.GetMaxSpellRing(magic.CurrLevel),
            Capacity = Cultisyses.GetKnownSpellCapacity(magic.CurrLevel),
            KnownFamilies = new HashSet<string>(StringComparer.Ordinal)
        };
        foreach (var skill in actorExtend.GetLearnedSkillsInOrder())
        {
            if (skill.IsNull || !SkillCastResourceResolver.UsesResource(skill, SkillCastResources.Mana)) continue;
            var profile = MagicSpellProfile.Resolve(skill);
            if (profile == null) continue;
            context.KnownCount++;
            context.KnownFamilies.Add(profile.FamilySignature);
        }
        return context;
    }

    private static Compatibility ResolveCompatibility(EntryModel entry, MageContext mage)
    {
        var affinity = entry.Entry.Profile.ElementRequirement.GetWeightedAffinity(mage.Root);
        return new Compatibility(affinity, entry.Entry.Profile.Ring <= mage.MaxRing,
            affinity >= MagicSetting.MagicStudyAffinityThreshold,
            mage.KnownFamilies.Contains(entry.Entry.Profile.FamilySignature),
            mage.KnownCount >= mage.Capacity);
    }

    private void RefreshFilterAssets()
    {
        _entityIds = _entries.Select(entry => entry.EntityId).Distinct(StringComparer.Ordinal)
            .OrderBy(id => id.Localize(), StringComparer.Ordinal).ToArray();
        _trajectoryIds = _entries.Select(entry => entry.TrajectoryId).Distinct(StringComparer.Ordinal)
            .OrderBy(id => id.Localize(), StringComparer.Ordinal).ToArray();
        if (_entityFilter != null && !_entityIds.Contains(_entityFilter, StringComparer.Ordinal))
            _entityFilter = null;
        if (_trajectoryFilter != null && !_trajectoryIds.Contains(_trajectoryFilter, StringComparer.Ordinal))
            _trajectoryFilter = null;

        var presentSemantics = new HashSet<SemanticAsset>(_entries.SelectMany(entry => entry.Semantics));
        _elementFilters.RemoveWhere(semantic => !presentSemantics.Contains(semantic));
        foreach (var semantic in _semanticFilters.Keys
                     .Where(semantic => !presentSemantics.Contains(semantic)).ToArray())
            _semanticFilters.Remove(semantic);
    }

    private void RefreshDynamicFilters()
    {
        if (_filterContent == null) return;
        if (_dynamicFilterRoot != null) Object.DestroyImmediate(_dynamicFilterRoot);

        var presentSemantics = _entries.SelectMany(entry => entry.Semantics).Distinct().ToArray();
        var presentElements = ElementSemantics.Where(presentSemantics.Contains).ToArray();
        var semanticGroups = presentSemantics.Where(semantic => semantic.Facet.id != "element")
            .GroupBy(semantic => semantic.Facet)
            .OrderBy(group => ModClass.L.SemanticFacetLibrary.list.IndexOf(group.Key))
            .ThenBy(group => group.Key.id, StringComparer.Ordinal)
            .Select(group => (Facet: group.Key,
                Semantics: group.OrderBy(semantic => semantic.GetName(), StringComparer.Ordinal).ToArray()))
            .ToArray();
        var rowCount = (presentElements.Length + 5) / 6 +
                       semanticGroups.Sum(group => (group.Semantics.Length + 5) / 6);
        var height = 18f * (1 + semanticGroups.Length) + 22f * rowCount + 4f;
        _dynamicFilterRoot = UiLayout.Create(_filterContent, "DynamicFilters", false,
            RootWidth - 8f, Math.Max(22f, height), 2f);
        CreateFilterSection(_dynamicFilterRoot.transform, "Cultiway.MagicWeb.UI.Filter.Elements".Localize(),
            presentElements, false);
        foreach (var group in semanticGroups)
        {
            CreateFilterSection(_dynamicFilterRoot.transform, group.Facet.GetName(), group.Semantics, true);
        }
    }

    private void CreateFilterSection(
        Transform parent,
        string title,
        IReadOnlyList<SemanticAsset> semantics,
        bool triState)
    {
        if (semantics.Count == 0) return;
        UiElements.CreateText(parent, $"Title_{title}", title, RootWidth - 12f, 16f, 7,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        for (var offset = 0; offset < semantics.Count; offset += 6)
        {
            var row = UiLayout.Create(parent, $"Row_{title}_{offset}", true, RootWidth - 12f, 20f, 3f);
            foreach (var semantic in semantics.Skip(offset).Take(6))
            {
                var currentSemantic = semantic;
                Button button = null;
                button = UiElements.CreateButton(row.transform, currentSemantic.id,
                    currentSemantic.GetName(), 80f, 19f, () =>
                    {
                        if (triState)
                        {
                            var state = _semanticFilters.TryGetValue(currentSemantic, out var existing)
                                ? existing
                                : SemanticFilterState.Off;
                            state = (SemanticFilterState)(((int)state + 1) % 3);
                            if (state == SemanticFilterState.Off) _semanticFilters.Remove(currentSemantic);
                            else _semanticFilters[currentSemantic] = state;
                        }
                        else if (!_elementFilters.Add(currentSemantic))
                        {
                            _elementFilters.Remove(currentSemantic);
                        }

                        UpdateChipVisual(button, currentSemantic, triState);
                        ApplyFilterChange();
                    });
                UpdateChipVisual(button, currentSemantic, triState);
                UiTooltip.Set(button.gameObject, currentSemantic.GetName(),
                    triState
                        ? "Cultiway.MagicWeb.UI.Tooltip.TagState".Localize()
                        : "Cultiway.MagicWeb.UI.Tooltip.ElementState".Localize());
            }
        }
    }

    private void UpdateChipVisual(Button button, SemanticAsset semantic, bool triState)
    {
        var state = triState
            ? _semanticFilters.TryGetValue(semantic, out var current) ? current : SemanticFilterState.Off
            : _elementFilters.Contains(semantic) ? SemanticFilterState.Required : SemanticFilterState.Off;
        var prefix = state switch
        {
            SemanticFilterState.Required => "+ ",
            SemanticFilterState.Excluded => "- ",
            _ => string.Empty
        };
        button.GetComponentInChildren<Text>().text = prefix + semantic.GetName();
        button.GetComponent<Image>().color = state switch
        {
            SemanticFilterState.Required => new Color(0.42f, 0.82f, 0.56f, 1f),
            SemanticFilterState.Excluded => new Color(0.9f, 0.42f, 0.4f, 1f),
            _ => Color.white
        };
    }

    private void UpdateControls()
    {
        _groupButton.GetComponentInChildren<Text>().text = $"Cultiway.MagicWeb.UI.Group.{_groupMode}".Localize();
        _sortButton.GetComponentInChildren<Text>().text = $"Cultiway.MagicWeb.UI.Sort.{_sortMode}".Localize();
        UiElements.SetButtonIcon(_directionButton,
            _sortDescending ? UiIcons.MoveDown : UiIcons.MoveUp);
        _minLevelText.text = FormatMagicItemLevel(ItemLevel.FromValue(_minItemLevel));
        _maxLevelText.text = FormatMagicItemLevel(ItemLevel.FromValue(_maxItemLevel));
        _sourceFilterButton.GetComponentInChildren<Text>().text =
            $"Cultiway.MagicWeb.UI.Filter.Source.{_sourceFilter}".Localize();
        _entityFilterButton.GetComponentInChildren<Text>().text = _entityFilter == null
            ? "Cultiway.MagicWeb.UI.Filter.AllEntities".Localize()
            : _entityFilter.Localize();
        _trajectoryFilterButton.GetComponentInChildren<Text>().text = _trajectoryFilter == null
            ? "Cultiway.MagicWeb.UI.Filter.AllTrajectories".Localize()
            : _trajectoryFilter.Localize();
        UpdateFilterButton();
    }

    private void UpdateFilterButton()
    {
        var count = (_minItemLevel > 0 ? 1 : 0) + (_maxItemLevel < MaxItemLevel ? 1 : 0) +
                    (_sourceFilter != SourceFilter.All ? 1 : 0) + (_entityFilter != null ? 1 : 0) +
                    (_trajectoryFilter != null ? 1 : 0) + _elementFilters.Count + _semanticFilters.Count +
                    (_understandableOnly ? 1 : 0) + (_hideKnown ? 1 : 0);
        _filterButton.GetComponentInChildren<Text>().text = string.Format(
            "Cultiway.MagicWeb.UI.Format.FilterCount".Localize(), count);
    }

    private void SetFilterExpanded(bool expanded)
    {
        _filterExpanded = expanded;
        _filterRoot.SetActive(expanded);
        var bodyHeight = expanded ? BodyExpandedHeight : BodyCollapsedHeight;
        UiLayout.SetSize(_body.transform, RootWidth, bodyHeight);
        UiLayout.SetSize(_leftScroll.transform, LeftWidth, bodyHeight);
        UiLayout.SetSize(_rightScroll.transform, RightWidth, bodyHeight);
        UpdateFilterButton();
    }

    private void CycleGroupMode()
    {
        _groupMode = (GroupMode)(((int)_groupMode + 1) % Enum.GetValues(typeof(GroupMode)).Length);
        _expandedGroupKey = null;
        _groupCollapsed = false;
        _page = 0;
        Refresh();
    }

    private void CycleSortMode()
    {
        _sortMode = (SortMode)(((int)_sortMode + 1) % Enum.GetValues(typeof(SortMode)).Length);
        _page = 0;
        Refresh();
    }

    private void ToggleSortDirection()
    {
        _sortDescending = !_sortDescending;
        _page = 0;
        Refresh();
    }

    private void CycleSourceFilter()
    {
        _sourceFilter = (SourceFilter)(((int)_sourceFilter + 1) % Enum.GetValues(typeof(SourceFilter)).Length);
        ApplyFilterChange();
    }

    private void CycleEntityFilter()
    {
        _entityFilter = CycleAssetFilter(_entityFilter, _entityIds);
        ApplyFilterChange();
    }

    private void CycleTrajectoryFilter()
    {
        _trajectoryFilter = CycleAssetFilter(_trajectoryFilter, _trajectoryIds);
        ApplyFilterChange();
    }

    private static string CycleAssetFilter(string current, IReadOnlyList<string> values)
    {
        if (values.Count == 0) return null;
        if (current == null) return values[0];
        var index = -1;
        for (var i = 0; i < values.Count; i++)
        {
            if (!string.Equals(values[i], current, StringComparison.Ordinal)) continue;
            index = i;
            break;
        }
        return index < 0 || index + 1 >= values.Count ? null : values[index + 1];
    }

    private void AdjustItemLevel(bool minimum, int delta)
    {
        if (minimum) _minItemLevel = Math.Clamp(_minItemLevel + delta, 0, _maxItemLevel);
        else _maxItemLevel = Math.Clamp(_maxItemLevel + delta, _minItemLevel, MaxItemLevel);
        ApplyFilterChange();
    }

    private static string FormatMagicItemLevel(ItemLevel itemLevel)
    {
        return SkillCastResources.Mana.ItemLevelFormatter(itemLevel);
    }

    private void ApplyFilterChange()
    {
        _page = 0;
        Refresh();
    }

    private void ResetView()
    {
        _search.text = string.Empty;
        _groupMode = GroupMode.Entity;
        _sortMode = SortMode.Name;
        _sortDescending = false;
        _sourceFilter = SourceFilter.All;
        _minItemLevel = 0;
        _maxItemLevel = MaxItemLevel;
        _entityFilter = null;
        _trajectoryFilter = null;
        _elementFilters.Clear();
        _semanticFilters.Clear();
        _understandableOnly = false;
        _hideKnown = false;
        _understandableOnlyToggle.isOn = false;
        _hideKnownToggle.isOn = false;
        _expandedGroupKey = null;
        _groupCollapsed = false;
        _page = 0;
        RefreshDynamicFilters();
        Refresh();
    }

    private static double GetRemainingYears(EntryModel entry)
    {
        if (entry.Entry.IsDefault) return double.PositiveInfinity;
        var elapsed = GetWorldTime() - entry.Entry.LastAccessWorldTime;
        return Math.Max(0d, MagicSetting.MagicWebExpirationYears - elapsed / TimeScales.SecPerYear);
    }

    private static double GetWorldTime()
    {
        return World.world?.map_stats?.world_time ?? 0d;
    }

    private static int GetWorldYear()
    {
        return Mathf.FloorToInt((float)(GetWorldTime() / TimeScales.SecPerYear));
    }

}
