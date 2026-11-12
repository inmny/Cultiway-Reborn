using System;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>原版恢复从已经标注的真实像素部位取关节，不重新生成一具替身。</summary>
internal abstract partial class BodySurfaceBinding
{
    /// <summary>同一人形原图提供四条实际肢链。</summary>
    private sealed partial class Native
    {
        /// <summary>像素划分给出自然关节，正在出招的链优先使用同一轮正式求解结果。</summary>
        /// <param name="basis">真实接手表面。</param><param name="stance">整身动作。</param>
        /// <param name="right">右臂。</param><param name="left">左臂。</param>
        internal override BodyRecoveryRig RecoveryRig(BodySurfacePose basis, BodyPose? stance = null,
            BodyPose? right = null, BodyPose? left = null)
        {
            if (!Actor.asset.is_humanoid || To == null) return null;
            Vector3[][] original = Chains(basis), rest = Chains(To);
            if (original == null || rest == null) return null;
            int originalPart = Array.FindIndex(basis.Parts, part => part.ImageCoordinates != null && part.Attachment != "item"),
                naturalPart = Array.FindIndex(To.Parts, part => part.ImageCoordinates != null && part.Attachment != "item");
            if (originalPart < 0 || naturalPart < 0) return null;
            int originalIndex = basis.Parts[originalPart].TransformIndex < 0 ? originalPart : basis.Parts[originalPart].TransformIndex,
                naturalIndex = To.Parts[naturalPart].TransformIndex < 0 ? naturalPart : To.Parts[naturalPart].TransformIndex;
            // 原版转向是图片横轴反射；不能把有限纵深也绕竖轴翻到身体另一侧。
            Matrix4x4 align = basis.Matrices[originalIndex] * To.Matrices[naturalIndex].inverse;
            for (int i = 0; i < 4; i++) rest[i] = Array.ConvertAll(rest[i], align.MultiplyPoint3x4);
            if (left.HasValue) original[0] = new[] { left.Value.Shoulder, left.Value.Elbow, left.Value.Hand };
            if (right.HasValue) original[1] = new[] { right.Value.Shoulder, right.Value.Elbow, right.Value.Hand };
            var rig = new BodyRecoveryRig(RecoveryColors(basis), original, rest, true);
            nativeFrames[rig.Parts] = nativeFrames.TryGetValue(basis.Parts, out var frame) ? frame : (sprite, heldSprite);
            return rig;
        }

        /// <summary>按制作划分的上下肢边界取中点，掌足仍使用自己的真实部位。</summary>
        /// <param name="surface">实际像素或动作表面。</param>
        private static Vector3[][] Chains(BodySurfacePose surface)
        {
            var result = new Vector3[4][];
            for (int i = 0; i < 4; i++)
            {
                string side = i % 2 == 0 ? "left" : "right", limb = i < 2 ? "arm." : "leg.";
                if (!Bounds(limb + side + ".upper", out Bounds upper) || !Bounds(limb + side + ".lower", out Bounds lower)) return null;
                Vector3 start = upper.center, joint = (upper.center + lower.center) * .5f, end = lower.center;
                start.z = upper.max.z; joint.z = (upper.min.z + lower.max.z) * .5f; end.z = lower.min.z;
                if (Bounds((i < 2 ? "hand." : "foot.") + side, out Bounds distal))
                {
                    end = distal.center;
                    if (i >= 2) end.z = distal.min.z;
                }
                result[i] = new[] { start, joint, end };
            }
            return result;

            /// <summary>只统计真正属于本段的三角面，掌足归属不会使整个手臂变成长包络。</summary>
            /// <param name="id">实际制作部位。</param><param name="bounds">真实范围。</param>
            bool Bounds(string id, out Bounds bounds)
            {
                bool found = false; bounds = default;
                for (int part = 0; part < surface.Parts.Length; part++)
                {
                    BodySurfacePart piece = surface.Parts[part];
                    if (piece.Id != id) continue;
                    Matrix4x4 matrix = surface.Matrices[piece.TransformIndex < 0 ? part : piece.TransformIndex];
                    foreach (int vertex in piece.Triangles)
                    {
                        Vector3 point = matrix.MultiplyPoint3x4(surface.Vertices(part)[vertex]);
                        if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                        else bounds.Encapsulate(point);
                    }
                }
                return found;
            }
        }
    }
}
