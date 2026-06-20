using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.UI.Components;

internal class GeoRegionSelectedRelationsContainer : GeoRegionSelectedContainerBase
{
    protected override float LeftPadding => 6f;
    protected override float RightPadding => 6f;
    protected override float MinimumWidth => 96f;
    protected override float MinimumHeight => 22f;
    protected override int ConstraintCount => 1;
    protected override Vector2 CellSize => new(28f, 28f);
    protected override Vector2 GridSpacing => new(2f, 0f);
    protected override bool KeepVisibleWhenEmpty => true;
    protected override string BackgroundTitleKey => _backgroundTitleKey;

    private RelationMode _mode = RelationMode.Overlapping;
    private string _backgroundTitleKey = "Cultiway.SelectedGeoRegion.Related";

    internal void Configure(RelationMode mode)
    {
        _mode = mode;
        _backgroundTitleKey = mode == RelationMode.Overlapping
            ? "Cultiway.SelectedGeoRegion.Related"
            : "Cultiway.SelectedGeoRegion.Adjacent";
        SetBackgroundTitle(_backgroundTitleKey, null);
    }

    protected override void Build(GeoRegion region)
    {
        GeoRegionManager manager = WorldboxGame.I.GeoRegions;

        if (_mode == RelationMode.Overlapping)
        {
            foreach (GeoRegion related in manager.GetOverlappingRegions(region, 6))
            {
                AddRelationIcon("Cultiway.SelectedGeoRegion.RelatedRegion", related);
            }

            return;
        }

        foreach (GeoRegion adjacent in manager.GetAdjacentRegions(region, region.data.Layer, 6))
        {
            AddRelationIcon("Cultiway.SelectedGeoRegion.AdjacentRegion", adjacent);
        }
    }

    private void AddRelationIcon(string relationTitleKey, GeoRegion target)
    {
        string description = LMTools.Format(
            "Cultiway.SelectedGeoRegion.Relation.Description",
            ("name", target.name),
            ("category", target.GetCategory().GetDisplayName()),
            ("layer", GeoRegionSelectedTagsContainer.FormatLayer(target.data.Layer)),
            ("tiles", target.data.TileCount));

        AddIcon(
            target.GetCategory().GetSpriteIcon(),
            LMTools.GetOrKey(relationTitleKey),
            description,
            RegionColor(target),
            () => SelectGeoRegion(target));
    }

    internal enum RelationMode
    {
        Overlapping,
        Adjacent
    }
}
