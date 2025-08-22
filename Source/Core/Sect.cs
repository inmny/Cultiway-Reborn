using Cultiway.Const;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Core;

public class Sect : MetaObject<SectData>
{
    public override MetaType meta_type => MetaTypeExtend.Sect.Back();

    public void Setup(Actor founder)
    {
        generateNewMetaObject();
        data.FounderActorName = founder.getName();
        data.FounderActorID = founder.data.id;
        data.name = founder.generateName(meta_type, getID());
    }

    public override void generateBanner()
    {
        data.BannerBackgroundIndex = ModClass.L.SectBannerLibrary.getNewIndexBackground();
        data.BannerIconIndex = ModClass.L.SectBannerLibrary.getNewIndexIcon();
    }

    public Sprite getBannerBackground()
    {
        return ModClass.L.SectBannerLibrary.getSpriteBackground(data.BannerBackgroundIndex);
    }

    public Sprite getBannerIcon()
    {
        return ModClass.L.SectBannerLibrary.getSpriteIcon(data.BannerIconIndex);
    }

    public override ColorLibrary getColorLibrary()
    {
        // TODO: 添加颜色库
        return AssetManager.families_colors_library;
    }
}    