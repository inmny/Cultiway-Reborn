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
            frames = new List<string>
            {
                "families/frame_00", "families/frame_01", "families/frame_02", "families/frame_03", "families/frame_04", "families/frame_05", "families/frame_06", "families/frame_07", "families/frame_08", "families/frame_09",
                "families/frame_10", "families/frame_11", "families/frame_12", "families/frame_13", "families/frame_14", "families/frame_15", "families/frame_16", "families/frame_17", "families/frame_18", "families/frame_19",
                "families/frame_20", "families/frame_21", "families/frame_22"
            }
        });
    }
}