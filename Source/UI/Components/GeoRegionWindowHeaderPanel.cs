using System;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI.Components;

internal class GeoRegionWindowHeaderPanel : MonoBehaviour
{
    private static readonly Color HeaderValueColor = new(1f, 0.60730225f, 0.1102941f, 1f);
    private const int HeaderTextMinSize = 6;
    private const int HeaderTextMaxSize = 10;

    private StatsIcon _categoryStat;
    private StatsIcon _layerStat;
    private StatsIcon _tilesStat;
    private StatsIcon _centerStat;
    private StatsIcon _ageStat;
    private StatsIcon _citiesStat;
    private GeoRegionBanner _banner;
    private GeoRegionWindowRegionHover _bannerHover;
    private bool _initialized;

    internal void Initialize()
    {
        if (_initialized) return;

        Transform bannerBackground = transform.Find("BannerBackground")
                                     ?? throw new InvalidOperationException("GeoRegionWindow 原版 Header 缺少 BannerBackground 节点");
        bannerBackground.gameObject.SetActive(true);

        Transform bannerContainer = bannerBackground.Find("Container")
                                    ?? throw new InvalidOperationException("GeoRegionWindow 原版 Header 缺少 BannerBackground/Container 节点");
        HideOriginalHeaderIcons(bannerContainer);

        Transform leftColumn = transform.Find("content_info_left")
                               ?? throw new InvalidOperationException("GeoRegionWindow 原版 Header 缺少 content_info_left 节点");
        Transform rightColumn = transform.Find("content_info_right")
                                ?? throw new InvalidOperationException("GeoRegionWindow 原版 Header 缺少 content_info_right 节点");

        _categoryStat = RequireStat(leftColumn, "i_age");
        _layerStat = RequireStat(leftColumn, "i_renown");
        _tilesStat = RequireStat(leftColumn, "i_population");
        _centerStat = RequireStat(rightColumn, "i_army");
        _ageStat = RequireStat(rightColumn, "i_cities");
        _citiesStat = RequireStat(rightColumn, "i_territory");
        _banner = SetupBanner(bannerContainer);
        _bannerHover = _banner.gameObject.GetComponent<GeoRegionWindowRegionHover>() ?? _banner.gameObject.AddComponent<GeoRegionWindowRegionHover>();

        _initialized = true;
    }

    internal void Refresh(GeoRegion region)
    {
        Initialize();

        if (region == null || region.isRekt())
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        GeoRegionAsset category = region.GetCategory();
        int cityCount = WorldboxGame.I.GeoRegions.GetCitiesInRegion(region, int.MaxValue).Count;

        SetTextStat(_categoryStat, category.GetSpriteIcon(), category.GetDisplayName(), "Cultiway.GeoRegion.Category");
        SetTextStat(_layerStat, LoadUiIcon("iconWorldInfo"), GeoRegionSelectedTagsContainer.FormatLayer(region.data.Layer), "Cultiway.GeoRegion.Layer");
        SetNumericStat(_tilesStat, LoadUiIcon("iconZones"), region.data.TileCount, "Cultiway.GeoRegion.Tiles");
        SetTextStat(_centerStat, LoadUiIcon("iconCityZones"), $"{region.data.CenterX}, {region.data.CenterY}", "Cultiway.GeoRegion.Center");
        SetNumericStat(_ageStat, LoadUiIcon("iconAge"), region.getAge(), "Cultiway.GeoRegion.Age");
        SetNumericStat(_citiesStat, LoadUiIcon("iconCity"), cityCount, "Cultiway.GeoRegion.Cities");

        _banner.enable_default_click = false;
        _banner.enable_tab_show_click = true;
        _banner.load(region);
        _bannerHover.Setup(region);
    }

    private static void SetTextStat(StatsIcon stat, Sprite icon, string value, string titleKey)
    {
        stat.gameObject.SetActive(true);
        stat.enable_animation = false;
        stat.checkDestroyTween();

        Image iconImage = stat.getIcon();
        iconImage.sprite = icon;
        iconImage.preserveAspect = true;
        iconImage.gameObject.SetActive(icon != null);

        ApplyTextStyle(stat.text, value);
        SetupTooltip(stat, titleKey, value);
    }

    private static void SetNumericStat(StatsIcon stat, Sprite icon, int value, string titleKey)
    {
        stat.gameObject.SetActive(true);
        stat.enable_animation = true;

        Image iconImage = stat.getIcon();
        iconImage.sprite = icon;
        iconImage.preserveAspect = true;
        iconImage.gameObject.SetActive(icon != null);

        stat.text.fontStyle = FontStyle.Bold;
        stat.text.fontSize = HeaderTextMaxSize;
        stat.text.resizeTextForBestFit = true;
        stat.text.resizeTextMinSize = HeaderTextMinSize;
        stat.text.resizeTextMaxSize = HeaderTextMaxSize;
        stat.setValue(value);
        SetupTooltip(stat, titleKey, value.ToString());
    }

    private static void ApplyTextStyle(Text text, string value)
    {
        text.text = value;
        text.color = HeaderValueColor;
        text.fontStyle = FontStyle.Bold;
        text.fontSize = HeaderTextMaxSize;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = HeaderTextMinSize;
        text.resizeTextMaxSize = HeaderTextMaxSize;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }

    private static void SetupTooltip(StatsIcon stat, string titleKey, string value)
    {
        TipButton tipButton = stat.GetComponent<TipButton>() ?? stat.gameObject.AddComponent<TipButton>();
        string title = LMTools.GetOrKey(titleKey);
        tipButton.textOnClick = string.Empty;
        tipButton.textOnClickDescription = string.Empty;
        tipButton.text_description_2 = string.Empty;
        tipButton.setHoverAction(
            () => Tooltip.show(
                stat.gameObject,
                WorldboxGame.Tooltips.RawTip.id,
                new TooltipData
                {
                    tip_name = title,
                    tip_description = value
                }));
    }

    private static StatsIcon RequireStat(Transform column, string name)
    {
        Transform row = column.Find(name)
                        ?? throw new InvalidOperationException($"GeoRegionWindow 原版 Header 缺少统计条: {name}");
        row.gameObject.SetActive(true);

        StatsIcon stat = row.GetComponent<StatsIcon>()
                         ?? throw new InvalidOperationException($"GeoRegionWindow 原版 Header 统计条缺少 StatsIcon: {name}");
        if (stat.text == null)
        {
            throw new InvalidOperationException($"GeoRegionWindow 原版 Header 统计条缺少 Text 引用: {name}");
        }

        return stat;
    }

    private static GeoRegionBanner SetupBanner(Transform bannerContainer)
    {
        Transform bannerTransform = bannerContainer.Find("Main Banner")
                                    ?? throw new InvalidOperationException("GeoRegionWindow 原版 Header 缺少 Main Banner 节点");
        bannerTransform.gameObject.SetActive(true);

        GeoRegionBanner geoRegionBanner = bannerTransform.GetComponent<GeoRegionBanner>();
        if (geoRegionBanner != null) return geoRegionBanner;

        KingdomBanner kingdomBanner = bannerTransform.GetComponent<KingdomBanner>();
        geoRegionBanner = bannerTransform.gameObject.AddComponent<GeoRegionBanner>();
        if (kingdomBanner != null)
        {
            kingdomBanner.CopyCompatibleSerializedFieldsTo(geoRegionBanner);
            Object.DestroyImmediate(kingdomBanner);
        }

        return geoRegionBanner;
    }

    private static void HideOriginalHeaderIcons(Transform bannerContainer)
    {
        bannerContainer.HideChildrenByPath("easter_egg_container");
    }

    private static Sprite LoadUiIcon(string iconName)
    {
        return SpriteTextureLoader.getSprite("ui/Icons/" + iconName);
    }
}
