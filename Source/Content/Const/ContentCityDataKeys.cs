// ReSharper disable InconsistentNaming

namespace Cultiway.Content.Const;

/// <summary>
/// 城市(City)级别的数据键，对应 <c>city.data</c>（CityData / MetaObjectData）。
/// </summary>
public static class ContentCityDataKeys
{
    /// <summary>城市城墙阶段：0=无墙，1=仅内墙(木)，2=内墙(石,宽2)+外墙(木)+箭塔，3=内墙(石,宽2)+外墙(石,宽2)。</summary>
    public const string CityWallStage_int = "cw.content.city.wall_stage";

    /// <summary>建内墙时记录的内墙半径（此后固定，不随城市扩张变化）。0=未记录。</summary>
    public const string CityWallInnerRadius_int = "cw.content.city.wall_inner_radius";
}
