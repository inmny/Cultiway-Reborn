namespace Cultiway.Const;

/// <summary>世界日历的统一时间长度，动作和移动仍使用原来的模拟秒。</summary>
public static class TimeScales
{
    /// <summary>每天十二时。</summary>
    public const int HoursPerDay = 12;
    /// <summary>每月三十天。</summary>
    public const int DaysPerMonth = 30;
    /// <summary>每年十二个月。</summary>
    public const int MonthsPerYear = 12;
    /// <summary>未配置世界默认六秒一天。</summary>
    public const float DefaultSecPerDay = 6f;
    /// <summary>当前世界一倍速下每天所需的模拟秒。</summary>
    public static float SecPerDay => Cultiway.Core.WorldCalendar.DaySeconds;
    /// <summary>一小时所需的模拟秒。</summary>
    public static float SecPerHour => SecPerDay / HoursPerDay;
    /// <summary>一个月所需的模拟秒。</summary>
    public static float SecPerMonth => SecPerDay * DaysPerMonth;
    /// <summary>一年所需的模拟秒。</summary>
    public static float SecPerYear => SecPerMonth * MonthsPerYear;
    /// <summary>新日历相对于原来年月的放慢倍数。</summary>
    public static float CalendarRatio => SecPerYear / 60f;
    /// <summary>是否采用逐步模拟。</summary>
    public static bool precise_simulate = false;
}
