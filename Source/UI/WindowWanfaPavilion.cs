using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Core.SkillLibV3.Wanfa;
using Cultiway.UI.Prefab;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Visuals;
using UnityEngine;
using UnityEngine.UI;
using NeoModLoader.api;

namespace Cultiway.UI;

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
        var create = WanfaUiFactory.CreateIconButton(toolbar.transform, "New", WanfaUiIcons.NewSkill, 28f, 22f,
            () => WindowWanfaSkillEditor.Open(WanfaPavilionService.Instance.CreateDraft(), false));
        WanfaUiFactory.SetTooltip(create.gameObject, "Cultiway.Wanfa.UI.Action.New",
            "Cultiway.Wanfa.UI.Tooltip.New");
        _search = WanfaUiFactory.CreateInput(toolbar.transform, "Search", string.Empty,
            "Cultiway.Wanfa.UI.Placeholder.Search".Localize(), 132f, 22f);
        AddSearchIcon(_search);
        WanfaUiFactory.SetTooltip(_search, "Cultiway.Wanfa.UI.Placeholder.Search",
            "Cultiway.Wanfa.UI.Tooltip.Search");
        _search.onValueChanged.AddListener(_ => Refresh());
        _elementFilterButton = WanfaUiFactory.CreateIconTextButton(toolbar.transform, "ElementFilter",
            WanfaUiIcons.Element, "Cultiway.Wanfa.UI.Filter.AllVisual".Localize(), 88f, 22f, CycleElementFilter);
        WanfaUiFactory.SetTooltip(_elementFilterButton.gameObject, "Cultiway.Wanfa.UI.Tooltip.ElementFilter.Title",
            "Cultiway.Wanfa.UI.Tooltip.ElementFilter");
        _entityFilterButton = WanfaUiFactory.CreateIconTextButton(toolbar.transform, "EntityFilter",
            WanfaUiIcons.Entity, "Cultiway.Wanfa.UI.Filter.AllEntities".Localize(), 98f, 22f, CycleEntityFilter);
        WanfaUiFactory.SetTooltip(_entityFilterButton.gameObject, "Cultiway.Wanfa.UI.Tooltip.EntityFilter.Title",
            "Cultiway.Wanfa.UI.Tooltip.EntityFilter");
        _favoriteOnly = WanfaUiFactory.CreateIconToggle(toolbar.transform, "FavoriteOnly", WanfaUiIcons.Favorite,
            false, 28f, 22f);
        WanfaUiFactory.SetTooltip(_favoriteOnly, "Cultiway.Wanfa.UI.Label.FavoriteOnly",
            "Cultiway.Wanfa.UI.Tooltip.FavoriteOnly");
        _favoriteOnly.onValueChanged.AddListener(_ => Refresh());
        _sortButton = WanfaUiFactory.CreateIconTextButton(toolbar.transform, "Sort", WanfaUiIcons.Sort,
            SortNamePaths[0].Localize(), 70f, 22f, CycleSort);
        WanfaUiFactory.SetTooltip(_sortButton.gameObject, "Cultiway.Wanfa.UI.Tooltip.Sort.Title",
            "Cultiway.Wanfa.UI.Tooltip.Sort");

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
                () => WanfaPavilionService.Instance.RequestGrant(item.Id));
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

    private static void AddSearchIcon(InputField input)
    {
        var icon = new GameObject("SearchIcon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(input.transform, false);
        var rect = icon.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(14f, 14f);
        rect.anchoredPosition = new Vector2(10f, 0f);
        var image = icon.GetComponent<Image>();
        image.sprite = SpriteTextureLoader.getSprite(WanfaUiIcons.Search);
        image.preserveAspect = true;
        image.raycastTarget = false;
        input.textComponent.rectTransform.offsetMin = new Vector2(20f, 1f);
        input.placeholder.GetComponent<RectTransform>().offsetMin = new Vector2(20f, 1f);
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
