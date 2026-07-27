using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>在固定矩形内按非负权重绘制连续彩色分段。</summary>
internal sealed class UiWeightedSegmentBar : MaskableGraphic
{
    private float[] weights = Array.Empty<float>();
    private Color[] colors = Array.Empty<Color>();

    /// <summary>创建指定尺寸的比例条。</summary>
    public static UiWeightedSegmentBar Create(Transform parent, string name, float width, float height)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        UiLayout.SetSize(obj.transform, width, height);
        var bar = obj.AddComponent<UiWeightedSegmentBar>();
        bar.raycastTarget = false;
        return bar;
    }

    /// <summary>更新分段权重和颜色；无有效权重时只绘制底槽。</summary>
    public void SetSegments(float[] segmentWeights, Color[] segmentColors)
    {
        if (segmentWeights == null) throw new ArgumentNullException(nameof(segmentWeights));
        if (segmentColors == null) throw new ArgumentNullException(nameof(segmentColors));
        if (segmentWeights.Length != segmentColors.Length)
            throw new ArgumentException("比例条的权重和颜色数量必须一致");

        weights = (float[])segmentWeights.Clone();
        colors = (Color[])segmentColors.Clone();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        AddQuad(vh, rect.xMin, rect.yMin, rect.xMax, rect.yMax,
            UiTheme.Current.Palette.SegmentTrack);

        double total = 0d;
        var activeCount = 0;
        for (var i = 0; i < weights.Length; i++)
        {
            if (!IsPositive(weights[i])) continue;
            total += weights[i];
            activeCount++;
        }
        if (total <= 0d || rect.width <= 2f || rect.height <= 2f) return;

        float left = rect.xMin + 1f;
        float right = rect.xMax - 1f;
        float bottom = rect.yMin + 1f;
        float top = rect.yMax - 1f;
        float contentWidth = right - left;
        var activeIndex = 0;
        for (var i = 0; i < weights.Length; i++)
        {
            if (!IsPositive(weights[i])) continue;
            activeIndex++;
            float end = activeIndex == activeCount
                ? right
                : left + contentWidth * (float)(weights[i] / total);
            AddQuad(vh, left, bottom, end, top, colors[i]);
            if (activeIndex < activeCount)
                AddQuad(vh, end - 0.35f, bottom, end + 0.35f, top,
                    UiTheme.Current.Palette.SegmentDivider);
            left = end;
        }
    }

    /// <summary>判断权重是否是可参与绘制的有限正数。</summary>
    private static bool IsPositive(float value)
    {
        return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>向当前网格追加一个矩形面片。</summary>
    private static void AddQuad(VertexHelper vh, float left, float bottom, float right, float top, Color color)
    {
        if (right <= left || top <= bottom) return;
        var start = vh.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = new Vector2(left, bottom);
        vh.AddVert(vertex);
        vertex.position = new Vector2(left, top);
        vh.AddVert(vertex);
        vertex.position = new Vector2(right, top);
        vh.AddVert(vertex);
        vertex.position = new Vector2(right, bottom);
        vh.AddVert(vertex);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}
