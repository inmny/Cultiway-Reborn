using Cultiway.Abstract;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI.Prefab;

public class CharacterPanelIconValue : APrefabPreview<CharacterPanelIconValue>
{
    private static readonly Color ValueColor = new(1f, 0.60730225f, 0.1102941f, 1f);

    private Image _icon;
    private Text _text;
    private TipButton _tip;
    private string _iconPath;

    protected override void OnInit()
    {
        _icon = transform.FindRecursive("Icon").GetComponent<Image>();
        _text = transform.FindRecursive("Text").GetComponent<Text>();
        _tip = GetComponent<TipButton>() ?? gameObject.AddComponent<TipButton>();
        _tip.enabled = true;
        _tip.type = WorldboxGame.Tooltips.RawTip.id;
    }

    public void Setup(CharacterPanelIconValueState state)
    {
        Init();

        if (_iconPath != state.IconPath)
        {
            _iconPath = state.IconPath;
            _icon.sprite = LoadIcon(_iconPath);
        }

        _text.text = state.Text;
        _text.color = state.TextColor ?? ValueColor;
        _tip.textOnClick = state.TooltipTitle;
        _tip.textOnClickDescription = state.TooltipDescription;
        _tip.text_description_2 = state.TooltipDetail;
        _tip.setHoverAction(() => Tooltip.show(gameObject, WorldboxGame.Tooltips.RawTip.id, new TooltipData
        {
            tip_name = state.TooltipTitle,
            tip_description = state.TooltipDescription,
            tip_description_2 = state.TooltipDetail
        }));
    }

    private static void _init()
    {
        GameObject obj = LoadOriginalIconValue();
        obj.name = nameof(CharacterPanelIconValue);
        obj.transform.SetParent(ModClass.I.PrefabLibrary, false);
        obj.GetComponent<RectTransform>().sizeDelta = new Vector2(40f, 12f);

        var statsIcon = obj.GetComponent<StatsIcon>();
        if (statsIcon != null)
        {
            Object.DestroyImmediate(statsIcon);
        }

        var tip = obj.GetComponent<TipButton>() ?? obj.AddComponent<TipButton>();
        tip.enabled = true;
        tip.type = WorldboxGame.Tooltips.RawTip.id;

        Text text = obj.transform.FindRecursive("Text").GetComponent<Text>();
        text.font = Cultiway.UI.UiTheme.Current.Font;
        text.fontStyle = FontStyle.Bold;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 4;
        text.resizeTextMaxSize = 7;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        Prefab = obj.AddComponent<CharacterPanelIconValue>();
    }

    private static GameObject LoadOriginalIconValue()
    {
        GameObject source = Resources.Load<GameObject>("ui/IconValue");
        if (source != null)
        {
            return Object.Instantiate(source);
        }

        GameObject obj = ModClass.NewPrefabPreview(nameof(CharacterPanelIconValue), typeof(Image), typeof(Button),
            typeof(TipButton));
        Image background = obj.GetComponent<Image>();
        background.sprite = UiResources.GetSprite(UiResources.WindowInner);
        background.type = Image.Type.Sliced;
        background.color = new Color(0.7735849f, 0.7735849f, 0.7735849f, 0.6313726f);

        GameObject icon = obj.NewChild("Icon", typeof(Image));
        SetRect(icon.GetComponent<RectTransform>(), new Vector2(9f, 9f), new Vector2(6f, 0f));

        GameObject text = obj.NewChild("Text", typeof(Text), typeof(Shadow));
        SetRect(text.GetComponent<RectTransform>(), new Vector2(24f, 12f), new Vector2(23f, 0f));
        return obj;
    }

    private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static Sprite LoadIcon(string iconPath)
    {
        if (!string.IsNullOrEmpty(iconPath))
        {
            Sprite sprite = SpriteTextureLoader.getSprite(iconPath);
            if (sprite != null) return sprite;
        }

        return SpriteTextureLoader.getSprite("ui/icons/iconMana")
               ?? SpriteTextureLoader.getSprite("ui/icons/iconDamage");
    }
}
