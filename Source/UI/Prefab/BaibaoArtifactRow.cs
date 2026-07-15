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
    private UiListRowChrome _chrome;
    private Image _icon;
    private Text _name;
    private Text _detail;
    private Button _favorite;
    private Image _favoriteIcon;
    private Button _gift;

    protected override void OnInit()
    {
        _row = GetComponent<Button>();
        _chrome = UiListRowChrome.From(gameObject);
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
        UiTooltip.Set(_icon.gameObject, blueprint.Name, error ?? _detail.text);

        _row.onClick.RemoveAllListeners();
        _row.onClick.AddListener(inspect.Invoke);
        _chrome.SetState(active
            ? UiControlState.Selected
            : error == null ? UiControlState.Normal : UiControlState.Error);

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
        UiStateStyle.SetSelected(_gift, giftSelected);
    }

    private static void Configure(Button button, Action action, string title, string description)
    {
        button.interactable = action != null;
        button.onClick.RemoveAllListeners();
        if (action != null) button.onClick.AddListener(action.Invoke);
        UiTooltip.Set(button.gameObject, title, description);
    }

    private static void _init()
    {
        GameObject obj = UiLayout.Create(ModClass.I.PrefabLibrary, nameof(BaibaoArtifactRow), true,
            280f, 46f, 3f);
        UiListRowChrome.Attach(obj, true);
        obj.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(6, 4, 3, 3);

        GameObject iconObject = new("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconObject.transform.SetParent(obj.transform, false);
        UiLayout.SetSize(iconObject.transform, 40f, 40f);
        iconObject.GetComponent<Image>().preserveAspect = true;

        GameObject labels = UiLayout.Create(obj.transform, "Labels", false, 158f, 40f, 0f);
        UiElements.CreateText(labels.transform, "Name", string.Empty, 158f, 21f, 8, TextAnchor.MiddleLeft,
            FontStyle.Bold);
        UiElements.CreateText(labels.transform, "Detail", string.Empty, 158f, 19f, 6,
            TextAnchor.MiddleLeft);
        UiElements.CreateIconButton(obj.transform, "Favorite", UiIcons.Favorite, 24f, 24f, () => { },
            4f);
        UiElements.CreateIconButton(obj.transform, "Gift", UiIcons.Gift, 24f, 24f, () => { }, 4f);
        Prefab = obj.AddComponent<BaibaoArtifactRow>();
    }
}
