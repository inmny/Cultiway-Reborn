using System;
using System.Linq;
using Cultiway.Content.CreatureCompositions.Visuals;
using Cultiway.Content.Skills;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Content.Combat;

/// <summary>地面兵器复制已经确认的实际表面，资源重建只读取实际装备资料。</summary>
internal sealed partial class GroundWeaponService
{
    /// <summary>原版独立兵器沿实际透明像素建立有限厚度，不使用整张图片矩形。</summary>
    /// <param name="image">实际装备图片。</param>
    private static BodySurfacePose NativeAppearance(Sprite image)
    {
        var parts = BodySurfaceBinding.NativePixels(image, "item", image.rect.height / image.pixelsPerUnit)
            .Select(part => new BodySurfacePart(part.Id, part.Vertices, part.Triangles, "item",
                imageCoordinates: part.ImageCoordinates)).ToArray();
        return new BodySurfacePose(parts, parts.Select(_ => Matrix4x4.identity).ToArray(), Vector3.zero, 1f, 0f);
    }

    /// <summary>先复制实际三角面与颜色，再以最长轴统一保存朝向和大小；不改变原持物或共享网格。</summary>
    /// <param name="surface">真实接触时的身体及原持物。</param><param name="image">贴图表面的实际图片。</param>
    /// <param name="position">原兵器的实际世界中心。</param><param name="rotation">统一轴向后的实际朝向。</param>
    private static SpatialVisualMesh Bake(BodySurfacePose surface, Sprite image, out Vector3 position, out Quaternion rotation)
    {
        int first = Array.FindIndex(surface.Parts, part => part.Attachment == "item" && part.Triangles.Length > 0);
        BodySurfacePart initial = surface.Parts[first];
        Matrix4x4 frame = surface.Matrices[initial.TransformIndex < 0 ? first : initial.TransformIndex];
        Quaternion inverse = Quaternion.Inverse(frame.rotation);
        Vector3 origin = frame.GetColumn(3);
        Color32[] pixels = surface.Parts.Any(part => part.Attachment == "item" && !part.HasColors)
            ? CreatureAppearanceService.ReadPixels(image) : null;
        var builder = new SpatialVisualMeshBuilder();
        for (int p = 0; p < surface.Parts.Length; p++)
        {
            BodySurfacePart part = surface.Parts[p];
            if (part.Attachment != "item") continue;
            Matrix4x4 matrix = surface.Matrices[part.TransformIndex < 0 ? p : part.TransformIndex];
            Vector3[] vertices = surface.Vertices(p);
            for (int t = 0; t < part.Triangles.Length; t += 3)
            {
                int a = part.Triangles[t], b = part.Triangles[t + 1], c = part.Triangles[t + 2];
                Color32 color;
                if (part.FaceColors != null) color = part.FaceColors[t / 3];
                else if (part.VertexColors != null) color = (Color32)(((Color)part.VertexColors[a] +
                    (Color)part.VertexColors[b] + (Color)part.VertexColors[c]) / 3f);
                else
                {
                    Vector2 uv = part.ImageCoordinates == null ? Vector2.zero :
                        (part.ImageCoordinates[a] + part.ImageCoordinates[b] + part.ImageCoordinates[c]) / 3f;
                    int x = Mathf.Clamp(Mathf.FloorToInt(uv.x * image.pixelsPerUnit + image.pivot.x), 0, (int)image.rect.width - 1),
                        y = Mathf.Clamp(Mathf.FloorToInt(uv.y * image.pixelsPerUnit + image.pivot.y), 0, (int)image.rect.height - 1);
                    color = pixels[y * (int)image.rect.width + x];
                    if (color.a == 0) color = new Color32(180, 185, 190, 255);
                }
                builder.AddFace(new[] { inverse * (matrix.MultiplyPoint3x4(vertices[a]) - origin),
                    inverse * (matrix.MultiplyPoint3x4(vertices[b]) - origin),
                    inverse * (matrix.MultiplyPoint3x4(vertices[c]) - origin) }, color);
            }
        }
        SpatialVisualMesh source = builder.Build("ground-weapon", SpatialSurface.Metal);
        Vector3 size = source.Bounds.size;
        Vector3 axis = size.x > size.y && size.x > size.z ? Vector3.right : size.y > size.z ? Vector3.up : Vector3.forward;
        Quaternion canonical = Quaternion.FromToRotation(axis, Vector3.forward);
        Vector3 center = source.Bounds.center;
        position = origin + frame.rotation * center;
        rotation = frame.rotation * Quaternion.Inverse(canonical);
        return new SpatialVisualMesh("ground-weapon", source.Vertices.Select(point => canonical * (point - center)).ToArray(),
            source.Normals.Select(normal => canonical * normal).ToArray(), source.Triangles, source.Colors, SpatialSurface.Metal);
    }
}
