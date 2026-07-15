using System;
using Cultiway.Abstract;
using Cultiway.Content.Artifacts.Baibao;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

/// <summary>百宝阁主目录中的紧凑蓝图条目。</summary>
public sealed class BaibaoArtifactRow : APrefabPreview<BaibaoArtifactRow>
{
    private Button _row;
    private Image _background;
    private Image _icon;
    private Text _name;
    private Text _detail;
    private Button _favorite;
    private Image _favoriteIcon;
    private Button _gift;

    protected override void OnInit()
    {
        _row = GetComponent<Button>();
        _background = GetComponent<Image>();
        _icon = transform.Find("Icon").GetComponent<Image>();
        _name = transform.Find("Labels/Name").GetComponent<Text>();
        _detail = transform.Find("Labels/Detail").GetComponent<Text>();
        _favorite = transform.Find("Favorite").GetComponent<Button>();
        _favoriteIcon = transform.Find("Favorite/Icon").GetComponent<Image>();
        _gift = transform.Find("Gift").GetComponent<Button>();
    }

    public void Setup(
        ArtifactBlueprint blueprint,
        bool active,
        bool giftSelected,
        Action inspect,
        Action favorite,
        Action gift)
    {
        Init();
        BaibaoPavilionService service = BaibaoPavilionService.Instance;
        string error = service.Validate(blueprint);
        _name.text = blueprint.Name;
        _detail.text = string.Format("Cultiway.Baibao.UI.Format.DirectoryRow".Localize(),
            BaibaoPresentation.GetShapeName(blueprint), blueprint.Level.GetName(),
            blueprint.AbilitySet.abilities?.Length ?? 0);
        _icon.sprite = service.GetIcon(blueprint);
        _icon.preserveAspect = true;
        WanfaUiFactory.SetTooltip(_icon.gameObject, blueprint.Name, error ?? _detail.text);

        _row.onClick.RemoveAllListeners();
        _row.onClick.AddListener(inspect.Invoke);
        _background.color = active
            ? BaibaoUiFactory.SelectionColor
            : error == null ? Color.white : new Color(1f, 0.62f, 0.58f, 1f);

        _favoriteIcon.color = blueprint.Favorite
            ? ColorStyleLibrary.m.favorite_selected
            : ColorStyleLibrary.m.favorite_not_selected;
        Configure(_favorite, favorite,
            blueprint.Favorite ? "Cultiway.Baibao.UI.Action.Unfavorite" : "Cultiway.Baibao.UI.Action.Favorite",
            blueprint.Favorite ? "Cultiway.Baibao.UI.Tooltip.Unfavorite" :
                "Cultiway.Baibao.UI.Tooltip.Favorite");
        Configure(_gift, error == null ? gift : null,
            giftSelected ? "Cultiway.Baibao.UI.Action.RemoveFromGift" : "Cultiway.Baibao.UI.Action.AddToGift",
            giftSelected ? "Cultiway.Baibao.UI.Tooltip.DeselectForGrant" :
                "Cultiway.Baibao.UI.Tooltip.SelectForGrant");
        BaibaoUiFactory.SetSelected(_gift, giftSelected);
    }

    private static void Configure(Button button, Action action, string title, string description)
    {
        button.interactable = action != null;
        button.onClick.RemoveAllListeners();
        if (action != null) button.onClick.AddListener(action.Invoke);
        WanfaUiFactory.SetTooltip(button.gameObject, title, description);
    }

    private static void _init()
    {
        GameObject obj = WanfaUiFactory.CreateLayout(ModClass.I.PrefabLibrary, nameof(BaibaoArtifactRow), true,
            280f, 46f, 3f);
        Image background = obj.AddComponent<Image>();
        background.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        background.type = Image.Type.Sliced;
        Button row = obj.AddComponent<Button>();
        row.targetGraphic = background;
        obj.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(6, 4, 3, 3);

        GameObject iconObject = new("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconObject.transform.SetParent(obj.transform, false);
        WanfaUiFactory.SetLayout(iconObject.transform, 40f, 40f);
        iconObject.GetComponent<Image>().preserveAspect = true;

        GameObject labels = WanfaUiFactory.CreateLayout(obj.transform, "Labels", false, 158f, 40f, 0f);
        WanfaUiFactory.CreateText(labels.transform, "Name", string.Empty, 158f, 21f, 8, TextAnchor.MiddleLeft,
            FontStyle.Bold);
        WanfaUiFactory.CreateText(labels.transform, "Detail", string.Empty, 158f, 19f, 6,
            TextAnchor.MiddleLeft);
        WanfaUiFactory.CreateIconButton(obj.transform, "Favorite", BaibaoUiIcons.Favorite, 24f, 24f, () => { },
            4f);
        WanfaUiFactory.CreateIconButton(obj.transform, "Gift", BaibaoUiIcons.Grant, 24f, 24f, () => { }, 4f);
        Prefab = obj.AddComponent<BaibaoArtifactRow>();
    }
}
