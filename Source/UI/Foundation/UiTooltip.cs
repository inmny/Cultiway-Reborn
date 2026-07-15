using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>统一包装原版 TipButton，保留其悬停、点击和缩放行为。</summary>
internal static class UiTooltip
{
    public static void Set(GameObject target, string title, string description, string detail = null)
    {
        TipButton tipButton = Prepare(target);
        tipButton.type = WorldboxGame.Tooltips.RawTip.id;
        tipButton.textOnClick = title;
        tipButton.textOnClickDescription = description;
        tipButton.text_description_2 = detail;
        tipButton.clickAction = null;
        tipButton.setHoverAction(tipButton.showTooltipDefault);
    }

    public static void Set(InputField input, string title, string description, string detail = null)
    {
        Transform icon = input.transform.Find("SearchIcon");
        GameObject target = icon == null ? CreateInputTooltipIcon(input) : icon.gameObject;
        Set(target, title, description, detail);
    }

    public static void Set(Toggle toggle, string title, string description, string detail = null)
    {
        Transform icon = toggle.transform.Find("Icon") ?? toggle.transform.Find("Box");
        if (icon == null)
            throw new InvalidOperationException($"Toggle {toggle.name} 缺少可承载 TipButton 的图标");

        GameObject target = icon.gameObject;
        Set(target, title, description, detail);
        Button button = target.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => toggle.isOn = !toggle.isOn);
    }

    public static void Set(GameObject target, TooltipAction action)
    {
        TipButton tipButton = Prepare(target);
        tipButton.clickAction = null;
        tipButton.setHoverAction(action);
    }

    private static TipButton Prepare(GameObject target)
    {
        if (!target.TryGetComponent<Button>(out _) && !target.TryGetComponent<Slider>(out _))
        {
            if (target.GetComponent<Selectable>() != null)
                throw new InvalidOperationException($"TipButton 目标 {target.name} 不能直接使用 Selectable 控件");

            Button hoverButton = target.AddComponent<Button>();
            hoverButton.transition = Selectable.Transition.None;
            hoverButton.targetGraphic = null;
            hoverButton.navigation = new Navigation { mode = Navigation.Mode.None };
            Graphic graphic = target.GetComponent<Graphic>();
            if (graphic != null) graphic.raycastTarget = true;
        }
        return target.GetComponent<TipButton>() ?? target.AddComponent<TipButton>();
    }

    private static GameObject CreateInputTooltipIcon(InputField input)
    {
        GameObject icon = new("TooltipIcon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(input.transform, false);
        RectTransform rect = icon.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(12f, 12f);
        rect.anchoredPosition = new Vector2(-8f, 0f);
        Image image = icon.GetComponent<Image>();
        UiResources.SetImage(image, UiIcons.Info);

        RectTransform textRect = input.textComponent.rectTransform;
        textRect.offsetMax = new Vector2(-18f, textRect.offsetMax.y);
        RectTransform placeholderRect = input.placeholder.GetComponent<RectTransform>();
        placeholderRect.offsetMax = new Vector2(-18f, placeholderRect.offsetMax.y);
        return icon;
    }
}
