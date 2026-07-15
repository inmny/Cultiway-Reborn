using System;
using Cultiway.Abstract;
using Cultiway.Core.SkillLibV3.Wanfa;
using Cultiway.UI.Components;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Blueprints;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

public sealed class WanfaBlueprintRow : APrefabPreview<WanfaBlueprintRow>
{
    private UiListRowChrome _chrome;
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
    private Button _select;
    private Image _selectMarker;

    protected override void OnInit()
    {
        _chrome = UiListRowChrome.From(gameObject);
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
        _select = transform.Find("Select").GetComponent<Button>();
        _selectMarker = transform.Find("Select/Selected").GetComponent<Image>();
    }

    public void Setup(SkillBlueprint blueprint, bool allowMove, bool selected, Action favorite, Action moveUp,
        Action moveDown, Action edit, Action copy, Action delete, Action select)
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
        var detail = string.Format("Cultiway.Wanfa.UI.Format.BlueprintDetail".Localize(), category, entityName,
            trajectoryName, blueprint.Modifiers.Count, blueprint.Revision, state);
        if (service.TryResolveItemLevel(blueprint, out var itemLevel))
        {
            detail = string.Format("Cultiway.Wanfa.UI.Format.ItemLevelPrefix".Localize(),
                SkillCastResourceFormatter.FormatItemLevel(blueprint.CastResourceRequirement, itemLevel), detail);
        }
        _detail.text = detail;
        _chrome.SetState(selected
            ? UiControlState.Selected
            : validation.IsCompatible ? UiControlState.Normal : UiControlState.Error);

        var entity = string.IsNullOrWhiteSpace(blueprint.EntityAssetId)
            ? null
            : ModClass.I.SkillV3.SkillLib.get(blueprint.EntityAssetId);
        Sprite[] frames = null;
        if (entity != null && entity.IsAnimationIndexValid(blueprint.AnimationIndex))
        {
            frames = entity.GetAnimation(blueprint.AnimationIndex).Frames;
        }
        _icon.sprite = frames is { Length: > 0 } ? frames[0] : null;
        UiTooltip.Set(_icon.gameObject, () => SkillTooltip.Show(_icon.gameObject, blueprint));
        SetFavoriteButton(blueprint.Favorite, favorite);
        SetButton(_up, moveUp, "Cultiway.Wanfa.UI.Action.MoveUp", "Cultiway.Wanfa.UI.Tooltip.MoveUp", allowMove);
        SetButton(_down, moveDown, "Cultiway.Wanfa.UI.Action.MoveDown", "Cultiway.Wanfa.UI.Tooltip.MoveDown",
            allowMove);
        SetButton(_edit, edit, "Cultiway.Wanfa.UI.Action.Edit", "Cultiway.Wanfa.UI.Tooltip.Edit");
        SetButton(_copy, copy, "Cultiway.Wanfa.UI.Action.Copy", "Cultiway.Wanfa.UI.Tooltip.Copy");
        SetButton(_delete, delete, "Cultiway.Wanfa.UI.Action.Delete", "Cultiway.Wanfa.UI.Tooltip.Delete");
        SetSelectionButton(selected, select, validation.IsCompatible);
    }

    private static void SetButton(Button button, Action action, string titleKey, string descriptionKey,
        bool interactable = true)
    {
        button.interactable = interactable;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action.Invoke);
        UiTooltip.Set(button.gameObject, titleKey, descriptionKey);
    }

    private void SetFavoriteButton(bool selected, Action action)
    {
        _favoriteIcon.color = selected
            ? ColorStyleLibrary.m.favorite_selected
            : ColorStyleLibrary.m.favorite_not_selected;
        _favorite.onClick.RemoveAllListeners();
        _favorite.onClick.AddListener(action.Invoke);
        UiTooltip.Set(_favorite.gameObject,
            selected ? "Cultiway.Wanfa.UI.Action.Unfavorite" : "Cultiway.Wanfa.UI.Action.Favorite",
            selected ? "Cultiway.Wanfa.UI.Tooltip.Unfavorite" : "Cultiway.Wanfa.UI.Tooltip.Favorite");
    }

    private void SetSelectionButton(bool selected, Action action, bool interactable)
    {
        _select.interactable = interactable;
        _select.onClick.RemoveAllListeners();
        _select.onClick.AddListener(action.Invoke);
        UiStateStyle.SetSelected(_select, selected);
        _selectMarker.gameObject.SetActive(selected);
        _select.transform.Find("Icon").GetComponent<Image>().color = Color.white;
        UiTooltip.Set(_select.gameObject,
            selected ? "Cultiway.Wanfa.UI.Action.Selected" : "Cultiway.Wanfa.UI.Action.Select",
            selected
                ? "Cultiway.Wanfa.UI.Tooltip.DeselectForGrant"
                : "Cultiway.Wanfa.UI.Tooltip.SelectForGrant");
    }

    private static void _init()
    {
        var obj = UiLayout.Create(ModClass.I.PrefabLibrary, nameof(WanfaBlueprintRow), true, 500f, 38f,
            3f);
        UiListRowChrome.Attach(obj, false);
        obj.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(6, 4, 2, 2);
        var iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconObj.transform.SetParent(obj.transform, false);
        UiLayout.SetSize(iconObj.transform, 34f, 34f);
        iconObj.GetComponent<Image>().preserveAspect = true;

        var labels = UiLayout.Create(obj.transform, "Labels", false, 230f, 34f, 0f);
        UiElements.CreateText(labels.transform, "Name", string.Empty, 230f, 17f, 8, TextAnchor.MiddleLeft,
            FontStyle.Bold);
        UiElements.CreateText(labels.transform, "Detail", string.Empty, 230f, 17f, 6);
        UiElements.CreateIconButton(obj.transform, "Favorite", UiIcons.Favorite, 28f, 24f, () => { });
        UiElements.CreateIconButton(obj.transform, "Up", UiIcons.MoveUp, 20f, 24f, () => { }, 3f, 1.45f,
            90f);
        UiElements.CreateIconButton(obj.transform, "Down", UiIcons.MoveDown, 20f, 24f, () => { }, 3f,
            1.45f, 90f);
        UiElements.CreateIconButton(obj.transform, "Edit", UiIcons.Edit, 28f, 24f, () => { }, 3f);
        UiElements.CreateIconButton(obj.transform, "Copy", UiIcons.Copy, 28f, 24f, () => { });
        UiElements.CreateIconButton(obj.transform, "Delete", UiIcons.Delete, 28f, 24f, () => { });
        var select = UiElements.CreateIconButton(obj.transform, "Select", UiIcons.Select, 28f, 24f,
            () => { });
        var selected = new GameObject("Selected", typeof(RectTransform), typeof(Image));
        selected.transform.SetParent(select.transform, false);
        var selectedRect = selected.GetComponent<RectTransform>();
        selectedRect.anchorMin = selectedRect.anchorMax = new Vector2(1f, 0f);
        selectedRect.sizeDelta = new Vector2(10f, 10f);
        selectedRect.anchoredPosition = new Vector2(-5f, 5f);
        var selectedImage = selected.GetComponent<Image>();
        selectedImage.sprite = SpriteTextureLoader.getSprite("ui/icons/IconOn");
        selectedImage.color = ColorStyleLibrary.m.getSelectorColor();
        selectedImage.raycastTarget = false;
        selected.SetActive(false);
        Prefab = obj.AddComponent<WanfaBlueprintRow>();
    }
}
