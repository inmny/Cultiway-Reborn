using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>原版的被抓持目标按共同表面固定图片和位置，不继续显示下一帧的翅膀。</summary>
internal abstract partial class BodySurfaceBinding
{
    /// <summary>模型投影借用原图片池入口，实际绘制采用模型面色。</summary>
    private Sprite nativeModelPicture;

    /// <summary>图片池只在模型布局换版时更新入口，不随自然动画切帧。</summary>
    private BodySurfacePart[] nativeModelParts;

    /// <summary>活动模型发布共同脚下和持物，出招者与目标使用同一实际尺度。</summary>
    /// <param name="pose">已确认的真实模型表面。</param><param name="data">原版本轮图片资料。</param>
    /// <param name="index">实际人物在图片资料中的顺序。</param>
    private bool PresentNativeModel(BodySurfacePose pose, ActorRenderData data, int index)
    {
        if (!Array.Exists(pose.Parts, part => part.FaceColors?.Length > 0)) return false;
        if (nativeModelParts != pose.Parts)
        {
            nativeModelParts = pose.Parts;
            nativeModelPicture = data.main_sprite_colored[index];
        }
        Vector3 root = new(pose.Foot.x, pose.Foot.y + pose.Foot.z, pose.Foot.z);
        data.has_normal_render[index] = true;
        data.positions[index] = root;
        data.scales[index] = Vector3.one;
        data.rotations[index] = Vector3.zero;
        data.flip_x_states[index] = false;
        data.main_sprite_colored[index] = nativeModelPicture;
        data.has_item[index] = Array.Exists(pose.Parts, part => part.Attachment == "item" && part.Triangles.Length > 0);
        if (data.has_item[index])
        {
            // 这里只借用图片池的绘制入口，持物的形状和颜色均来自同一实际模型。
            data.item_pos[index] = root;
            data.item_scale[index] = Vector3.one;
            data.item_sprites[index] = nativeModelPicture;
        }
        return true;
    }

    /// <summary>普通像素身体的实际图片交接。</summary>
    private sealed partial class Native
    {
        /// <summary>一个绑定期间保留实际出现的图片布局，随绑定释放，不成为全局图片缓存。</summary>
        private readonly Dictionary<BodySurfacePart[], (Sprite Body, Sprite Item)> nativeFrames = new();

        /// <summary>取得固定表面对应的最终合成图，不借用后来切换的动画帧。</summary>
        /// <param name="pose">实际被抓住的表面。</param>
        internal override Sprite PinnedSprite(BodySurfacePose pose) => nativeFrames.TryGetValue(pose.Parts, out var frame) ? frame.Body : null;

        /// <summary>普通绘制和接触沿同一平面变换；原版并行准备已经结束。</summary>
        /// <param name="pose">真实固定表面。</param><param name="data">本轮原版绘制资料。</param><param name="index">实际人物顺序。</param>
        internal override bool PresentPinned(BodySurfacePose pose, ActorRenderData data, int index)
        {
            if (!nativeFrames.TryGetValue(pose.Parts, out var frame)) return false;
            int body = Array.FindIndex(pose.Parts, part => part.Attachment != "item");
            data.has_normal_render[index] = body >= 0;
            if (body >= 0)
            {
                BodySurfacePart part = pose.Parts[body];
                Matrix4x4 matrix = pose.Matrices[part.TransformIndex < 0 ? body : part.TransformIndex];
                Project(matrix, out Vector3 root, out Vector3 scale, out Vector3 rotation, out bool flip);
                root.z = Actor.position_height;
                data.positions[index] = root; data.scales[index] = scale; data.rotations[index] = rotation;
                data.flip_x_states[index] = flip;
                data.main_sprite_colored[index] = frame.Body;
            }
            int item = Array.FindIndex(pose.Parts, part => part.Attachment == "item");
            data.has_item[index] = item >= 0 && frame.Item != null;
            if (data.has_item[index])
            {
                BodySurfacePart part = pose.Parts[item];
                Matrix4x4 matrix = pose.Matrices[part.TransformIndex < 0 ? item : part.TransformIndex];
                Project(matrix, out Vector3 root, out Vector3 scale, out _, out _);
                data.item_pos[index] = root; data.item_scale[index] = scale; data.item_sprites[index] = frame.Item;
            }
            return true;
        }

        /// <summary>共同空间的图片横轴和竖轴投回原版，保留真正接触时点的位置、旋转和翻转。</summary>
        /// <param name="matrix">图片真实平面变换。</param><param name="root">原版显示位置。</param>
        /// <param name="scale">实际显示比例。</param><param name="rotation">图片平面的旋转。</param><param name="flip">实际横向翻转。</param>
        private static void Project(Matrix4x4 matrix, out Vector3 root, out Vector3 scale, out Vector3 rotation, out bool flip)
        {
            Vector3 a = matrix.GetColumn(0), b = matrix.GetColumn(2), c = matrix.GetColumn(3);
            Vector2 x = new(a.x, a.y + a.z), y = new(b.x, b.y + b.z);
            root = new Vector3(c.x, c.y + c.z, c.z);
            scale = new Vector3(x.magnitude, y.magnitude, 1f);
            rotation = new Vector3(0f, 0f, Mathf.Atan2(y.y, y.x) * Mathf.Rad2Deg - 90f);
            flip = x.x * y.y - x.y * y.x < 0f;
        }
    }
}
