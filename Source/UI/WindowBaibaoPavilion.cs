using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.Artifacts.Baibao;
using Cultiway.Content.Libraries;
using Cultiway.UI.Prefab;
using NeoModLoader.api;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>
/// 百宝阁主目录。只负责蓝图检索、排序和选择，并把炼制、收录交给各自窗口。
/// </summary>
public sealed class WindowBaibaoPavilion : AbstractWideWindow<WindowBaibaoPavilion>
{
    public const string Id = "Cultiway.UI.WindowBaibaoPavilion";
    public static readonly Vector2 WindowSize = new(600f, 360f);
    private const float RootHeight = 318f;
    private const float BlueprintListHeight = 288f;
    private static readonly string[] SortNamePaths =
    {
        "Cultiway.Baibao.UI.Sort.Manual",
        "Cultiway.Baibao.UI.Sort.Name",
        "Cultiway.Baibao.UI.Sort.Recent",
        "Cultiway.Baibao.UI.Sort.Quality",
    };

    private MonoObjPool<BaibaoArtifactRow> _rowPool;
    private InputField _search;
    private Button _shapeFilterButton;
    private Toggle _favoriteOnly;
    private Button _sortButton;
    private Text _selectedCount;
    private ArtifactShapeAsset[] _shapes = [];
    private int _shapeFilter;
    private int _sort;

    protected override void Init()
    {
        BackgroundTransform.Find("Scroll View").gameObject.SetActive(false);
        GameObject root = WanfaUiFactory.CreateLayout(BackgroundTransform, "BaibaoRoot", false, 520f, RootHeight,
            4f);
        root.transform.localPosition = new Vector3(0f, -8f);

        GameObject toolbar = WanfaUiFactory.CreateLayout(root.transform, "Toolbar", true, 520f, 24f, 4f);
        Button forge = WanfaUiFactory.CreateIconButton(toolbar.transform, "Forge", BaibaoUiIcons.Forge, 28f, 22f,
            WindowBaibaoForge.Open);
        WanfaUiFactory.SetTooltip(forge.gameObject, "Cultiway.Baibao.UI.Action.Forge",
            "Cultiway.Baibao.UI.Tooltip.Forge");
        Button archive = WanfaUiFactory.CreateIconButton(toolbar.transform, "Archive", BaibaoUiIcons.Archive, 28f,
            22f, BeginArchive);
        WanfaUiFactory.SetTooltip(archive.gameObject, "Cultiway.Baibao.UI.Action.Archive",
            "Cultiway.Baibao.UI.Tooltip.BeginArchive");
        Button grant = WanfaUiFactory.CreateIconButton(toolbar.transform, "Grant", BaibaoUiIcons.Grant, 28f, 22f,
            BeginGrant);
        WanfaUiFactory.SetTooltip(grant.gameObject, "Cultiway.Baibao.UI.Action.Grant",
            "Cultiway.Baibao.UI.Tooltip.BeginGrant");

        _search = WanfaUiFactory.CreateInput(toolbar.transform, "Search", string.Empty,
            "Cultiway.Baibao.UI.Placeholder.Search".Localize(), 126f, 22f);
        AddSearchIcon(_search);
        WanfaUiFactory.SetTooltip(_search, "Cultiway.Baibao.UI.Placeholder.Search",
            "Cultiway.Baibao.UI.Tooltip.Search");
        _search.onValueChanged.AddListener(_ => Refresh());

        _shapeFilterButton = WanfaUiFactory.CreateIconTextButton(toolbar.transform, "ShapeFilter",
            BaibaoUiIcons.Shape, "Cultiway.Baibao.UI.Filter.AllShapes".Localize(), 88f, 22f, CycleShapeFilter);
        WanfaUiFactory.SetTooltip(_shapeFilterButton.gameObject,
            "Cultiway.Baibao.UI.Tooltip.ShapeFilter.Title", "Cultiway.Baibao.UI.Tooltip.ShapeFilter");
        _favoriteOnly = WanfaUiFactory.CreateIconToggle(toolbar.transform, "FavoriteOnly", BaibaoUiIcons.Favorite,
            false, 28f, 22f);
        WanfaUiFactory.SetTooltip(_favoriteOnly, "Cultiway.Baibao.UI.Label.FavoriteOnly",
            "Cultiway.Baibao.UI.Tooltip.FavoriteOnly");
        _favoriteOnly.onValueChanged.AddListener(_ => Refresh());
        _sortButton = WanfaUiFactory.CreateIconTextButton(toolbar.transform, "Sort", BaibaoUiIcons.Sort,
            SortNamePaths[0].Localize(), 70f, 22f, CycleSort);
        WanfaUiFactory.SetTooltip(_sortButton.gameObject, "Cultiway.Baibao.UI.Tooltip.Sort.Title",
            "Cultiway.Baibao.UI.Tooltip.Sort");
        _selectedCount = WanfaUiFactory.CreateText(toolbar.transform, "SelectedCount", string.Empty, 76f, 22f, 6);

        Transform content = WanfaUiFactory.CreateScrollContent(root.transform, "BlueprintList", 520f,
            BlueprintListHeight);
        _rowPool = new MonoObjPool<BaibaoArtifactRow>(BaibaoArtifactRow.Prefab, content);
        BaibaoPavilionService.Instance.Changed += Refresh;
        RefreshShapes();
    }

    public override void OnNormalEnable()
    {
        RefreshShapes();
        Refresh();
    }

    private void Refresh()
    {
        if (_rowPool == null) return;
        _rowPool.Clear();
        BaibaoPavilionService service = BaibaoPavilionService.Instance;
        _selectedCount.text = string.Format("Cultiway.Baibao.UI.Format.SelectedCount".Localize(),
            service.SelectedBlueprintCount);

        IEnumerable<ArtifactBlueprint> query = service.Blueprints;
        string search = _search.text.Trim();
        if (search.Length > 0) query = query.Where(blueprint => MatchesSearch(blueprint, search));
        if (_shapeFilter > 0)
        {
            string shapeId = _shapes[_shapeFilter - 1].id;
            query = query.Where(blueprint => blueprint.ShapeId == shapeId);
        }
        if (_favoriteOnly.isOn) query = query.Where(blueprint => blueprint.Favorite);

        query = _sort switch
        {
            1 => query.OrderBy(blueprint => blueprint.Name, StringComparer.Ordinal),
            2 => query.OrderByDescending(blueprint => blueprint.UpdatedAtUtcTicks),
            3 => query.OrderByDescending(blueprint => (int)blueprint.Level),
            _ => query.OrderBy(blueprint => blueprint.SortOrder),
        };

        bool allowMove = _sort == 0 && search.Length == 0 && _shapeFilter == 0 && !_favoriteOnly.isOn;
        foreach (ArtifactBlueprint blueprint in query)
        {
            ArtifactBlueprint item = blueprint;
            _rowPool.GetNext().Setup(item, allowMove, service.IsSelected(item.Id),
                () => service.SetFavorite(item.Id, !item.Favorite),
                () => service.Move(item.Id, -1),
                () => service.Move(item.Id, 1),
                () => service.Delete(item.Id),
                () => service.ToggleSelected(item.Id));
        }
    }

    private static bool MatchesSearch(ArtifactBlueprint blueprint, string search)
    {
        if (Contains(blueprint.Name, search) || Contains(blueprint.ShapeId, search) ||
            Contains(blueprint.SourceActorName, search))
        {
            return true;
        }

        ArtifactShapeAsset shape = ModClass.L.ItemShapeLibrary.get(blueprint.ShapeId) as ArtifactShapeAsset;
        if (shape != null && shape.ingredient_name_candidates.Any(name => Contains(name, search))) return true;
        string[] atomIds = blueprint.AtomData.atom_ids ?? [];
        for (int i = 0; i < atomIds.Length; i++)
        {
            ArtifactAtomAsset atom = Cultiway.Content.Libraries.Manager.ArtifactAtomLibrary.get(atomIds[i]);
            if (Contains(atomIds[i], search) || atom != null && atom.name_stems.Any(name => Contains(name, search)))
                return true;
        }
        return false;
    }

    private static bool Contains(string value, string search)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void CycleShapeFilter()
    {
        _shapeFilter = (_shapeFilter + 1) % (_shapes.Length + 1);
        _shapeFilterButton.GetComponentInChildren<Text>().text = _shapeFilter == 0
            ? "Cultiway.Baibao.UI.Filter.AllShapes".Localize()
            : GetShapeName(_shapes[_shapeFilter - 1]);
        Refresh();
    }

    private void CycleSort()
    {
        _sort = (_sort + 1) % SortNamePaths.Length;
        _sortButton.GetComponentInChildren<Text>().text = SortNamePaths[_sort].Localize();
        Refresh();
    }

    private void RefreshShapes()
    {
        _shapes = ModClass.L.ItemShapeLibrary.list
            .OfType<ArtifactShapeAsset>()
            .OrderBy(shape => shape.id, StringComparer.Ordinal)
            .ToArray();
        if (_shapeFilter > _shapes.Length) _shapeFilter = 0;
    }

    private static string GetShapeName(ArtifactShapeAsset shape)
    {
        return shape.ingredient_name_candidates.FirstOrDefault() ?? shape.id.Localize();
    }

    internal static void ShowSaveResult(BaibaoSaveResult result, string savedKey)
    {
        string text = result.Status switch
        {
            BaibaoSaveStatus.Saved => savedKey.Localize(),
            BaibaoSaveStatus.Duplicate => "Cultiway.Baibao.UI.Tip.Duplicate".Localize(),
            _ => string.Format("Cultiway.Baibao.UI.Format.Invalid".Localize(), result.Error),
        };
        WorldTip.showNow(text, false, "top", 3f);
    }

    private void BeginArchive()
    {
        SelectWorldTool(Manager.BaibaoArchiveButton);
    }

    private void BeginGrant()
    {
        if (BaibaoPavilionService.Instance.SelectedBlueprintCount == 0)
        {
            WorldTip.showNow("Cultiway.Baibao.UI.Tip.SelectBlueprint".Localize(), false, "top", 3f);
            return;
        }
        BaibaoWorldToolSession.BeginGrantSession();
        SelectWorldTool(Manager.BaibaoGrantButton);
    }

    private void SelectWorldTool(PowerButton button)
    {
        GetComponent<ScrollWindow>().clickHide();
        PowerButtonSelector.instance.unselectAll();
        PowerButtonSelector.instance.clickPowerButton(button);
    }

    private static void AddSearchIcon(InputField input)
    {
        GameObject icon = new("SearchIcon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(input.transform, false);
        RectTransform rect = icon.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(14f, 14f);
        rect.anchoredPosition = new Vector2(10f, 0f);
        Image image = icon.GetComponent<Image>();
        image.sprite = SpriteTextureLoader.getSprite(BaibaoUiIcons.Search);
        image.preserveAspect = true;
        image.raycastTarget = false;
        input.textComponent.rectTransform.offsetMin = new Vector2(20f, 1f);
        input.placeholder.GetComponent<RectTransform>().offsetMin = new Vector2(20f, 1f);
    }
}
