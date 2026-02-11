using Cultiway.Const;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;

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
        part_background.sprite = meta_object.getBannerBackground();
        part_icon.sprite = meta_object.getBannerIcon();
        part_background.color = meta_object.getColor().getColorMainSecond();
        part_icon.color = meta_object.getColor().getColorBanner();
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
        if (go.transform.Find("TiltEffect/dead") != null)
        {
            Destroy(go.transform.Find("TiltEffect/dead").gameObject);
        }
        if (go.transform.Find("TiltEffect/left") != null)
        {
            Destroy(go.transform.Find("TiltEffect/left").gameObject);
        }
        if (go.transform.Find("TiltEffect/winner") != null)
        {
            Destroy(go.transform.Find("TiltEffect/winner").gameObject);
        }
        if (go.transform.Find("TiltEffect/loser") != null)
        {
            Destroy(go.transform.Find("TiltEffect/loser").gameObject);
        }

        go.GetComponent<UiButtonHoverAnimation>().default_scale = new(0.75f, 0.75f, 1);
        go.GetComponent<TipButton>().setDefaultScale(new Vector3(0.75f, 0.75f, 1));
        go.SetActive(true);
        //Destroy(go.transform.Find(""));
        _prefab = go.AddComponent<GeoRegionBanner>();
        _prefab.AddComponent<DraggableLayoutElement>();
        _prefab.name = "PrefabBannerGeoRegion";
        _prefab.transform.localScale = Vector3.one * 0.75f;
    }
    private static GeoRegionBanner _prefab;
}