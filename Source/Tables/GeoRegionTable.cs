using db.tables;
using SQLite;

namespace Cultiway.Tables;

/// <summary>保存地区在某个历史时间点的人口概况，供统计图和历史查询使用。</summary>
public class GeoRegionTable : HistoryTable, IPopulationTable
{
    /// <summary>该地区当时的总人口。</summary>
    [Column(nameof(population))]
    public long? population { get; set; }
    /// <summary>该地区当时的成年人口。</summary>
    [Column(nameof(adults))]
    public long? adults { get; set; }
    /// <summary>该地区当时的儿童人口。</summary>
    [Column(nameof(children))]
    public long? children { get; set; }
}

/// <summary>每年保存一次的地区历史记录。</summary>
public class GeoRegionTableYearly1 : GeoRegionTable;
/// <summary>每五年保存一次的地区历史记录。</summary>
public class GeoRegionTableYearly5 : GeoRegionTable;
/// <summary>每十年保存一次的地区历史记录。</summary>
public class GeoRegionTableYearly10 : GeoRegionTable;
/// <summary>每五十年保存一次的地区历史记录。</summary>
public class GeoRegionTableYearly50 : GeoRegionTable;
/// <summary>每一百年保存一次的地区历史记录。</summary>
public class GeoRegionTableYearly100 : GeoRegionTable;
/// <summary>每五百年保存一次的地区历史记录。</summary>
public class GeoRegionTableYearly500 : GeoRegionTable;
/// <summary>每一千年保存一次的地区历史记录。</summary>
public class GeoRegionTableYearly1000 : GeoRegionTable;
/// <summary>每五千年保存一次的地区历史记录。</summary>
public class GeoRegionTableYearly5000 : GeoRegionTable;
/// <summary>每一万年保存一次的地区历史记录。</summary>
public class GeoRegionTableYearly10000 : GeoRegionTable;
