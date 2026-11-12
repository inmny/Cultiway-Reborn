using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>普通持物受控时读取实际骨架关节，不用标准人形位置代替外部模型。</summary>
internal static partial class BodyWorldfallRenderer
{
    /// <summary>同一模型接入可提供当前普通右臂。</summary>
    private sealed partial class SurfaceBinding
    {
        /// <summary>肩、肘和腕均取当前自然模型的同轮矩阵。</summary>
        /// <param name="surface">本轮实际模型表面。</param>
        internal override BodyPose? HeldArm(BodySurfacePose surface)
        {
            if (!Actor.hasWeapon() || !ReferenceEquals(Actor.getHandRendererAsset(), Actor.getWeaponAsset())) return null;
            int upper = (int)upperRight.GetValue(rig), lower = (int)lowerRight.GetValue(rig);
            if (upper < 0 || lower < 0) return null;
            Vector3 shoulder = surface.Matrices[upper].MultiplyPoint3x4(ReadVector(partPivot.GetValue(nativeParts.GetValue(upper)))),
                pivot = ReadVector(partPivot.GetValue(nativeParts.GetValue(lower))),
                elbow = surface.Matrices[lower].MultiplyPoint3x4(pivot),
                hand = surface.Matrices[lower].MultiplyPoint3x4(pivot + new Vector3(.004f, -.012f, -.3f));
            Vector3 direction = (hand - shoulder).normalized;
            Vector3 bend = elbow - shoulder - direction * Vector3.Dot(elbow - shoulder, direction);
            return new BodyPose(surface.Foot, shoulder, elbow, hand, Quaternion.identity, surface.Height,
                Vector3.zero, 0f, new Vector3(Mathf.Cos(surface.Heading), Mathf.Sin(surface.Heading), 0f), 0f, bend.normalized);
        }

        /// <summary>物品根点和各实际部件共用同一平移，原实例、外观及编号均保留。</summary>
        /// <param name="surface">本轮身体。</param><param name="original">真实建立时的表面。</param>
        /// <param name="delta">实际手腕平移。</param>
        internal override BodySurfacePose ExternalHeld(BodySurfacePose surface, BodySurfacePose original, Vector3 delta)
        {
            BodySurfacePose result = base.ExternalHeld(surface, original, delta);
            var matrices = (Matrix4x4[])result.Matrices.Clone();
            Matrix4x4 move = Matrix4x4.Translate(delta);
            foreach (var part in held) matrices[part.Root] = move * original.Matrices[part.Root];
            return result.WithPartTransforms(matrices);
        }
    }
}
