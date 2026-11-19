using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Core.SkillLibV3.Wanfa;
using Cultiway.UI.Prefab;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Core.Skills;
using UnityEngine;
using UnityEngine.UI;
using NeoModLoader.api;

namespace Cultiway.UI;

public sealed partial class WindowWanfaPavilion : AbstractWideWindow<WindowWanfaPavilion>
{
    public const string Id = "Cultiway.UI.WindowWanfaPavilion";
    public static readonly Vector2 WindowSize = new(600f, 360f);
    private const float RootHeight = 318f;
    private const float BlueprintListHeight = 288f;
    private static readonly string[] SortNamePaths =
    {
        "Cultiway.Wanfa.UI.Sort.Manual", "Cultiway.Wanfa.UI.Sort.Name", "Cultiway.Wanfa.UI.Sort.Recent",
        "Cultiway.Wanfa.UI.Sort.Revision"
    };

    private MonoObjPool<WanfaBlueprintRow> _rowPool;
    private InputField _search;
    private Button _elementFilterButton;
    private Button _entityFilterButton;
    private Toggle _favoriteOnly;
    private Button _sortButton;
    private Button _targetFilterButton;
    private Text _selectedCount;
    private UiOptionMenu _optionMenu;
    private CanvasGroup _rootCanvas;
    private int _elementFilter;
    private int _entityFilter;
    private int _sort;
    private string[] _entityIds;
    private SkillVfxElementAsset[] _vfxFilters;

    protected override void Init()
    {
        UiWindowContext context = UiWindowContext.Bind(BackgroundTransform);
        UiWideWindowFit.Attach(BackgroundTransform);
        var root = UiLayout.Create(BackgroundTransform, "WanfaRoot", false, 520f, RootHeight, 4f);
        root.transform.localPosition = new Vector3(0f, -8f);
        _rootCanvas = root.AddComponent<CanvasGroup>();

        var toolbar = UiLayout.Create(root.transform, "Toolbar", true, 520f, 24f, 4f);
        var create = UiElements.CreateIconButton(toolbar.transform, "New", UiIcons.Add, 28f, 22f,
            ShowCreationChoices);
        UiTooltip.Set(create.gameObject, "Cultiway.Wanfa.UI.Action.New",
            "Cultiway.Wanfa.UI.Tooltip.New");
        _search = UiSearchField.Create(toolbar.transform, "Search", string.Empty,
            LocalizedTextManager.getText("Cultiway.Wanfa.UI.Placeholder.Search"), 100f, 22f).Input;
        UiTooltip.Set(_search, "Cultiway.Wanfa.UI.Placeholder.Search",
            "Cultiway.Wanfa.UI.Tooltip.Search");
        _search.onValueChanged.AddListener(_ => Refresh());
        _elementFilterButton = UiElements.CreateIconTextButton(toolbar.transform, "ElementFilter",
            WanfaUiIcons.Element, LocalizedTextManager.getText("Cultiway.Wanfa.UI.Filter.AllVisual"), 88f, 22f,
            ShowElementFilterMenu);
        UiTooltip.Set(_elementFilterButton.gameObject, "Cultiway.Wanfa.UI.Tooltip.ElementFilter.Title",
            "Cultiway.Wanfa.UI.Tooltip.ElementFilter");
        _entityFilterButton = UiElements.CreateIconTextButton(toolbar.transform, "EntityFilter",
            WanfaUiIcons.Entity, LocalizedTextManager.getText("Cultiway.Wanfa.UI.Filter.AllEntities"), 98f, 22f,
            ShowEntityFilterMenu);
        UiTooltip.Set(_entityFilterButton.gameObject, "Cultiway.Wanfa.UI.Tooltip.EntityFilter.Title",
            "Cultiway.Wanfa.UI.Tooltip.EntityFilter");
        _favoriteOnly = UiElements.CreateIconToggle(toolbar.transform, "FavoriteOnly", UiIcons.Favorite,
            false, 28f, 22f, UiIconToggleStyle.Favorite);
        UiTooltip.Set(_favoriteOnly, "Cultiway.Wanfa.UI.Label.FavoriteOnly",
            "Cultiway.Wanfa.UI.Tooltip.FavoriteOnly");
        _favoriteOnly.onValueChanged.AddListener(_ => Refresh());
        _sortButton = UiElements.CreateIconTextButton(toolbar.transform, "Sort", UiIcons.Sort,
            LocalizedTextManager.getText(SortNamePaths[0]), 70f, 22f, ShowSortMenu);
        UiTooltip.Set(_sortButton.gameObject, "Cultiway.Wanfa.UI.Tooltip.Sort.Title",
            "Cultiway.Wanfa.UI.Tooltip.Sort");
        _targetFilterButton = UiElements.CreateIconButton(toolbar.transform, "TargetFilter", UiIcons.TargetFilter,
            28f, 22f, ShowTargetFilter, 4f);
        UiTooltip.Set(_targetFilterButton.gameObject, "Cultiway.Wanfa.UI.Action.TargetFilter",
            "Cultiway.Wanfa.UI.Tooltip.TargetFilter");
        _selectedCount = UiElements.CreateText(toolbar.transform, "SelectedCount", string.Empty, 52f, 22f, 6);

        UiScrollPane catalog = UiScrollPane.CreateVertical(root.transform, "BlueprintList", 520f,
            BlueprintListHeight);
        catalog.AttachOriginalScrollbar(context.ScrollbarTemplate);
        catalog.SetWindowFrame(UiTheme.Current.Metrics.SpacingMd);
        _rowPool = new MonoObjPool<WanfaBlueprintRow>(WanfaBlueprintRow.Prefab, catalog.Content);
        _optionMenu = new UiOptionMenu(BackgroundTransform, _rootCanvas, context.ScrollbarTemplate,
            new UiOptionMenuConfig
            {
                SearchPlaceholder = "Cultiway.Construction.Search",
                EmptyText = "Cultiway.Construction.Empty",
                CloseText = "Cultiway.Construction.Close",
                LocalizeLabels = true,
                CloseTooltipTitle = "Cultiway.UI.OptionMenu.Action.Close",
                CloseTooltipDescription = "Cultiway.UI.OptionMenu.Tooltip.Close",
            });
        WanfaPavilionService.Instance.Changed += Refresh;
        WanfaPavilionService.Instance.WorldStateClearing += ClearConstructionMenus;
        RefreshVfxFilters();
        RefreshEntityIds();
    }

    public override void OnNormalEnable()
    {
        RefreshVfxFilters();
        RefreshEntityIds();
        Refresh();
    }

    /// <summary>目录包含世界构造时，关闭窗口即释放该次选择的回调。</summary>
    public override void OnNormalDisable() => _optionMenu.Clear();

    private void OnDestroy()
    {
        WanfaPavilionService.Instance.Changed -= Refresh;
        WanfaPavilionService.Instance.WorldStateClearing -= ClearConstructionMenus;
    }

    private void Refresh()
    {
        if (_rowPool == null) return;
        _rowPool.Clear();
        var service = WanfaPavilionService.Instance;
        _selectedCount.text = string.Format(LocalizedTextManager.getText("Cultiway.Wanfa.UI.Format.SelectedCount"),
            service.SelectedBlueprintCount);
        UiElements.SetButtonLabel(_elementFilterButton, _elementFilter == 0
            ? LocalizedTextManager.getText("Cultiway.Wanfa.UI.Filter.AllVisual")
            : _vfxFilters[_elementFilter - 1].id.Localize());
        UiElements.SetButtonLabel(_entityFilterButton, _entityFilter == 0
            ? LocalizedTextManager.getText("Cultiway.Wanfa.UI.Filter.AllEntities")
            : _entityIds[_entityFilter - 1].Localize());
        UiElements.SetButtonLabel(_sortButton, LocalizedTextManager.getText(SortNamePaths[_sort]));
        var targetFilter = service.GrantTargetFilter;
        UiStateStyle.ApplyVisual(_targetFilterButton, !targetFilter.ExpressionState.IsComplete
            ? UiControlState.Error
            : targetFilter.Expression.Count > 0 ? UiControlState.Selected : UiControlState.Normal);
        IEnumerable<SkillBlueprint> query = service.Blueprints;
        var search = _search.text.Trim();
        if (search.Length > 0)
        {
            query = query.Where(item => MatchesSearch(item, search));
        }
        if (_elementFilter > 0)
        {
            var filter = _vfxFilters[_elementFilter - 1];
            query = query.Where(item => WanfaPavilionService.Instance.ResolveVfxElement(item) == filter);
        }
        if (_entityFilter > 0)
        {
            var entityId = _entityIds[_entityFilter - 1];
            query = query.Where(item => item.EntityAssetId == entityId);
        }
        if (_favoriteOnly.isOn)
        {
            query = query.Where(item => item.Favorite);
        }

        query = _sort switch
        {
            1 => query.OrderBy(item => WanfaPavilionService.Instance.GetDisplayName(item), StringComparer.Ordinal),
            2 => query.OrderByDescending(item => item.UpdatedAtUtcTicks),
            3 => query.OrderByDescending(item => item.Revision),
            _ => query.OrderBy(item => item.SortOrder)
        };

        var allowMove = _sort == 0 && search.Length == 0 && _elementFilter == 0 && _entityFilter == 0 &&
                        !_favoriteOnly.isOn;
        foreach (var blueprint in query)
        {
            var item = blueprint;
            _rowPool.GetNext().Setup(item, allowMove, service.IsSelected(item.Id),
                () => service.SetFavorite(item.Id, !item.Favorite),
                () => service.Move(item.Id, -1),
                () => service.Move(item.Id, 1),
                () => WindowWanfaSkillEditor.Open(item, true),
                () => HandleCopy(item),
                () => service.Delete(item.Id),
                () => service.ToggleSelected(item.Id));
        }
    }

    private static bool MatchesSearch(SkillBlueprint blueprint, string search)
    {
        return WanfaPavilionService.Instance.GetDisplayName(blueprint)
                   .IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
               Contains(blueprint.EntityAssetId, search) ||
               LocalizedContains(blueprint.EntityAssetId, search) ||
               Contains(blueprint.TrajectoryAssetId, search) ||
               LocalizedContains(blueprint.TrajectoryAssetId, search) ||
               blueprint.Modifiers.Any(item => Contains(item.AssetId, search) ||
                                                LocalizedContains(item.AssetId, search)) ||
               (!string.IsNullOrWhiteSpace(blueprint.Category) &&
                blueprint.Category.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool Contains(string value, string search)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool LocalizedContains(string id, string search)
    {
        return !string.IsNullOrWhiteSpace(id) && Contains(id.Localize(), search);
    }

    /// <summary>沿用新建入口选择旧法术或当前世界实际存在的构造。</summary>
    private void ShowCreationChoices()
    {
        var options = new List<UiOptionMenuOption>
        {
            new() { Label = LocalizedTextManager.getText("Cultiway.Construction.LegacyDraft"), IconPath = UiIcons.Add,
                Select = () => WindowWanfaSkillEditor.Open(WanfaPavilionService.Instance.CreateDraft(), false) },
            new() { Label = ShareText("ConstructionLibrary"), IconPath = UiIcons.Favorite, Select = ShowConstructionLibrary }
        };
        foreach (ConstructionEntry entry in ConstructionEditing.Access.List().OrderBy(e => e.Name, StringComparer.Ordinal))
        {
            ConstructionEntry item = entry;
            Actor creator = item.Creator == 0 ? null : World.world.units.get(item.Creator);
            string name = item.Name ?? LocalizedTextManager.getText("Cultiway.Construction.Unnamed");
            string label = creator == null ? name : string.Format(LocalizedTextManager.getText("Cultiway.Construction.ByAuthor"), name, creator.getName());
            options.Add(new UiOptionMenuOption
            {
                Label = label,
                IconPath = UiIcons.Edit,
                Select = () => WindowWanfaConstructionEditor.Open(item.Id,
                    ControllableUnit.isControllingUnit() ? ControllableUnit.getControllableUnit() : null)
            });
        }
        _optionMenu.Show(LocalizedTextManager.getText("Cultiway.Construction.WorldConstructions"), options, true);
    }

    private static void HandleCopy(SkillBlueprint source)
    {
        WindowWanfaSkillEditor.Open(source.CreateCopy(), false);
    }

    private void ShowElementFilterMenu()
    {
        List<UiOptionMenuOption> options = new()
        {
            new UiOptionMenuOption
            {
                Label = LocalizedTextManager.getText("Cultiway.Wanfa.UI.Filter.AllVisual"),
                IconPath = WanfaUiIcons.Element,
                Selected = _elementFilter == 0,
                Select = () => SetElementFilter(0),
            },
        };
        for (int i = 0; i < _vfxFilters.Length; i++)
        {
            int index = i + 1;
            string label = _vfxFilters[i].id.Localize();
            options.Add(new UiOptionMenuOption
            {
                Label = label,
                IconPath = WanfaUiIcons.Element,
                SearchText = $"{label} {_vfxFilters[i].id}",
                Selected = _elementFilter == index,
                Select = () => SetElementFilter(index),
            });
        }
        _optionMenu.Show(LocalizedTextManager.getText("Cultiway.Wanfa.UI.Tooltip.ElementFilter.Title"), options, true);
    }

    private void ShowEntityFilterMenu()
    {
        List<UiOptionMenuOption> options = new()
        {
            new UiOptionMenuOption
            {
                Label = LocalizedTextManager.getText("Cultiway.Wanfa.UI.Filter.AllEntities"),
                IconPath = WanfaUiIcons.Entity,
                Selected = _entityFilter == 0,
                Select = () => SetEntityFilter(0),
            },
        };
        for (int i = 0; i < _entityIds.Length; i++)
        {
            int index = i + 1;
            string label = _entityIds[i].Localize();
            options.Add(new UiOptionMenuOption
            {
                Label = label,
                IconPath = WanfaUiIcons.Entity,
                SearchText = $"{label} {_entityIds[i]}",
                Selected = _entityFilter == index,
                Select = () => SetEntityFilter(index),
            });
        }
        _optionMenu.Show(LocalizedTextManager.getText("Cultiway.Wanfa.UI.Tooltip.EntityFilter.Title"), options, true);
    }

    private void ShowSortMenu()
    {
        List<UiOptionMenuOption> options = new();
        for (int i = 0; i < SortNamePaths.Length; i++)
        {
            int index = i;
            options.Add(new UiOptionMenuOption
            {
                Label = LocalizedTextManager.getText(SortNamePaths[i]),
                IconPath = UiIcons.Sort,
                Selected = _sort == i,
                Select = () => SetSort(index),
            });
        }
        _optionMenu.Show(LocalizedTextManager.getText("Cultiway.Wanfa.UI.Tooltip.Sort.Title"), options);
    }

    private void SetElementFilter(int index)
    {
        _elementFilter = index;
        Refresh();
    }

    private void SetEntityFilter(int index)
    {
        _entityFilter = index;
        Refresh();
    }

    private void SetSort(int index)
    {
        _sort = index;
        Refresh();
    }

    private static void ShowTargetFilter()
    {
        WindowActorTargetFilter.Open(WanfaPavilionService.Instance.GrantTargetFilter,
            "Cultiway.ActorTargetFilter.UI.Context.Wanfa",
            "Cultiway.ActorTargetFilter.UI.Expression.Empty",
            "Cultiway.ActorTargetFilter.UI.Filter.Semantics");
    }

    private void RefreshEntityIds()
    {
        _entityIds = ModClass.I.SkillV3.SkillLib.list
            .Where(item => item.CanBeLearned && item.EditorSelectable)
            .OrderBy(item => item.EditorSortOrder)
            .Select(item => item.id)
            .ToArray();
        if (_entityFilter > _entityIds.Length) _entityFilter = 0;
    }

    private void RefreshVfxFilters()
    {
        _vfxFilters = ModClass.I.SkillV3.VfxElementLib.list
            .Where(item => item != SkillVfxElementLibrary.Generic)
            .ToArray();
        if (_elementFilter > _vfxFilters.Length) _elementFilter = 0;
    }
}
