using System;
using Cultiway.Abstract;
using Cultiway.Content.WanfaPavilion;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Blueprints;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.Prefab;

public sealed class WanfaBlueprintRow : APrefabPreview<WanfaBlueprintRow>
{
    private Image _icon;
    private Text _name;
    private Text _detail;
    private Button _favorite;
    private Image _favoriteIcon;
    private Button _up;
    private Button _down;
    private Button _edit;
    private Button _copy;
    private Button _delete;
    private Button _grant;

    protected override void OnInit()
    {
        _icon = transform.Find("Icon").GetComponent<Image>();
        _name = transform.Find("Labels/Name").GetComponent<Text>();
        _detail = transform.Find("Labels/Detail").GetComponent<Text>();
        _favorite = transform.Find("Favorite").GetComponent<Button>();
        _favoriteIcon = transform.Find("Favorite/Icon").GetComponent<Image>();
        _up = transform.Find("Up").GetComponent<Button>();
        _down = transform.Find("Down").GetComponent<Button>();
        _edit = transform.Find("Edit").GetComponent<Button>();
        _copy = transform.Find("Copy").GetComponent<Button>();
        _delete = transform.Find("Delete").GetComponent<Button>();
        _grant = transform.Find("Grant").GetComponent<Button>();
    }

    public void Setup(SkillBlueprint blueprint, bool allowMove, Action favorite, Action moveUp, Action moveDown,
        Action edit, Action copy, Action delete, Action grant)
    {
        Init();
        var service = WanfaPavilionService.Instance;
        _name.text = service.GetDisplayName(blueprint);
        var validation = service.Validate(blueprint);
        var state = validation.IsCompatible
            ? "Cultiway.Wanfa.UI.State.Valid".Localize()
            : "Cultiway.Wanfa.UI.State.Damaged".Localize();
        var entityName = string.IsNullOrWhiteSpace(blueprint.EntityAssetId)
            ? "Cultiway.Wanfa.UI.State.NoEntity".Localize()
            : blueprint.EntityAssetId.Localize();
        var trajectoryName = string.IsNullOrWhiteSpace(blueprint.TrajectoryAssetId)
            ? "Cultiway.Wanfa.UI.State.NoTrajectory".Localize()
            : blueprint.TrajectoryAssetId.Localize();
        var category = string.IsNullOrWhiteSpace(blueprint.Category)
            ? string.Empty
            : string.Format("Cultiway.Wanfa.UI.Format.CategoryPrefix".Localize(), blueprint.Category) + " ";
        _detail.text = string.Format("Cultiway.Wanfa.UI.Format.BlueprintDetail".Localize(), category, entityName,
            trajectoryName, blueprint.Modifiers.Count, blueprint.Revision, state);

        var entity = string.IsNullOrWhiteSpace(blueprint.EntityAssetId)
            ? null
            : ModClass.I.SkillV3.SkillLib.get(blueprint.EntityAssetId);
        Sprite[] frames = null;
        if (entity != null) frames = entity.PrefabEntity.GetComponent<AnimData>().frames;
        _icon.sprite = frames is { Length: > 0 } ? frames[0] : null;
        SetFavoriteButton(blueprint.Favorite, favorite);
        SetButton(_up, "↑", moveUp, allowMove);
        SetButton(_down, "↓", moveDown, allowMove);
        SetButton(_edit, "Cultiway.Wanfa.UI.Action.Edit".Localize(), edit);
        SetButton(_copy, "Cultiway.Wanfa.UI.Action.Copy".Localize(), copy);
        SetButton(_delete, "Cultiway.Wanfa.UI.Action.Delete".Localize(), delete);
        SetButton(_grant, "Cultiway.Wanfa.UI.Action.Grant".Localize(), grant, validation.IsCompatible);
    }

    private static void SetButton(Button button, string label, Action action, bool interactable = true)
    {
        button.GetComponentInChildren<Text>().text = label;
        button.interactable = interactable;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action.Invoke);
    }

    private void SetFavoriteButton(bool selected, Action action)
    {
        _favoriteIcon.color = selected
            ? ColorStyleLibrary.m.favorite_selected
            : ColorStyleLibrary.m.favorite_not_selected;
        _favorite.onClick.RemoveAllListeners();
        _favorite.onClick.AddListener(action.Invoke);
    }

    private static void _init()
    {
        var obj = WanfaUiFactory.CreateLayout(ModClass.I.PrefabLibrary, nameof(WanfaBlueprintRow), true, 500f, 38f,
            3f);
        var image = obj.AddComponent<Image>();
        image.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        image.type = Image.Type.Sliced;
        var iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconObj.transform.SetParent(obj.transform, false);
        WanfaUiFactory.SetLayout(iconObj.transform, 34f, 34f);
        iconObj.GetComponent<Image>().preserveAspect = true;

        var labels = WanfaUiFactory.CreateLayout(obj.transform, "Labels", false, 190f, 34f, 0f);
        WanfaUiFactory.CreateText(labels.transform, "Name", string.Empty, 190f, 17f, 8, TextAnchor.MiddleLeft,
            FontStyle.Bold);
        WanfaUiFactory.CreateText(labels.transform, "Detail", string.Empty, 190f, 17f, 6);
        var favorite = WanfaUiFactory.CreateButton(obj.transform, "Favorite", string.Empty, 34f, 24f, () => { });
        var favoriteIcon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        favoriteIcon.transform.SetParent(favorite.transform, false);
        WanfaUiFactory.Stretch(favoriteIcon.GetComponent<RectTransform>(), 8f, 8f, 3f, 3f);
        favoriteIcon.GetComponent<Image>().sprite = SpriteTextureLoader.getSprite("ui/Icons/iconFavoriteStar");
        favoriteIcon.GetComponent<Image>().raycastTarget = false;
        WanfaUiFactory.CreateButton(obj.transform, "Up", "↑", 20f, 24f, () => { });
        WanfaUiFactory.CreateButton(obj.transform, "Down", "↓", 20f, 24f, () => { });
        WanfaUiFactory.CreateButton(obj.transform, "Edit", "Cultiway.Wanfa.UI.Action.Edit".Localize(), 42f,
            24f, () => { });
        WanfaUiFactory.CreateButton(obj.transform, "Copy", "Cultiway.Wanfa.UI.Action.Copy".Localize(), 42f,
            24f, () => { });
        WanfaUiFactory.CreateButton(obj.transform, "Delete", "Cultiway.Wanfa.UI.Action.Delete".Localize(), 42f,
            24f, () => { });
        WanfaUiFactory.CreateButton(obj.transform, "Grant", "Cultiway.Wanfa.UI.Action.Grant".Localize(), 42f,
            24f, () => { });
        Prefab = obj.AddComponent<WanfaBlueprintRow>();
    }
}
