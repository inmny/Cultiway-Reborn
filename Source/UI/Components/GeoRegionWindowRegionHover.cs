using Cultiway.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Cultiway.UI.Components;

/// <summary>地区窗口中的悬停处理器，鼠标移到旗帜上时在地图中突出显示对应地区。</summary>
internal class GeoRegionWindowRegionHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // 当前旗帜代表的地区，以及地图高亮是否已经开启。
    private GeoRegion _region;
    private bool _hovering;

    /// <summary>指定当前旗帜代表、需要在地图上高亮的地区。</summary>
    internal void Setup(GeoRegion region)
    {
        _region = region;
    }

    /// <summary>鼠标进入旗帜时突出显示地区范围。</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_region == null || _region.isRekt()) return;
        _hovering = true;
        ModClass.I?.CustomMapModeManager?.SetUiHoveredGeoRegion(_region);
    }

    /// <summary>鼠标离开旗帜时恢复地图显示。</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        ClearHover();
    }

    private void OnDisable()
    {
        ClearHover();
    }

    private void OnDestroy()
    {
        ClearHover();
    }

    /// <summary>旗帜不再悬停、关闭或销毁时清除地图高亮。</summary>
    private void ClearHover()
    {
        if (!_hovering) return;
        _hovering = false;
        if (_region == null) return;
        ModClass.I?.CustomMapModeManager?.ClearUiHoveredGeoRegion(_region);
    }
}
