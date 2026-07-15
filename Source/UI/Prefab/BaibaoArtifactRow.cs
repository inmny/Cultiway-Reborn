using System;
using Cultiway.Abstract;
using Cultiway.Content.Artifacts.Baibao;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

public sealed class BaibaoArtifactRow : APrefabPreview<BaibaoArtifactRow>
{
    private Image _icon;
    private Text _name;
    private Text _detail;
    private Button _favorite;
    private Image _favoriteIcon;
    private Button _up;
    private Button _down;
    private Button _delete;
    private Button _select;
    private Image _selectMarker;

    protected override void OnInit()
    {
        _icon = transform.Find("Icon").GetComponent<Image>();
        _name = transform.Find("Labels/Name").GetComponent<Text>();
        _detail = transform.Find("Labels/Detail").GetComponent<Text>();
        _favorite = transform.Find("Favorite").GetComponent<Button>();
        _favoriteIcon = transform.Find("Favorite/Icon").GetComponent<Image>();
        _up = transform.Find("Up").GetComponent<Button>();
        _down = transform.Find("Down").GetComponent<Button>();
        _delete = transform.Find("Delete").GetComponent<Button>();
        _select = transform.Find("Select").GetComponent<Button>();
        _selectMarker = transform.Find("Select/Selected").GetComponent<Image>();
    }

    public void Setup(
        ArtifactBlueprint blueprint,
        bool allowMove,
        bool selected,
        Action favorite,
        Action moveUp,
        Action moveDown,
        Action delete,
        Action select)
    {
        Init();
        BaibaoPavilionService service = BaibaoPavilionService.Instance;
        string error = service.Validate(blueprint);
        string state = error == null
            ? "Cultiway.Baibao.UI.State.Valid".Localize()
            : "Cultiway.Baibao.UI.State.Damaged".Localize();
        string origin = blueprint.OriginKind == ArtifactBlueprintOriginKind.Forged
            ? "Cultiway.Baibao.UI.State.Forged".Localize()
            : string.Format("Cultiway.Baibao.UI.Format.ArchivedOrigin".Localize(), blueprint.SourceActorName);
        string detail = string.Format("Cultiway.Baibao.UI.Format.BlueprintDetail".Localize(),
            service.GetShapeName(blueprint), blueprint.Level.GetName(), blueprint.AtomData.atom_ids.Length,
            blueprint.AbilitySet.abilities.Length, origin, state);

        _name.text = blueprint.Name;
        _detail.text = detail;
        _icon.sprite = service.GetIcon(blueprint);
        _icon.preserveAspect = true;
        WanfaUiFactory.SetTooltip(_icon.gameObject, blueprint.Name, error ?? detail);

        SetFavoriteButton(blueprint.Favorite, favorite);
        SetButton(_up, moveUp, "Cultiway.Baibao.UI.Action.MoveUp",
            "Cultiway.Baibao.UI.Tooltip.MoveUp", allowMove);
        SetButton(_down, moveDown, "Cultiway.Baibao.UI.Action.MoveDown",
            "Cultiway.Baibao.UI.Tooltip.MoveDown", allowMove);
        SetButton(_delete, delete, "Cultiway.Baibao.UI.Action.Delete",
            "Cultiway.Baibao.UI.Tooltip.Delete");
        SetSelectionButton(selected, select, error == null);
    }

    private static void SetButton(Button button, Action action, string titleKey, string descriptionKey,
        bool interactable = true)
    {
        button.interactable = interactable;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action.Invoke);
        WanfaUiFactory.SetTooltip(button.gameObject, titleKey, descriptionKey);
    }

    private void SetFavoriteButton(bool selected, Action action)
    {
        _favoriteIcon.color = selected
            ? ColorStyleLibrary.m.favorite_selected
            : ColorStyleLibrary.m.favorite_not_selected;
        _favorite.onClick.RemoveAllListeners();
        _favorite.onClick.AddListener(action.Invoke);
        WanfaUiFactory.SetTooltip(_favorite.gameObject,
            selected ? "Cultiway.Baibao.UI.Action.Unfavorite" : "Cultiway.Baibao.UI.Action.Favorite",
            selected ? "Cultiway.Baibao.UI.Tooltip.Unfavorite" : "Cultiway.Baibao.UI.Tooltip.Favorite");
    }

    private void SetSelectionButton(bool selected, Action action, bool interactable)
    {
        _select.interactable = interactable;
        _select.onClick.RemoveAllListeners();
        _select.onClick.AddListener(action.Invoke);
        _selectMarker.gameObject.SetActive(selected);
        _select.transform.Find("Icon").GetComponent<Image>().color = selected
            ? ColorStyleLibrary.m.getSelectorColor()
            : Color.white;
        WanfaUiFactory.SetTooltip(_select.gameObject,
            selected ? "Cultiway.Baibao.UI.Action.Selected" : "Cultiway.Baibao.UI.Action.Select",
            selected
                ? "Cultiway.Baibao.UI.Tooltip.DeselectForGrant"
                : "Cultiway.Baibao.UI.Tooltip.SelectForGrant");
    }

    private static void _init()
    {
        GameObject obj = WanfaUiFactory.CreateLayout(ModClass.I.PrefabLibrary, nameof(BaibaoArtifactRow), true,
            500f, 38f, 3f);
        Image background = obj.AddComponent<Image>();
        background.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        background.type = Image.Type.Sliced;

        GameObject iconObject = new("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconObject.transform.SetParent(obj.transform, false);
        WanfaUiFactory.SetLayout(iconObject.transform, 34f, 34f);
        iconObject.GetComponent<Image>().preserveAspect = true;

        GameObject labels = WanfaUiFactory.CreateLayout(obj.transform, "Labels", false, 279f, 34f, 0f);
        WanfaUiFactory.CreateText(labels.transform, "Name", string.Empty, 279f, 17f, 8, TextAnchor.MiddleLeft,
            FontStyle.Bold);
        WanfaUiFactory.CreateText(labels.transform, "Detail", string.Empty, 279f, 17f, 6);
        WanfaUiFactory.CreateIconButton(obj.transform, "Favorite", BaibaoUiIcons.Favorite, 28f, 24f, () => { });
        WanfaUiFactory.CreateIconButton(obj.transform, "Up", BaibaoUiIcons.MoveUp, 20f, 24f, () => { }, 3f,
            1.45f, 90f);
        WanfaUiFactory.CreateIconButton(obj.transform, "Down", BaibaoUiIcons.MoveDown, 20f, 24f, () => { }, 3f,
            1.45f, 90f);
        WanfaUiFactory.CreateIconButton(obj.transform, "Delete", BaibaoUiIcons.Delete, 28f, 24f, () => { });
        Button select = WanfaUiFactory.CreateIconButton(obj.transform, "Select", BaibaoUiIcons.Select, 28f, 24f,
            () => { });
        GameObject selected = new("Selected", typeof(RectTransform), typeof(Image));
        selected.transform.SetParent(select.transform, false);
        RectTransform selectedRect = selected.GetComponent<RectTransform>();
        selectedRect.anchorMin = selectedRect.anchorMax = new Vector2(1f, 0f);
        selectedRect.sizeDelta = new Vector2(10f, 10f);
        selectedRect.anchoredPosition = new Vector2(-5f, 5f);
        Image selectedImage = selected.GetComponent<Image>();
        selectedImage.sprite = SpriteTextureLoader.getSprite("ui/icons/IconOn");
        selectedImage.color = ColorStyleLibrary.m.getSelectorColor();
        selectedImage.raycastTarget = false;
        selected.SetActive(false);
        Prefab = obj.AddComponent<BaibaoArtifactRow>();
    }
}
