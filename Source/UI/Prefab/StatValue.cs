using Cultiway.Abstract;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

public class StatValue : APrefabPreview<StatValue>
{
    private BaseStatAsset _asset;
    private float         _value;
    public  Image         Icon      { get; private set; }
    public  Text          Text      { get; private set; }
    public  TipButton     TipButton { get; private set; }

    protected override void OnInit()
    {
        Icon = transform.Find(nameof(Icon)).GetComponent<Image>();
        Text = transform.Find(nameof(Text)).GetComponent<Text>();
        TipButton = transform.Find(nameof(Icon)).GetComponent<TipButton>();
    }

    public void Setup(float value, Sprite icon, BaseStatAsset asset)
    {
        Init();
        _value = value;
        _asset = asset;
        name = asset.id;

        Setup(value);
        Icon.sprite = icon;
        TipButton.textOnClick = asset.id;
    }

    public void Setup(float value)
    {
        Init();
        _value = value * _asset.tooltip_multiply_for_visual_number;
        Text.text = _asset.show_as_percents ? $"{(int)_value}%" : $"{(int)_value}";
    }

    private static void _init()
    {
        GameObject obj = ModClass.NewPrefabPreview(nameof(StatValue), typeof(Image));
        var bg = obj.GetComponent<Image>();
        bg.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        bg.type = Image.Type.Sliced;
        obj.GetComponent<RectTransform>().sizeDelta = new Vector2(18, 25);

        GameObject icon_obj = obj.NewChild(nameof(Icon), typeof(Image), typeof(Button), typeof(TipButton));
        icon_obj.transform.localPosition = new Vector3(0, 5.5f);
        icon_obj.GetComponent<RectTransform>().sizeDelta = new Vector2(14, 14);
        icon_obj.GetComponent<Image>().sprite = SpriteTextureLoader.getSprite("ui/icons/iconDamage");
        icon_obj.GetComponent<TipButton>().textOnClick = "damage";

        GameObject text_obj = obj.NewChild(nameof(Text), typeof(Text));
        text_obj.transform.localPosition = new Vector3(0, -4.6f);
        text_obj.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 10);
        var text = text_obj.GetComponent<Text>();
        text.alignment = TextAnchor.MiddleCenter;
        text.font = LocalizedTextManager.current_font;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 1;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;

        Prefab = obj.AddComponent<StatValue>();
        Prefab.Icon = icon_obj.GetComponent<Image>();
        Prefab.Text = text_obj.GetComponent<Text>();
        Prefab.TipButton = icon_obj.GetComponent<TipButton>();
    }
}