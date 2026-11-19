using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Railways;
using Cultiway.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.Railways;

/// <summary>铁路信息页的简易路线图，按真实轨道缩放，白点为车站、黄点为列车。</summary>
internal sealed class RailwayRoutePreview : MonoBehaviour
{
    /// <summary>图像像素宽度。</summary>
    private const int Width = 448;
    /// <summary>图像像素高度。</summary>
    private const int Height = 88;
    /// <summary>边缘给车站圆点留出的距离。</summary>
    private const int Inset = 6;
    /// <summary>窗口始终复用同一张纹理。</summary>
    private Texture2D texture;
    /// <summary>每次重画复用像素数组。</summary>
    private readonly Color32[] pixels = new Color32[Width * Height];
    /// <summary>随当前语言更新的标题。</summary>
    private Text heading;
    /// <summary>随当前语言更新的图例。</summary>
    private Text legend;

    /// <summary>在详情滚动区顶部建立一次，不新增窗口或独立地图模式。</summary>
    /// <param name="parent">原有滚动内容。</param>
    internal static RailwayRoutePreview Create(Transform parent)
    {
        var obj = UiLayout.Create(parent, "RoutePreview", false, 460, 132, UiTheme.Current.Metrics.SpacingXs);
        var preview = obj.AddComponent<RailwayRoutePreview>();
        preview.heading = UiElements.CreateText(obj.transform, "Heading", "", Width, 14, 8,
            TextAnchor.UpperLeft, FontStyle.Bold);
        var imageObject = new GameObject("Diagram", typeof(RectTransform), typeof(RawImage));
        imageObject.transform.SetParent(obj.transform, false);
        UiLayout.SetSize(imageObject.transform, Width, Height);
        var image = imageObject.GetComponent<RawImage>();
        image.raycastTarget = false;
        preview.texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
        { name = "Cultiway_RailwayRoute", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        image.texture = preview.texture;
        preview.legend = UiElements.CreateText(obj.transform, "Legend", "", Width, 20, 7, TextAnchor.UpperLeft);
        preview.Hide();
        return preview;
    }

    /// <summary>清世界或切换页面时隐藏，只保留可复用的图像。</summary>
    internal void Hide() => gameObject.SetActive(false);

    /// <summary>先算真实范围，再画按状态着色的轨迹、站点及实际列车位置。</summary>
    /// <param name="lines">选中车站或列车涉及的真实线路。</param>
    /// <param name="trainPosition">当前列车位置；车站页没有列车标记。</param>
    internal void Bind(IReadOnlyList<TrainLinkSaveData> lines, Vector2? trainPosition = null)
    {
        if (lines.Count == 0) { Hide(); return; }
        gameObject.SetActive(true);
        heading.text = WindowRailwayInfo.T("RouteDiagram");
        legend.text = WindowRailwayInfo.T("RouteDiagramLegend");
        Array.Clear(pixels, 0, pixels.Length);
        var paths = lines.Select(l => l.Tiles.Select(id => (Vector2)World.world.tiles_list[id].posV3).ToArray()).ToArray();
        var points = paths.SelectMany(p => p).ToArray();
        float minX = points.Min(p => p.x), minY = points.Min(p => p.y);
        float spanX = points.Max(p => p.x) - minX, spanY = points.Max(p => p.y) - minY;
        float scale = Math.Min((Width - Inset * 2f) / Math.Max(1, spanX), (Height - Inset * 2f) / Math.Max(1, spanY));
        Vector2 Project(Vector2 p) => new((Width - spanX * scale) * .5f + (p.x - minX) * scale,
            (Height - spanY * scale) * .5f + (p.y - minY) * scale);
        for (int i = 0; i < paths.Length; i++)
        {
            Color32 color = LineColor(lines[i].State);
            for (int j = 1; j < paths[i].Length; j++) DrawLine(Project(paths[i][j - 1]), Project(paths[i][j]), color);
        }
        foreach (var path in paths)
        {
            Dot(Project(path[0]), 2, UiTheme.Current.Palette.PrimaryText);
            Dot(Project(path[path.Length - 1]), 2, UiTheme.Current.Palette.PrimaryText);
        }
        if (trainPosition.HasValue) Dot(Project(trainPosition.Value), 3, UiTheme.Current.Palette.Warning);
        texture.SetPixels32(pixels); texture.Apply(false);
    }

    /// <summary>路线颜色与铁路运营图及详情中的状态一致。</summary>
    /// <param name="state">实际工程状态。</param>
    private static Color LineColor(RailwayLinkState state) => state switch
    {
        RailwayLinkState.Normal => UiTheme.Current.Palette.Success,
        RailwayLinkState.Damaged => UiTheme.Current.Palette.Error,
        RailwayLinkState.Construction or RailwayLinkState.Repairing => UiTheme.Current.Palette.Warning,
        _ => UiTheme.Current.Palette.Disabled
    };

    /// <summary>线段按像素间隔插值，保持转弯和缩放后的轨迹形状可辨。</summary>
    /// <param name="from">图中起点。</param>
    /// <param name="to">图中终点。</param>
    /// <param name="color">线路状态色。</param>
    private void DrawLine(Vector2 from, Vector2 to, Color32 color)
    {
        int steps = Math.Max(1, Mathf.CeilToInt(Math.Max(Math.Abs(to.x - from.x), Math.Abs(to.y - from.y))));
        for (int i = 0; i <= steps; i++) Dot(Vector2.Lerp(from, to, i / (float)steps), 0, color);
    }

    /// <summary>画站点或列车标记，并裁去缩放后超出图像的像素。</summary>
    /// <param name="position">图中中心。</param>
    /// <param name="radius">标记大小。</param>
    /// <param name="color">状态色。</param>
    private void Dot(Vector2 position, int radius, Color32 color)
    {
        int cx = Mathf.RoundToInt(position.x), cy = Mathf.RoundToInt(position.y);
        for (int y = Math.Max(0, cy - radius); y <= Math.Min(Height - 1, cy + radius); y++)
        for (int x = Math.Max(0, cx - radius); x <= Math.Min(Width - 1, cx + radius); x++) pixels[y * Width + x] = color;
    }

    /// <summary>窗口销毁时释放唯一的动态图像。</summary>
    private void OnDestroy() => Destroy(texture);
}
