using System;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>恢复直接采集Worldfall原骨架关节，两种显示继续读取同一份表面。</summary>
internal static partial class BodyWorldfallRenderer
{
    /// <summary>身体自己的四条实际肢链。</summary>
    private sealed partial class SurfaceBinding
    {
        /// <summary>衣料只改本绑定的私有模型；自然身体仍持有原资源。</summary>
        private object recoveryModel;
        /// <summary>私有部位及复用法线，不改外部共享模型。</summary>
        private Array recoveryParts;
        /// <summary>对应私有部位的复用法线。</summary>
        private Vector3[][] recoveryNormals;
        /// <summary>本帧真正提交的身体，外部命中资料也读取它。</summary>
        internal object DisplayedBody;

        /// <summary>把共同衣摆顶点写入私有显示；浅复制实例承接当帧光照和摆放，不留下换世界后的资源改写。</summary>
        /// <param name="target">原人物本帧身体。</param><param name="pose">已经确认的共同表面。</param>
        private object DisplayRecovery(object target, BodySurfacePose pose)
        {
            if (!PinsWholeBody || !pose.HasDeformation) return target;
            if (recoveryModel == null)
            {
                recoveryModel = PrivateHeldModel(meshModel.GetValue(target), out recoveryParts);
                recoveryNormals = new Vector3[recoveryParts.Length][];
                for (int i = 0; i < recoveryParts.Length; i++)
                    recoveryNormals[i] = new Vector3[((float[])partPositions.GetValue(recoveryParts.GetValue(i))).Length / 3];
            }
            object originalModel = meshModel.GetValue(target);
            Vector3 min = ReadVector(heldModelMin.GetValue(originalModel)), max = ReadVector(heldModelMax.GetValue(originalModel));
            for (int i = 0; i < pose.Parts.Length; i++)
            {
                BodySurfacePart source = pose.Parts[i];
                int index = source.TransformIndex < 0 ? i : source.TransformIndex;
                if (source.Attachment == "item" || index >= recoveryParts.Length) continue;
                Vector3[] points = pose.Vertices(i);
                // 掌足为接触而切分过，仍沿原骨架矩阵显示；只有真实变形部位才改写原模型顶点。
                if (source.Triangles.Length == 0 || ReferenceEquals(points, source.Vertices)) continue;
                Bounds bounds = UpdateDisplayPart(recoveryParts.GetValue(index), points, recoveryNormals[index]);
                min = Vector3.Min(min, bounds.min); max = Vector3.Max(max, bounds.max);
            }
            heldModelMin.SetValue(recoveryModel, vectorValue(min)); heldModelMax.SetValue(recoveryModel, vectorValue(max));
            object display = cloneHeldPart.Invoke(target, null);
            meshModel.SetValue(display, recoveryModel);
            return display;
        }

        /// <summary>当前与自然矩阵分别提供原姿态和收势姿态，不推进外部动画。</summary>
        /// <param name="basis">真实接手表面。</param><param name="stance">整身动作。</param>
        /// <param name="right">右臂。</param><param name="left">左臂。</param>
        internal override IBodyRecoveryRig RecoveryRig(BodySurfacePose basis, BodyPose? stance = null,
            BodyPose? right = null, BodyPose? left = null)
        {
            if (To == null) return null;
            var joints = new Vector3[4][];
            var rest = new Vector3[4][];
            Quaternion yaw = Quaternion.AngleAxis((basis.Heading - To.Heading) * Mathf.Rad2Deg, Vector3.forward);
            for (int i = 0; i < 4; i++)
            {
                bool l = i % 2 == 0;
                int upper = (int)(i < 2 ? l ? upperLeft : upperRight : l ? upperLegLeft : upperLegRight).GetValue(rig),
                    lower = (int)(i < 2 ? l ? lowerLeft : lowerRight : l ? lowerLegLeft : lowerLegRight).GetValue(rig);
                if (upper < 0 || lower < 0) return null;
                Vector3 root = ReadVector(partPivot.GetValue(nativeParts.GetValue(upper))),
                    joint = ReadVector(partPivot.GetValue(nativeParts.GetValue(lower))),
                    end = i < 2 ? joint + new Vector3(l ? -.004f : .004f, -.012f, -.3f) : ReadVector((l ? soleLeft : soleRight).GetValue(rig));
                joints[i] = new[] { basis.Matrices[upper].MultiplyPoint3x4(root), basis.Matrices[lower].MultiplyPoint3x4(joint), basis.Matrices[lower].MultiplyPoint3x4(end) };
                rest[i] = new[] { To.Matrices[upper].MultiplyPoint3x4(root), To.Matrices[lower].MultiplyPoint3x4(joint), To.Matrices[lower].MultiplyPoint3x4(end) };
                rest[i] = Array.ConvertAll(rest[i], point => basis.Foot + yaw * (point - To.Foot));
            }
            var owners = new int[basis.Matrices.Length];
            for (int i = 0; i < nativeParts.Length; i++)
            {
                owners[i] = BodyRecoveryRig.PartOwner(Part(i));
                if (owners[i] != 0) continue;
                for (int parent = i; parent >= 0; parent = (int)parentPart.GetValue(nativeParts.GetValue(parent)))
                {
                    if (parent == (int)torsoPart.GetValue(rig)) break;
                    if (parent != (int)hips.GetValue(rig)) continue;
                    owners[i] = 13; break;
                }
            }
            foreach (HeldSurface piece in held)
            {
                owners[piece.Root] = 6;
                for (int i = 0; i < piece.Parts.Length; i++) owners[piece.Start + i] = 6;
            }
            return new BodyRecoveryRig(basis, joints, rest, matrixOwners: owners);
        }
    }
}
