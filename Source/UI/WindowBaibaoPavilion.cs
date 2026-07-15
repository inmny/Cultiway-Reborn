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
    private Button _clearGift;
    private Button _beginGift;
    private Button _targetFilterButton;
    private Text _resultCount;
    private Text _giftCount;
    private BaibaoBlueprintInspector _inspector;
    private UiOptionMenu _optionMenu;
    private readonly UiSegmentedTabs _viewTabs = new();
    private CanvasGroup _rootCanvas;
    private GameObject _deleteConfirmation;
    private UiModal _deleteModal;
    private Text _deleteMessage;
    private ArtifactShapeAsset[] _shapes = [];
    private string _activeBlueprintId;
    private int _shapeFilter;
    private int _sort;
    private PavilionView _view;

    protected override void Init()
    {
        UiWindowContext context = UiWindowContext.Bind(BackgroundTransform);

        GameObject root = UiLayout.Create(BackgroundTransform, "BaibaoRoot", false, 520f, RootHeight,
            4f);
        root.transform.localPosition = new Vector3(0f, -8f);
        _rootCanvas = root.AddComponent<CanvasGroup>();

        CreateToolbar(root.transform);
        CreateViewBar(root.transform);
        GameObject body = UiLayout.Create(root.transform, "Body", true, 520f, 284f, 4f,
            TextAnchor.UpperLeft);
        UiScrollPane catalog = UiScrollPane.CreateVertical(body.transform, "BlueprintList", 318f, 284f);
        catalog.AttachOriginalScrollbar(context.ScrollbarTemplate);
        catalog.SetSurface(UiSurface.WindowEmpty, UiTheme.Current.Metrics.SpacingMd);
        _rowPool = new MonoObjPool<BaibaoArtifactRow>(BaibaoArtifactRow.Prefab, catalog.Content);
        _inspector = new BaibaoBlueprintInspector(body.transform, 198f, 284f);
        _optionMenu = new UiOptionMenu(BackgroundTransform, _rootCanvas, context.ScrollbarTemplate,
            new UiOptionMenuConfig
            {
                SearchPlaceholder = "Cultiway.UI.OptionMenu.Placeholder.Search".Localize(),
                EmptyText = "Cultiway.UI.OptionMenu.State.Empty".Localize(),
                CloseText = "Cultiway.UI.OptionMenu.Action.Close".Localize(),
                CloseTooltipTitle = "Cultiway.UI.OptionMenu.Action.Close",
                CloseTooltipDescription = "Cultiway.UI.OptionMenu.Tooltip.Close",
            });
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
        BaibaoPavilionService.Instance.Changed -= Refresh;
    }

    private void CreateToolbar(Transform root)
    {
        GameObject toolbar = UiLayout.Create(root, "Toolbar", true, 520f, 24f, 4f);
        Button forge = UiElements.CreateIconButton(toolbar.transform, "Forge", BaibaoUiIcons.Forge, 28f, 22f,
            WindowBaibaoForge.Open);
        UiTooltip.Set(forge.gameObject, "Cultiway.Baibao.UI.Action.NewArtifact",
            "Cultiway.Baibao.UI.Tooltip.Forge");
        Button archive = UiElements.CreateIconButton(toolbar.transform, "Archive", BaibaoUiIcons.Archive, 28f,
            22f, BeginArchive);
        UiTooltip.Set(archive.gameObject, "Cultiway.Baibao.UI.Action.Archive",
            "Cultiway.Baibao.UI.Tooltip.BeginArchive");

        _search = UiSearchField.Create(toolbar.transform, "Search", string.Empty,
            "Cultiway.Baibao.UI.Placeholder.Search".Localize(), 158f, 22f).Input;
        UiTooltip.Set(_search, "Cultiway.Baibao.UI.Placeholder.Search",
            "Cultiway.Baibao.UI.Tooltip.Search");
        _search.onValueChanged.AddListener(_ => Refresh());

        _shapeFilterButton = UiElements.CreateIconTextButton(toolbar.transform, "ShapeFilter",
            BaibaoUiIcons.Shape, string.Empty, 104f, 22f, ShowShapeMenu);
        UiTooltip.Set(_shapeFilterButton.gameObject,
            "Cultiway.Baibao.UI.Tooltip.ShapeFilter.Title", "Cultiway.Baibao.UI.Tooltip.ShapeFilter.Menu");
        _favoriteOnly = UiElements.CreateIconToggle(toolbar.transform, "FavoriteOnly", UiIcons.Favorite,
            false, 28f, 22f, UiIconToggleStyle.Favorite);
        UiTooltip.Set(_favoriteOnly, "Cultiway.Baibao.UI.Label.FavoriteOnly",
            "Cultiway.Baibao.UI.Tooltip.FavoriteOnly");
        _favoriteOnly.onValueChanged.AddListener(_ => Refresh());
        _sortButton = UiElements.CreateIconTextButton(toolbar.transform, "Sort", UiIcons.Sort,
            string.Empty, 92f, 22f, ShowSortMenu);
        UiTooltip.Set(_sortButton.gameObject, "Cultiway.Baibao.UI.Tooltip.Sort.Title",
            "Cultiway.Baibao.UI.Tooltip.Sort.Menu");
        _resultCount = UiElements.CreateText(toolbar.transform, "ResultCount", string.Empty, 58f, 22f, 6,
            TextAnchor.MiddleRight);
    }

    private void CreateViewBar(Transform root)
    {
        GameObject views = UiLayout.Create(root, "Views", true, 520f, 22f, 4f);
        Button catalogTab = UiElements.CreateIconTextButton(views.transform, "Catalog", BaibaoUiIcons.Pavilion,
            "Cultiway.Baibao.UI.View.Catalog".Localize(), 112f, 21f, () => SelectView(PavilionView.Catalog));
        Button giftTab = UiElements.CreateIconTextButton(views.transform, "GiftSet", UiIcons.Gift,
            "Cultiway.Baibao.UI.View.GiftSet".Localize(), 112f, 21f, () => SelectView(PavilionView.GiftSet));
        _viewTabs.Add(catalogTab);
        _viewTabs.Add(giftTab);
        _giftCount = UiElements.CreateText(views.transform, "GiftCount", string.Empty, 138f, 21f, 6,
            TextAnchor.MiddleLeft);
        _targetFilterButton = UiElements.CreateIconButton(views.transform, "TargetFilter", UiIcons.TargetFilter,
            28f, 21f, ShowTargetFilter, 4f);
        UiTooltip.Set(_targetFilterButton.gameObject, "Cultiway.Baibao.UI.Action.TargetFilter",
            "Cultiway.Baibao.UI.Tooltip.TargetFilter");
        _clearGift = UiElements.CreateIconButton(views.transform, "ClearGift", UiIcons.Reset, 34f, 21f,
            () => BaibaoPavilionService.Instance.ClearSelected());
        UiTooltip.Set(_clearGift.gameObject, "Cultiway.Baibao.UI.Action.ClearGiftSet",
            "Cultiway.Baibao.UI.Tooltip.ClearGiftSet");
        _beginGift = UiElements.CreateIconTextButton(views.transform, "BeginGift", UiIcons.Gift,
            "Cultiway.Baibao.UI.Action.Grant".Localize(), 76f, 21f, BeginGrant);
        UiTooltip.Set(_beginGift.gameObject, "Cultiway.Baibao.UI.Action.Grant",
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
        _viewTabs.SetSelected((int)_view);
        var targetFilter = service.GrantTargetFilter;
        UiStateStyle.ApplyVisual(_targetFilterButton, !targetFilter.ExpressionState.IsComplete
            ? UiControlState.Error
            : targetFilter.Expression.Count > 0 ? UiControlState.Selected : UiControlState.Normal);
        UiElements.SetButtonLabel(_shapeFilterButton, _shapeFilter == 0
            ? "Cultiway.Baibao.UI.Filter.AllShapes".Localize()
            : BaibaoPresentation.GetShapeName(_shapes[_shapeFilter - 1]));
        UiElements.SetButtonLabel(_sortButton, SortNamePaths[_sort].Localize());
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
        List<UiOptionMenuOption> options = new()
        {
            new UiOptionMenuOption
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
            options.Add(new UiOptionMenuOption
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
        List<UiOptionMenuOption> options = new();
        for (int i = 0; i < SortNamePaths.Length; i++)
        {
            int index = i;
            options.Add(new UiOptionMenuOption
            {
                Label = SortNamePaths[i].Localize(),
                IconPath = UiIcons.Sort,
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

    private static void ShowTargetFilter()
    {
        WindowActorTargetFilter.Open(BaibaoPavilionService.Instance.GrantTargetFilter,
            "Cultiway.ActorTargetFilter.UI.Context.Baibao",
            "Cultiway.ActorTargetFilter.UI.Expression.Empty",
            "Cultiway.ActorTargetFilter.UI.Filter.Semantics");
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
        _deleteConfirmation = UiLayout.Create(BackgroundTransform, "DeleteConfirmation", false,
            274f, 94f, 6f, TextAnchor.MiddleCenter);
        _deleteConfirmation.transform.localPosition = Vector3.zero;
        Image background = _deleteConfirmation.AddComponent<Image>();
        background.sprite = UiResources.GetSprite(UiResources.WindowEmpty);
        background.type = Image.Type.Sliced;
        _deleteMessage = UiElements.CreateText(_deleteConfirmation.transform, "Message", string.Empty, 264f,
            34f, 8, TextAnchor.MiddleCenter, FontStyle.Bold);
        GameObject actions = UiLayout.Create(_deleteConfirmation.transform, "Actions", true, 264f,
            25f, 6f, TextAnchor.MiddleCenter);
        Button confirm = UiElements.CreateIconTextButton(actions.transform, "Confirm", UiIcons.Delete,
            "Cultiway.Baibao.UI.Action.ConfirmDelete".Localize(), 104f, 23f, ConfirmDelete);
        UiTooltip.Set(confirm.gameObject, "Cultiway.Baibao.UI.Action.ConfirmDelete",
            "Cultiway.Baibao.UI.Tooltip.ConfirmDelete");
        Button cancel = UiElements.CreateIconTextButton(actions.transform, "Cancel", UiIcons.Cancel,
            "Cultiway.Baibao.UI.Action.Cancel".Localize(), 90f, 23f, HideDeleteConfirmation);
        UiTooltip.Set(cancel.gameObject, "Cultiway.Baibao.UI.Action.Cancel",
            "Cultiway.Baibao.UI.Tooltip.CancelDelete");
        _deleteModal = new UiModal(_deleteConfirmation, _rootCanvas);
    }

    private void ShowDeleteConfirmation(ArtifactBlueprint blueprint)
    {
        _activeBlueprintId = blueprint.Id;
        _deleteMessage.text = string.Format("Cultiway.Baibao.UI.Format.ConfirmDelete".Localize(), blueprint.Name);
        _deleteModal.Show();
    }

    private void ConfirmDelete()
    {
        string id = _activeBlueprintId;
        HideDeleteConfirmation();
        BaibaoPavilionService.Instance.Delete(id);
    }

    private void HideDeleteConfirmation()
    {
        _deleteModal.Hide();
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
        SelectWorldTool(Content.UI.Manager.Instance.BaibaoArchiveButton);
    }

    private void BeginGrant()
    {
        if (BaibaoPavilionService.Instance.SelectedBlueprintCount == 0)
        {
            WorldTip.showNow("Cultiway.Baibao.UI.Tip.SelectBlueprint".Localize(), false, "top", 3f);
            return;
        }
        if (!BaibaoWorldToolSession.BeginGrantSession()) return;
        SelectWorldTool(Content.UI.Manager.Instance.BaibaoGrantButton);
    }

    private void SelectWorldTool(PowerButton button)
    {
        GetComponent<ScrollWindow>().clickHide();
        PowerButtonSelector.instance.unselectAll();
        PowerButtonSelector.instance.clickPowerButton(button);
    }
}
