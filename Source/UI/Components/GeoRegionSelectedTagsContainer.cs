using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.UI.Components;

internal class GeoRegionSelectedTagsContainer : GeoRegionSelectedContainerBase
{
    protected override float MinimumWidth => 84f;
    protected override float MinimumHeight => 20f;
    protected override Vector2 GridSpacing => new(1f, 1f);
    protected override int ConstraintCount => 1;
    protected override Vector2 CellSize => new(18f, 18f);
    protected override string BackgroundTitleKey => "Cultiway.SelectedGeoRegion.Overview";

    protected override void Build(GeoRegion region)
    {
        GeoRegionAsset category = region.GetCategory();

        AddIcon(
            category.GetSpriteIcon(),
            LMTools.Format("Cultiway.SelectedGeoRegion.Category.Title", ("category", category.GetDisplayName())),
            LMTools.Format(
                "Cultiway.SelectedGeoRegion.Category.Description",
                ("category_id", category.id),
                ("layer", FormatLayer(region.data.Layer))),
            RegionColor(region));

        AddIcon(
            LoadSprite("cultiway/icons/iconGeoRegion"),
            LMTools.Format("Cultiway.SelectedGeoRegion.Layer.Title", ("layer", FormatLayer(region.data.Layer))),
            LMTools.GetOrKey("Cultiway.SelectedGeoRegion.Layer.Description"));

        AddIcon(
            LoadSprite("ui/Icons/iconZones"),
            LMTools.Format("Cultiway.SelectedGeoRegion.Size.Title", ("size", FormatSize(region.data.TileCount, category))),
            LMTools.Format(
                "Cultiway.SelectedGeoRegion.Size.Description",
                ("tiles", region.data.TileCount),
                ("range", FormatTileRange(category))));

        AddIcon(
            LoadSprite("ui/Icons/iconWorldInfo"),
            LMTools.GetOrKey("Cultiway.SelectedGeoRegion.Center.Title"),
            LMTools.Format(
                "Cultiway.SelectedGeoRegion.Center.Description",
                ("x", region.data.CenterX),
                ("y", region.data.CenterY)));
    }

    internal static string FormatLayer(GeoRegionLayer layer)
    {
        return layer switch
        {
            GeoRegionLayer.Primary => LMTools.GetOrKey("Cultiway.GeoRegion.Layer.Primary"),
            GeoRegionLayer.Landform => LMTools.GetOrKey("Cultiway.GeoRegion.Layer.Landform"),
            GeoRegionLayer.Landmass => LMTools.GetOrKey("Cultiway.GeoRegion.Layer.Landmass"),
            GeoRegionLayer.Peninsula => LMTools.GetOrKey("Cultiway.GeoRegion.Layer.Peninsula"),
            GeoRegionLayer.Strait => LMTools.GetOrKey("Cultiway.GeoRegion.Layer.Strait"),
            GeoRegionLayer.Archipelago => LMTools.GetOrKey("Cultiway.GeoRegion.Layer.Archipelago"),
            _ => throw new System.InvalidOperationException($"未知 GeoRegionLayer: {layer}")
        };
    }

    private static string FormatSize(int tileCount, GeoRegionAsset category)
    {
        if (category.MaxTiles > category.MinTiles && category.MaxTiles > 0)
        {
            float ratio = (tileCount - category.MinTiles) / (float)(category.MaxTiles - category.MinTiles);
            if (ratio < 0.33f) return LMTools.GetOrKey("Cultiway.GeoRegion.Size.Small");
            if (ratio < 0.66f) return LMTools.GetOrKey("Cultiway.GeoRegion.Size.Medium");
            return LMTools.GetOrKey("Cultiway.GeoRegion.Size.Large");
        }

        if (tileCount < 64) return LMTools.GetOrKey("Cultiway.GeoRegion.Size.Tiny");
        if (tileCount < 256) return LMTools.GetOrKey("Cultiway.GeoRegion.Size.Small");
        if (tileCount < 1024) return LMTools.GetOrKey("Cultiway.GeoRegion.Size.Medium");
        return LMTools.GetOrKey("Cultiway.GeoRegion.Size.Large");
    }

    private static string FormatTileRange(GeoRegionAsset category)
    {
        if (category.MinTiles <= 0 && category.MaxTiles <= 0) return LMTools.GetOrKey("Cultiway.GeoRegion.TileRange.Unlimited");
        if (category.MaxTiles <= 0) return $"{category.MinTiles}+";
        return $"{category.MinTiles}-{category.MaxTiles}";
    }
}
