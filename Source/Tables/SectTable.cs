using db.tables;
using SQLite;

namespace Cultiway.Tables;

public class SectTable : HistoryTable, IPopulationTable
{
    [Column(nameof(population))]
    public long? population { get; set; }
    [Column(nameof(adults))]
    public long? adults { get; set; }
    [Column(nameof(children))]
    public long? children { get; set; }
}

public class SectTableYearly1 : SectTable;
public class SectTableYearly5 : SectTable;
public class SectTableYearly10 : SectTable;
public class SectTableYearly50 : SectTable;
public class SectTableYearly100 : SectTable;
public class SectTableYearly500 : SectTable;
public class SectTableYearly1000 : SectTable;
public class SectTableYearly5000 : SectTable;
public class SectTableYearly10000 : SectTable;