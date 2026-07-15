using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>带统一搜索图标和文字内边距的输入框。</summary>
internal sealed class UiSearchField
{
    public InputField Input { get; }
    public Image Icon { get; }

    private UiSearchField(InputField input, Image icon)
    {
        Input = input;
        Icon = icon;
    }

    public static UiSearchField Create(Transform parent, string name, string value, string placeholder,
        float width, float height)
    {
        return Enhance(UiElements.CreateInput(parent, name, value, placeholder, width, height));
    }

    public static UiSearchField Enhance(InputField input, string iconPath = UiIcons.Search)
    {
        GameObject icon = new("SearchIcon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(input.transform, false);
        RectTransform rect = icon.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(14f, 14f);
        rect.anchoredPosition = new Vector2(10f, 0f);
        Image image = icon.GetComponent<Image>();
        UiResources.SetImage(image, iconPath);
        image.raycastTarget = false;
        input.textComponent.rectTransform.offsetMin = new Vector2(20f, 1f);
        input.placeholder.GetComponent<RectTransform>().offsetMin = new Vector2(20f, 1f);
        return new UiSearchField(input, image);
    }
}
