using System;
using System.Linq;
using Cultiway.Content.CreatureCompositions.Visuals;
using Cultiway.Content.Skills;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals;
using Cultiway.Core.Visuals.Forms;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Cultiway.Content.Combat;

/// <summary>地面兵器复制已经确认的实际表面，资源重建只读取实际装备资料。</summary>
internal sealed partial class GroundWeaponService
{
    /// <summary>原版独立兵器沿实际透明像素建立有限厚度，不使用整张图片矩形。</summary>
    /// <param name="image">实际装备图片。</param><param name="height">原像素深度参数，旧版缺失时按图片高度重建。</param>
    private static BodySurfacePose NativeAppearance(Sprite image, float height = 0f)
    {
        var parts = BodySurfaceBinding.NativePixels(image, "item", (height > 0f ? height : image.rect.height / image.pixelsPerUnit))
            .Select(part => new BodySurfacePart(part.Id, part.Vertices, part.Triangles, "item",
                imageCoordinates: part.ImageCoordinates)).ToArray();
        return new BodySurfacePose(parts, parts.Select(_ => Matrix4x4.identity).ToArray(), Vector3.zero, 1f, 0f);
    }

    /// <summary>先复制实际三角面与颜色，再以最长轴统一保存朝向和大小；不改变原持物或共享网格。</summary>
    /// <param name="surface">真实接触时的身体及原持物。</param><param name="image">贴图表面的实际图片。</param>
    /// <param name="form">主动借用的实际形体编号。</param>
    /// <param name="position">原兵器的实际世界中心。</param><param name="rotation">统一轴向后的实际朝向。</param>
    private static SpatialVisualMesh Bake(BodySurfacePose surface, Sprite image, out Vector3 position, out Quaternion rotation, string form = null)
    {
        int first = Array.FindIndex(surface.Parts, part => part.Attachment == "item" && part.Triangles.Length > 0);
        BodySurfacePart initial = surface.Parts[first];
        Matrix4x4 frame = surface.Matrices[initial.TransformIndex < 0 ? first : initial.TransformIndex];
        Quaternion inverse = Quaternion.Inverse(frame.rotation);
        Vector3 origin = frame.GetColumn(3);
        SpatialVisualMesh source = Extract(surface, image, inverse, origin, form == null ? null : FormResources.Load(form));
        Vector3 size = source.Bounds.size;
        Vector3 axis = size.x > size.y && size.x > size.z ? Vector3.right : size.y > size.z ? Vector3.up : Vector3.forward;
        Quaternion canonical = Quaternion.FromToRotation(axis, Vector3.forward);
        Vector3 center = source.Bounds.center;
        position = origin + frame.rotation * center;
        rotation = frame.rotation * Quaternion.Inverse(canonical);
        return new SpatialVisualMesh("ground-weapon", source.Vertices.Select(point => canonical * (point - center)).ToArray(),
            source.Normals.Select(normal => canonical * normal).ToArray(), source.Triangles, source.Colors, SpatialSurface.Metal);
    }

    /// <summary>沿资源三角面、当前数值变换和实际材质取几何，不重新选择尺寸和朝向。</summary>
    /// <param name="surface">实际资源与姿态。</param><param name="image">原图片。</param>
    /// <param name="inverse">当前参考方向的逆转角。</param><param name="origin">当前参考中心。</param>
    /// <param name="form">实际形体表面，空值使用持物自己的颜色。</param>
    private static SpatialVisualMesh Extract(BodySurfacePose surface, Sprite image, Quaternion inverse, Vector3 origin, FormResource form)
    {
        Color32[] pixels = surface.Parts.Any(part => part.Attachment == "item" && !part.HasColors)
            ? CreatureAppearanceService.ReadPixels(image) : null;
        var builder = new SpatialVisualMeshBuilder();
        for (int p = 0; p < surface.Parts.Length; p++)
        {
            BodySurfacePart part = surface.Parts[p];
            if (part.Attachment != "item") continue;
            Matrix4x4 matrix = surface.Matrices[part.TransformIndex < 0 ? p : part.TransformIndex];
            Vector3[] vertices = surface.Vertices(p);
            Color? formColor = form == null ? null : form.Surface(form.Description.Parts.Single(description =>
                part.Id == "part:" + description.Id).Surface, false).GetColor("_Color");
            for (int t = 0; t < part.Triangles.Length; t += 3)
            {
                int a = part.Triangles[t], b = part.Triangles[t + 1], c = part.Triangles[t + 2];
                Color32 color;
                if (formColor.HasValue) color = (Color32)formColor.Value;
                else if (part.FaceColors != null) color = part.FaceColors[t / 3];
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
        return builder.Build("ground-weapon", SpatialSurface.Metal);

    }

    /// <summary>把原部件变换保存到兵器自身坐标，只保存资源引用及数值姿态。</summary>
    /// <param name="record">正式地面资料。</param><param name="surface">真实接触表面。</param>
    /// <param name="form">主动形体编号。</param>
    private static void CaptureAppearance(Record record, BodySurfacePose surface, string form)
    {
        record.AppearanceSource = form != null ? "form" : AppearanceProvider == null ? "native" : "worldfall";
        record.Form = form;
        int[] parts = Enumerable.Range(0, surface.Parts.Length).Where(p => surface.Parts[p].Attachment == "item" &&
            surface.Parts[p].Triangles.Length > 0).ToArray();
        record.PartIds = parts.Select(p => surface.Parts[p].Id).ToArray();
        Matrix4x4 inverse = Matrix4x4.TRS(Vector(record.Position), Rotation(record), Vector3.one).inverse;
        record.PartTransforms = parts.Select(p =>
        {
            int index = surface.Parts[p].TransformIndex;
            Matrix4x4 matrix = inverse * surface.Matrices[index < 0 ? p : index];
            return Enumerable.Range(0, 16).Select(i => matrix[i]).ToArray();
        }).ToArray();
    }

    /// <summary>从稳定形体读取原部件，不把主动借用还原成原版贴图。</summary>
    /// <param name="form">已存在的完整形体。</param>
    private static BodySurfacePose FormAppearance(FormResource form)
    {
        BodySurfacePart[] parts = form.Description.Parts.Select((part, i) => new BodySurfacePart("part:" + part.Id,
            form.Meshes[i].vertices, form.Meshes[i].triangles, "item")).ToArray();
        return new BodySurfacePose(parts, parts.Select(_ => Matrix4x4.identity).ToArray(), Vector3.zero, 1f, 0f);
    }

    /// <summary>资源仍有效时恢复完整部件姿态；来源已移除时只报告并丢弃外观，保留原物品。</summary>
    /// <param name="item">恢复的真实原物品。</param><param name="record">数值资料。</param>
    /// <param name="scale">旧版或切换显示环境时的实际大小。</param>
    private static SpatialVisualMesh RestoreAppearance(Item item, Record record, out float scale)
    {
        Sprite image = ItemRendering.getItemMainSpriteFrame(item.getAsset());
        try
        {
            FormResource form = record.Form == null ? null : FormResources.Load(record.Form);
            BodySurfacePose surface = form != null ? FormAppearance(form) : AppearanceProvider?.Invoke(item) ?? NativeAppearance(image, record.ImageHeight);
            string currentSource = form != null ? "form" : AppearanceProvider == null ? "native" : "worldfall";
            int[] parts = Enumerable.Range(0, surface.Parts.Length).Where(p => surface.Parts[p].Attachment == "item" &&
                surface.Parts[p].Triangles.Length > 0).ToArray();
            if (record.PartTransforms != null && record.AppearanceSource == currentSource)
            {
                if (!record.PartIds.SequenceEqual(parts.Select(p => surface.Parts[p].Id)))
                    throw new InvalidDataException("兵器资源的部件布局已经改变");
                var matrices = (Matrix4x4[])surface.Matrices.Clone();
                for (int i = 0; i < parts.Length; i++)
                {
                    var matrix = new Matrix4x4();
                    for (int j = 0; j < 16; j++) matrix[j] = record.PartTransforms[i][j];
                    int index = surface.Parts[parts[i]].TransformIndex;
                    matrices[index < 0 ? parts[i] : index] = matrix;
                }
                scale = 1f;
                return SavedColors(Extract(surface.WithPartTransforms(matrices), image, Quaternion.identity, Vector3.zero, form), record);
            }
            SpatialVisualMesh rebuilt = Bake(surface, image, out _, out _, record.Form);
            scale = record.Length / rebuilt.Bounds.size.z;
            return rebuilt;
        }
        catch (Exception error) when (error is InvalidDataException or IOException)
        {
            ModClass.I.WorldSaves.LastReport.Skip("地面兵器", record.ItemId.ToString(), "外观资料未恢复：" + error.Message);
            record.Form = null; record.AppearanceSource = "item"; record.PartIds = null; record.PartTransforms = null; record.FaceColors = null;
            BodySurfacePose surface = AppearanceProvider?.Invoke(item) ?? NativeAppearance(image, record.ImageHeight);
            SpatialVisualMesh rebuilt = Bake(surface, image, out _, out _);
            scale = record.Length / rebuilt.Bounds.size.z;
            return rebuilt;
        }
    }


    /// <summary>保留接触时实际配色；资源更新导致面布局变化时，只丢弃旧色号。</summary>
    /// <param name="model">由当前资源重建的实际几何。</param><param name="record">已检查的保存资料。</param>
    private static SpatialVisualMesh SavedColors(SpatialVisualMesh model, Record record)
    {
        if (record.FaceColors == null) return model;
        if (record.FaceColors.Length != model.Colors.Length)
        {
            ModClass.I.WorldSaves.LastReport.Skip("地面兵器", record.ItemId.ToString(), "材质面布局已更新，采用当前资源配色");
            record.FaceColors = null;
            return model;
        }
        Color32[] colors = record.FaceColors.Select(value => new Color32((byte)value, (byte)(value >> 8),
            (byte)(value >> 16), (byte)(value >> 24))).ToArray();
        return new SpatialVisualMesh(model.Name, model.Vertices, model.Normals, model.Triangles, colors, model.Surface);
    }

}
