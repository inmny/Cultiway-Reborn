using System.Collections.Generic;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Blueprints;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.Prefab;

internal sealed class WanfaModifierTooltipModel
{
    public string Title;
    public string Description;
    public string Detail;
    public string IconPath;
    public string OutlineColor;

    public static WanfaModifierTooltipModel FromSpec(SkillModifierSpec spec)
    {
        if (spec == null)
        {
            return Missing("Cultiway.Wanfa.UI.Overview.Modifier".Localize());
        }

        var modifier = ModClass.I.SkillV3.ModifierLib.get(spec.AssetId);
        if (modifier == null) return Missing(spec.AssetId);

        var values = new List<string>();
        foreach (var field in modifier.EditorFields)
        {
            var value = spec.Parameters != null &&
                        spec.Parameters.TryGetValue(field.ParameterKey, out var storedValue)
                ? field.ToDisplayValue(storedValue)
                : "-";
            values.Add($"{field.DisplayName}: {value}{field.Unit}");
        }
        if (values.Count == 0)
        {
            values.Add("Cultiway.Wanfa.UI.Tooltip.Modifier.NoParameters".Localize());
        }

        return new WanfaModifierTooltipModel
        {
            Title = modifier.id.Localize(),
            Description = $"{modifier.EditorCategoryKey.Localize()} · " +
                          $"Cultiway.SkillModifier.Rarity.{modifier.Rarity}".Localize(),
            Detail = string.Join("\n", values),
            IconPath = modifier.EditorIconPath,
            OutlineColor = GetRarityColor(modifier.Rarity)
        };
    }

    private static WanfaModifierTooltipModel Missing(string title)
    {
        return new WanfaModifierTooltipModel
        {
            Title = title,
            Description = "Cultiway.Wanfa.UI.State.Damaged".Localize(),
            Detail = "Cultiway.Wanfa.UI.Detail.MissingModifier".Localize(),
            IconPath = WanfaUiIcons.Cancel,
            OutlineColor = "#FB2C21"
        };
    }

    private static string GetRarityColor(SkillModifierRarity rarity)
    {
        return rarity switch
        {
            SkillModifierRarity.Rare => "#4CCFFF",
            SkillModifierRarity.Epic => "#D28CFF",
            SkillModifierRarity.Legendary => "#FFB347",
            _ => "#FFFFFF"
        };
    }
}

internal sealed class WanfaModifierIcon : MonoBehaviour
{
    private Image _icon;
    private IconOutline _outline;
    private TipButton _tipButton;
    private WanfaModifierTooltipModel _model;

    public static WanfaModifierIcon Create(Transform parent, string name, float size)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement),
            typeof(TipButton), typeof(WanfaModifierIcon));
        item.transform.SetParent(parent, false);
        var itemBackground = item.GetComponent<Image>();
        itemBackground.color = Color.clear;
        itemBackground.raycastTarget = true;
        var button = item.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = null;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var itemLayout = item.GetComponent<LayoutElement>();
        itemLayout.minWidth = itemLayout.preferredWidth = size;
        itemLayout.minHeight = itemLayout.preferredHeight = size;
        item.GetComponent<RectTransform>().sizeDelta = new Vector2(size, size);

        var outline = new GameObject("Outline", typeof(RectTransform), typeof(Image), typeof(IconOutline));
        outline.transform.SetParent(item.transform, false);
        WanfaUiFactory.Stretch(outline.GetComponent<RectTransform>(), 1f, 1f, 1f, 1f);
        outline.GetComponent<Image>().raycastTarget = false;

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(item.transform, false);
        WanfaUiFactory.Stretch(icon.GetComponent<RectTransform>(), 1f, 1f, 1f, 1f);
        var iconImage = icon.GetComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        outline.GetComponent<IconOutline>().parent_image = iconImage;
        return item.GetComponent<WanfaModifierIcon>();
    }

    public void Setup(WanfaModifierTooltipModel model)
    {
        if (_icon == null)
        {
            _icon = transform.Find("Icon").GetComponent<Image>();
            _outline = transform.Find("Outline").GetComponent<IconOutline>();
            _tipButton = GetComponent<TipButton>();
        }

        _model = model;
        _icon.sprite = SpriteTextureLoader.getSprite(model.IconPath);
        if (_icon.sprite == null)
        {
            _icon.sprite = SpriteTextureLoader.getSprite(WanfaUiIcons.Modifier);
        }
        _outline.parent_image = _icon;
        _outline.show(new ContainerItemColor(model.OutlineColor, null));
        _tipButton.clickAction = null;
        _tipButton.setHoverAction(ShowTooltip);
    }

    private void ShowTooltip()
    {
        Tooltip.show(gameObject, WorldboxGame.Tooltips.RawTip.id, new TooltipData
        {
            tip_name = _model.Title,
            tip_description = _model.Description,
            tip_description_2 = _model.Detail
        });
    }
}
