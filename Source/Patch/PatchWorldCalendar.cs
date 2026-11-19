using System.Collections.Generic;
using System.Reflection.Emit;
using Cultiway.Const;
using Cultiway.Core;
using HarmonyLib;

namespace Cultiway.Patch;

/// <summary>原版日期、人物年龄与历史年份使用同一套日历，不改变动作计时。</summary>
internal static class PatchWorldCalendar
{
    /// <summary>年月日历史显示。</summary>
    /// <param name="pTime">原始时间戳。</param>
    /// <param name="__result">原版按日、月、年排列的结果。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(Date), nameof(Date.getRawDate))]
    private static bool RawDate(double pTime, ref int[] __result)
    { __result = new[] { WorldCalendar.Day(pTime), WorldCalendar.Month(pTime), WorldCalendar.Year(pTime) }; return false; }

    /// <summary>从第一年开始的年份。</summary>
    /// <param name="pTime">原始时间戳。</param>
    /// <param name="__result">年份。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(Date), nameof(Date.getYear))]
    private static bool Year(double pTime, ref int __result) { __result = WorldCalendar.Year(pTime); return false; }

    /// <summary>从零开始的年份。</summary>
    /// <param name="pTime">原始时间戳。</param>
    /// <param name="__result">年份。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(Date), nameof(Date.getYear0))]
    private static bool YearZero(double pTime, ref int __result) { __result = WorldCalendar.Year(pTime) - 1; return false; }

    /// <summary>当前世界年份。</summary>
    /// <param name="__result">年份。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(Date), nameof(Date.getCurrentYear))]
    private static bool CurrentYear(ref int __result) { __result = WorldCalendar.Year(World.world.getCurWorldTime()); return false; }

    /// <summary>当前世界月份。</summary>
    /// <param name="__result">月份。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(Date), nameof(Date.getCurrentMonth))]
    private static bool CurrentMonth(ref int __result) { __result = WorldCalendar.Month(World.world.getCurWorldTime()); return false; }

    /// <summary>当前月份。</summary>
    /// <param name="pTimestamp">原始时间戳。</param>
    /// <param name="__result">月份。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(Date), nameof(Date.getMonth))]
    private static bool Month(double pTimestamp, ref int __result) { __result = WorldCalendar.Month(pTimestamp); return false; }

    /// <summary>实际经过的年数，人物寿命和年龄沿用原版年龄加成。</summary>
    /// <param name="pFrom">事件开始时刻。</param>
    /// <param name="__result">完整年数。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(Date), nameof(Date.getYearsSince))]
    private static bool YearsSince(double pFrom, ref int __result) { __result = WorldCalendar.YearsSince(pFrom); return false; }

    /// <summary>实际经过的完整月数。</summary>
    /// <param name="pFrom">事件开始时刻。</param>
    /// <param name="__result">完整月数。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(Date), nameof(Date.getMonthsSince))]
    private static bool MonthsSince(double pFrom, ref int __result) { __result = (int)WorldCalendar.MonthsSince(pFrom); return false; }

    /// <summary>让原版月份进度条继续使用其固定的五单位范围。</summary>
    /// <param name="__result">月内进度。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(Date), nameof(Date.getMonthTime))]
    private static bool MonthBar(ref float __result) { __result = WorldCalendar.MonthBarTime; return false; }

    /// <summary>明确在原始计时推进后更新年月缓存，避免已编译的原版快速方法继续使用旧换算。</summary>
    /// <param name="__instance">当前世界时间记录。</param>
    /// <param name="pElapsed">本次动作模拟秒。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(MapStats), nameof(MapStats.updateWorldTime))]
    private static bool Tick(MapStats __instance, float pElapsed)
    {
        __instance.world_time += pElapsed;
        int year = WorldCalendar.Year(__instance.world_time);
        if (__instance._last_year != year) World.world.updateObjectAge();
        __instance._last_year = year;
        __instance._last_month = WorldCalendar.Month(__instance.world_time);
        return false;
    }

    /// <summary>原版人物年龄入口直接读取新日历，已生成成年人物的年龄加成仍然有效。</summary>
    /// <param name="__instance">人物保存资料。</param>
    /// <param name="__result">实际年龄。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(ActorData), nameof(ActorData.getAge))]
    private static bool ActorAge(ActorData __instance, ref int __result)
    { __result = WorldCalendar.YearsSince(__instance.created_time) + __instance.age_overgrowth; return false; }

    /// <summary>纪元进度也按新年长推进，天气与光照动作仍按原速度更新。</summary>
    /// <param name="instructions">原版纪元更新指令。</param>
    [HarmonyTranspiler, HarmonyPatch(typeof(WorldAgeManager), nameof(WorldAgeManager.update))]
    private static IEnumerable<CodeInstruction> EraProgress(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (!instruction.LoadsField(AccessTools.Field(typeof(MapStats), nameof(MapStats.current_world_ages_duration)))) continue;
            yield return new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(TimeScales), nameof(TimeScales.CalendarRatio)));
            yield return new CodeInstruction(OpCodes.Mul);
        }
    }
}
