using System;
using System.Collections.Generic;
using Cultiway.Content.CreatureCompositions.Visuals;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>原版恢复把实际像素配色和已有模型面色合为同一表面，避免模型手臂误采样衣服纹理。</summary>
internal abstract partial class BodySurfaceBinding
{
    /// <summary>只在接手时制作一次颜色，不逐帧读取图片。</summary>
    private sealed partial class Native
    {
        /// <summary>沿实际像素边界切分原三角面，表面形状和部位保持原样。</summary>
        /// <param name="basis">真实接手的像素及动作模型。</param>
        private BodySurfacePose RecoveryColors(BodySurfacePose basis)
        {
            var parts = new BodySurfacePart[basis.Parts.Length];
            Color32[] bodyPixels = CreatureAppearanceService.ReadPixels(sprite);
            Color32[] itemPixels = heldSprite == null ? null : CreatureAppearanceService.ReadPixels(heldSprite);
            for (int i = 0; i < parts.Length; i++)
            {
                BodySurfacePart original = basis.Parts[i];
                if (original.ImageCoordinates == null) { parts[i] = original; continue; }
                Sprite image = original.Attachment == "item" ? heldSprite : sprite;
                Color32[] pixels = original.Attachment == "item" ? itemPixels : bodyPixels;
                if (image == null) { parts[i] = original; continue; }
                int width = Mathf.RoundToInt(image.rect.width), height = Mathf.RoundToInt(image.rect.height);
                var vertices = new List<Vector3>(); var triangles = new List<int>(); var colors = new List<Color32>();
                Vector3[] actual = basis.Vertices(i);
                for (int face = 0; face < original.Triangles.Length; face += 3)
                {
                    var source = new List<(Vector3 Point, Vector2 Uv)>(3);
                    Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
                    for (int corner = 0; corner < 3; corner++)
                    {
                        int vertex = original.Triangles[face + corner];
                        Vector2 uv = original.ImageCoordinates[vertex] * image.pixelsPerUnit + image.pivot;
                        source.Add((actual[vertex], uv)); min = Vector2.Min(min, uv); max = Vector2.Max(max, uv);
                    }
                    for (int y = Mathf.Max(0, Mathf.FloorToInt(min.y)); y <= Mathf.Min(height - 1, Mathf.FloorToInt(max.y)); y++)
                    for (int x = Mathf.Max(0, Mathf.FloorToInt(min.x)); x <= Mathf.Min(width - 1, Mathf.FloorToInt(max.x)); x++)
                    {
                        Color32 color = pixels[y * width + x];
                        if (color.a == 0) continue;
                        var polygon = Clip(Clip(Clip(Clip(source, 0, x, true), 0, x + 1, false), 1, y, true), 1, y + 1, false);
                        if (polygon.Count < 3) continue;
                        int start = vertices.Count;
                        foreach (var value in polygon) vertices.Add(value.Point);
                        for (int p = 1; p + 1 < polygon.Count; p++)
                        {
                            if (Vector3.Cross(polygon[p].Point - polygon[0].Point, polygon[p + 1].Point - polygon[0].Point).sqrMagnitude < 1e-16f) continue;
                            triangles.Add(start); triangles.Add(start + p); triangles.Add(start + p + 1); colors.Add(color);
                        }
                    }
                }
                parts[i] = new BodySurfacePart(original.Id, vertices.ToArray(), triangles.ToArray(), original.Attachment,
                    original.TransformIndex, original.Group, faceColors: colors.ToArray()).WithSelections(original.Selections);
            }
            return new BodySurfacePose(parts, basis.Matrices, basis.Foot, basis.Height, basis.Heading);
        }

        /// <summary>只在原面内插入边界点，不增加外部表面。</summary>
        /// <param name="source">原多边形。</param><param name="axis">图片轴。</param>
        /// <param name="edge">实际像素边界。</param><param name="above">是否保留大坐标一侧。</param>
        private static List<(Vector3 Point, Vector2 Uv)> Clip(List<(Vector3 Point, Vector2 Uv)> source, int axis, float edge, bool above)
        {
            var result = new List<(Vector3, Vector2)>(source.Count + 1);
            for (int i = 0; i < source.Count; i++)
            {
                var a = source[(i + source.Count - 1) % source.Count]; var b = source[i];
                float da = (a.Uv[axis] - edge) * (above ? 1f : -1f), db = (b.Uv[axis] - edge) * (above ? 1f : -1f);
                if ((da >= 0f) != (db >= 0f))
                {
                    float t = da / (da - db);
                    result.Add((Vector3.LerpUnclamped(a.Point, b.Point, t), Vector2.LerpUnclamped(a.Uv, b.Uv, t)));
                }
                if (db >= 0f) result.Add(b);
            }
            return result;
        }
    }
}
