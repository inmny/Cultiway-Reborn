using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Cultiway.UI;

internal static class BaibaoUiFactory
{
    private static readonly Color SelectedColor = new(0.68f, 0.68f, 0.68f, 1f);
    private static readonly Color NormalColor = Color.white;

    public static Color SelectionColor => SelectedColor;

    public static GameObject CreatePanel(Transform parent, string name, bool horizontal, float width, float height,
        float spacing = 3f, TextAnchor? alignment = null)
    {
        GameObject panel = WanfaUiFactory.CreateLayout(parent, name, horizontal, width, height, spacing, alignment);
        const int padding = 6;
        if (horizontal)
            panel.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(padding, padding, padding, padding);
        else
            panel.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(padding, padding, padding, padding);
        Image image = panel.AddComponent<Image>();
        image.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        image.type = Image.Type.Sliced;
        image.color = new Color(0.82f, 0.82f, 0.82f, 0.94f);
        return panel;
    }

    public static Text CreateSectionTitle(Transform parent, string name, string value, float width)
    {
        Text title = WanfaUiFactory.CreateText(parent, name, value, width, 18f, 8, TextAnchor.MiddleLeft,
            FontStyle.Bold);
        title.color = new Color(1f, 0.86f, 0.55f, 1f);
        return title;
    }

    public static void SetSelected(Button button, bool selected)
    {
        button.GetComponent<Image>().color = selected ? SelectedColor : NormalColor;
    }

    public static void AddScrollBackground(Transform content)
    {
        const float contentInset = 6f;
        float scrollbarAreaWidth = WanfaUiFactory.OriginalVerticalScrollbarReservedWidth + 2f;
        RectTransform viewport = (RectTransform)content.parent;
        RectTransform root = (RectTransform)viewport.parent;

        GameObject backgroundObject = new("Background", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(root, false);
        backgroundObject.transform.SetAsFirstSibling();
        WanfaUiFactory.Stretch(backgroundObject.GetComponent<RectTransform>(), 0f, scrollbarAreaWidth, 0f, 0f);
        Image background = backgroundObject.GetComponent<Image>();
        background.sprite = SpriteTextureLoader.getSprite("ui/special/windowEmptyFrame");
        background.type = Image.Type.Sliced;
        background.raycastTarget = false;

        viewport.offsetMin = new Vector2(contentInset, contentInset);
        viewport.offsetMax = new Vector2(-(scrollbarAreaWidth + contentInset), -contentInset);
    }

    public static Button CreateSwatchButton(Transform parent, string name, Color color, float size,
        UnityAction action)
    {
        Button button = WanfaUiFactory.CreateIconButton(parent, name, BaibaoUiIcons.Color, size, size, action, 4f);
        Image icon = button.transform.Find("Icon").GetComponent<Image>();
        icon.sprite = null;
        icon.color = color;
        icon.preserveAspect = false;
        return button;
    }

    public static void AddSearchIcon(InputField input)
    {
        GameObject icon = new("SearchIcon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(input.transform, false);
        RectTransform rect = icon.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(14f, 14f);
        rect.anchoredPosition = new Vector2(10f, 0f);
        Image image = icon.GetComponent<Image>();
        image.sprite = SpriteTextureLoader.getSprite(BaibaoUiIcons.Search);
        image.preserveAspect = true;
        image.raycastTarget = false;
        input.textComponent.rectTransform.offsetMin = new Vector2(20f, 1f);
        input.placeholder.GetComponent<RectTransform>().offsetMin = new Vector2(20f, 1f);
    }

    public static void SetButtonLabel(Button button, string value)
    {
        button.GetComponentInChildren<Text>().text = value;
    }
}

internal sealed class BaibaoMenuOption
{
    public string Label;
    public bool Selected;
    public Action Select;
}

/// <summary>窗口内复用的模态选项菜单，用于直接选择筛选与排序项。</summary>
internal sealed class BaibaoOptionMenu
{
    private const int Capacity = 12;
    private readonly GameObject _panel;
    private readonly Text _title;
    private readonly Button[] _options = new Button[Capacity];
    private readonly CanvasGroup _owner;

    public BaibaoOptionMenu(Transform parent, CanvasGroup owner)
    {
        _owner = owner;
        _panel = WanfaUiFactory.CreateLayout(parent, "BaibaoOptionMenu", false, 224f, 120f, 3f,
            TextAnchor.UpperCenter);
        _panel.transform.localPosition = new Vector3(0f, -4f, 0f);
        Image background = _panel.AddComponent<Image>();
        background.sprite = SpriteTextureLoader.getSprite("ui/special/windowEmptyFrame");
        background.type = Image.Type.Sliced;
        _title = WanfaUiFactory.CreateText(_panel.transform, "Title", string.Empty, 214f, 20f, 8,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        for (int i = 0; i < Capacity; i++)
        {
            Button option = WanfaUiFactory.CreateIconTextButton(_panel.transform, $"Option{i}",
                BaibaoUiIcons.Options, string.Empty, 214f, 22f, () => { });
            option.gameObject.SetActive(false);
            _options[i] = option;
        }
        Button close = WanfaUiFactory.CreateIconTextButton(_panel.transform, "Close", BaibaoUiIcons.Cancel,
            "Cultiway.Baibao.UI.Action.CloseMenu".Localize(), 214f, 22f, Hide);
        WanfaUiFactory.SetTooltip(close.gameObject, "Cultiway.Baibao.UI.Action.CloseMenu",
            "Cultiway.Baibao.UI.Tooltip.CloseMenu");
        _panel.SetActive(false);
    }

    public void Show(string title, IReadOnlyList<BaibaoMenuOption> options)
    {
        if (options.Count > Capacity) throw new ArgumentOutOfRangeException(nameof(options));
        _title.text = title;
        for (int i = 0; i < _options.Length; i++)
        {
            Button button = _options[i];
            bool active = i < options.Count;
            button.gameObject.SetActive(active);
            if (!active) continue;
            BaibaoMenuOption option = options[i];
            WanfaUiFactory.SetButtonIcon(button,
                option.Selected ? BaibaoUiIcons.Confirm : BaibaoUiIcons.Options);
            BaibaoUiFactory.SetButtonLabel(button, option.Label);
            BaibaoUiFactory.SetSelected(button, option.Selected);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                Hide();
                option.Select();
            });
        }
        WanfaUiFactory.SetLayout(_panel.transform, 224f, 48f + options.Count * 25f);
        _panel.transform.SetAsLastSibling();
        _panel.SetActive(true);
        _owner.interactable = false;
    }

    public void Hide()
    {
        _panel.SetActive(false);
        _owner.interactable = true;
    }
}
