using System.Collections.Generic;

namespace Cultiway.Utils.Extension;

public static class TooltipTools
{
    public static void InsertLineAfter(this Tooltip tooltip, string afterLabelKey, string labelKey, string value,
        string color = null, bool localize = true, int limitValue = 21)
    {
        string[] labels = tooltip.stats_description.text.Split('\n');
        string[] values = tooltip.stats_values.text.Split('\n');
        int insertIndex = FindLineIndex(labels, afterLabelKey) + 1;
        if (insertIndex <= 0 || labels.Length != values.Length)
        {
            tooltip.addLineText(labelKey, value, color, pLocalize: localize, pLimitValue: limitValue);
            return;
        }

        List<string> labelList = new(labels);
        List<string> valueList = new(values);
        labelList.Insert(insertIndex, localize ? LMTools.GetOrKey(labelKey) : labelKey);
        valueList.Insert(insertIndex, FormatLineValue(value, color, limitValue));
        tooltip.stats_description.text = string.Join("\n", labelList);
        tooltip.stats_values.text = string.Join("\n", valueList);
    }

    public static int FindLineIndex(this Tooltip tooltip, string targetLabel)
    {
        return FindLineIndex(tooltip.stats_description.text.Split('\n'), targetLabel);
    }

    private static int FindLineIndex(string[] labels, string targetLabel)
    {
        string localizedLabel = LMTools.GetOrKey(targetLabel);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] == localizedLabel || labels[i] == targetLabel) return i;
        }

        return -1;
    }

    private static string FormatLineValue(string value, string color, int limitValue)
    {
        if (value != null && value.Length > limitValue)
        {
            value = value.Substring(0, limitValue - 1) + "...";
        }

        return string.IsNullOrEmpty(color) ? value : Toolbox.coloredText(value ?? string.Empty, color);
    }
}
