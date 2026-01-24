using db.tables;
using SQLite;

namespace Cultiway.Tables;

public class GeoRegionTable : HistoryTable, IPopulationTable
{
    [Column(nameof(population))]
    public long? population { get; set; }
    [Column(nameof(adults))]
    public long? adults { get; set; }
    [Column(nameof(children))]
    public long? children { get; set; }
}

public class GeoRegionTableYearly1 : GeoRegionTable;
public class GeoRegionTableYearly5 : GeoRegionTable;
public class GeoRegionTableYearly10 : GeoRegionTable;
public class GeoRegionTableYearly50 : GeoRegionTable;
public class GeoRegionTableYearly100 : GeoRegionTable;
public class GeoRegionTableYearly500 : GeoRegionTable;
public class GeoRegionTableYearly1000 : GeoRegionTable;
public class GeoRegionTableYearly5000 : GeoRegionTable;
public class GeoRegionTableYearly10000 : GeoRegionTable;
