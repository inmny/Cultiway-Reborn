using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>原版普通持物沿真实安装点进入臂链，控制时保留原图片和物品。</summary>
internal abstract partial class BodySurfaceBinding
{
    /// <summary>只有人形提供此处的原版右臂，其他身体沿各自真实骨架接入。</summary>
    private sealed partial class Native
    {
        /// <summary>原版图片安装点就是当前持物握点，肩和两段骨长沿用共同人形骨架。</summary>
        /// <param name="surface">当前真实身体及原持物。</param>
        internal override BodyPose? HeldArm(BodySurfacePose surface)
        {
            if (!Actor.asset.is_humanoid || !Actor.hasWeapon() ||
                !ReferenceEquals(Actor.getHandRendererAsset(), Actor.getWeaponAsset())) return null;
            int item = Array.FindIndex(surface.Parts, p => p.Attachment == "item" && p.Triangles.Length > 0);
            if (item < 0) return null;
            BodySurfacePart part = surface.Parts[item];
            Vector3 hand = surface.Matrices[part.TransformIndex < 0 ? item : part.TransformIndex].GetColumn(3);
            float height = surface.Height;
            if (!surface.Supports("arm.right.upper")) return null;
            Vector3 shoulder = surface.Nearest("arm.right.upper", surface.Foot + Vector3.forward * height);
            Vector3 direction = hand - shoulder;
            float upper = height * CompiledBodyAction.UpperArm, lower = height * CompiledBodyAction.LowerArm;
            if (direction.magnitude > (upper + lower) * BodyJointSolver.Extension ||
                direction.magnitude < BodyJointSolver.MinimumReach(upper, lower)) return null;
            BodyJointSolver.Solve(shoulder, hand, upper, lower, Vector3.right,
                direction.normalized, null, direction.magnitude, -1f, -1f,
                out Vector3 elbow, out _, out Vector3 bend);
            return new BodyPose(surface.Foot, shoulder, elbow, hand, Quaternion.identity, height,
                Vector3.zero, 0f, Actor.flip ? Vector3.left : Vector3.right, 0f, bend);
        }

        /// <summary>实际袖、前臂和手的像素随原关节分段移动，其余身体与当前自然图片一致。</summary>
        /// <param name="surface">本轮原图片。</param><param name="before">原臂链。</param>
        /// <param name="after">新的真实臂链。</param>
        internal override BodySurfacePose ExternalArm(BodySurfacePose surface, in BodyPose before, in BodyPose after)
        {
            var vertices = new Vector3[surface.Parts.Length][];
            Quaternion upper = Quaternion.FromToRotation(before.Elbow - before.Shoulder, after.Elbow - after.Shoulder),
                lower = Quaternion.FromToRotation(before.Hand - before.Elbow, after.Hand - after.Elbow);
            for (int i = 0; i < surface.Parts.Length; i++)
            {
                BodySurfacePart part = surface.Parts[i];
                if (!BodySurfaceComposition.Replaces(part.Id, BodyOccupancy.RightArm))
                { vertices[i] = surface.Vertices(i); continue; }
                Matrix4x4 matrix = surface.Matrices[part.TransformIndex < 0 ? i : part.TransformIndex], inverse = matrix.inverse;
                Vector3[] original = surface.Vertices(i);
                vertices[i] = new Vector3[original.Length];
                for (int v = 0; v < original.Length; v++)
                {
                    Vector3 point = matrix.MultiplyPoint3x4(original[v]);
                    Vector3 moved = part.Id == "arm.right.upper"
                        ? after.Shoulder + upper * (point - before.Shoulder)
                        : part.Id == "arm.right.lower"
                            ? after.Elbow + lower * (point - before.Elbow)
                            : point + after.Hand - before.Hand;
                    vertices[i][v] = inverse.MultiplyPoint3x4(moved);
                }
            }
            BodySurfacePose result = surface.Deformed(vertices);
            if (surface.Opaque?.Parts.Length > 0)
            {
                var parts = new List<BodySurfacePart>();
                var bodyVertices = new List<Vector3[]>();
                for (int i = 0; i < surface.Parts.Length; i++)
                    if (surface.Parts[i].Attachment != "item")
                    { parts.Add(surface.Parts[i]); bodyVertices.Add(vertices[i]); }
                result = result.WithOpaque(new BodySurfacePose(parts.ToArray(), surface.Matrices,
                    surface.Foot, surface.Height, surface.Heading).Deformed(bodyVertices.ToArray()));
            }
            return result;
        }

        /// <summary>更新握点后按原版真实前后关系重新扣除遮挡。</summary>
        /// <param name="surface">本轮身体与臂链。</param><param name="original">原物品表面。</param>
        /// <param name="delta">实际握点平移。</param>
        internal override BodySurfacePose ExternalHeld(BodySurfacePose surface, BodySurfacePose original, Vector3 delta) =>
            OccludeHeld(base.ExternalHeld(surface, original, delta));
    }
}
