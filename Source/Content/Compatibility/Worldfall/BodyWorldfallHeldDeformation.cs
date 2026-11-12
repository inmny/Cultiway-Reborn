using System;
using System.Collections.Generic;
using System.Reflection;
using Cultiway.Core.Skills;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>弓形变形与搭箭显示沿同一实际表面，不修改外部共用资源。</summary>
internal static partial class BodyWorldfallRenderer
{
    /// <summary>外部已经提供的模型派生与部件复制。</summary>
    private static MethodInfo deriveHeldModel, cloneHeldPart;

    /// <summary>可变显示的顶点法线、三角面方向及上传版本。</summary>
    private static FieldInfo heldNormals, heldAxes, heldVersion, heldModelName, heldModelMin, heldModelMax;

    /// <summary>外部接口只在安装时查找。</summary>
    private static void InstallHeldDeformation()
    {
        Type model = meshModel.FieldType, part = modelParts.FieldType.GetElementType();
        deriveHeldModel = AccessTools.Method(model, "Derive");
        cloneHeldPart = AccessTools.Method(typeof(object), "MemberwiseClone");
        heldNormals = AccessTools.Field(part, "Nrm");
        heldAxes = AccessTools.Field(part, "Axis");
        heldVersion = AccessTools.Field(part, "Version");
        heldModelName = AccessTools.Field(model, "Name");
        heldModelMin = AccessTools.Field(model, "Min");
        heldModelMax = AccessTools.Field(model, "Max");
    }

    /// <summary>只复制将被改写的显示数组；原三角连接、材质和枢轴仍沿原资源。</summary>
    /// <param name="model">实际弓或弦模型。</param><param name="output">本绑定独立的显示部件。</param>
    private static object PrivateHeldModel(object model, out Array output)
    {
        var original = (Array)modelParts.GetValue(model);
        output = (Array)original.Clone();
        for (int i = 0; i < original.Length; i++)
        {
            object source = original.GetValue(i), part = cloneHeldPart.Invoke(source, null);
            partPositions.SetValue(part, ((float[])partPositions.GetValue(source)).Clone());
            heldNormals.SetValue(part, ((float[])heldNormals.GetValue(source)).Clone());
            heldAxes.SetValue(part, ((byte[])heldAxes.GetValue(source)).Clone());
            output.SetValue(part, i);
        }
        return deriveHeldModel.Invoke(model, new[] { heldModelName.GetValue(model), output });
    }


    /// <summary>同一实际顶点刷新私有显示和法线，身体衣料与持弓沿用同一上传路径。</summary>
    /// <param name="part">绑定独占的显示部件。</param><param name="vertices">共同表面的实际顶点。</param>
    /// <param name="normals">复用的法线数组。</param>
    private static Bounds UpdateDisplayPart(object part, Vector3[] vertices, Vector3[] normals)
    {
        Bounds bounds = new(vertices[0], Vector3.zero);
        Array.Clear(normals, 0, normals.Length);
        var raw = (float[])partPositions.GetValue(part);
        var triangles = (int[])partTriangles.GetValue(part);
        var axes = (byte[])heldAxes.GetValue(part);
        for (int v = 0; v < vertices.Length; v++)
        {
            Vector3 point = vertices[v];
            raw[v * 3] = point.x; raw[v * 3 + 1] = point.y; raw[v * 3 + 2] = point.z;
            bounds.Encapsulate(point);
        }
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            normals[a] += normal; normals[b] += normal; normals[c] += normal;
            Vector3 absolute = new(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            axes[i / 3] = (byte)(absolute.x >= absolute.y && absolute.x >= absolute.z ? 0 : absolute.y >= absolute.z ? 1 : 2);
        }
        var normalValues = (float[])heldNormals.GetValue(part);
        for (int v = 0; v < normals.Length; v++)
        {
            Vector3 normal = normals[v].normalized;
            normalValues[v * 3] = normal.x; normalValues[v * 3 + 1] = normal.y; normalValues[v * 3 + 2] = normal.z;
        }
        heldVersion.SetValue(part, (int)heldVersion.GetValue(part) + 1);
        return bounds;
    }

    /// <summary>人形的可变持物仍使用原有共同姿态与三角面查询。</summary>
    private sealed partial class SurfaceBinding
    {
        /// <summary>通常只有持弓与搭箭两种布局，同一布局的弯曲通过顶点快照插值。</summary>
        private readonly Dictionary<int, BodySurfacePart[]> heldLayouts = new();

        /// <summary>读取实际出现的持物与当前形状；没有显示的箭没有可接触面。</summary>
        /// <param name="matrices">已采集身体的共同矩阵。</param><param name="foot">共同脚下。</param>
        /// <param name="height">共同体高。</param><param name="facing">共同朝向。</param>
        /// <param name="natural">是否读取完整原生动作，其他情况读取本次重定位的持物。</param>
        private BodySurfacePose CaptureHeldSurface(Matrix4x4[] matrices, Vector3 foot, float height, float facing, bool natural)
        {
            int mask = 0;
            Vector3[][] deformed = Equals(itemKind, bowKind) ? new Vector3[parts.Length][] : null;
            for (int i = 0; i < held.Count; i++)
            {
                HeldSurface piece = held[i];
                object source = natural ? piece.NaturalField.GetValue(figure) : piece.Source;
                bool visible = source != null && (natural ? naturalMeshes : itemList).Contains(source);
                matrices[piece.Root] = visible ? WorldMatrix(source, -1) : Matrix4x4.identity;
                var sourceParts = visible ? (Array)modelParts.GetValue(meshModel.GetValue(source)) : null;
                for (int part = 0; part < piece.Parts.Length; part++)
                {
                    matrices[piece.Start + part] = visible ? WorldMatrix(source, part) : Matrix4x4.identity;
                    if (!visible || !piece.Deforms) continue;
                    var raw = (float[])partPositions.GetValue(sourceParts.GetValue(part));
                    var vertices = new Vector3[raw.Length / 3];
                    for (int v = 0; v < vertices.Length; v++) vertices[v] = new Vector3(raw[v * 3], raw[v * 3 + 1], raw[v * 3 + 2]);
                    deformed[piece.SurfaceStart + part] = vertices;
                }
                if (visible) mask |= 1 << i;
            }
            if (!heldLayouts.TryGetValue(mask, out BodySurfacePart[] layout))
            {
                layout = (BodySurfacePart[])parts.Clone();
                for (int i = 0; i < held.Count; i++)
                {
                    if ((mask & (1 << i)) != 0) continue;
                    HeldSurface piece = held[i];
                    for (int p = 0; p < piece.Parts.Length; p++)
                    {
                        int index = piece.SurfaceStart + p;
                        BodySurfacePart original = parts[index];
                        layout[index] = new BodySurfacePart(original.Id, original.Vertices, Array.Empty<int>(),
                            original.Attachment, original.TransformIndex, original.Group);
                    }
                }
                heldLayouts.Add(mask, layout);
            }
            var result = new BodySurfacePose(layout, matrices, foot, height, facing);
            return deformed == null ? result : result.Deformed(deformed);
        }

        /// <summary>合并身体动作可能移除其他部件，持物按稳定的变换编号寻找当前表面。</summary>
        /// <param name="piece">当前持物。</param><param name="part">物品中的部件。</param><param name="pose">最终共同表面。</param>
        private static int HeldSurfaceIndex(HeldSurface piece, int part, BodySurfacePose pose) =>
            Array.FindIndex(pose.Parts, value => value.Attachment == "item" && value.TransformIndex == piece.Start + part);

        /// <summary>冻结或插值后的弓形写入私有显示，同时刷新法线和模型边界。</summary>
        /// <param name="piece">本轮持物。</param><param name="pose">已确认的共同形状。</param>
        private static void DisplayHeldDeformation(HeldSurface piece, BodySurfacePose pose)
        {
            Vector3 min = Vector3.one * float.PositiveInfinity, max = Vector3.one * float.NegativeInfinity;
            for (int p = 0; p < piece.DisplayParts.Length; p++)
            {
                object part = piece.DisplayParts.GetValue(p);
                Vector3[] vertices = pose.Vertices(HeldSurfaceIndex(piece, p, pose)), normals = piece.DisplayNormals[p];
                Bounds bounds = UpdateDisplayPart(part, vertices, normals);
                min = Vector3.Min(min, bounds.min); max = Vector3.Max(max, bounds.max);
            }
            object model = meshModel.GetValue(piece.Display);
            heldModelMin.SetValue(model, vectorValue(min)); heldModelMax.SetValue(model, vectorValue(max));
        }
    }
}
