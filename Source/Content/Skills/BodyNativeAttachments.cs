using System;
using UnityEngine;
using Cultiway.Core.Skills;

namespace Cultiway.Content.Skills;

/// <summary>原版身体沿实际图片接入持物，不借用三维模组的武器模型。</summary>
internal abstract partial class BodySurfaceBinding
{
    /// <summary>原版实际持物、任务及其独立图片变换。</summary>
    private sealed partial class Native
    {
        /// <summary>采集开始时的真实持物资源，工具切换不沿用旧表面。</summary>
        private readonly IHandRenderer initialHeld;

        /// <summary>最初是否实际绘制持物；自身借用武器时的隐藏不算换物。</summary>
        private readonly bool initiallyHeldVisible;

        /// <summary>上次实际持物图与不透明轮廓，动画换图后重新采集。</summary>
        private Sprite heldSprite;

        /// <summary>原版持物的实际像素几何。</summary>
        private BodySurfacePart[] heldParts;

        /// <summary>上次合并的两份资源，姿态更新不复制相同几何。</summary>
        private BodySurfacePart[] combinedBody, combinedHeld, combinedParts;

        /// <summary>实际任务、资源或显示条件变化后解除旧持物；不让自己借用武器取消自己。</summary>
        private string HeldChangedReason => !ReferenceEquals(initialHeld, HeldAsset(Actor)) ||
                                            !ConstructedAbilityService.HasWeaponControl(Actor) &&
                                            initiallyHeldVisible !=
                                            (HeldAsset(Actor) != null && Actor.checkHasRenderedItem())
            ? "HeldChanged"
            : null;

        /// <summary>与原版同样选择实际物品、携带资源或任务工具；空任务不产生虚假持物。</summary>
        /// <param name="actor">实际人物。</param>
        private static IHandRenderer HeldAsset(Actor actor) => actor.canUseItems() && !actor.isEgg()
            ? actor.getHandRendererAsset()
            : null;

        /// <summary>图片平面的两个实际轴转换到共同空间；有限深度不改变原版投影。</summary>
        /// <param name="root">实际共同安装点。</param><param name="rotation">原版实际旋转。</param>
        /// <param name="scale">原版实际图片比例。</param><param name="flip">主图自身的水平翻转。</param>
        private static Matrix4x4 ImageMatrix(Vector3 root, Quaternion rotation, Vector3 scale, bool flip = false)
        {
            Vector3 horizontal = rotation * Vector3.right * (scale.x * (flip ? -1f : 1f)),
                vertical = rotation * Vector3.up * scale.y;
            Vector3 x = new(horizontal.x, 0f, horizontal.y),
                y = new(vertical.x, 0f, vertical.y),
                depth = y - new Vector3(0f, -1f, 1f) * Mathf.Abs(scale.y);
            Matrix4x4 matrix = Matrix4x4.identity;
            matrix.SetColumn(0, new Vector4(x.x, x.y, x.z, 0f));
            matrix.SetColumn(1, new Vector4(depth.x, depth.y, depth.z, 0f));
            matrix.SetColumn(2, new Vector4(y.x, y.y, y.z, 0f));
            matrix.SetColumn(3, new Vector4(root.x, root.y, root.z, 1f));
            return matrix;
        }

        /// <summary>沿原版独立持物的真实图集、比例、安装点和旋转添加表面，不取得物品所有权。</summary>
        /// <param name="body">同轮身体。</param><param name="rotation">原版已完成旋转。</param>
        private BodySurfacePose AppendHeld(BodySurfacePose body, Quaternion rotation)
        {
            Sprite raw = Actor.getRenderedItemSprite();
            if (raw == null) return body;
            IHandRenderer renderer = Actor.getCachedHandRendererAsset();
            int color = renderer.is_colored ? Actor.kingdom.getColor().GetHashCode() : -900000;
            Sprite image = DynamicSprites.getCachedAtlasItemSprite(DynamicSprites.getItemSpriteID(raw, color), raw);
            if (image != heldSprite)
            {
                heldSprite = image;
                AssetManager.dynamic_sprites_library.checkDirty();
                heldParts = NativePixels(image, "item", body.Height / Actor.actor_scale);
                for (int part = 0; part < heldParts.Length; part++)
                    heldParts[part] = new BodySurfacePart("item", heldParts[part].Vertices,
                        heldParts[part].Triangles, "item");
            }

            if (!ReferenceEquals(combinedBody, body.Parts) || !ReferenceEquals(combinedHeld, heldParts))
            {
                combinedBody = body.Parts;
                combinedHeld = heldParts;
                combinedParts = new BodySurfacePart[body.Parts.Length + heldParts.Length];
                body.Parts.CopyTo(combinedParts, 0);
                for (int part = 0; part < heldParts.Length; part++)
                    combinedParts[body.Parts.Length + part] = new BodySurfacePart("item", heldParts[part].Vertices,
                        heldParts[part].Triangles, "item", body.Matrices.Length + part);
            }

            // 与ActorManager一致：安装偏移不受调试物品放大影响，绘制深度使用未旋转的安装高度。
            Vector2 mount = Actor.getAnimationFrameData()?.pos_item ?? Vector2.zero;
            Vector3 offset = new(mount.x * Actor.current_scale.x, mount.y * Actor.current_scale.y, 0f);
            Vector3 turned = rotation * offset;
            Vector3 picture = new(body.Foot.x + turned.x, body.Foot.y + body.Foot.z + turned.y,
                -.01f + offset.y);
            Vector3 root = new(picture.x, picture.y - picture.z, picture.z);
            int debugScale = DebugConfig.isOn(DebugOption.RenderBigItems) ? 10 : 1;
            Matrix4x4 itemMatrix = ImageMatrix(root, rotation, Actor.current_scale * debugScale);
            var matrices = new Matrix4x4[body.Matrices.Length + heldParts.Length];
            body.Matrices.CopyTo(matrices, 0);
            for (int part = body.Matrices.Length; part < matrices.Length; part++) matrices[part] = itemMatrix;
            var result = new BodySurfacePose(combinedParts, matrices, body.Foot, body.Height, body.Heading)
                .WithOpaque(body.Opaque);
            return OccludeHeld(result);
        }

        /// <summary>原版持物在身体后方时扣除真实前景；实际出招身体变形后重新计算同一遮挡。</summary>
        /// <param name="pose">本轮实际身体与独立持物。</param>
        private static BodySurfacePose OccludeHeld(BodySurfacePose pose)
        {
            BodySurfaceOcclusion[] masks = null;
            BodySurfaceOcclusion cover = null;
            for (int part = 0; part < pose.Parts.Length; part++)
            {
                BodySurfacePart piece = pose.Parts[part];
                if (piece.Attachment != "item") continue;
                Matrix4x4 matrix = pose.Matrices[piece.TransformIndex < 0 ? part : piece.TransformIndex];
                if (matrix.m13 <= pose.Foot.y || pose.Opaque == null) continue;
                cover ??= BodySurfaceOcclusion.Create(pose.Opaque);
                masks ??= new BodySurfaceOcclusion[pose.Parts.Length];
                masks[part] = cover;
            }

            return pose.WithOcclusions(masks);
        }
    }
}