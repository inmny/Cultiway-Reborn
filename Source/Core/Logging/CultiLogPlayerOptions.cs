namespace Cultiway.Core.Logging;

public static class CultiLogPlayerOptions
{
    public const string Enabled = "cultiway_log_enabled";
    public const string DiskEnabled = "cultiway_log_disk_enabled";

    public static readonly CultiLogCategory[] Categories =
    [
        CultiLogCategory.General,
        CultiLogCategory.Combat,
        CultiLogCategory.Sect,
        CultiLogCategory.Cultivation,
        CultiLogCategory.Book,
        CultiLogCategory.Skill,
        CultiLogCategory.Pathfinding,
        CultiLogCategory.Item,
        CultiLogCategory.Train,
        CultiLogCategory.Geo,
        CultiLogCategory.AI,
        CultiLogCategory.UI,
        CultiLogCategory.Perf,
        CultiLogCategory.AIGC,
        CultiLogCategory.Error
    ];

    public static string GetCategoryOptionId(CultiLogCategory category)
    {
        return category switch
        {
            CultiLogCategory.General => "cultiway_log_cat_general",
            CultiLogCategory.Combat => "cultiway_log_cat_combat",
            CultiLogCategory.Sect => "cultiway_log_cat_sect",
            CultiLogCategory.Cultivation => "cultiway_log_cat_cultivation",
            CultiLogCategory.Book => "cultiway_log_cat_book",
            CultiLogCategory.Skill => "cultiway_log_cat_skill",
            CultiLogCategory.Pathfinding => "cultiway_log_cat_pathfinding",
            CultiLogCategory.Item => "cultiway_log_cat_item",
            CultiLogCategory.Train => "cultiway_log_cat_train",
            CultiLogCategory.Geo => "cultiway_log_cat_geo",
            CultiLogCategory.AI => "cultiway_log_cat_ai",
            CultiLogCategory.UI => "cultiway_log_cat_ui",
            CultiLogCategory.Perf => "cultiway_log_cat_perf",
            CultiLogCategory.AIGC => "cultiway_log_cat_aigc",
            CultiLogCategory.Error => "cultiway_log_cat_error",
            _ => "cultiway_log_cat_none"
        };
    }
}
