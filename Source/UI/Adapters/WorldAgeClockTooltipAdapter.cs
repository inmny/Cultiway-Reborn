using System;
using System.Globalization;
using UnityEngine;

namespace Cultiway.UI;

/// <summary>为原版 WorldAgeClock 提供基于实际世界时间推进量的现实时间换算。</summary>
internal sealed class WorldAgeClockTooltipAdapter : MonoBehaviour
{
    private const float SampleWindowSeconds = 0.75f;
    private const float SampleSmoothing = 0.35f;
    private const double WorldSecondsPerMonth = 5.0;
    private const double WorldSecondsPerYear = 60.0;
    private const double GameDaysPerWorldSecond = 6.0;

    private MapStats sampledMapStats;
    private int sampledWorldSeedId = -1;
    private double previousWorldTime;
    private double accumulatedWorldTime;
    private float accumulatedRealTime;
    private float actualSpeed;
    private float sampledRequestedSpeed = -1f;
    private bool hasActualSpeed;
    private bool sampledPaused;
    private bool bound;

    public static void Attach(UiWorldAgeInfo worldAgeInfo)
    {
        WorldAgeClockTooltipAdapter adapter =
            worldAgeInfo.GetComponent<WorldAgeClockTooltipAdapter>()
            ?? worldAgeInfo.gameObject.AddComponent<WorldAgeClockTooltipAdapter>();
        adapter.Bind();
    }

    private void Bind()
    {
        if (bound)
        {
            return;
        }

        bound = true;
        UiTooltip.Set(gameObject, ShowTooltip);
        TipButton tipButton = GetComponent<TipButton>();
        tipButton.showOnClick = false;
        tipButton.setHoverAction(ShowTooltip, false);
        ResetMeasurement();
    }

    private void OnEnable()
    {
        ResetMeasurement();
    }

    private void Update()
    {
        if (!bound)
        {
            return;
        }

        MapBox world = World.world;
        MapStats mapStats = world?.map_stats;
        if (!Config.game_loaded || mapStats == null)
        {
            ResetMeasurement();
            return;
        }

        int worldSeedId = MapBox.current_world_seed_id;
        double worldTime = mapStats.world_time;
        bool paused = world.isPaused();
        float requestedSpeed = GetRequestedSpeed();
        if (!ReferenceEquals(sampledMapStats, mapStats) ||
            sampledWorldSeedId != worldSeedId ||
            worldTime < previousWorldTime ||
            Math.Abs(requestedSpeed - sampledRequestedSpeed) > 0.001f ||
            paused != sampledPaused)
        {
            BeginMeasurement(mapStats, worldSeedId, worldTime, requestedSpeed, paused);
            return;
        }

        double worldDelta = Math.Max(0.0, worldTime - previousWorldTime);
        previousWorldTime = worldTime;
        if (paused)
        {
            return;
        }

        float realDelta = Math.Max(0f, Time.unscaledDeltaTime);
        accumulatedWorldTime += worldDelta;
        accumulatedRealTime += realDelta;
        if (accumulatedRealTime < SampleWindowSeconds)
        {
            return;
        }

        float sampledSpeed = (float)(accumulatedWorldTime / accumulatedRealTime);
        actualSpeed = hasActualSpeed
            ? Mathf.Lerp(actualSpeed, sampledSpeed, SampleSmoothing)
            : sampledSpeed;
        hasActualSpeed = true;
        accumulatedWorldTime = 0.0;
        accumulatedRealTime = 0f;
    }

    private void ShowTooltip()
    {
        string title = "Cultiway.WorldAgeClock.Tooltip.Title".Localize();
        string description;
        string detail;
        float requestedSpeed = GetRequestedSpeed();

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
        else if (!hasActualSpeed)
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
            string realDuration = FormatRealDuration(WorldSecondsPerYear / actualSpeed);
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

    private void BeginMeasurement(
        MapStats mapStats,
        int worldSeedId,
        double worldTime,
        float requestedSpeed,
        bool paused)
    {
        sampledMapStats = mapStats;
        sampledWorldSeedId = worldSeedId;
        previousWorldTime = worldTime;
        sampledRequestedSpeed = requestedSpeed;
        sampledPaused = paused;
        accumulatedWorldTime = 0.0;
        accumulatedRealTime = 0f;
        actualSpeed = 0f;
        hasActualSpeed = false;
    }

    private void ResetMeasurement()
    {
        sampledMapStats = null;
        sampledWorldSeedId = -1;
        previousWorldTime = 0.0;
        sampledRequestedSpeed = -1f;
        sampledPaused = false;
        accumulatedWorldTime = 0.0;
        accumulatedRealTime = 0f;
        actualSpeed = 0f;
        hasActualSpeed = false;
    }

    private static float GetRequestedSpeed()
    {
        WorldTimeScaleAsset timeScale = Config.time_scale_asset;
        if (timeScale == null)
        {
            return 0f;
        }

        return Math.Max(0f, timeScale.multiplier) * Math.Max(1, timeScale.ticks);
    }

    private static string FormatGameDuration(double worldSeconds)
    {
        double days = worldSeconds * GameDaysPerWorldSecond;
        if (days >= 360.0)
        {
            return FormatLocalized(
                "Cultiway.WorldAgeClock.Unit.GameYears",
                FormatNumber(worldSeconds / WorldSecondsPerYear));
        }

        if (days >= 30.0)
        {
            return FormatLocalized(
                "Cultiway.WorldAgeClock.Unit.GameMonths",
                FormatNumber(worldSeconds / WorldSecondsPerMonth));
        }

        return FormatLocalized(
            "Cultiway.WorldAgeClock.Unit.GameDays",
            FormatNumber(days));
    }

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

    private static string FormatLocalized(string key, params object[] arguments)
    {
        return string.Format(LocalizedTextManager.getCulture(), key.Localize(), arguments);
    }

    private static string FormatNumber(double value)
    {
        CultureInfo culture = LocalizedTextManager.getCulture();
        return value.ToString("0.##", culture);
    }
}
