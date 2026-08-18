using Cultiway.Const;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Core;

/// <summary>地区旗帜控件，向玩家显示地区图标，并可打开该地区的提示或详情。</summary>
public class GeoRegionBanner : BannerGeneric<GeoRegion, GeoRegionData>
{
    /// <summary>声明该旗帜展示的是地理区域。</summary>
    public override MetaType meta_type => MetaTypeExtend.GeoRegion.Back();
    /// <summary>鼠标悬停或点击提示入口触发时，显示当前地区的说明。</summary>
    public override void tooltipAction()
    {
        if (meta_object == null) return;
        Tooltip.show(this, WorldboxGame.Tooltips.GeoRegion.id, new TooltipData()
        {
            tip_name = meta_object.id.ToString()
        });
    }

    /// <summary>载入地区后隐藏原版国家装饰，只保留玩家可辨认的地区图标。</summary>
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

    /// <summary>供地区列表和选中栏复制使用的旗帜模板，首次访问时创建。</summary>
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

    /// <summary>以原版国家旗帜为基础制作地区旗帜模板，并调整到选中栏使用的尺寸。</summary>
    private static void CreatePrefab()
    {
        var go = Instantiate(Resources.Load<KingdomBanner>("ui/PrefabBannerKingdom"), ModClass.I.PrefabLibrary).gameObject;
        go.SetActive(false);
        Destroy(go.GetComponent<KingdomBanner>());
        HideVanillaBannerDecoration(go.transform);

        go.GetComponent<UiButtonHoverAnimation>().default_scale = new(0.75f, 0.75f, 1);
        go.GetComponent<TipButton>().setDefaultScale(new Vector3(0.75f, 0.75f, 1));
        go.SetActive(true);
        _prefab = go.AddComponent<GeoRegionBanner>();
        _prefab.AddComponent<DraggableLayoutElement>();
        _prefab.name = "PrefabBannerGeoRegion";
        _prefab.transform.localScale = Vector3.one * 0.75f;
    }

    /// <summary>隐藏胜负、死亡和背景等国家专用图层，避免地区图标出现无关标记。</summary>
    private static void HideVanillaBannerDecoration(Transform root)
    {
        root.HideChildrenByPath(
            "TiltEffect/Background",
            "TiltEffect/dead",
            "TiltEffect/left",
            "TiltEffect/winner",
            "TiltEffect/loser");
    }

    // 已创建的地区旗帜模板。
    private static GeoRegionBanner _prefab;
}
