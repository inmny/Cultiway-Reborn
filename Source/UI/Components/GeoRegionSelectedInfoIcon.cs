using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Cultiway.UI.Components;

internal class GeoRegionSelectedInfoIcon : MonoBehaviour
{
    internal const float DefaultSize = 24f;

    private Image _hitbox;
    private Image _icon;
    private Button _button;
    private TipButton _tipButton;

    internal static GeoRegionSelectedInfoIcon Create(Transform parent, string name, float size = DefaultSize)
    {
        GameObject root = new(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(TipButton), typeof(LayoutElement));
        root.transform.SetParent(parent);
        root.transform.localScale = Vector3.one;
        root.transform.localPosition = Vector3.zero;

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = Vector2.zero;

        LayoutElement layout = root.GetComponent<LayoutElement>();
        layout.preferredWidth = size;
        layout.preferredHeight = size;
        layout.minWidth = size;
        layout.minHeight = size;

        Image hitbox = root.GetComponent<Image>();
        hitbox.sprite = null;
        hitbox.color = Color.clear;
        hitbox.raycastTarget = true;

        GameObject iconObject = root.NewChild("Icon", typeof(RectTransform), typeof(Image));
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(size-2, size-2);
        iconRect.anchoredPosition = Vector2.zero;

        GeoRegionSelectedInfoIcon icon = root.AddComponent<GeoRegionSelectedInfoIcon>();
        icon._hitbox = hitbox;
        icon._icon = iconObject.GetComponent<Image>();
        icon._icon.raycastTarget = false;
        icon._button = root.GetComponent<Button>();
        icon._button.transition = Selectable.Transition.None;
        icon._button.targetGraphic = hitbox;
        icon._tipButton = root.GetComponent<TipButton>();
        icon._tipButton.type = WorldboxGame.Tooltips.RawTip.id;
        return icon;
    }

    internal void Setup(Sprite sprite, string title, string description, Color? color = null, UnityAction clickAction = null)
    {
        _icon.sprite = sprite != null ? sprite : SpriteTextureLoader.getSprite(GeoRegionAsset.DefaultIconPath);
        _icon.color = Color.white;
        _hitbox.color = Color.clear;

        _tipButton.textOnClick = title;
        _tipButton.textOnClickDescription = description;
        _tipButton.showOnClick = clickAction == null;

        _button.onClick.RemoveAllListeners();
        if (clickAction != null)
        {
            _button.onClick.AddListener(clickAction);
        }
    }
}
