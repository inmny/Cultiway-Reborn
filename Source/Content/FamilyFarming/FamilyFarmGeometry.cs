using System;
using System.Collections.Generic;

namespace Cultiway.Content.FamilyFarming;

/// <summary>把可用格子整理成矩形，并按农民人数计算起步和扩大后的尺寸。</summary>
internal static class FamilyFarmGeometry
{
    /// <summary>优先接近方形，在家庭面积上限内满足本户劳动力所需面积。</summary>
    /// <param name="data">村庄的起步尺寸和面积上限。</param>
    /// <param name="farmers">实际参与耕作的人数。</param>
    internal static (int width, int height) Size(FamilyFarmData data, int farmers)
    {
        int area = Math.Min(data.MaxArea, data.Width * data.Height * Math.Max(1, farmers));
        int bestWidth = data.Width, bestHeight = data.Height;
        double score = double.MaxValue;
        for (int w = 1; w <= 12; w++)
        for (int h = w; h <= 12; h++)
        {
            if (w * h < area || w * h > data.MaxArea) continue;
            double candidate = (w * h - area) * 10 + (h - w);
            if (candidate >= score) continue;
            score = candidate; bestWidth = w; bestHeight = h;
        }
        if (farmers <= 1) return (data.Width, data.Height);
        return (bestWidth, bestHeight);
    }

    /// <summary>剔除不可用格子后，把剩余部分分成互不重叠的矩形。</summary>
    /// <param name="plot">需要整理的原田块。</param>
    /// <param name="removed">失效地格编号。</param>
    internal static List<FamilyFarmPlot> Split(FamilyFarmPlot plot, HashSet<int> removed)
    {
        var result = new List<FamilyFarmPlot>();
        var used = new HashSet<int>(removed);
        for (int y = plot.Y; y < plot.Y + plot.Height; y++)
        for (int x = plot.X; x < plot.X + plot.Width; x++)
        {
            if (used.Contains(x + y * MapBox.width)) continue;
            int w = 1, h = 1;
            while (x + w < plot.X + plot.Width && !used.Contains(x + w + y * MapBox.width)) w++;
            while (y + h < plot.Y + plot.Height)
            {
                bool complete = true;
                for (int dx = 0; dx < w; dx++) if (used.Contains(x + dx + (y + h) * MapBox.width)) { complete = false; break; }
                if (!complete) break;
                h++;
            }
            var part = new FamilyFarmPlot { Owner = plot.Owner, X = x, Y = y, Width = w, Height = h };
            foreach (int cell in part.Cells()) used.Add(cell);
            result.Add(part);
        }
        return result;
    }
}
