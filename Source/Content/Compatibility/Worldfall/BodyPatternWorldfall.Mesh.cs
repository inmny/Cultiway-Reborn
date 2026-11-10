using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>原面内的纹样制作，不改变原身体轮廓、骨架或父级。</summary>
internal static partial class BodyOrganWorldfall
{
    /// <summary>在原三角面内沿像素边界分色，再派生实际显示模型和同源接触分组。</summary>
    /// <param name="model">当前精度的原模型。</param><param name="plan">实际制作图片。</param>
    private static PatternBody BuildPatternBody(object model, PatternPlan plan)
    {
        var original = (Array)modelParts.GetValue(model);
        Array parts = (Array)original.Clone();
        var names = new List<string>((string[])patternSlotNames.GetValue(model));
        var kinds = new List<byte>((byte[])patternSlotKinds.GetValue(model));
        var flags = new List<byte>((byte[])patternSlotFlags.GetValue(model));
        var colors = new List<uint>((uint[])patternSlotColors.GetValue(model));
        var sources = new List<byte>();
        var paints = new List<PatternSample>();
        var slots = new Dictionary<(byte source, int sample), byte>();
        var groups = new PatternGroup[parts.Length][];
        Vector3 min = Read(patternModelMin.GetValue(model)), max = Read(patternModelMax.GetValue(model));
        // 使用原CreatureColors的侧面映射：直立身体沿X，其余身体沿Y，高度沿Z。
        bool upright = (string)patternModelInfo.Invoke(model, new object[] { "kind" }) == "biped";
        int axis = upright ? 0 : 1;
        int baseSlots = names.Count;

        for (int partIndex = 0; partIndex < parts.Length; partIndex++)
        {
            object source = original.GetValue(partIndex);
            var sourcePos = (float[])partPositions.GetValue(source);
            var sourceNormals = (float[])patternNormals.GetValue(source);
            var sourceTri = (int[])partTriangles.GetValue(source);
            var sourceSlots = (byte[])patternSlots.GetValue(source);
            var sourceAxes = (byte[])patternAxes.GetValue(source);
            var positions = new List<float>();
            var normals = new List<float>();
            var triangles = new List<int>();
            var faceSlots = new List<byte>();
            var faceAxes = new List<byte>();
            var grouped = new Dictionary<PatternSample, List<int>>();
            var polygon = new List<PatternVertex>(7);
            var clipped = new List<PatternVertex>(7);

            for (int face = 0; face < sourceTri.Length; face += 3)
            {
                PatternVertex a = Vertex(sourceTri[face]), b = Vertex(sourceTri[face + 1]), c = Vertex(sourceTri[face + 2]);
                Vector2 lower = Vector2.Min(a.Pixel, Vector2.Min(b.Pixel, c.Pixel)),
                    upper = Vector2.Max(a.Pixel, Vector2.Max(b.Pixel, c.Pixel));
                int x0 = Mathf.FloorToInt(lower.x), y0 = Mathf.FloorToInt(lower.y),
                    x1 = Mathf.Max(x0, Mathf.CeilToInt(upper.x) - 1), y1 = Mathf.Max(y0, Mathf.CeilToInt(upper.y) - 1);
                PatternSample first = plan.Pixels[y0 * plan.Width + x0];
                bool uniform = true;
                for (int y = y0; y <= y1 && uniform; y++)
                for (int x = x0; x <= x1; x++)
                    if (plan.Pixels[y * plan.Width + x] != first) { uniform = false; break; }
                if (uniform)
                {
                    Emit(a, b, c, first, face / 3);
                    continue;
                }
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    polygon.Clear(); polygon.Add(a); polygon.Add(b); polygon.Add(c);
                    ClipPattern(polygon, clipped, 0, x, true);
                    ClipPattern(polygon, clipped, 0, x + 1, false);
                    ClipPattern(polygon, clipped, 1, y, true);
                    ClipPattern(polygon, clipped, 1, y + 1, false);
                    PatternSample paint = plan.Pixels[y * plan.Width + x];
                    for (int i = 1; i + 1 < polygon.Count; i++) Emit(polygon[0], polygon[i], polygon[i + 1], paint, face / 3);
                }
            }

            object derivedPart = clonePatternPart.Invoke(source, null);
            partPositions.SetValue(derivedPart, positions.ToArray()); patternNormals.SetValue(derivedPart, normals.ToArray());
            partTriangles.SetValue(derivedPart, triangles.ToArray()); patternSlots.SetValue(derivedPart, faceSlots.ToArray());
            patternAxes.SetValue(derivedPart, faceAxes.ToArray());
            patternVersion.SetValue(derivedPart, (int)patternVersion.GetValue(source) + 1);
            parts.SetValue(derivedPart, partIndex);
            groups[partIndex] = grouped.OrderBy(pair => pair.Key.Id).Select(pair => new PatternGroup
                { Triangles = pair.Value.ToArray(), Layers = pair.Key.Layers }).ToArray();

            /// <summary>原顶点与法线沿相同制作坐标投影，边缘只往画布内收极小数值。</summary>
            /// <param name="index">原顶点编号。</param>
            PatternVertex Vertex(int index)
            {
                Vector3 point = new(sourcePos[index * 3], sourcePos[index * 3 + 1], sourcePos[index * 3 + 2]);
                return new PatternVertex
                {
                    Point = point,
                    Normal = new Vector3(sourceNormals[index * 3], sourceNormals[index * 3 + 1], sourceNormals[index * 3 + 2]),
                    Pixel = new Vector2(
                        Mathf.Lerp(plan.Bounds.xMin, plan.Bounds.xMax - .0001f, Mathf.InverseLerp(min[axis], max[axis], point[axis])),
                        Mathf.Lerp(plan.Bounds.yMin, plan.Bounds.yMax - .0001f, Mathf.InverseLerp(min.z, max.z, point.z)))
                };
            }

            /// <summary>显示与接触各引用同一新三角形，保留原法线和方向分类。</summary>
            /// <param name="a">第一顶点。</param><param name="b">第二顶点。</param><param name="c">第三顶点。</param>
            /// <param name="paint">实际像素来源。</param><param name="face">原三角形编号。</param>
            void Emit(PatternVertex a, PatternVertex b, PatternVertex c, PatternSample paint, int face)
            {
                if (Vector3.Cross(b.Point - a.Point, c.Point - a.Point).sqrMagnitude == 0f) return;
                byte slot = sourceSlots[face];
                paint = plan.ForMaterial(names[slot], paint);
                if (paint.Id != 0)
                {
                    var key = (slot, paint.Id);
                    if (!slots.TryGetValue(key, out byte paintedSlot))
                    {
                        if (names.Count >= 256) throw new InvalidDataException("Worldfall纹样超出单模型256种三角面配色：" + names.Count);
                        paintedSlot = (byte)names.Count;
                        slots.Add(key, paintedSlot);
                        names.Add("cultiway.pattern." + names.Count);
                        kinds.Add(kinds[slot]); flags.Add(flags[slot]); colors.Add(paint.Apply(colors[slot]));
                        sources.Add(slot); paints.Add(paint);
                    }
                    slot = paintedSlot;
                }
                if (!grouped.TryGetValue(paint, out List<int> indices)) grouped.Add(paint, indices = new List<int>());
                foreach (PatternVertex vertex in new[] { a, b, c })
                {
                    int index = positions.Count / 3;
                    positions.Add(vertex.Point.x); positions.Add(vertex.Point.y); positions.Add(vertex.Point.z);
                    Vector3 normal = vertex.Normal.normalized;
                    normals.Add(normal.x); normals.Add(normal.y); normals.Add(normal.z);
                    triangles.Add(index); indices.Add(index);
                }
                faceSlots.Add(slot); faceAxes.Add(sourceAxes[face]);
            }
        }

        object result = derivePatternModel.Invoke(model, new[] { model.GetType().GetField("Name").GetValue(model), parts });
        patternSlotNames.SetValue(result, names.ToArray()); patternSlotKinds.SetValue(result, kinds.ToArray());
        patternSlotFlags.SetValue(result, flags.ToArray()); patternSlotColors.SetValue(result, colors.ToArray());
        return new PatternBody { Base = model, Model = result, BaseSlots = baseSlots, Colors = colors.ToArray(),
            Sources = sources.ToArray(), Paints = paints.ToArray(), Groups = groups };
    }

    /// <summary>原面内裁切保留位置、法线和制作坐标，不产生外壳或封口。</summary>
    /// <param name="polygon">本轮多边形。</param><param name="work">可复用工作区。</param>
    /// <param name="axis">制作图横向或纵向。</param><param name="boundary">像素边界。</param>
    /// <param name="greater">保留边界较大的一侧。</param>
    private static void ClipPattern(List<PatternVertex> polygon, List<PatternVertex> work, int axis, float boundary, bool greater)
    {
        work.Clear();
        for (int i = 0; i < polygon.Count; i++)
        {
            PatternVertex a = polygon[(i + polygon.Count - 1) % polygon.Count], b = polygon[i];
            float da = a.Pixel[axis] - boundary, db = b.Pixel[axis] - boundary;
            bool aIn = greater ? da >= 0f : da <= 0f, bIn = greater ? db >= 0f : db <= 0f;
            if (aIn != bIn)
            {
                float at = da / (da - db);
                work.Add(new PatternVertex { Point = Vector3.LerpUnclamped(a.Point, b.Point, at),
                    Normal = Vector3.LerpUnclamped(a.Normal, b.Normal, at), Pixel = Vector2.LerpUnclamped(a.Pixel, b.Pixel, at) });
            }
            if (bIn) work.Add(b);
        }
        polygon.Clear(); polygon.AddRange(work);
    }

    /// <summary>制作时的原表面点，发布后只留下显示与接触共用数组。</summary>
    private struct PatternVertex
    {
        /// <summary>原模型坐标与法线。</summary>
        internal Vector3 Point, Normal;
        /// <summary>原身体制作图上的连续位置。</summary>
        internal Vector2 Pixel;
    }
}
