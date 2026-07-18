using System.Globalization;
using Cultiway.Abstract;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI.Prefab;

public class CharacterPanelProgressBar : APrefabPreview<CharacterPanelProgressBar>
{
    private static readonly Color DefaultFillColor = new(0f, 0.62f, 0.78f, 1f);

    private StatBar _bar;
    private Image _barImage;
    private Image _icon;
    private TipButton _tip;
    private string _iconPath;

    protected override void OnInit()
    {
        _bar = GetComponent<StatBar>();
        _barImage = transform.FindRecursive("Bar").GetComponent<Image>();
        _icon = transform.FindRecursive("Icon").GetComponent<Image>();
        _tip = GetComponent<TipButton>() ?? gameObject.AddComponent<TipButton>();
        _tip.enabled = true;
        _tip.type = WorldboxGame.Tooltips.RawTip.id;
    }

    public void Setup(CharacterPanelProgressBarState state)
    {
        Init();

        if (_iconPath != state.IconPath)
        {
            _iconPath = state.IconPath;
            _icon.sprite = LoadIcon(_iconPath);
        }

        float max = Mathf.Max(0f, state.Max);
        float value = Mathf.Clamp(state.Value, 0f, max);
        _barImage.color = state.FillColor ?? DefaultFillColor;
        _bar.setBar(value, max, "/" + FormatNumber(max), pReset: false, pFloat: false, pUpdateText: true, 0.2f);

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
        GameObject obj = CreateNativeHealthBar(nameof(CharacterPanelProgressBar), new Vector2(92f, 12f));
        obj.transform.SetParent(ModClass.I.PrefabLibrary, false);

        var tip = obj.GetComponent<TipButton>() ?? obj.AddComponent<TipButton>();
        tip.enabled = true;
        tip.type = WorldboxGame.Tooltips.RawTip.id;

        Prefab = obj.AddComponent<CharacterPanelProgressBar>();
    }

    /// <summary>克隆原版角色血条，并统一配置可复用的尺寸与文字样式。</summary>
    internal static GameObject CreateNativeHealthBar(string name, Vector2 size)
    {
        GameObject obj = LoadOriginalHealthBar();
        obj.name = name;
        obj.GetComponent<RectTransform>().sizeDelta = size;

        Text text = obj.transform.FindRecursive("Text").GetComponent<Text>();
        text.font = Cultiway.UI.UiTheme.Current.Font;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 1;
        text.resizeTextMaxSize = 9;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return obj;
    }

    private static GameObject LoadOriginalHealthBar()
    {
        GameObject possessionUi = Resources.Load<GameObject>("ui/PossessionUI");
        Transform source = possessionUi == null ? null : possessionUi.transform.FindRecursive("HealthBar");
        if (source != null)
        {
            return Object.Instantiate(source.gameObject);
        }

        return CreateFallbackBar();
    }

    private static GameObject CreateFallbackBar()
    {
        GameObject obj = ModClass.NewPrefabPreview(nameof(CharacterPanelProgressBar), typeof(Image), typeof(Button),
            typeof(TipButton), typeof(StatBar));
        SetRect(obj.GetComponent<RectTransform>(), new Vector2(92f, 12f), Vector2.zero);

        Image background = obj.GetComponent<Image>();
        background.color = new Color(0.8301887f, 0.8301887f, 0.8301887f, 1f);
        background.sprite = UiResources.GetSprite(UiResources.WindowInner);
        background.type = Image.Type.Sliced;

        GameObject fillBackground = obj.NewChild("Background", typeof(Image));
        RectTransform fillBackgroundRect = fillBackground.GetComponent<RectTransform>();
        fillBackgroundRect.anchorMin = new Vector2(0f, 0f);
        fillBackgroundRect.anchorMax = new Vector2(1f, 1f);
        fillBackgroundRect.offsetMin = new Vector2(16f, 2f);
        fillBackgroundRect.offsetMax = new Vector2(-5f, -2f);
        fillBackground.GetComponent<Image>().color = new Color(0.1882353f, 0.1882353f, 0.1882353f, 1f);

        GameObject mask = obj.NewChild("Mask", typeof(Image), typeof(Mask));
        RectTransform maskRect = mask.GetComponent<RectTransform>();
        maskRect.anchorMin = new Vector2(0f, 0f);
        maskRect.anchorMax = new Vector2(1f, 1f);
        maskRect.offsetMin = new Vector2(16f, 2f);
        maskRect.offsetMax = new Vector2(-5f, -2f);
        mask.GetComponent<Mask>().showMaskGraphic = false;

        GameObject bar = mask.NewChild("Bar", typeof(Image));
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(0f, 1f);
        barRect.pivot = new Vector2(0f, 0.5f);
        barRect.sizeDelta = new Vector2(30f, 0f);

        GameObject icon = obj.NewChild("Icon", typeof(Image), typeof(Shadow));
        SetRect(icon.GetComponent<RectTransform>(), new Vector2(14f, 13.64f), new Vector2(8.3f, 0f));

        GameObject text = obj.NewChild("Text", typeof(Text), typeof(Shadow));
        SetRect(text.GetComponent<RectTransform>(), new Vector2(70f, 8f), new Vector2(52.5f, 0.4f));
        Text textComponent = text.GetComponent<Text>();
        textComponent.font = Cultiway.UI.UiTheme.Current.Font;
        textComponent.alignment = TextAnchor.MiddleCenter;
        textComponent.color = Color.white;

        StatBar statBar = obj.GetComponent<StatBar>();
        statBar.textField = textComponent;
        statBar.mask = maskRect;
        statBar.bar = barRect;
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

    private static string FormatNumber(float value)
    {
        return Mathf.FloorToInt(value).ToString(CultureInfo.InvariantCulture);
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
