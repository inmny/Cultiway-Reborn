using Cultiway.Core;
using UnityEngine;

namespace Cultiway.UI.Components;

public class SectNameplateBanner : MonoBehaviour
{
    private SectBanner _banner;
    private int _lastShowFrame = -1;

    public void Show(Sect sect)
    {
        if (sect == null || sect.isRekt()) return;

        EnsureBanner();
        _lastShowFrame = Time.frameCount;
        _banner.gameObject.SetActive(true);
        _banner.load(sect);
    }

    private void LateUpdate()
    {
        if (_banner == null) return;
        if (_lastShowFrame == Time.frameCount) return;

        _banner.gameObject.SetActive(false);
    }

    private void EnsureBanner()
    {
        if (_banner != null) return;

        Transform parent = FindBannerParent();
        _banner = Instantiate(SectBanner.Prefab, parent);
        _banner.name = "Sect Nameplate Banner";
        _banner.transform.localScale = Vector3.one * 0.75f;

        ArmyBanner armyBanner = parent.GetComponentInChildren<ArmyBanner>(true);
        if (armyBanner != null)
        {
            _banner.transform.SetSiblingIndex(armyBanner.transform.GetSiblingIndex());
        }
    }

    private Transform FindBannerParent()
    {
        ArmyBanner armyBanner = GetComponentInChildren<ArmyBanner>(true);
        return armyBanner == null ? transform : armyBanner.transform.parent;
    }
}
