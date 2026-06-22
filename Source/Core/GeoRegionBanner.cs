using Cultiway.Const;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Core;

public class GeoRegionBanner : BannerGeneric<GeoRegion, GeoRegionData>
{
    public override MetaType meta_type => MetaTypeExtend.GeoRegion.Back();
    public override void tooltipAction()
    {
        if (meta_object == null) return;
        Tooltip.show(this, WorldboxGame.Tooltips.GeoRegion.id, new TooltipData()
        {
            tip_name = meta_object.id.ToString()
        });
    }

    public override void setupBanner()
    {
        base.setupBanner();
        HideVanillaBannerDecoration(transform);
        part_background.SetActiveIfPresent(false);

        if (part_icon == null)
        {
            throw new System.InvalidOperationException("GeoRegion banner 缺少 Icon 图层");
        }

        part_icon.gameObject.SetActive(true);
        part_icon.sprite = meta_object.getBannerIcon();
        part_icon.color = Color.white;
        part_icon.preserveAspect = true;
    }

    public static GeoRegionBanner Prefab
    {
        get
        {
            if (_prefab == null)
            {
                CreatePrefab();
            }
            return _prefab;
        }
    }

    private static void CreatePrefab()
    {
        var go = Instantiate(Resources.Load<KingdomBanner>("ui/PrefabBannerKingdom"), ModClass.I.PrefabLibrary).gameObject;
        go.SetActive(false);
        Destroy(go.GetComponent<KingdomBanner>());
        HideVanillaBannerDecoration(go.transform);

        go.GetComponent<UiButtonHoverAnimation>().default_scale = new(0.75f, 0.75f, 1);
        go.GetComponent<TipButton>().setDefaultScale(new Vector3(0.75f, 0.75f, 1));
        go.SetActive(true);
        //Destroy(go.transform.Find(""));
        _prefab = go.AddComponent<GeoRegionBanner>();
        _prefab.AddComponent<DraggableLayoutElement>();
        _prefab.name = "PrefabBannerGeoRegion";
        _prefab.transform.localScale = Vector3.one * 0.75f;
    }

    private static void HideVanillaBannerDecoration(Transform root)
    {
        root.HideChildrenByPath(
            "TiltEffect/Background",
            "TiltEffect/dead",
            "TiltEffect/left",
            "TiltEffect/winner",
            "TiltEffect/loser");
    }

    private static GeoRegionBanner _prefab;
}
