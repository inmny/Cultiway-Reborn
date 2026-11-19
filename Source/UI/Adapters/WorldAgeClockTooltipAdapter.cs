using System.Globalization;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Performance;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>为原版 WorldAgeClock 提供基于实际世界时间推进量的现实时间换算。</summary>
internal sealed class WorldAgeClockTooltipAdapter : MonoBehaviour
{
    /// <summary>原版时钟和悬停事件已绑定。</summary>
    private bool bound;
    /// <summary>年份进度条下方的日期和小时。</summary>
    private Text dayHour;
    /// <summary>用于同步原版年份文字的时钟。</summary>
    private UiWorldAgeInfo clock;
    /// <summary>已格式化的年份。</summary>
    private int shownYear = -1;
    /// <summary>当前语言下的年份文字。</summary>
    private string yearLabel;
    /// <summary>上次显示的日和小时。</summary>
    private (int day, int hour) shownTime = (-1, -1);
    /// <summary>上次显示文字使用的语言。</summary>
    private string shownLanguage;

    /// <summary>在原版时钟上只安装一份日期和换算提示。</summary>
    /// <param name="worldAgeInfo">原版纪元时钟。</param>
    public static void Attach(UiWorldAgeInfo worldAgeInfo)
    {
        WorldAgeClockTooltipAdapter adapter =
            worldAgeInfo.GetComponent<WorldAgeClockTooltipAdapter>()
            ?? worldAgeInfo.gameObject.AddComponent<WorldAgeClockTooltipAdapter>();
        adapter.Bind(worldAgeInfo);
    }

    /// <summary>根据原版年份条的位置建立紧凑日期行，并保留原有悬停操作。</summary>
    /// <param name="worldAgeInfo">用于取得原版字体和布局的时钟。</param>
    private void Bind(UiWorldAgeInfo worldAgeInfo)
    {
        if (bound)
        {
            return;
        }

        bound = true;
        clock = worldAgeInfo;
        var yearBar = (RectTransform)worldAgeInfo._bar_year.transform;
        float height = UiTheme.Current.Metrics.SpacingMd;
        dayHour = UiElements.CreateText(transform, "Calendar-DayHour", "", yearBar.rect.width, height,
            worldAgeInfo._text_year.fontSize, TextAnchor.MiddleCenter);
        dayHour.font = worldAgeInfo._text_year.font;
        dayHour.color = worldAgeInfo._text_year.color;
        dayHour.raycastTarget = false;
        var rect = dayHour.rectTransform;
        rect.anchorMin = rect.anchorMax = yearBar.anchorMin;
        rect.anchoredPosition = yearBar.anchoredPosition - new Vector2(0, yearBar.rect.height * yearBar.pivot.y + height / 2);
        UiTooltip.Set(gameObject, ShowTooltip);
        TipButton tipButton = GetComponent<TipButton>();
        tipButton.showOnClick = false;
        tipButton.setHoverAction(ShowTooltip, false);
    }

    /// <summary>日期或语言变化时更新一行文字，暂停时自然保持原值。</summary>
    private void LateUpdate()
    {
        if (!bound || !Config.game_loaded || World.world?.map_stats == null) return;
        double now = World.world.getCurWorldTime();
        var value = (WorldCalendar.Day(now), WorldCalendar.Hour(now));
        string language = LocalizedTextManager.instance.language;
        int year = WorldCalendar.Year(now);
        if (year != shownYear || language != shownLanguage)
        {
            shownYear = year;
            yearLabel = LocalizedTextManager.getText("year_era").Replace("$current_era_year$", year.ToText());
        }
        if (clock._text_year.text != yearLabel) clock._text_year.text = yearLabel;
        if (value == shownTime && language == shownLanguage) return;
        shownTime = value; shownLanguage = language;
        dayHour.text = FormatLocalized("cultiway_world_age_clock_day_hour", value.Item1, value.Item2);
    }

    /// <summary>以当前日历和实际推进速度换算现实时间。</summary>
    private void ShowTooltip()
    {
        string title = "Cultiway.WorldAgeClock.Tooltip.Title".Localize();
        string description;
        string detail;
        float requestedSpeed = WorldTimeRateTracker.GetRequestedSpeed();
        float actualSpeed = WorldTimeRateTracker.ActualSpeed;

        MapBox world = World.world;
        if (!Config.game_loaded || world?.map_stats == null)
        {
            description = "Cultiway.WorldAgeClock.Tooltip.WorldUnavailable".Localize();
            detail = string.Empty;
        }
        else if (world.isPaused())
        {
            description = "Cultiway.WorldAgeClock.Tooltip.Paused".Localize();
            detail = FormatLocalized(
                "Cultiway.WorldAgeClock.Tooltip.ConfiguredSpeed",
                FormatNumber(requestedSpeed));
        }
        else if (!WorldTimeRateTracker.HasActualSpeed)
        {
            description = "Cultiway.WorldAgeClock.Tooltip.Measuring".Localize();
            detail = FormatLocalized(
                "Cultiway.WorldAgeClock.Tooltip.ConfiguredSpeed",
                FormatNumber(requestedSpeed));
        }
        else if (actualSpeed <= 0.001f)
        {
            description = "Cultiway.WorldAgeClock.Tooltip.NotAdvancing".Localize();
            detail = FormatLocalized(
                "Cultiway.WorldAgeClock.Tooltip.Speed",
                FormatNumber(actualSpeed),
                FormatNumber(requestedSpeed));
        }
        else
        {
            string gameDuration = FormatGameDuration(actualSpeed);
            string realDuration = FormatRealDuration(TimeScales.SecPerYear / actualSpeed);
            description =
                FormatLocalized("Cultiway.WorldAgeClock.Tooltip.RealToGame", gameDuration) +
                "\n" +
                FormatLocalized("Cultiway.WorldAgeClock.Tooltip.GameToReal", realDuration);
            detail = FormatLocalized(
                "Cultiway.WorldAgeClock.Tooltip.Speed",
                FormatNumber(actualSpeed),
                FormatNumber(requestedSpeed));
        }

        Tooltip.show(gameObject, WorldboxGame.Tooltips.RawTip.id, new TooltipData
        {
            tip_name = title,
            tip_description = description,
            tip_description_2 = detail
        });
    }

    /// <summary>把模拟时长换算成合适的日历单位。</summary>
    /// <param name="worldSeconds">一段原始模拟秒。</param>
    private static string FormatGameDuration(double worldSeconds)
    {
        double days = worldSeconds / TimeScales.SecPerDay;
        if (days >= 360.0)
        {
            return FormatLocalized(
                "Cultiway.WorldAgeClock.Unit.GameYears",
                FormatNumber(worldSeconds / TimeScales.SecPerYear));
        }

        if (days >= 30.0)
        {
            return FormatLocalized(
                "Cultiway.WorldAgeClock.Unit.GameMonths",
                FormatNumber(worldSeconds / TimeScales.SecPerMonth));
        }

        if (days < 1.0)
            return FormatLocalized("cultiway_world_age_clock_unit_game_hours", FormatNumber(worldSeconds / TimeScales.SecPerHour));

        return FormatLocalized(
            "Cultiway.WorldAgeClock.Unit.GameDays",
            FormatNumber(days));
    }

    /// <summary>把现实等待时间显示成秒、分钟或小时。</summary>
    /// <param name="seconds">现实秒数。</param>
    private static string FormatRealDuration(double seconds)
    {
        if (seconds >= 3600.0)
        {
            return FormatLocalized(
                "Cultiway.WorldAgeClock.Unit.RealHours",
                FormatNumber(seconds / 3600.0));
        }

        if (seconds >= 60.0)
        {
            return FormatLocalized(
                "Cultiway.WorldAgeClock.Unit.RealMinutes",
                FormatNumber(seconds / 60.0));
        }

        return FormatLocalized(
            "Cultiway.WorldAgeClock.Unit.RealSeconds",
            FormatNumber(seconds));
    }

    /// <summary>按当前语言格式化日历文字。</summary>
    /// <param name="key">文字键。</param>
    /// <param name="arguments">需要填入的数值。</param>
    private static string FormatLocalized(string key, params object[] arguments)
    {
        return string.Format(LocalizedTextManager.getCulture(), key.Localize(), arguments);
    }

    /// <summary>数字最多显示两位小数。</summary>
    /// <param name="value">需要显示的数值。</param>
    private static string FormatNumber(double value)
    {
        CultureInfo culture = LocalizedTextManager.getCulture();
        return value.ToString("0.##", culture);
    }
}
