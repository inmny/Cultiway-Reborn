using System;
using Cultiway.Const;
using Cultiway.Core.Components;
using Cultiway.Core.GeoLib.Components;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core;

/// <summary>
/// 表示地图上的一个地理地区，保存名称、颜色、分类和所属地块等信息。
/// 地图模式、地区详情和单位统计都会通过此对象读取地区当前状态。
/// </summary>
public class GeoRegion : MetaObject<GeoRegionData>
{
    // 以下编号记录各类可见结果上次发生变化的次数，供界面和关系查询判断是否需要重新计算。
    private int presentationRevision = 1;
    private int geometryRevision = 1;
    private int adjacencyRevision = 1;
    private int crossLayerRevision = 1;
    private int compositionRevision = 1;

    /// <summary>供通用元对象系统识别这是地理地区。</summary>
    public override MetaType meta_type => MetaTypeExtend.GeoRegion.Back();
    /// <summary>与地区绑定的实体，用于接入地块扩展和实体回收流程。</summary>
    public Entity E {get; private set;}

    /// <summary>名称或颜色上次变化的编号，地图显示用它判断是否需要刷新。</summary>
    internal int PresentationRevision => presentationRevision;
    /// <summary>所含地块或边界上次变化的编号，轮廓相关功能用它判断是否需要重算。</summary>
    internal int GeometryRevision => geometryRevision;
    /// <summary>同层相邻关系上次变化的编号，相邻地区查询用它判断是否需要重算。</summary>
    internal int AdjacencyRevision => adjacencyRevision;
    /// <summary>跨层重叠关系上次变化的编号，包含和重叠查询用它判断是否需要重算。</summary>
    internal int CrossLayerRevision => crossLayerRevision;
    /// <summary>城市、王国等地区组成上次变化的编号，详情统计用它判断是否需要刷新。</summary>
    internal int CompositionRevision => compositionRevision;

    /// <summary>统计当前地区内仍有效的全部单位。</summary>
    public override int countUnits()
    {
        return getUnits().CountValidUnits();
    }

    /// <summary>统计当前地区内仍有效的成年单位。</summary>
    public override int countAdults()
    {
        return getUnits().CountValidAdults();
    }

    /// <summary>统计当前地区内仍有效的未成年单位。</summary>
    public override int countChildren()
    {
        return getUnits().CountValidChildren();
    }

    /// <summary>名称真正改完后，通知地图显示和相关地区刷新名称信息。</summary>
    public override void trackName(bool pPostChange = false)
    {
        base.trackName(pPostChange);
        if (!pPostChange) return;

        GeoRegionManager manager = WorldboxGame.I?.GeoRegions;
        if (manager != null)
        {
            manager.NotifyRegionPresentationChanged(this);
        }
        else
        {
            ApplyRuntimeChanges(GeoRegions.GeoRegionRuntimeChangeKind.Presentation);
        }
    }

    /// <summary>更新地区颜色；成功更新后通知地图显示和相关地区刷新颜色信息。</summary>
    public override bool updateColor(ColorAsset pColor)
    {
        if (!base.updateColor(pColor)) return false;

        GeoRegionManager manager = WorldboxGame.I?.GeoRegions;
        if (manager != null)
        {
            manager.NotifyRegionPresentationChanged(this);
        }
        else
        {
            ApplyRuntimeChanges(GeoRegions.GeoRegionRuntimeChangeKind.Presentation);
        }
        return true;
    }

    /// <summary>
    /// 记录地区哪些内容已改变，使依赖这些内容的界面、关系和统计在下次使用时更新。
    /// </summary>
    internal void ApplyRuntimeChanges(
        GeoRegions.GeoRegionRuntimeChangeKind changes,
        bool markStatsDirty = true)
    {
        if (changes == GeoRegions.GeoRegionRuntimeChangeKind.None) return;

        if ((changes & GeoRegions.GeoRegionRuntimeChangeKind.Presentation) != 0)
        {
            presentationRevision = NextRevision(presentationRevision);
        }
        if ((changes & GeoRegions.GeoRegionRuntimeChangeKind.Geometry) != 0)
        {
            geometryRevision = NextRevision(geometryRevision);
        }
        if ((changes & GeoRegions.GeoRegionRuntimeChangeKind.Adjacency) != 0)
        {
            adjacencyRevision = NextRevision(adjacencyRevision);
        }
        if ((changes & GeoRegions.GeoRegionRuntimeChangeKind.CrossLayer) != 0)
        {
            crossLayerRevision = NextRevision(crossLayerRevision);
        }
        if ((changes & GeoRegions.GeoRegionRuntimeChangeKind.Composition) != 0)
        {
            compositionRevision = NextRevision(compositionRevision);
        }

        if (markStatsDirty)
        {
            stats_dirty_version = NextRevision(stats_dirty_version);
        }
    }

    /// <summary>递增变化编号；达到整数上限后从 1 重新开始，0 始终表示尚无有效编号。</summary>
    private static int NextRevision(int revision)
    {
        return revision == int.MaxValue ? 1 : revision + 1;
    }

    /// <summary>释放地区时先把绑定实体标记为待回收，再执行通用清理。</summary>
    public override void Dispose()
    {
        if (!E.IsNull)
        {
            E.AddTag<TagRecycle>();
        }
        base.Dispose();
    }
    /// <summary>仅当通用删除条件满足，且旧的地块归属读取不再使用此地区时才允许移除。</summary>
    public override bool isReadyForRemoval()
    {
        return base.isReadyForRemoval() &&
               (WorldboxGame.I?.GeoRegions?.CanRecycleRegion(this) ?? true);
    }
    /// <summary>创建地区对应的实体，并写入地区编号供地块系统关联。</summary>
    public void BaseSetup()
    {
        E = ModClass.I.TileExtendManager.World.CreateEntity(
            new GeoRegionBinder(getID())
        );
    }
    /// <summary>生成名称、颜色、旗帜等通用元对象初始数据。</summary>
    public void Setup()
    {
        generateNewMetaObject();
    }

    /// <summary>为地区随机选择旗帜背景和图案编号。</summary>
    public override void generateBanner()
    {
        data.BannerBackgroundIndex = ModClass.L.GeoRegionBannerLibrary.getNewIndexBackground();
        data.BannerIconIndex = ModClass.L.GeoRegionBannerLibrary.getNewIndexIcon();
    }

    /// <summary>按地区数据中记录的背景编号取得旗帜背景图片。</summary>
    public Sprite getBannerBackground()
    {
        return ModClass.L.GeoRegionBannerLibrary.getSpriteBackground(data.BannerBackgroundIndex);
    }

    /// <summary>取得按地区实际轮廓绘制的旗帜图案；无法绘制时使用分类图标。</summary>
    public Sprite getBannerIcon()
    {
        return GeoRegionShapeSpriteCache.GetSprite(this);
    }

    /// <summary>根据地区数据中记录的分类编号取得分类配置；数据缺失或编号无效时明确报错。</summary>
    public GeoRegionAsset GetCategory()
    {
        if (data == null) throw new InvalidOperationException($"GeoRegion 数据为空: id={getID()}");

        var categoryId = data.CategoryId;
        if (string.IsNullOrEmpty(categoryId))
        {
            throw new InvalidOperationException(
                $"GeoRegion 分类为空: id={getID()}, name={name}, layer={data.Layer}, tiles={data.TileCount}");
        }

        var lib = ModClass.L?.GeoRegionLibrary ?? throw new InvalidOperationException("GeoRegionLibrary 尚未初始化");
        var category = lib.getSimple(categoryId);
        if (category != null) return category;

        throw new InvalidOperationException(
            $"GeoRegion 分类无效: id={getID()}, name={name}, layer={data.Layer}, category={categoryId}");
    }

    /// <summary>返回地区可使用的颜色库，目前沿用家族颜色库。</summary>
    public override ColorLibrary getColorLibrary()
    {
        // TODO: 添加颜色库
        return AssetManager.families_colors_library;
    }
}    
