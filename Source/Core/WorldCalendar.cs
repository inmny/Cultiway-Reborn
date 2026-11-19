using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Cultiway.Const;
using Newtonsoft.Json;

namespace Cultiway.Core;

/// <summary>按每次日长调整的实际生效时刻累计日期，保留已有年龄和作物月龄。</summary>
public static class WorldCalendar
{
    /// <summary>最初启用日历的历史时刻。</summary>
    public const string StartKey = "cw.calendar.start";
    /// <summary>曾采用每日一百二十秒的历史时刻。</summary>
    public const string SlowStartKey = "cw.calendar.half_speed_start";
    /// <summary>曾采用每日十二秒的历史时刻。</summary>
    public const string DayRateStartKey = "cw.calendar.day_rate_start";
    /// <summary>曾采用每日六秒的历史时刻。</summary>
    public const string SixSecondStartKey = "cw.calendar.six_second_day_start";
    /// <summary>世界唯一的可编辑日长记录。</summary>
    public const string SaveKey = "cw.calendar.day_lengths";
    /// <summary>缓存所属世界。</summary>
    private static MapStats currentWorld;
    /// <summary>按生效时刻排序的日长记录，历史查询通过二分查找完成。</summary>
    private static List<DayLength> lengths;

    /// <summary>一次日长调整和该时刻已经走过的天数。</summary>
    private sealed class DayLength
    {
        /// <summary>生效时的原始世界秒。</summary>
        public double Start;
        /// <summary>生效时累计的日历天数。</summary>
        public double Days;
        /// <summary>从此时开始每天需要的模拟秒。</summary>
        public float Seconds;
    }

    /// <summary>当前世界采用的日长，主菜单采用默认值。</summary>
    public static float DaySeconds
    {
        get
        {
            if (World.world?.map_stats == null) return TimeScales.DefaultSecPerDay;
            Load(); return lengths[lengths.Count - 1].Seconds;
        }
    }

    /// <summary>玩家输入和保存记录共用的日长边界。</summary>
    /// <param name="seconds">一倍速下每天所需秒数。</param>
    public static bool ValidDaySeconds(float seconds) => !float.IsNaN(seconds) && !float.IsInfinity(seconds) &&
        seconds >= .1f && seconds <= 86400f;

    /// <summary>只影响此刻以后的日期，不重写人物、作物或历史时间戳。</summary>
    /// <param name="seconds">一倍速下每天所需秒数。</param>
    public static bool SetDaySeconds(float seconds)
    {
        if (!ValidDaySeconds(seconds) || World.world?.map_stats == null) return false;
        Load();
        var last = lengths[lengths.Count - 1];
        if (last.Seconds == seconds) return true;
        double now = World.world.getCurWorldTime(), days = DaysAt(now);
        if (last.Start == now) last.Seconds = seconds;
        else lengths.Add(new DayLength { Start = now, Days = days, Seconds = seconds });
        Save(); return true;
    }

    /// <summary>读取本世界记录；此前各次固定日长形成同一条实际计时历史。</summary>
    private static void Load()
    {
        var stats = World.world?.map_stats;
        if (stats == null)
        {
            currentWorld = null;
            lengths = new List<DayLength> { new() { Seconds = TimeScales.DefaultSecPerDay } };
            return;
        }
        if (ReferenceEquals(currentWorld, stats)) return;
        stats.custom_data ??= new SaveCustomData();
        stats.custom_data.get(SaveKey, out string json, null);
        lengths = json == null ? new List<DayLength>() : JsonConvert.DeserializeObject<List<DayLength>>(json);
        if (lengths == null) throw new InvalidDataException("世界日长记录无效。");
        if (json == null)
        {
            // 先保留实际经历过的固定流速，再从此刻开始采用可编辑日长。
            AddRecorded(StartKey, 60); AddRecorded(SlowStartKey, 120);
            AddRecorded(DayRateStartKey, 12); AddRecorded(SixSecondStartKey, 6);
            if (lengths.Count == 0 || lengths[lengths.Count - 1].Seconds != TimeScales.DefaultSecPerDay)
            {
                double now = stats.world_time;
                lengths.Add(new DayLength { Start = now, Days = DaysAt(now), Seconds = TimeScales.DefaultSecPerDay });
            }
        }
        if (lengths.Count == 0) throw new InvalidDataException("世界日长记录为空。");
        double previous = double.NegativeInfinity;
        foreach (var length in lengths)
        {
            if (length == null || !ValidDaySeconds(length.Seconds) || double.IsNaN(length.Start) ||
                double.IsInfinity(length.Start) || double.IsNaN(length.Days) || double.IsInfinity(length.Days) ||
                length.Start < previous) throw new InvalidDataException("世界日长或生效顺序无效。");
            previous = length.Start;
        }
        currentWorld = stats;
        if (json == null) Save();
    }

    /// <summary>保留本世界确实保存过的流速变化，未经历的阶段不添加。</summary>
    /// <param name="key">此前生效时刻的保存编号。</param>
    /// <param name="seconds">该阶段实际使用的日长。</param>
    private static void AddRecorded(string key, float seconds)
    {
        World.world.map_stats.custom_data.get(key, out string saved, null);
        if (saved == null) return;
        double start = double.Parse(saved, CultureInfo.InvariantCulture);
        lengths.Add(new DayLength { Start = start, Days = DaysAt(start), Seconds = seconds });
    }

    /// <summary>将日长历史随世界保存，日常查询不重复写入。</summary>
    private static void Save() => currentWorld.custom_data.set(SaveKey, JsonConvert.SerializeObject(lengths));

    /// <summary>查找给定原始时刻属于哪段日长，负数出生时间沿用原版历史。</summary>
    /// <param name="timestamp">原始世界时间戳。</param>
    private static double DaysAt(double timestamp)
    {
        int low = 0, high = lengths.Count - 1, found = -1;
        while (low <= high)
        {
            int middle = (low + high) / 2;
            if (lengths[middle].Start <= timestamp) { found = middle; low = middle + 1; }
            else high = middle - 1;
        }
        if (found < 0) return timestamp * 6d;
        var length = lengths[found];
        return length.Days + (timestamp - length.Start) / length.Seconds;
    }

    /// <summary>把原始时刻换算为当前日长下的日历秒。</summary>
    /// <param name="timestamp">原始世界时间戳。</param>
    public static double CalendarTime(double timestamp) { Load(); return DaysAt(timestamp) * DaySeconds; }

    /// <summary>给定时刻的年份，从第一年开始。</summary>
    /// <param name="timestamp">原始世界时间戳。</param>
    public static int Year(double timestamp) { Load(); return (int)Math.Floor(DaysAt(timestamp) / 360d) + 1; }
    /// <summary>给定时刻的月份，一至十二。</summary>
    /// <param name="timestamp">原始世界时间戳。</param>
    public static int Month(double timestamp) => Part(timestamp, 30d, 12) + 1;
    /// <summary>给定时刻的日期，一至三十。</summary>
    /// <param name="timestamp">原始世界时间戳。</param>
    public static int Day(double timestamp) => Part(timestamp, 1d, 30) + 1;
    /// <summary>给定时刻的时数，零至十一。</summary>
    /// <param name="timestamp">原始世界时间戳。</param>
    public static int Hour(double timestamp) => Part(timestamp, 1d / 12d, 12);

    /// <summary>按天数分段，负数历史时间也返回合法年月日时。</summary>
    /// <param name="timestamp">原始世界时间戳。</param>
    /// <param name="unitDays">一个分段的天数。</param>
    /// <param name="count">周期内分段数量。</param>
    private static int Part(double timestamp, double unitDays, int count)
    {
        Load(); double part = Math.Floor(DaysAt(timestamp) / unitDays) % count;
        return (int)(part < 0 ? part + count : part);
    }

    /// <summary>实际经过的完整年数。</summary>
    /// <param name="timestamp">出生或事件开始的原始时间戳。</param>
    public static int YearsSince(double timestamp) => (int)(ElapsedDays(timestamp) / 360d);
    /// <summary>实际经过的月数，保留小数用于作物生长。</summary>
    /// <param name="timestamp">播种或事件开始的原始时间戳。</param>
    public static double MonthsSince(double timestamp) => ElapsedDays(timestamp) / 30d;
    /// <summary>跨越日长变化累计实际日历天数。</summary>
    /// <param name="timestamp">事件开始时刻。</param>
    private static double ElapsedDays(double timestamp) { Load(); return DaysAt(World.world.getCurWorldTime()) - DaysAt(timestamp); }
    /// <summary>月份进度条仍使用原版固定的零至五范围。</summary>
    public static float MonthBarTime
    { get { Load(); return (float)((DaysAt(World.world.getCurWorldTime()) % 30d + 30d) % 30d / 6d); } }
}
