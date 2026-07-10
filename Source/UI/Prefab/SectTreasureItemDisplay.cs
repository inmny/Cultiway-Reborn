using Cultiway.Abstract;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI.Prefab;

/// <summary>
/// 藏宝阁物品格子，在通用特殊物品图标上补充外借状态。
/// </summary>
public class SectTreasureItemDisplay : APrefabPreview<SectTreasureItemDisplay>
{
    private SpecialItemDisplay _itemDisplay;
    private Image _loanMarker;

    protected override void OnInit()
    {
        _itemDisplay ??= GetComponent<SpecialItemDisplay>();
        _loanMarker ??= transform.Find("Loan Marker").GetComponent<Image>();
    }

    /// <summary>
    /// 显示物品图标和原版特殊物品提示，并标识已经外借的法宝。
    /// </summary>
    public void Setup(Entity item)
    {
        Init();
        _itemDisplay.Setup(item.GetComponent<SpecialItem>());
        _loanMarker.gameObject.SetActive(item.HasComponent<SectTreasureLoan>());
        name = $"SectTreasure_{item.Id}";
    }

    private static void _init()
    {
        GameObject obj = Object.Instantiate(SpecialItemDisplay.Prefab.gameObject, ModClass.I.PrefabLibrary, false);
        obj.name = nameof(SectTreasureItemDisplay);

        GameObject marker = new("Loan Marker", typeof(RectTransform), typeof(Image));
        marker.transform.SetParent(obj.transform, false);
        RectTransform markerRect = marker.GetComponent<RectTransform>();
        markerRect.anchorMin = new Vector2(1f, 1f);
        markerRect.anchorMax = new Vector2(1f, 1f);
        markerRect.pivot = new Vector2(1f, 1f);
        markerRect.anchoredPosition = Vector2.zero;
        markerRect.sizeDelta = new Vector2(7f, 7f);

        Image markerImage = marker.GetComponent<Image>();
        markerImage.sprite = SpriteTextureLoader.getSprite("ui/icons/iconFavoriteWeapon");
        markerImage.raycastTarget = false;
        marker.SetActive(false);

        Prefab = obj.AddComponent<SectTreasureItemDisplay>();
        Prefab._itemDisplay = obj.GetComponent<SpecialItemDisplay>();
        Prefab._loanMarker = markerImage;
    }
}
