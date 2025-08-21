using System.Collections.Generic;
using System.Linq;

namespace Cultiway.Core.Libraries;

public class SectBannerLibrary : GenericBannerLibrary
{
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