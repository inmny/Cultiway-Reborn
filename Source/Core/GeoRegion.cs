using Cultiway.Const;
using Cultiway.Core.Components;
using Cultiway.Core.GeoLib.Components;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core;

public class GeoRegion : MetaObject<GeoRegionData>
{
    public override MetaType meta_type => MetaTypeExtend.GeoRegion.Back();
    public Entity E {get; private set;}
    public override void Dispose()
    {
        if (!E.IsNull)
        {
            E.AddTag<TagRecycle>();
        }
        base.Dispose();
    }
    public override bool isReadyForRemoval()
    {
        return base.isReadyForRemoval() && E.GetIncomingLinks<BelongToRelation>().Count == 0;
    }
    public void BaseSetup()
    {
        E = ModClass.I.TileExtendManager.World.CreateEntity(
            new GeoRegionBinder(getID())
        );
    }
    public void Setup(Actor founder)
    {
        generateNewMetaObject();
        data.name = NameGenerator.getName(WorldboxGame.NameGenerators.GeoRegion.id, ActorSex.Male, true, null, World.world.map_stats.life_dna);
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