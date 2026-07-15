using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.Artifacts.Baibao;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.UI.Components;
using Cultiway.UI.Prefab;
using NeoModLoader.api;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>百宝阁主目录，以紧凑目录和固定详情检查器管理蓝图与赠宝套装。</summary>
public sealed class WindowBaibaoPavilion : AbstractWideWindow<WindowBaibaoPavilion>
{
    private enum PavilionView
    {
        Catalog,
        GiftSet,
    }

    public const string Id = "Cultiway.UI.WindowBaibaoPavilion";
    public static readonly Vector2 WindowSize = new(600f, 380f);
    private const float RootHeight = 338f;
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
    private Button _catalogTab;
    private Button _giftTab;
    private Button _clearGift;
    private Button _beginGift;
    private Text _resultCount;
    private Text _giftCount;
    private BaibaoBlueprintInspector _inspector;
    private BaibaoOptionMenu _optionMenu;
    private CanvasGroup _rootCanvas;
    private GameObject _deleteConfirmation;
    private Text _deleteMessage;
    private ArtifactShapeAsset[] _shapes = [];
    private string _activeBlueprintId;
    private int _shapeFilter;
    private int _sort;
    private PavilionView _view;

    protected override void Init()
    {
        Transform originalScrollView = BackgroundTransform.Find("Scroll View");
        Transform scrollbarTemplate = originalScrollView.Find("Scrollbar Vertical Mask");
        originalScrollView.gameObject.SetActive(false);

        GameObject root = WanfaUiFactory.CreateLayout(BackgroundTransform, "BaibaoRoot", false, 520f, RootHeight,
            4f);
        root.transform.localPosition = new Vector3(0f, -8f);
        _rootCanvas = root.AddComponent<CanvasGroup>();

        CreateToolbar(root.transform);
        CreateViewBar(root.transform);
        GameObject body = WanfaUiFactory.CreateLayout(root.transform, "Body", true, 520f, 284f, 4f,
            TextAnchor.UpperLeft);
        Transform content = WanfaUiFactory.CreateScrollContent(body.transform, "BlueprintList", 318f, 284f);
        WanfaUiFactory.AttachOriginalVerticalScrollbar(content, scrollbarTemplate);
        BaibaoUiFactory.AddScrollBackground(content);
        _rowPool = new MonoObjPool<BaibaoArtifactRow>(BaibaoArtifactRow.Prefab, content);
        _inspector = new BaibaoBlueprintInspector(body.transform, 198f, 284f);
        _optionMenu = new BaibaoOptionMenu(BackgroundTransform, _rootCanvas, scrollbarTemplate);
        CreateDeleteConfirmation();

        BaibaoPavilionService.Instance.Changed += Refresh;
        RefreshShapes();
    }

    public override void OnNormalEnable()
    {
        RefreshShapes();
        Refresh();
    }

    private void OnDestroy()
    {
        if (BaibaoPavilionService.Instance != null)
            BaibaoPavilionService.Instance.Changed -= Refresh;
    }

    private void CreateToolbar(Transform root)
    {
        GameObject toolbar = WanfaUiFactory.CreateLayout(root, "Toolbar", true, 520f, 24f, 4f);
        Button forge = WanfaUiFactory.CreateIconButton(toolbar.transform, "Forge", BaibaoUiIcons.Forge, 28f, 22f,
            WindowBaibaoForge.Open);
        WanfaUiFactory.SetTooltip(forge.gameObject, "Cultiway.Baibao.UI.Action.NewArtifact",
            "Cultiway.Baibao.UI.Tooltip.Forge");
        Button archive = WanfaUiFactory.CreateIconButton(toolbar.transform, "Archive", BaibaoUiIcons.Archive, 28f,
            22f, BeginArchive);
        WanfaUiFactory.SetTooltip(archive.gameObject, "Cultiway.Baibao.UI.Action.Archive",
            "Cultiway.Baibao.UI.Tooltip.BeginArchive");

        _search = WanfaUiFactory.CreateInput(toolbar.transform, "Search", string.Empty,
            "Cultiway.Baibao.UI.Placeholder.Search".Localize(), 158f, 22f);
        BaibaoUiFactory.AddSearchIcon(_search);
        WanfaUiFactory.SetTooltip(_search, "Cultiway.Baibao.UI.Placeholder.Search",
            "Cultiway.Baibao.UI.Tooltip.Search");
        _search.onValueChanged.AddListener(_ => Refresh());

        _shapeFilterButton = WanfaUiFactory.CreateIconTextButton(toolbar.transform, "ShapeFilter",
            BaibaoUiIcons.Shape, string.Empty, 104f, 22f, ShowShapeMenu);
        WanfaUiFactory.SetTooltip(_shapeFilterButton.gameObject,
            "Cultiway.Baibao.UI.Tooltip.ShapeFilter.Title", "Cultiway.Baibao.UI.Tooltip.ShapeFilter.Menu");
        _favoriteOnly = WanfaUiFactory.CreateIconToggle(toolbar.transform, "FavoriteOnly", BaibaoUiIcons.Favorite,
            false, 28f, 22f);
        WanfaUiFactory.SetTooltip(_favoriteOnly, "Cultiway.Baibao.UI.Label.FavoriteOnly",
            "Cultiway.Baibao.UI.Tooltip.FavoriteOnly");
        _favoriteOnly.onValueChanged.AddListener(_ => Refresh());
        _sortButton = WanfaUiFactory.CreateIconTextButton(toolbar.transform, "Sort", BaibaoUiIcons.Sort,
            string.Empty, 92f, 22f, ShowSortMenu);
        WanfaUiFactory.SetTooltip(_sortButton.gameObject, "Cultiway.Baibao.UI.Tooltip.Sort.Title",
            "Cultiway.Baibao.UI.Tooltip.Sort.Menu");
        _resultCount = WanfaUiFactory.CreateText(toolbar.transform, "ResultCount", string.Empty, 58f, 22f, 6,
            TextAnchor.MiddleRight);
    }

    private void CreateViewBar(Transform root)
    {
        GameObject views = WanfaUiFactory.CreateLayout(root, "Views", true, 520f, 22f, 4f);
        _catalogTab = WanfaUiFactory.CreateIconTextButton(views.transform, "Catalog", BaibaoUiIcons.Pavilion,
            "Cultiway.Baibao.UI.View.Catalog".Localize(), 112f, 21f, () => SelectView(PavilionView.Catalog));
        _giftTab = WanfaUiFactory.CreateIconTextButton(views.transform, "GiftSet", BaibaoUiIcons.Grant,
            "Cultiway.Baibao.UI.View.GiftSet".Localize(), 112f, 21f, () => SelectView(PavilionView.GiftSet));
        _giftCount = WanfaUiFactory.CreateText(views.transform, "GiftCount", string.Empty, 170f, 21f, 6,
            TextAnchor.MiddleLeft);
        _clearGift = WanfaUiFactory.CreateIconButton(views.transform, "ClearGift", BaibaoUiIcons.Reset, 34f, 21f,
            () => BaibaoPavilionService.Instance.ClearSelected());
        WanfaUiFactory.SetTooltip(_clearGift.gameObject, "Cultiway.Baibao.UI.Action.ClearGiftSet",
            "Cultiway.Baibao.UI.Tooltip.ClearGiftSet");
        _beginGift = WanfaUiFactory.CreateIconTextButton(views.transform, "BeginGift", BaibaoUiIcons.Grant,
            "Cultiway.Baibao.UI.Action.Grant".Localize(), 76f, 21f, BeginGrant);
        WanfaUiFactory.SetTooltip(_beginGift.gameObject, "Cultiway.Baibao.UI.Action.Grant",
            "Cultiway.Baibao.UI.Tooltip.BeginGrant");
    }

    private void Refresh()
    {
        if (_rowPool == null) return;
        _rowPool.Clear();
        BaibaoPavilionService service = BaibaoPavilionService.Instance;
        IEnumerable<ArtifactBlueprint> query = service.Blueprints;
        if (_view == PavilionView.GiftSet) query = query.Where(blueprint => service.IsSelected(blueprint.Id));

        string search = _search.text.Trim();
        if (search.Length > 0) query = query.Where(blueprint => MatchesSearch(blueprint, search));
        if (_shapeFilter > 0)
        {
            string shapeId = _shapes[_shapeFilter - 1].id;
            query = query.Where(blueprint => blueprint.ShapeId == shapeId);
        }
        if (_favoriteOnly.isOn) query = query.Where(blueprint => blueprint.Favorite);
        query = Sort(query);
        List<ArtifactBlueprint> visible = query.ToList();

        if (visible.All(blueprint => blueprint.Id != _activeBlueprintId))
            _activeBlueprintId = visible.FirstOrDefault()?.Id;
        bool allowMove = _view == PavilionView.Catalog && _sort == 0 && search.Length == 0 &&
                         _shapeFilter == 0 && !_favoriteOnly.isOn;
        for (int i = 0; i < visible.Count; i++)
        {
            ArtifactBlueprint blueprint = visible[i];
            _rowPool.GetNext().Setup(blueprint, blueprint.Id == _activeBlueprintId,
                service.IsSelected(blueprint.Id),
                () => SelectBlueprint(blueprint.Id),
                () => service.SetFavorite(blueprint.Id, !blueprint.Favorite),
                () => service.ToggleSelected(blueprint.Id));
        }

        _resultCount.text = string.Format("Cultiway.Baibao.UI.Format.ResultCount".Localize(), visible.Count);
        _giftCount.text = string.Format("Cultiway.Baibao.UI.Format.GiftSetCount".Localize(),
            service.SelectedBlueprintCount);
        _clearGift.interactable = service.SelectedBlueprintCount > 0;
        _beginGift.interactable = service.SelectedBlueprintCount > 0;
        BaibaoUiFactory.SetSelected(_catalogTab, _view == PavilionView.Catalog);
        BaibaoUiFactory.SetSelected(_giftTab, _view == PavilionView.GiftSet);
        BaibaoUiFactory.SetButtonLabel(_shapeFilterButton, _shapeFilter == 0
            ? "Cultiway.Baibao.UI.Filter.AllShapes".Localize()
            : BaibaoPresentation.GetShapeName(_shapes[_shapeFilter - 1]));
        BaibaoUiFactory.SetButtonLabel(_sortButton, SortNamePaths[_sort].Localize());
        RefreshInspector(allowMove);
    }

    private IEnumerable<ArtifactBlueprint> Sort(IEnumerable<ArtifactBlueprint> query)
    {
        return _sort switch
        {
            1 => query.OrderBy(blueprint => blueprint.Name, StringComparer.Ordinal),
            2 => query.OrderByDescending(blueprint => blueprint.UpdatedAtUtcTicks),
            3 => query.OrderByDescending(blueprint => (int)blueprint.Level),
            _ => query.OrderBy(blueprint => blueprint.SortOrder),
        };
    }

    private void RefreshInspector(bool allowMove)
    {
        BaibaoPavilionService service = BaibaoPavilionService.Instance;
        ArtifactBlueprint blueprint = service.Get(_activeBlueprintId);
        if (blueprint == null)
        {
            _inspector.Clear();
            return;
        }

        _inspector.Show(blueprint, new BaibaoBlueprintInspectorActions
        {
            Edit = () => WindowBaibaoForge.Open(blueprint),
            Copy = () => WindowBaibaoForge.OpenCopy(blueprint),
            Favorite = () => service.SetFavorite(blueprint.Id, !blueprint.Favorite),
            Gift = service.Validate(blueprint) == null ? () => service.ToggleSelected(blueprint.Id) : null,
            MoveUp = () => service.Move(blueprint.Id, -1),
            MoveDown = () => service.Move(blueprint.Id, 1),
            Delete = () => ShowDeleteConfirmation(blueprint),
            FavoriteSelected = blueprint.Favorite,
            GiftSelected = service.IsSelected(blueprint.Id),
            CanMove = allowMove,
        });
    }

    private void SelectBlueprint(string id)
    {
        _activeBlueprintId = id;
        Refresh();
    }

    private void SelectView(PavilionView view)
    {
        _view = view;
        Refresh();
    }

    private void ShowShapeMenu()
    {
        List<BaibaoMenuOption> options = new()
        {
            new BaibaoMenuOption
            {
                Label = "Cultiway.Baibao.UI.Filter.AllShapes".Localize(),
                IconPath = BaibaoUiIcons.Shape,
                Selected = _shapeFilter == 0,
                Select = () => SetShapeFilter(0),
            },
        };
        for (int i = 0; i < _shapes.Length; i++)
        {
            int index = i + 1;
            string label = BaibaoPresentation.GetShapeName(_shapes[i]);
            options.Add(new BaibaoMenuOption
            {
                Label = label,
                IconPath = BaibaoUiIcons.Shape,
                SearchText = $"{label} {_shapes[i].id} {_shapes[i].appearance_family}",
                Selected = _shapeFilter == index,
                Select = () => SetShapeFilter(index),
            });
        }
        _optionMenu.Show("Cultiway.Baibao.UI.Tooltip.ShapeFilter.Title".Localize(), options, true);
    }

    private void ShowSortMenu()
    {
        List<BaibaoMenuOption> options = new();
        for (int i = 0; i < SortNamePaths.Length; i++)
        {
            int index = i;
            options.Add(new BaibaoMenuOption
            {
                Label = SortNamePaths[i].Localize(),
                IconPath = BaibaoUiIcons.Sort,
                Selected = _sort == i,
                Select = () => SetSort(index),
            });
        }
        _optionMenu.Show("Cultiway.Baibao.UI.Tooltip.Sort.Title".Localize(), options);
    }

    private void SetShapeFilter(int index)
    {
        _shapeFilter = index;
        Refresh();
    }

    private void SetSort(int index)
    {
        _sort = index;
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

    private static bool MatchesSearch(ArtifactBlueprint blueprint, string search)
    {
        if (Contains(blueprint.Name, search) || Contains(blueprint.ShapeId, search) ||
            Contains(blueprint.SourceActorName, search) || Contains(BaibaoPresentation.GetShapeName(blueprint), search))
            return true;

        ArtifactAtomEntry[] atoms = blueprint.AtomData.entries ?? [];
        for (int i = 0; i < atoms.Length; i++)
        {
            ArtifactAtomAsset atom = Cultiway.Content.Libraries.Manager.ArtifactAtomLibrary.get(atoms[i].atom_id);
            if (Contains(atoms[i].atom_id, search) || atom != null &&
                (Contains(BaibaoPresentation.GetAtomName(atom), search) || atom.name_stems.Any(name => Contains(name, search))))
                return true;
        }
        return (blueprint.AbilitySet.abilities ?? []).Any(ability =>
            Contains(BaibaoPresentation.GetAbilityName(ability.ability_id), search));
    }

    private static bool Contains(string value, string search)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void CreateDeleteConfirmation()
    {
        _deleteConfirmation = WanfaUiFactory.CreateLayout(BackgroundTransform, "DeleteConfirmation", false,
            274f, 94f, 6f, TextAnchor.MiddleCenter);
        _deleteConfirmation.transform.localPosition = Vector3.zero;
        Image background = _deleteConfirmation.AddComponent<Image>();
        background.sprite = SpriteTextureLoader.getSprite("ui/special/windowEmptyFrame");
        background.type = Image.Type.Sliced;
        _deleteMessage = WanfaUiFactory.CreateText(_deleteConfirmation.transform, "Message", string.Empty, 264f,
            34f, 8, TextAnchor.MiddleCenter, FontStyle.Bold);
        GameObject actions = WanfaUiFactory.CreateLayout(_deleteConfirmation.transform, "Actions", true, 264f,
            25f, 6f, TextAnchor.MiddleCenter);
        Button confirm = WanfaUiFactory.CreateIconTextButton(actions.transform, "Confirm", BaibaoUiIcons.Delete,
            "Cultiway.Baibao.UI.Action.ConfirmDelete".Localize(), 104f, 23f, ConfirmDelete);
        WanfaUiFactory.SetTooltip(confirm.gameObject, "Cultiway.Baibao.UI.Action.ConfirmDelete",
            "Cultiway.Baibao.UI.Tooltip.ConfirmDelete");
        Button cancel = WanfaUiFactory.CreateIconTextButton(actions.transform, "Cancel", BaibaoUiIcons.Cancel,
            "Cultiway.Baibao.UI.Action.Cancel".Localize(), 90f, 23f, HideDeleteConfirmation);
        WanfaUiFactory.SetTooltip(cancel.gameObject, "Cultiway.Baibao.UI.Action.Cancel",
            "Cultiway.Baibao.UI.Tooltip.CancelDelete");
        _deleteConfirmation.SetActive(false);
    }

    private void ShowDeleteConfirmation(ArtifactBlueprint blueprint)
    {
        _activeBlueprintId = blueprint.Id;
        _deleteMessage.text = string.Format("Cultiway.Baibao.UI.Format.ConfirmDelete".Localize(), blueprint.Name);
        _deleteConfirmation.transform.SetAsLastSibling();
        _deleteConfirmation.SetActive(true);
        _rootCanvas.interactable = false;
    }

    private void ConfirmDelete()
    {
        string id = _activeBlueprintId;
        HideDeleteConfirmation();
        BaibaoPavilionService.Instance.Delete(id);
    }

    private void HideDeleteConfirmation()
    {
        _deleteConfirmation.SetActive(false);
        _rootCanvas.interactable = true;
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
}
