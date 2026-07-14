using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Components;

/// <summary>灵根总览图中所有材质纹理图形的共同基类。</summary>
internal abstract class ElementRootTexturedGraphic : MaskableGraphic
{
    private Texture _texture;

    public override Texture mainTexture => _texture == null ? s_WhiteTexture : _texture;

    /// <summary>设置当前区域使用的纹理；长条纹理会横向循环，圆形纹理保持边缘钳制。</summary>
    public void SetTexture(Texture texture, bool repeat = false)
    {
        texture ??= s_WhiteTexture;
        var changed = _texture != texture;
        _texture = texture;
        if (_texture != s_WhiteTexture)
        {
            _texture.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            _texture.filterMode = FilterMode.Bilinear;
        }
        if (changed) SetMaterialDirty();
        SetVerticesDirty();
    }

    /// <summary>把局部坐标映射到以图形中心为原点的完整圆形材质。</summary>
    protected Vector2 GetDiscUv(Vector2 position, float referenceRadius, float visualScale = 1f)
    {
        var diameter = Mathf.Max(1f, referenceRadius * 2f * visualScale);
        return new Vector2(0.5f + position.x / diameter, 0.5f + position.y / diameter);
    }

    protected static void AddQuad(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
        Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3, Color32 color)
    {
        var start = vh.currentVertCount;
        AddVertex(vh, p0, uv0, color);
        AddVertex(vh, p1, uv1, color);
        AddVertex(vh, p2, uv2, color);
        AddVertex(vh, p3, uv3, color);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    protected static void AddQuad(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
        Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3,
        Color32 color0, Color32 color1, Color32 color2, Color32 color3)
    {
        var start = vh.currentVertCount;
        AddVertex(vh, p0, uv0, color0);
        AddVertex(vh, p1, uv1, color1);
        AddVertex(vh, p2, uv2, color2);
        AddVertex(vh, p3, uv3, color3);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    protected static Color32 MultiplyAlpha(Color32 color, float alpha)
    {
        color.a = (byte)Mathf.RoundToInt(color.a * Mathf.Clamp01(alpha));
        return color;
    }

    protected static void AddVertex(VertexHelper vh, Vector2 position, Vector2 uv, Color32 color)
    {
        var vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.uv0 = uv;
        vertex.color = color;
        vh.AddVert(vertex);
    }
}

/// <summary>把一整圈对应的透明长条纹理弯曲为圆环片段。</summary>
internal sealed class ElementRootRingSegmentGraphic : ElementRootTexturedGraphic
{
    private const float StripVInset = 1f / 256f;

    private float _innerRadius;
    private float _outerRadius = 1f;
    private float _startAngle;
    private float _sweepAngle = 360f;
    private float _startAlpha = 1f;
    private float _endAlpha = 1f;
    private float _phase;

    /// <summary>
    /// 更新圆环片段。横向 UV 按圆环自身的顺时针角度取样，起止透明度只用于五行边界交叉淡化。
    /// </summary>
    public void Configure(float innerRadius, float outerRadius, float startAngle, float sweepAngle,
        float startAlpha = 1f, float endAlpha = 1f)
    {
        _innerRadius = Mathf.Clamp01(innerRadius);
        _outerRadius = Mathf.Clamp(outerRadius, _innerRadius, 1f);
        _startAngle = startAngle;
        _sweepAngle = sweepAngle;
        _startAlpha = Mathf.Clamp01(startAlpha);
        _endAlpha = Mathf.Clamp01(endAlpha);
        SetVerticesDirty();
    }

    /// <summary>设置长条纹理在圆环内部的循环相位；相位变化不会移动圆环片段本身。</summary>
    public void SetPhase(float phase)
    {
        phase = Mathf.Repeat(phase, 1f);
        if (Mathf.Abs(_phase - phase) <= 0.000001f) return;
        _phase = phase;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_outerRadius <= 0f || Mathf.Abs(_sweepAngle) <= 0.0001f) return;

        var radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f;
        if (radius <= 0f) return;

        var inner = radius * _innerRadius;
        var outer = radius * _outerRadius;
        var angularSegments = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(_sweepAngle) / 3f));
        var color32 = (Color32)color;

        for (var angularIndex = 0; angularIndex < angularSegments; angularIndex++)
        {
            var progress0 = (float)angularIndex / angularSegments;
            var progress1 = (float)(angularIndex + 1) / angularSegments;
            var angle0 = _startAngle + _sweepAngle * progress0;
            var angle1 = _startAngle + _sweepAngle * progress1;
            var direction0 = GetDirection(angle0);
            var direction1 = GetDirection(angle1);
            var alpha0 = Mathf.Lerp(_startAlpha, _endAlpha,
                Mathf.SmoothStep(0f, 1f, progress0));
            var alpha1 = Mathf.Lerp(_startAlpha, _endAlpha,
                Mathf.SmoothStep(0f, 1f, progress1));
            var color0 = MultiplyAlpha(color32, alpha0);
            var color1 = MultiplyAlpha(color32, alpha1);
            var uv0 = new Vector2(-angle0 / 360f + _phase, StripVInset);
            var uv1 = new Vector2(-angle1 / 360f + _phase, 1f - StripVInset);

            AddQuad(vh,
                direction0 * inner, direction0 * outer,
                direction1 * outer, direction1 * inner,
                uv0, new Vector2(uv0.x, 1f - StripVInset),
                uv1, new Vector2(uv1.x, StripVInset),
                color0, color0, color1, color1);
        }
    }

    private static Vector2 GetDirection(float angle)
    {
        var radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }
}

/// <summary>阴阳太极共享的 S 形边界和面积求解。</summary>
internal static class YinYangDiagramGeometry
{
    public const float BoundaryAmplitude = 0.38f;

    /// <summary>计算单位圆给定纵坐标上的 S 形边界横坐标。</summary>
    public static float GetBoundary(float normalizedY, float offset)
    {
        normalizedY = Mathf.Clamp(normalizedY, -1f, 1f);
        var halfWidth = Mathf.Sqrt(Mathf.Max(0f, 1f - normalizedY * normalizedY));
        var boundary = offset + BoundaryAmplitude * Mathf.Sin(Mathf.PI * normalizedY);
        return Mathf.Clamp(boundary, -halfWidth, halfWidth);
    }

    /// <summary>求出使阴区域面积达到目标占比的 S 形边界横向偏移。</summary>
    public static float SolveOffset(float yinRatio)
    {
        yinRatio = Mathf.Clamp01(yinRatio);
        if (yinRatio <= 0f) return -2f;
        if (yinRatio >= 1f) return 2f;

        var low = -2f;
        var high = 2f;
        for (var i = 0; i < 28; i++)
        {
            var middle = (low + high) * 0.5f;
            if (CalculateYinAreaRatio(middle) < yinRatio)
                low = middle;
            else
                high = middle;
        }
        return (low + high) * 0.5f;
    }

    private static float CalculateYinAreaRatio(float offset)
    {
        const int samples = 512;
        var area = 0f;
        var step = 2f / samples;
        for (var i = 0; i < samples; i++)
        {
            var y = -1f + (i + 0.5f) * step;
            var halfWidth = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            area += (GetBoundary(y, offset) + halfWidth) * step;
        }
        return area / Mathf.PI;
    }
}

/// <summary>绘制太极中位于 S 形边界一侧的阴或阳区域。</summary>
internal sealed class ElementRootYinYangRegionGraphic : ElementRootTexturedGraphic
{
    private const float TextureScale = 1.12f;

    private float _radius;
    private float _boundaryOffset;
    private float _textureRotationDegrees;
    private bool _drawYin;

    /// <summary>更新阴阳区域；边界偏移以局部太极半径为单位。</summary>
    public void Configure(float radius, float boundaryOffset, bool drawYin)
    {
        _radius = Mathf.Clamp01(radius);
        _boundaryOffset = boundaryOffset;
        _drawYin = drawYin;
        SetVerticesDirty();
    }

    /// <summary>设置阴阳区域内部的纹理采样旋转角，不改变 S 形边界和鱼眼位置。</summary>
    public void SetTextureRotation(float rotationDegrees)
    {
        rotationDegrees = Mathf.Repeat(rotationDegrees, 360f);
        if (Mathf.Abs(Mathf.DeltaAngle(_textureRotationDegrees, rotationDegrees)) <= 0.001f) return;
        _textureRotationDegrees = rotationDegrees;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var fullRadius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f;
        var radius = fullRadius * _radius;
        if (radius <= 0.01f) return;

        var stripCount = Mathf.Clamp(Mathf.CeilToInt(radius), 24, 96);
        var color32 = (Color32)color;
        for (var i = 0; i < stripCount; i++)
        {
            var y0 = -radius + 2f * radius * i / stripCount;
            var y1 = -radius + 2f * radius * (i + 1) / stripCount;
            var normalizedY0 = y0 / radius;
            var normalizedY1 = y1 / radius;
            var halfWidth0 = radius * Mathf.Sqrt(Mathf.Max(0f, 1f - normalizedY0 * normalizedY0));
            var halfWidth1 = radius * Mathf.Sqrt(Mathf.Max(0f, 1f - normalizedY1 * normalizedY1));
            var boundary0 = radius * YinYangDiagramGeometry.GetBoundary(normalizedY0, _boundaryOffset);
            var boundary1 = radius * YinYangDiagramGeometry.GetBoundary(normalizedY1, _boundaryOffset);

            var x00 = _drawYin ? -halfWidth0 : boundary0;
            var x01 = _drawYin ? boundary0 : halfWidth0;
            var x10 = _drawYin ? -halfWidth1 : boundary1;
            var x11 = _drawYin ? boundary1 : halfWidth1;
            if (x01 - x00 <= 0.0001f && x11 - x10 <= 0.0001f) continue;

            var p0 = new Vector2(x00, y0);
            var p1 = new Vector2(x01, y0);
            var p2 = new Vector2(x11, y1);
            var p3 = new Vector2(x10, y1);
            AddQuad(vh, p0, p1, p2, p3,
                GetTextureUv(p0, radius), GetTextureUv(p1, radius),
                GetTextureUv(p2, radius), GetTextureUv(p3, radius), color32);
        }
    }

    private Vector2 GetTextureUv(Vector2 position, float radius)
    {
        var uv = GetDiscUv(position, radius, TextureScale);
        var offset = uv - new Vector2(0.5f, 0.5f);
        var radians = _textureRotationDegrees * Mathf.Deg2Rad;
        var sin = Mathf.Sin(radians);
        var cos = Mathf.Cos(radians);
        return new Vector2(
            0.5f + offset.x * cos - offset.y * sin,
            0.5f + offset.x * sin + offset.y * cos);
    }
}

/// <summary>绘制太极鱼眼等可带元素纹理的小圆。</summary>
internal sealed class ElementRootDiscGraphic : ElementRootTexturedGraphic
{
    private Vector2 _center;
    private float _radius;
    private float _textureScale = 1f;

    /// <summary>更新圆心和半径；两者均使用相对总图半径的坐标。</summary>
    public void Configure(Vector2 center, float radius, float textureScale = 1f)
    {
        _center = center;
        _radius = Mathf.Max(0f, radius);
        _textureScale = Mathf.Max(0.1f, textureScale);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var fullRadius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f;
        var center = _center * fullRadius;
        var radius = _radius * fullRadius;
        if (radius <= 0.25f) return;

        var segmentCount = Mathf.Clamp(Mathf.CeilToInt(radius * 2f), 16, 48);
        var color32 = (Color32)color;
        var centerIndex = vh.currentVertCount;
        AddVertex(vh, center, GetDiscUv(Vector2.zero, radius, _textureScale), color32);
        for (var i = 0; i <= segmentCount; i++)
        {
            var angle = Mathf.PI * 2f * i / segmentCount;
            var position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            AddVertex(vh, position, GetDiscUv(position - center, radius, _textureScale), color32);
            if (i > 0) vh.AddTriangle(centerIndex, centerIndex + i, centerIndex + i + 1);
        }
    }
}

/// <summary>给太极核心增加柔和高光，使材质在小尺寸下仍有球面层次。</summary>
internal sealed class ElementRootOrbHighlightGraphic : MaskableGraphic
{
    private float _radius;

    /// <summary>更新高光所依附的太极半径。</summary>
    public void Configure(float radius)
    {
        _radius = Mathf.Clamp01(radius);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var fullRadius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f;
        var radius = fullRadius * _radius;
        if (radius <= 2f) return;

        AddGradientEllipse(vh, new Vector2(-0.2f, 0.3f) * radius,
            new Vector2(0.36f, 0.16f) * radius,
            new Color32(255, 250, 230, 62));
    }

    private static void AddGradientEllipse(VertexHelper vh, Vector2 center, Vector2 radii, Color32 centerColor)
    {
        const int segments = 32;
        var centerIndex = vh.currentVertCount;
        AddVertex(vh, center, centerColor);
        var edgeColor = new Color32(centerColor.r, centerColor.g, centerColor.b, 0);
        for (var i = 0; i <= segments; i++)
        {
            var angle = Mathf.PI * 2f * i / segments;
            var position = center + new Vector2(Mathf.Cos(angle) * radii.x, Mathf.Sin(angle) * radii.y);
            AddVertex(vh, position, edgeColor);
            if (i > 0) vh.AddTriangle(centerIndex, centerIndex + i, centerIndex + i + 1);
        }
    }

    private static void AddVertex(VertexHelper vh, Vector2 position, Color32 color)
    {
        var vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vh.AddVert(vertex);
    }
}

/// <summary>在灵根数据无效时绘制占位轮廓；有效徽记不附加硬质描边。</summary>
internal sealed class ElementRootDiagramOverlayGraphic : MaskableGraphic
{
    private bool _valid;

    /// <summary>更新数据有效状态；有效状态完全依靠各材质自身的透明边缘构图。</summary>
    public void Configure(bool valid)
    {
        _valid = valid;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f;
        if (radius <= 0f) return;
        var widthScale = Mathf.Clamp(radius / 86f, 0.45f, 1f);

        if (!_valid)
        {
            AddCircle(vh, radius, widthScale, new Color32(155, 155, 155, 150));
        }
    }

    private static void AddCircle(VertexHelper vh, float radius, float width, Color32 color)
    {
        const int segments = 120;
        var inner = Mathf.Max(0f, radius - width);
        for (var i = 0; i < segments; i++)
        {
            var a0 = Mathf.PI * 2f * i / segments;
            var a1 = Mathf.PI * 2f * (i + 1) / segments;
            var d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
            var d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
            AddSolidQuad(vh, d0 * inner, d0 * radius, d1 * radius, d1 * inner, color);
        }
    }

    private static void AddSolidQuad(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
        Color32 color)
    {
        var start = vh.currentVertCount;
        AddSolidVertex(vh, p0, color);
        AddSolidVertex(vh, p1, color);
        AddSolidVertex(vh, p2, color);
        AddSolidVertex(vh, p3, color);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    private static void AddSolidVertex(VertexHelper vh, Vector2 position, Color32 color)
    {
        var vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vh.AddVert(vertex);
    }
}
