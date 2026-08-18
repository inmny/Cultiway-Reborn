using Cultiway.Core;
using UnityEngine;

namespace Cultiway.Patch;

/// <summary>
/// 监看角色所在的主要地区，仅在角色换人、跨地区或地区外观变化时更新角色信息栏里的地区标志。
/// </summary>
internal sealed class GeoRegionUnitMetaBannerBinding : MonoBehaviour
{
    // 承载角色信息标志的原版容器，以及专门显示地区的条目。
    private UnitMetaBanners container;
    private MetaBannerElement geoRegionElement;
    // 上次显示的角色、地区和地块，用于判断玩家眼前的标志是否需要重画。
    private Actor lastActor;
    private GeoRegion lastRegion;
    private int lastTileId = -1;
    // 上次记录的归属、外观和边界变化编号。
    private int lastAssignmentStamp;
    private int lastPresentationRevision;
    private int lastGeometryRevision;
    // 标记是否已有可比较的状态，并防止更新过程中再次进入刷新。
    private bool initialized;
    private bool refreshing;

    /// <summary>指定要维护的角色信息栏及其中的地区标志。</summary>
    internal void Configure(UnitMetaBanners owner, MetaBannerElement element)
    {
        container = owner;
        geoRegionElement = element;
    }

    /// <summary>记住角色当前所在地区，作为后续自动更新的比较起点。</summary>
    internal void CaptureCurrentState(Actor actor)
    {
        Capture(actor, ResolveRegion(actor));
    }

    /// <summary>每帧检查玩家正在查看的角色，仅在可见信息确实变化时更新地区标志。</summary>
    private void Update()
    {
        if (refreshing || container == null || geoRegionElement == null) return;

        Actor actor = PatchUnitMetaBanners.GetActor(container);
        GeoRegionManager manager = WorldboxGame.I?.GeoRegions;
        int tileId = GetTileId(actor);
        int assignmentStamp = tileId >= 0 && manager != null
            ? manager.GetAssignmentStampForTile(tileId, GeoRegionLayer.Primary)
            : 0;

        if (initialized &&
            ReferenceEquals(lastActor, actor) &&
            lastTileId == tileId &&
            lastAssignmentStamp == assignmentStamp &&
            GetPresentationRevision(lastRegion) == lastPresentationRevision &&
            GetGeometryRevision(lastRegion) == lastGeometryRevision)
        {
            return;
        }

        GeoRegion region = ResolveRegion(actor);
        bool wasInitialized = initialized;
        bool actorChanged = wasInitialized && !ReferenceEquals(lastActor, actor);
        bool regionChanged = wasInitialized && !ReferenceEquals(lastRegion, region);
        bool visibilityChanged = wasInitialized && (lastRegion == null) != (region == null);
        bool presentationChanged = wasInitialized &&
                                   GetPresentationRevision(region) != lastPresentationRevision;
        Capture(actor, region, tileId, assignmentStamp);
        if (!wasInitialized) return;

        refreshing = true;
        try
        {
            if (actorChanged || visibilityChanged)
            {
                RefreshWholeContainer(actor);
            }
            else if ((regionChanged || presentationChanged) && region != null)
            {
                geoRegionElement.banner.gameObject.SetActive(true);
                geoRegionElement.banner.load(region);
            }
        }
        finally
        {
            refreshing = false;
        }
    }

    /// <summary>角色更换或地区标志需要出现、消失时，重新显示整组角色信息标志。</summary>
    private void RefreshWholeContainer(Actor actor)
    {
        if (container is ActorSelectedMetaBanners selectedContainer)
        {
            if (actor != null && !actor.isRekt()) selectedContainer.update(actor);
            return;
        }

        container.refresh();
    }

    /// <summary>信息栏关闭时忘记旧状态，下次打开会以当前角色重新开始显示。</summary>
    private void OnDisable()
    {
        initialized = false;
        lastActor = null;
        lastRegion = null;
        lastTileId = -1;
        lastAssignmentStamp = 0;
        lastPresentationRevision = 0;
        lastGeometryRevision = 0;
    }

    /// <summary>读取角色所在地块的地区归属，并保存当前可见状态。</summary>
    private void Capture(Actor actor, GeoRegion region)
    {
        GeoRegionManager manager = WorldboxGame.I?.GeoRegions;
        int tileId = GetTileId(actor);
        int assignmentStamp = tileId >= 0 && manager != null
            ? manager.GetAssignmentStampForTile(tileId, GeoRegionLayer.Primary)
            : 0;
        Capture(actor, region, tileId, assignmentStamp);
    }

    /// <summary>保存本次显示使用的角色、地区和变化编号。</summary>
    private void Capture(Actor actor, GeoRegion region, int tileId, int assignmentStamp)
    {
        lastActor = actor;
        lastRegion = region;
        lastTileId = tileId;
        lastAssignmentStamp = assignmentStamp;
        lastPresentationRevision = GetPresentationRevision(region);
        lastGeometryRevision = GetGeometryRevision(region);
        initialized = true;
    }

    /// <summary>找到角色脚下所属的主要地区；角色无效或地区已删除时返回空。</summary>
    private static GeoRegion ResolveRegion(Actor actor)
    {
        if (actor == null || actor.isRekt()) return null;
        GeoRegion region = WorldboxGame.I?.GeoRegions?.GetPrimaryGeoRegionForTile(actor.current_tile);
        return region == null || region.isRekt() ? null : region;
    }

    /// <summary>取得角色当前地块编号，无法取得时返回 -1。</summary>
    private static int GetTileId(Actor actor)
    {
        return actor?.current_tile?.data?.tile_id ?? -1;
    }

    /// <summary>取得地区外观变化编号，用于发现名称、颜色或图标变化。</summary>
    private static int GetPresentationRevision(GeoRegion region)
    {
        return region?.PresentationRevision ?? 0;
    }

    /// <summary>取得地区边界变化编号，用于发现角色脚下地区重新划分。</summary>
    private static int GetGeometryRevision(GeoRegion region)
    {
        return region?.GeometryRevision ?? 0;
    }
}
