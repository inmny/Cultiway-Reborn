using Cultiway.Abstract;
using Cultiway.Core.Components;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI.Prefab;

/// <summary>
/// 背包页使用的固定尺寸物品格，在通用物品图标外提供背景和装备状态标记。
/// </summary>
internal sealed class InventoryItemDisplay : APrefabPreview<InventoryItemDisplay>
{
    internal const float CellSize = 28f;
    private const float IconSize = 22f;
    private const float MarkerSize = 8f;

    private Image _background;
    private Image _equippedMarker;
    private SpecialItemDisplay _itemDisplay;

    protected override void OnInit()
    {
        _background ??= GetComponent<Image>();
        _itemDisplay ??= transform.Find("Item").GetComponent<SpecialItemDisplay>();
        _equippedMarker ??= transform.Find("Equipped Marker").GetComponent<Image>();
    }

    /// <summary>
    /// 显示物品原色图标，并在法宝已装备时显示对应运行状态。
    /// </summary>
    public void Setup(
        SpecialItem item,
        UnityAction clickAction,
        bool equipped,
        Color stateColor)
    {
        Init();
        _itemDisplay.Setup(item, clickAction, Color.white);
        UiStateStyle.ApplyRow(
            _background,
            equipped ? UiControlState.Selected : UiControlState.Normal);
        _equippedMarker.gameObject.SetActive(equipped);
        _equippedMarker.color = stateColor;
        name = $"InventoryItem_{item.self.Id}";
    }

    /// <summary>
    /// 构建固定 28px 格子，并在绑定物品前确定内部图标尺寸。
    /// </summary>
    private static void _init()
    {
        GameObject obj = ModClass.NewPrefabPreview(nameof(InventoryItemDisplay), typeof(Image));
        RectTransform rootRect = obj.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(CellSize, CellSize);

        Image background = obj.GetComponent<Image>();
        UiResources.ApplySurface(background, UiSurface.Button);
        background.raycastTarget = false;

        GameObject itemObject = Object.Instantiate(
            SpecialItemDisplay.Prefab.gameObject,
            obj.transform,
            false);
        itemObject.name = "Item";
        RectTransform itemRect = itemObject.GetComponent<RectTransform>();
        itemRect.anchorMin = itemRect.anchorMax = new Vector2(0.5f, 0.5f);
        itemRect.pivot = new Vector2(0.5f, 0.5f);
        itemRect.anchoredPosition = Vector2.zero;
        itemRect.sizeDelta = new Vector2(IconSize, IconSize);
        itemObject.transform.localScale = Vector3.one;

        GameObject marker = new("Equipped Marker", typeof(RectTransform), typeof(Image));
        marker.transform.SetParent(obj.transform, false);
        RectTransform markerRect = marker.GetComponent<RectTransform>();
        markerRect.anchorMin = markerRect.anchorMax = Vector2.one;
        markerRect.pivot = Vector2.one;
        markerRect.anchoredPosition = new Vector2(-1f, -1f);
        markerRect.sizeDelta = new Vector2(MarkerSize, MarkerSize);

        Image markerImage = marker.GetComponent<Image>();
        UiResources.SetImage(markerImage, UiIcons.Equipped);
        markerImage.raycastTarget = false;
        marker.SetActive(false);

        Prefab = obj.AddComponent<InventoryItemDisplay>();
        Prefab._background = background;
        Prefab._itemDisplay = itemObject.GetComponent<SpecialItemDisplay>();
        Prefab._equippedMarker = markerImage;
    }
}
