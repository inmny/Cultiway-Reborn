using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cultiway.Core.Libraries;
using Cultiway.Core.Progression;
using Cultiway.Utils.Extension;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.UI;

/// <summary>修炼体系列表与 Tooltip 共用的只读展示格式化。</summary>
internal static class CultisysPresentation
{
    internal readonly struct StatBonus
    {
        public StatBonus(string iconPath, string name, string value, Color color)
        {
            IconPath = iconPath;
            Name = name;
            Value = value;
            Color = color;
        }

        public string IconPath { get; }
        public string Name { get; }
        public string Value { get; }
        public Color Color { get; }
    }

    public static string FormatProgression(ProgressionQuery query)
    {
        if (!query.Available) return "Cultiway.CultisysOverview.UI.State.Unavailable".Localize();

        string kind = query.Kind == ProgressionKind.Minor
            ? "Cultiway.CultisysOverview.UI.Kind.Minor".Localize()
            : "Cultiway.CultisysOverview.UI.Kind.Major".Localize();
        string state;
        if (!query.Approaching)
        {
            state = "Cultiway.CultisysOverview.UI.State.Accumulating".Localize();
        }
        else
        {
            state = query.Gate.State switch
            {
                ProgressionGateState.Satisfied => "Cultiway.CultisysOverview.UI.State.Ready".Localize(),
                ProgressionGateState.NotReady => "Cultiway.CultisysOverview.UI.State.NotReady".Localize(),
                ProgressionGateState.NeedsStart => "Cultiway.CultisysOverview.UI.State.NeedsStart".Localize(),
                ProgressionGateState.InProgress => "Cultiway.CultisysOverview.UI.State.InProgress".Localize(),
                _ => "Cultiway.CultisysOverview.UI.State.Blocked".Localize()
            };
        }
        return string.Format("Cultiway.CultisysOverview.UI.Format.Next".Localize(), kind, state);
    }

    public static Color ResolveProgressionColor(ProgressionQuery query)
    {
        UiPalette palette = UiTheme.Current.Palette;
        if (!query.Available || !query.Approaching || query.Gate.State == ProgressionGateState.NotReady)
            return palette.MutedText;
        return query.Gate.State switch
        {
            ProgressionGateState.Satisfied => palette.Success,
            ProgressionGateState.NeedsStart => palette.Warning,
            ProgressionGateState.InProgress => palette.AccentText,
            ProgressionGateState.Blocked => palette.Error,
            _ => palette.MutedText
        };
    }

    public static string ResolveProgressionReason(ProgressionQuery query)
    {
        string reason = query.Gate.Reason;
        return string.IsNullOrEmpty(reason) || !LMTools.Has(reason) ? null : LM.Get(reason);
    }

    public static List<StatBonus> BuildStatBonuses(BaseStats providedStats)
    {
        var result = new List<StatBonus>();
        if (providedStats == null) return result;
        List<BaseStatsContainer> stats = providedStats.getList();

        foreach (BaseStatsContainer container in stats
                     .OrderBy(entry => entry.asset?.sort_rank ?? int.MaxValue)
                     .ThenBy(entry => entry.id))
        {
            BaseStatAsset asset = container.asset;
            if (asset == null || asset.hidden) continue;
            float value = container.value;
            if (Mathf.Abs(value) < 0.005f) continue;

            Color color = value > 0f
                ? UiTheme.Current.Palette.Success
                : value < 0f
                    ? UiTheme.Current.Palette.Error
                    : UiTheme.Current.Palette.PrimaryText;
            result.Add(new StatBonus(asset.icon, ResolveStatName(asset), FormatStatValue(value, asset), color));
        }
        return result;
    }

    public static string ToHtml(Color color)
    {
        return $"#{ColorUtility.ToHtmlStringRGB(color)}";
    }

    private static string FormatStatValue(float value, BaseStatAsset asset)
    {
        if (Mathf.Abs(value) < 0.005f) return "-";
        float visualValue = value * asset.tooltip_multiply_for_visual_number;
        string number = asset.show_as_percents
            ? visualValue.ToString("0.#", CultureInfo.InvariantCulture) + "%"
            : visualValue.ToString(Mathf.Abs(visualValue) >= 1000f ? "#,0.#" : "0.##",
                CultureInfo.InvariantCulture);
        return visualValue > 0f ? "+" + number : number;
    }

    private static string ResolveStatName(BaseStatAsset asset)
    {
        string localeKey = asset.getLocaleID()?.Underscore();
        if (!string.IsNullOrEmpty(localeKey) && LMTools.Has(localeKey))
            return LocalizedTextManager.getText(localeKey, null, false);

        string idKey = asset.id?.Underscore();
        if (!string.IsNullOrEmpty(idKey) && LMTools.Has(idKey))
            return LocalizedTextManager.getText(idKey, null, false);
        return asset.id ?? "-";
    }
}
