using System.Collections.Generic;
using System.Text;
using Cultiway.Core;
using UnityEngine;

namespace Cultiway.UI.Components;

/// <summary>选中底栏的地区关系区域，显示与当前地区重叠或相邻的其他地区。</summary>
internal class GeoRegionSelectedRelationsContainer : GeoRegionSelectedContainerBase
{
    // 关系图标区域的留白、最小尺寸和单行排列方式。
    protected override float LeftPadding => 6f;
    protected override float RightPadding => 6f;
    protected override float MinimumWidth => 96f;
    protected override float MinimumHeight => 22f;
    protected override int ConstraintCount => 1;
    protected override Vector2 CellSize => new(28f, 28f);
    protected override Vector2 GridSpacing => new(2f, 0f);
    protected override bool KeepVisibleWhenEmpty => true;
    /// <summary>根据当前模式显示“相关地区”或“相邻地区”标题。</summary>
    protected override string BackgroundTitleKey => _backgroundTitleKey;

    // 当前展示重叠关系还是相邻关系，以及对应标题。
    private RelationMode _mode = RelationMode.Overlapping;
    private string _backgroundTitleKey = "Cultiway.SelectedGeoRegion.Related";
    // 本次要显示的地区，最多六个。
    private List<GeoRegion> _resolvedRelations = new();

    /// <summary>选择显示重叠地区或同层相邻地区，并更新区域标题。</summary>
    internal void Configure(RelationMode mode)
    {
        _mode = mode;
        _backgroundTitleKey = mode == RelationMode.Overlapping
            ? "Cultiway.SelectedGeoRegion.Related"
            : "Cultiway.SelectedGeoRegion.Adjacent";
        SetBackgroundTitle(_backgroundTitleKey, null);
    }

    /// <summary>重新查找关系地区；名单变化时让底栏重建图标。</summary>
    protected override string GetRefreshKey(GeoRegion region)
    {
        GeoRegionManager manager = WorldboxGame.I.GeoRegions;
        _resolvedRelations = _mode == RelationMode.Overlapping
            ? manager.GetOverlappingRegions(region, 6)
            : manager.GetAdjacentRegions(region, region.data.Layer, 6);

        var key = new StringBuilder();
        key.Append(region.getID()).Append('|').Append((int)_mode);
        for (int i = 0; i < _resolvedRelations.Count; i++)
        {
            key.Append('|').Append(_resolvedRelations[i].getID());
        }
        return key.ToString();
    }

    /// <summary>为当前关系名单创建可点击的地区图标。</summary>
    protected override void Build(GeoRegion region)
    {
        for (int i = 0; i < _resolvedRelations.Count; i++)
        {
            AddRelationIcon(_resolvedRelations[i]);
        }
    }

    /// <summary>添加关系地区图标；点击打开该地区，悬停则在地图上突出显示。</summary>
    private void AddRelationIcon(GeoRegion target)
    {
        GeoRegionSelectedInfoIcon icon = AddIcon(
            target.GetCategory().GetSpriteIcon(),
            "",
            "",
            RegionColor(target),
            () => SelectGeoRegion(target));
        icon.SetGeoRegionTooltip(target);
        icon.SetHoverGeoRegion(target);
    }

    /// <summary>关系区域可显示的两种地区联系。</summary>
    internal enum RelationMode
    {
        Overlapping,
        Adjacent
    }
}
