using Cultiway.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Cultiway.UI.Components;

internal class GeoRegionWindowRegionHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private GeoRegion _region;
    private bool _hovering;

    internal void Setup(GeoRegion region)
    {
        _region = region;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_region == null || _region.isRekt()) return;
        _hovering = true;
        ModClass.I?.CustomMapModeManager?.SetUiHoveredGeoRegion(_region);
    }

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

    private void ClearHover()
    {
        if (!_hovering) return;
        _hovering = false;
        if (_region == null) return;
        ModClass.I?.CustomMapModeManager?.ClearUiHoveredGeoRegion(_region);
    }
}
