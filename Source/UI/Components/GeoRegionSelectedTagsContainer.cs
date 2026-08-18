using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Components;

/// <summary>选中底栏的地区概况区域，以四个图标显示类别、层级、大小和中心坐标。</summary>
internal class GeoRegionSelectedTagsContainer : GeoRegionSelectedContainerBase
{
    // 玩家看到的类别、层级、大小和中心坐标图标。
    private GeoRegionSelectedInfoIcon _categoryIcon;
    private GeoRegionSelectedInfoIcon _layerIcon;
    private GeoRegionSelectedInfoIcon _sizeIcon;
    private GeoRegionSelectedInfoIcon _centerIcon;
    // 上次显示的四项内容，用于数值未变时不重复设置图标。
    private string _contentKey;

    // 单行四图标区域的尺寸、间距和排列方式。
    protected override float MinimumWidth => 84f;
    protected override float MinimumHeight => 20f;
    protected override Vector2 GridSpacing => new(1f, 1f);
    protected override int ConstraintCount => 1;
    protected override Vector2 CellSize => new(18f, 18f);
    protected override bool UseFlexibleOneRowSpacing => false;
    protected override float LayoutMoveDuration => 0f;
    protected override int LayoutDelayItems => 0;
    /// <summary>概况区域背景标题的本地化文本编号。</summary>
    protected override string BackgroundTitleKey => "Cultiway.SelectedGeoRegion.Overview";

    /// <summary>移除原版特性列表的排列组件，避免干扰地区概况图标的位置。</summary>
    protected override void CleanupOriginalChildren()
    {
        FlexibleOneRowGrid[] flexibleGrids = GetComponentsInChildren<FlexibleOneRowGrid>(true);
        for (int i = 0; i < flexibleGrids.Length; i++)
        {
            Object.DestroyImmediate(flexibleGrids[i]);
        }

        TraitsGrid[] traitsGrids = GetComponentsInChildren<TraitsGrid>(true);
        for (int i = 0; i < traitsGrids.Length; i++)
        {
            Object.DestroyImmediate(traitsGrids[i]);
        }
    }

    /// <summary>地区对象更换时重新创建四个概况图标。</summary>
    protected override string GetRefreshKey(GeoRegion region)
    {
        return region.getID().ToString();
    }

    /// <summary>为当前地区创建并填充四个概况图标。</summary>
    protected override void Build(GeoRegion region)
    {
        _categoryIcon = null;
        _layerIcon = null;
        _sizeIcon = null;
        _centerIcon = null;
        _contentKey = null;
        RefreshContent(region);
    }

    /// <summary>类别、层级、面积或中心变化时，更新玩家看到的图标说明。</summary>
    protected override void RefreshContent(GeoRegion region)
    {
        GeoRegionData data = region.data;
        string contentKey = $"{data.CategoryId}|{(int)data.Layer}|{data.TileCount}|{data.CenterX}|{data.CenterY}";
        if (_contentKey == contentKey) return;

        GeoRegionAsset category = region.GetCategory();
        SetupIcon(
            ref _categoryIcon,
            category.GetSpriteIcon(),
            LMTools.Format("Cultiway.SelectedGeoRegion.Category.Title", ("category", category.GetDisplayName())),
            LMTools.Format(
                "Cultiway.SelectedGeoRegion.Category.Description",
                ("category_id", category.id),
                ("layer", FormatLayer(data.Layer))),
            RegionColor(region));

        SetupIcon(
            ref _layerIcon,
            LoadSprite("cultiway/icons/iconGeoRegion"),
            LMTools.Format("Cultiway.SelectedGeoRegion.Layer.Title", ("layer", FormatLayer(data.Layer))),
            LMTools.GetOrKey("Cultiway.SelectedGeoRegion.Layer.Description"));

        SetupIcon(
            ref _sizeIcon,
            LoadSprite("ui/Icons/iconZones"),
            LMTools.Format("Cultiway.SelectedGeoRegion.Size.Title", ("size", FormatSize(data.TileCount, category))),
            LMTools.Format(
                "Cultiway.SelectedGeoRegion.Size.Description",
                ("tiles", data.TileCount),
                ("range", FormatTileRange(category))));

        SetupIcon(
            ref _centerIcon,
            LoadSprite("ui/Icons/iconWorldInfo"),
            LMTools.GetOrKey("Cultiway.SelectedGeoRegion.Center.Title"),
            LMTools.Format(
                "Cultiway.SelectedGeoRegion.Center.Description",
                ("x", data.CenterX),
                ("y", data.CenterY)));

        ArrangeOverviewIcons();
        _contentKey = contentKey;
    }

    /// <summary>把四个概况图标固定排成一行，避免原版布局动画造成位置漂移。</summary>
    private void ArrangeOverviewIcons()
    {
        GeoRegionSelectedInfoIcon[] icons =
        {
            _categoryIcon,
            _layerIcon,
            _sizeIcon,
            _centerIcon,
        };

        for (int i = 0; i < icons.Length; i++)
        {
            RectTransform rect = icons[i].GetComponent<RectTransform>();
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.up;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(9f + i * 19f, -9f);

            LayoutElement layout = icons[i].GetComponent<LayoutElement>();
            layout.ignoreLayout = true;
        }
    }

    /// <summary>首次显示时创建图标，后续只替换图案和说明。</summary>
    private void SetupIcon(
        ref GeoRegionSelectedInfoIcon icon,
        Sprite sprite,
        string title,
        string description,
        Color? color = null)
    {
        if (icon == null)
        {
            icon = AddIcon(sprite, title, description, color);
            return;
        }

        icon.Setup(sprite, title, description, color);
    }

    /// <summary>把地区层级转换为玩家可读的本地化名称。</summary>
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

    /// <summary>按类别规定的面积范围，将地区大小显示为微型、小型、中型或大型。</summary>
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

    /// <summary>把类别允许的地块数量范围整理为说明文字。</summary>
    private static string FormatTileRange(GeoRegionAsset category)
    {
        if (category.MinTiles <= 0 && category.MaxTiles <= 0) return LMTools.GetOrKey("Cultiway.GeoRegion.TileRange.Unlimited");
        if (category.MaxTiles <= 0) return $"{category.MinTiles}+";
        return $"{category.MinTiles}-{category.MaxTiles}";
    }
}
