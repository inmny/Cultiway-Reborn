using Cultiway.Abstract;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

/// <summary>修炼体系 Tooltip 属性表中的池化单元。</summary>
public sealed class CultisysStatBonusEntry : APrefabPreview<CultisysStatBonusEntry>
{
    private Image _icon;
    private Text _name;
    private Text _value;

    protected override void OnInit()
    {
        _icon = transform.Find("Icon").GetComponent<Image>();
        _name = transform.Find("Name").GetComponent<Text>();
        _value = transform.Find("Value").GetComponent<Text>();
    }

    internal void Setup(CultisysPresentation.StatBonus stat)
    {
        Init();
        _icon.sprite = string.IsNullOrEmpty(stat.IconPath)
            ? SpriteTextureLoader.getSprite("ui/icons/iconDamage")
            : SpriteTextureLoader.getSprite(stat.IconPath)
              ?? SpriteTextureLoader.getSprite("ui/icons/iconDamage");
        _name.text = stat.Name;
        _value.text = stat.Value;
        _value.color = stat.Color;
    }

    private static void _init()
    {
        GameObject obj = UiLayout.Create(ModClass.I.PrefabLibrary, nameof(CultisysStatBonusEntry), true,
            100f, 10f, 1f, TextAnchor.MiddleLeft);
        GameObject icon = new("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        icon.transform.SetParent(obj.transform, false);
        UiLayout.SetSize(icon.transform, 8f, 8f);
        icon.GetComponent<Image>().preserveAspect = true;

        Text name = UiElements.CreateText(obj.transform, "Name", string.Empty, 43f, 10f, 5,
            TextAnchor.MiddleLeft);
        Text value = UiElements.CreateText(obj.transform, "Value", string.Empty, 47f, 10f, 5,
            TextAnchor.MiddleRight, FontStyle.Bold);
        ConfigureBestFit(name);
        ConfigureBestFit(value);
        Prefab = obj.AddComponent<CultisysStatBonusEntry>();
    }

    private static void ConfigureBestFit(Text text)
    {
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 3;
        text.resizeTextMaxSize = 5;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }
}
