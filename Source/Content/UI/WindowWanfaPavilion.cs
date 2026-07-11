using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.UI.Prefab;
using Cultiway.Content.WanfaPavilion;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Visuals;
using UnityEngine;
using UnityEngine.UI;
using NeoModLoader.api;

namespace Cultiway.Content.UI;

public sealed class WindowWanfaPavilion : AbstractWideWindow<WindowWanfaPavilion>
{
    public const string Id = "Cultiway.UI.WindowWanfaPavilion";
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
    private int _elementFilter;
    private int _entityFilter;
    private int _sort;
    private string[] _entityIds;
    private SkillVfxElementAsset[] _vfxFilters;

    protected override void Init()
    {
        BackgroundTransform.Find("Scroll View").gameObject.SetActive(false);
        var root = WanfaUiFactory.CreateLayout(BackgroundTransform, "WanfaRoot", false, 520f, 238f, 4f);
        root.transform.localPosition = new Vector3(0f, -8f);

        var toolbar = WanfaUiFactory.CreateLayout(root.transform, "Toolbar", true, 520f, 24f, 4f);
        WanfaUiFactory.CreateButton(toolbar.transform, "New", "Cultiway.Wanfa.UI.Action.New".Localize(), 44f, 22f,
            () => WindowWanfaSkillEditor.Open(WanfaPavilionService.Instance.CreateDraft(), false));
        _search = WanfaUiFactory.CreateInput(toolbar.transform, "Search", string.Empty,
            "Cultiway.Wanfa.UI.Placeholder.Search".Localize(), 130f, 22f);
        _search.onValueChanged.AddListener(_ => Refresh());
        _elementFilterButton = WanfaUiFactory.CreateButton(toolbar.transform, "ElementFilter",
            "Cultiway.Wanfa.UI.Filter.AllVisual".Localize(), 62f, 22f, CycleElementFilter);
        _entityFilterButton = WanfaUiFactory.CreateButton(toolbar.transform, "EntityFilter",
            "Cultiway.Wanfa.UI.Filter.AllEntities".Localize(), 76f, 22f, CycleEntityFilter);
        _favoriteOnly = WanfaUiFactory.CreateToggle(toolbar.transform, "FavoriteOnly",
            "Cultiway.Wanfa.UI.Label.FavoriteOnly".Localize(), false, 62f, 22f);
        _favoriteOnly.onValueChanged.AddListener(_ => Refresh());
        _sortButton = WanfaUiFactory.CreateButton(toolbar.transform, "Sort", SortNamePaths[0].Localize(),
            54f, 22f, CycleSort);

        var content = WanfaUiFactory.CreateScrollContent(root.transform, "BlueprintList", 520f, 208f);
        _rowPool = new MonoObjPool<WanfaBlueprintRow>(WanfaBlueprintRow.Prefab, content);
        WanfaPavilionService.Instance.Changed += Refresh;
        RefreshVfxFilters();
        RefreshEntityIds();
    }

    public override void OnNormalEnable()
    {
        RefreshVfxFilters();
        RefreshEntityIds();
        Refresh();
    }

    private void Refresh()
    {
        if (_rowPool == null) return;
        _rowPool.Clear();
        IEnumerable<SkillBlueprint> query = WanfaPavilionService.Instance.Blueprints;
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
            _rowPool.GetNext().Setup(item, allowMove,
                () => WanfaPavilionService.Instance.SetFavorite(item.Id, !item.Favorite),
                () => WanfaPavilionService.Instance.Move(item.Id, -1),
                () => WanfaPavilionService.Instance.Move(item.Id, 1),
                () => WindowWanfaSkillEditor.Open(item, true),
                () => HandleCopy(item),
                () => WanfaPavilionService.Instance.Delete(item.Id),
                () => WanfaDropExportSession.Enter(item.Id));
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

    private static void HandleCopy(SkillBlueprint source)
    {
        WindowWanfaSkillEditor.Open(source.CreateCopy(), false);
    }

    private void CycleElementFilter()
    {
        _elementFilter = (_elementFilter + 1) % (_vfxFilters.Length + 1);
        _elementFilterButton.GetComponentInChildren<Text>().text = _elementFilter == 0
            ? "Cultiway.Wanfa.UI.Filter.AllVisual".Localize()
            : _vfxFilters[_elementFilter - 1].id.Localize();
        Refresh();
    }

    private void CycleEntityFilter()
    {
        _entityFilter = (_entityFilter + 1) % (_entityIds.Length + 1);
        _entityFilterButton.GetComponentInChildren<Text>().text = _entityFilter == 0
            ? "Cultiway.Wanfa.UI.Filter.AllEntities".Localize()
            : _entityIds[_entityFilter - 1].Localize();
        Refresh();
    }

    private void CycleSort()
    {
        _sort = (_sort + 1) % SortNamePaths.Length;
        _sortButton.GetComponentInChildren<Text>().text = SortNamePaths[_sort].Localize();
        Refresh();
    }

    private void RefreshEntityIds()
    {
        _entityIds = ModClass.I.SkillV3.SkillLib.list
            .Where(item => item.EditorSelectable)
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
