using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cultiway.UI.Components;

/// <summary>选中栏和详情窗口中的地区信息图标，可显示说明、响应点击并在地图上高亮相关地区。</summary>
internal class GeoRegionSelectedInfoIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    /// <summary>信息图标的默认边长。</summary>
    internal const float DefaultSize = 24f;

    // 透明点击区域、可见图案、点击按钮和悬停说明。
    private Image _hitbox;
    private Image _icon;
    private Button _button;
    private TipButton _tipButton;
    // 鼠标停留时需要在地图上高亮的地区。
    private GeoRegion _hoverGeoRegion;

    /// <summary>创建一个固定尺寸的信息图标，避免内容变化时挤动选中栏布局。</summary>
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

    /// <summary>设置玩家看到的图案、说明和点击动作；没有点击动作时点击会显示说明。</summary>
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

    /// <summary>指定鼠标停留时要在地图上高亮的地区。</summary>
    internal void SetHoverGeoRegion(GeoRegion region)
    {
        _hoverGeoRegion = region;
    }

    /// <summary>将通用说明改为地区详情提示，玩家悬停时可看到该地区信息。</summary>
    internal void SetGeoRegionTooltip(GeoRegion region)
    {
        if (region == null)
        {
            throw new System.InvalidOperationException("GeoRegion tooltip 目标为空");
        }

        _tipButton.type = WorldboxGame.Tooltips.GeoRegion.id;
        _tipButton.textOnClick = region.id.ToString();
        _tipButton.textOnClickDescription = "";
        _tipButton.text_description_2 = "";
    }

    /// <summary>鼠标进入图标时，在地图上突出显示关联地区。</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_hoverGeoRegion == null || _hoverGeoRegion.isRekt()) return;
        ModClass.I?.CustomMapModeManager?.SetUiHoveredGeoRegion(_hoverGeoRegion);
    }

    /// <summary>鼠标离开图标时，取消地图上的关联地区高亮。</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        ClearHoverGeoRegion();
    }

    private void OnDisable()
    {
        ClearHoverGeoRegion();
    }

    private void OnDestroy()
    {
        ClearHoverGeoRegion();
    }

    /// <summary>在鼠标离开、控件关闭或销毁时清除地图高亮。</summary>
    private void ClearHoverGeoRegion()
    {
        if (_hoverGeoRegion == null) return;
        ModClass.I?.CustomMapModeManager?.ClearUiHoveredGeoRegion(_hoverGeoRegion);
    }
}
