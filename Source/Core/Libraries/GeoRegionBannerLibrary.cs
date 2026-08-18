using System.Collections.Generic;
using System.Linq;

namespace Cultiway.Core.Libraries;

/// <summary>地区旗帜素材目录，为地区提供可选择的背景和图标。</summary>
public class GeoRegionBannerLibrary : GenericBannerLibrary
{
    /// <summary>载入地区可用的旗帜素材，供地区外观和自定义窗口显示。</summary>
    public override void init()
    {
        base.init();
        main = add(new BannerAsset
        {
            id = "main",
            backgrounds = SpriteTextureLoader.getSpriteList("sects/background").Select(x => $"sects/background/{x.name}").ToList(),
            icons = SpriteTextureLoader.getSpriteList("sects/icon").Select(x => $"sects/icon/{x.name}").ToList()
        });
    }
}