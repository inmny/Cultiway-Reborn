using Cultiway.Utils;
using Cultiway.Abstract;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

public class SectScriptureStatValue : APrefabPreview<SectScriptureStatValue>
{
    private static readonly Color ValueColor = new(1f, 0.60730225f, 0.1102941f, 1f);
    private static readonly Color BackgroundColor = new(0.7735849f, 0.7735849f, 0.7735849f, 0.6313726f);

    public Image Icon { get; private set; }
    public Text Value { get; private set; }

    protected override void OnInit()
    {
        Icon = transform.Find(nameof(Icon)).GetComponent<Image>();
        Value = transform.Find(nameof(Value)).GetComponent<Text>();
    }

    public void Setup(Sprite icon, int value)
    {
        Init();
        Icon.sprite = icon;
        Icon.color = Color.white;
        Value.color = ValueColor;
        Value.text = Toolbox.formatNumber(value, 4);
    }

    private static void _init()
    {
        GameObject obj = ModClass.NewPrefabPreview(nameof(SectScriptureStatValue), typeof(Image));
        SetRect(obj.GetComponent<RectTransform>(), new Vector2(25f, 12f), Vector2.zero);

        Image background = obj.GetComponent<Image>();
        background.sprite = Resources.Load<GameObject>("ui/IconValue")?.GetComponent<Image>()?.sprite;
        background.type = background.sprite == null ? Image.Type.Simple : Image.Type.Sliced;
        background.color = BackgroundColor;
        background.raycastTarget = false;

        GameObject icon = obj.NewChild(nameof(Icon), typeof(Image));
        SetRect(icon.GetComponent<RectTransform>(), new Vector2(9f, 9f), new Vector2(5.5f, 0f));
        Image iconImage = icon.GetComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        GameObject value = obj.NewChild(nameof(Value), typeof(Text), typeof(Shadow));
        SetRect(value.GetComponent<RectTransform>(), new Vector2(14f, 12f), new Vector2(18f, 0f));
        Text valueText = value.GetComponent<Text>();
        valueText.font = Cultiway.UI.UiTheme.Current.Font;
        valueText.fontSize = 6;
        valueText.fontStyle = FontStyle.Bold;
        valueText.alignment = TextAnchor.MiddleCenter;
        valueText.color = ValueColor;
        valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
        valueText.verticalOverflow = VerticalWrapMode.Truncate;
        valueText.raycastTarget = false;
        Shadow shadow = value.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(0.5f, -0.5f);

        Prefab = obj.AddComponent<SectScriptureStatValue>();
        Prefab.Icon = iconImage;
        Prefab.Value = valueText;
    }

    private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

}
