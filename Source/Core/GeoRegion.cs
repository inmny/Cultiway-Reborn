using Cultiway.Const;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Core;

public class GeoRegion : MetaObject<GeoRegionData>
{
    public override MetaType meta_type => MetaTypeExtend.GeoRegion.Back();

    public void Setup(Actor founder)
    {
        generateNewMetaObject();
        data.name = founder.generateName(meta_type, getID());
    }

    public override void generateBanner()
    {
        data.BannerBackgroundIndex = ModClass.L.GeoRegionBannerLibrary.getNewIndexBackground();
        data.BannerIconIndex = ModClass.L.GeoRegionBannerLibrary.getNewIndexIcon();
    }

    public Sprite getBannerBackground()
    {
        return ModClass.L.GeoRegionBannerLibrary.getSpriteBackground(data.BannerBackgroundIndex);
    }

    public Sprite getBannerIcon()
    {
        return ModClass.L.GeoRegionBannerLibrary.getSpriteIcon(data.BannerIconIndex);
    }

    public override ColorLibrary getColorLibrary()
    {
        // TODO: 添加颜色库
        return AssetManager.families_colors_library;
    }
}    