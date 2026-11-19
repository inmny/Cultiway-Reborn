namespace Cultiway.Content.FamilyFarming;

/// <summary>两季麦作的固定月份规则，月份使用一至十二月。</summary>
public static class FarmSeasonRules
{
    /// <summary>春小麦类型编号。</summary>
    public const int Spring = 1;
    /// <summary>冬小麦类型编号。</summary>
    public const int Winter = 2;
    /// <summary>按月份决定可播种的麦类，零表示不在播种期。</summary>
    /// <param name="month">当前月份。</param>
    public static int SowingSeason(int month) => month is 3 or 4 ? Spring : month is >= 9 and <= 11 ? Winter : 0;
    /// <summary>从实际播种时刻到成熟所需的月数。</summary>
    /// <param name="season">小麦类型。</param>
    public static int GrowingMonths(int season) => season == Winter ? 7 : 4;
    /// <summary>达到生长期且处于对应收获月份才允许正常收割。</summary>
    /// <param name="season">小麦类型。</param>
    /// <param name="ageMonths">实际生长月数。</param>
    /// <param name="month">当前月份。</param>
    public static bool CanHarvest(int season, double ageMonths, int month) => ageMonths >= GrowingMonths(season) &&
        (season == Winter ? month is 5 or 6 : month is >= 7 and <= 9);
    /// <summary>任一麦类正常收获月份，供收粮收尾使用。</summary>
    /// <param name="month">当前月份。</param>
    public static bool HarvestMonth(int month) => month is >= 5 and <= 9;
}
