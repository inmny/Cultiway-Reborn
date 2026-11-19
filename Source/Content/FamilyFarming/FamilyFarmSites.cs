using System;
using System.Collections.Generic;
using System.Linq;

namespace Cultiway.Content.FamilyFarming;

/// <summary>以城区为中心排列矩形田块，共用一格田埂并分批由近到远检查。</summary>
internal sealed class FamilyFarmSites
{
    /// <summary>田块最外沿距离城区边缘最多三十二格。</summary>
    internal const int Reach = 32;
    /// <summary>城区中心和横纵半径。</summary>
    internal readonly (int cx, int cy, int hx, int hy) Bounds;
    /// <summary>不同田块尺寸各自的候选与进度，供全村家庭共用。</summary>
    private readonly Dictionary<(int width, int height), Search> searches = new();

    /// <summary>一种尺寸的有序候选，不保存到世界存档。</summary>
    private sealed class Search
    {
        /// <summary>按距城区远近排列的左下角。</summary>
        internal List<(int x, int y)> Positions;
        /// <summary>下一次继续检查的位置。</summary>
        internal int Cursor;
    }

    /// <summary>建立当前城区的找地进度。</summary>
    /// <param name="bounds">城区中心和横纵半径。</param>
    internal FamilyFarmSites((int cx, int cy, int hx, int hy) bounds) => Bounds = bounds;

    /// <summary>田块必须完整位于城区周边，不能只让最近一角靠近。</summary>
    /// <param name="plot">需要检查的矩形。</param>
    internal bool Contains(FamilyFarmPlot plot) => plot.X >= Bounds.cx - Bounds.hx - Reach &&
        plot.Y >= Bounds.cy - Bounds.hy - Reach && plot.X + plot.Width - 1 <= Bounds.cx + Bounds.hx + Reach &&
        plot.Y + plot.Height - 1 <= Bounds.cy + Bounds.hy + Reach;

    /// <summary>矩形到城区边缘的格数，城区范围内为零。</summary>
    /// <param name="plot">用于比较远近的矩形。</param>
    internal int Distance(FamilyFarmPlot plot) => Math.Max(0, Math.Max(
        Math.Max(Bounds.cx - Bounds.hx - (plot.X + plot.Width - 1), plot.X - Bounds.cx - Bounds.hx),
        Math.Max(Bounds.cy - Bounds.hy - (plot.Y + plot.Height - 1), plot.Y - Bounds.cy - Bounds.hy)));

    /// <summary>每次最多检查十六个候选，随后从原位置继续，不重复从城心扫描。</summary>
    /// <param name="width">本次田块宽度。</param>
    /// <param name="height">本次田块高度。</param>
    internal IEnumerable<(int x, int y)> Next(int width, int height)
    {
        if (!searches.TryGetValue((width, height), out var search))
        {
            search = new Search { Positions = Arrange(width, height) };
            searches.Add((width, height), search);
        }
        for (int n = 0, count = Math.Min(16, search.Positions.Count); n < count; n++)
        {
            if (search.Cursor >= search.Positions.Count) search.Cursor = 0;
            yield return search.Positions[search.Cursor++];
        }
    }

    /// <summary>旧田释放后优先重新利用靠近城区的空位。</summary>
    internal void Reset() { foreach (var search in searches.Values) search.Cursor = 0; }

    /// <summary>矩形宽高各加一格作为排列步长，再按城区距离排列。</summary>
    /// <param name="width">田块宽度。</param>
    /// <param name="height">田块高度。</param>
    private List<(int x, int y)> Arrange(int width, int height)
    {
        // 先生成统一田埂的候选，再剔除地图边缘，最后按城区距离排序。
        var plots = new List<FamilyFarmPlot>();
        int anchorX = Bounds.cx - width / 2, anchorY = Bounds.cy - height / 2;
        int columns = (Bounds.hx + Reach) / (width + 1) + 1;
        int rows = (Bounds.hy + Reach) / (height + 1) + 1;
        for (int row = -rows; row <= rows; row++)
        for (int column = -columns; column <= columns; column++)
        {
            var plot = new FamilyFarmPlot { X = anchorX + column * (width + 1),
                Y = anchorY + row * (height + 1), Width = width, Height = height };
            if (!Contains(plot) || plot.X < 1 || plot.Y < 1 || plot.X + width >= MapBox.width ||
                plot.Y + height >= MapBox.height) continue;
            plots.Add(plot);
        }
        return plots.OrderBy(Distance).ThenBy(p => Math.Abs(p.X * 2 + width - 1 - Bounds.cx * 2) +
                Math.Abs(p.Y * 2 + height - 1 - Bounds.cy * 2))
            .ThenBy(p => p.Y).ThenBy(p => p.X).Select(p => (p.X, p.Y)).ToList();
    }
}
