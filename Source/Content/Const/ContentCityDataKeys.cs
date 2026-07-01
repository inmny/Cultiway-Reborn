// ReSharper disable InconsistentNaming

namespace Cultiway.Content.Const;

/// <summary>
/// 城市(City)级别的数据键，对应 <c>city.data</c>（CityData / MetaObjectData）。
/// </summary>
public static class ContentCityDataKeys
{
    /// <summary>城市城墙阶段：0=无墙，1=仅内墙(木)，2=内墙(石,宽2)+外墙(木)，3=内墙(石,宽2)+外墙(石,宽2)。</summary>
    public const string CityWallStage_int = "cw.content.city.wall_stage";

    /// <summary>内墙矩形 bounds（中心 cx/cy + 半宽 hx/hy），木墙阶段记录后固定。与 CityWallStage_int 配套。</summary>
    public const string CityWallInnerCX_int = "cw.content.city.wall_inner_cx";
    public const string CityWallInnerCY_int = "cw.content.city.wall_inner_cy";
    public const string CityWallInnerHX_int = "cw.content.city.wall_inner_hx";
    public const string CityWallInnerHY_int = "cw.content.city.wall_inner_hy";

    /// <summary>外墙矩形 bounds（动态，每次修外墙时更新）。用于拆除上一轮旧外墙。</summary>
    public const string CityWallOuterCX_int = "cw.content.city.wall_outer_cx";
    public const string CityWallOuterCY_int = "cw.content.city.wall_outer_cy";
    public const string CityWallOuterHX_int = "cw.content.city.wall_outer_hx";
    public const string CityWallOuterHY_int = "cw.content.city.wall_outer_hy";

    /// <summary>篝火丢失标记：篝火被摧毁时置 1，重建后用于触发城墙重置（回 stage 0）。</summary>
    public const string CityWallBonfireLost_int = "cw.content.city.wall_bonfire_lost";
}
