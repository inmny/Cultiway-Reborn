using System.Globalization;
using Cultiway.Abstract;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

/// <summary>修炼体系 Tooltip 中用于展示资源当前值与上限的紧凑进度条。</summary>
public sealed class CultisysProgressEntry : APrefabPreview<CultisysProgressEntry>
{
    public const float Width = 92f;
    public const float Height = 12f;
    private static readonly Color DefaultFillColor = new(0f, 0.62f, 0.78f, 1f);

    private StatBar _bar;
    private Image _icon;
    private Image _fillImage;
    private Text _value;

    protected override void OnInit()
    {
        _bar = GetComponent<StatBar>();
        _icon = transform.FindRecursive("Icon").GetComponent<Image>();
        _fillImage = transform.FindRecursive("Bar").GetComponent<Image>();
        _value = transform.FindRecursive("Text").GetComponent<Text>();
    }

    internal void Setup(CultisysDisplayLine line)
    {
        Init();
        _icon.sprite = string.IsNullOrEmpty(line.IconPath)
            ? SpriteTextureLoader.getSprite("cultiway/icons/iconCultivation")
            : SpriteTextureLoader.getSprite(line.IconPath)
              ?? SpriteTextureLoader.getSprite("cultiway/icons/iconCultivation");

        float max = Mathf.Max(0f, line.ProgressMax);
        float current = Mathf.Clamp(line.ProgressValue, 0f, max);
        _fillImage.color = DefaultFillColor;
        if (!string.IsNullOrEmpty(line.ColorHex)
            && ColorUtility.TryParseHtmlString(line.ColorHex, out Color color))
        {
            _fillImage.color = color;
        }

        _bar.setBar(current, max, string.Empty, pReset: false, pFloat: true, pUpdateText: false, 0.2f);
        _value.text = $"{FormatNumber(current)} / {FormatNumber(max)}";
    }

    private static string FormatNumber(float value)
    {
        return value.ToString(Mathf.Approximately(value, Mathf.Round(value)) ? "0" : "0.#",
            CultureInfo.InvariantCulture);
    }

    private static void _init()
    {
        GameObject obj = CharacterPanelProgressBar.CreateNativeHealthBar(nameof(CultisysProgressEntry),
            new Vector2(Width, Height));
        obj.transform.SetParent(ModClass.I.PrefabLibrary, false);
        UiLayout.SetSize(obj.transform, Width, Height);

        CanvasGroup canvasGroup = obj.GetComponent<CanvasGroup>() ?? obj.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        foreach (Graphic graphic in obj.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        TipButton tip = obj.GetComponent<TipButton>();
        if (tip != null) tip.enabled = false;

        Prefab = obj.AddComponent<CultisysProgressEntry>();
    }
}
