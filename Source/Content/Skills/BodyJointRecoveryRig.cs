using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>非人身体沿自己的真实父子骨段和足面起身，原受力过程仍只持有一份控制。</summary>
internal sealed class BodyJointRecoveryRig : IBodyRecoveryRig
{
    /// <summary>原关节及实际足面约束，只有受力主线程调用，不进入能力工作线程。</summary>
    private readonly BodyJointBinding standing;
    /// <summary>同一原身体的实际关节和制作几何，重复受击不重新猜测骨段。</summary>
    private readonly BodyStructure original;
    /// <summary>本次接手的实际身体和原部件。</summary>
    public BodySurfacePose Basis { get; }
    /// <summary>完整恢复保持同一真实几何。</summary>
    public BodySurfacePart[] Parts => Basis.Parts;
    /// <summary>接手姿态中的真实父子连接。</summary>
    private readonly BodyJointSkeleton skeleton;
    /// <summary>每个矩阵所属真实骨段及该骨段的原模型矩阵。</summary>
    private readonly int[] owners, restMatrices;
    /// <summary>本次真实身体前方，不从人形髋骨推算。</summary>
    private readonly Vector3 forward;
    /// <summary>接手时完整的原根部前方和矩阵，归还读取完成后的真实方向。</summary>
    private readonly Vector3 basisForward;
    /// <summary>实际根部矩阵，不从人形骨盆推算。</summary>
    private readonly int rootMatrix;
    /// <summary>当前身体的实际水平前方。</summary>
    public Vector3 Forward => forward;
    /// <summary>各矩阵自己的真实三角面，地面和障碍继续读取这些顶点。</summary>
    private readonly BodySurfaceMesh[] meshes;
    /// <summary>已有顶点变形只读取原数值，不重新制作身体。</summary>
    private readonly Vector3[][] vertices;

    /// <summary>先把原关节映射到接手表面，再冻结父子、部件和原足部约束。</summary>
    /// <param name="standing">本身体实际足面求解。</param><param name="original">原真实结构。</param>
    /// <param name="basis">受击当刻的实际表面。</param>
    internal BodyJointRecoveryRig(BodyJointBinding standing, BodyStructure original, BodySurfacePose basis)
    {
        this.standing = standing; this.original = original; Basis = basis;
        var bones = new List<BodyStructureBone>();
        foreach (BodyStructureBone bone in original.Bones.Values)
        {
            int source = MatrixIndex(original.Surface, bone.Part), current = MatrixIndex(basis, bone.Part);
            Matrix4x4 map = basis.Matrices[current] * original.Surface.Matrices[source].inverse;
            bones.Add(new BodyStructureBone(bone.Part, bone.Parent, map.MultiplyPoint3x4(bone.Pivot), map.rotation * bone.Axes));
        }
        var structure = new BodyStructure(basis, bones, original.Supports.Select(foot => foot.Part));
        skeleton = new BodyJointSkeleton(structure, true);
        BodyStructureBone root = bones.First(bone => bone.Parent == null);
        basisForward = root.Axes * Vector3.forward;
        forward = basisForward; forward.z = 0f; forward.Normalize();
        rootMatrix = skeleton.MatrixFor(root.Part);
        var rest = bones.ToDictionary(bone => skeleton.MatrixFor(bone.Part), bone => MatrixIndex(original.Surface, bone.Part));
        owners = Enumerable.Range(0, basis.Matrices.Length).Select(skeleton.OwnerMatrix).ToArray();
        restMatrices = Array.ConvertAll(owners, owner => rest[owner]);
        vertices = Enumerable.Range(0, Parts.Length).Select(basis.Vertices).ToArray();
        meshes = new BodySurfaceMesh[basis.Matrices.Length];
        for (int matrix = 0; matrix < meshes.Length; matrix++)
        {
            int selected = matrix;
            BodySurfacePart[] parts = Parts.Select((part, index) => (part, index))
                .Where(item => (item.part.TransformIndex < 0 ? item.index : item.part.TransformIndex) == selected)
                .Select(item => new BodySurfacePart(item.part.Id, vertices[item.index], item.part.Triangles, transformIndex: selected)).ToArray();
            meshes[matrix] = new BodySurfaceMesh(new BodySurfacePose(parts, basis.Matrices, basis.Foot, basis.Height, basis.Heading));
        }
    }

    /// <summary>真实部位沿自己原矩阵取得关节和安装，不按名称制造位置。</summary>
    /// <param name="surface">实际表面。</param><param name="part">已有部位。</param>
    private static int MatrixIndex(BodySurfacePose surface, string part)
    {
        int index = Array.FindIndex(surface.Parts, piece => piece.Matches(part));
        return surface.Parts[index].TransformIndex < 0 ? index : surface.Parts[index].TransformIndex;
    }

    /// <summary>滚动保持接手姿态，起身才沿原父子接回当前真实地表上的站姿。</summary>
    /// <param name="motion">实际整体运动。</param><param name="upright">本次直立朝向。</param>
    /// <param name="progress">起身进度。</param><param name="terrain">共同地表。</param>
    public BodySurfaceTransform[] Sample(in BodySurfaceTransform motion, Quaternion upright, float progress, AbilityHeightField terrain)
    {
        if (progress <= 0f) return Enumerable.Repeat(motion, Basis.Matrices.Length).ToArray();
        Vector3 foot = motion.Matrix.MultiplyPoint3x4(Basis.Foot);
        BodySurfacePose target = standing.StandingSurface(foot, upright * forward, terrain);
        Matrix4x4 inverse = motion.Matrix.inverse;
        var desired = new Matrix4x4[Basis.Matrices.Length];
        for (int i = 0; i < desired.Length; i++)
            desired[i] = inverse * target.Matrices[restMatrices[i]] * Basis.Matrices[owners[i]].inverse * Basis.Matrices[i];
        BodySurfacePose blended = skeleton.Blend(Basis, new BodySurfacePose(Parts, desired, Basis.Foot, Basis.Height, Basis.Heading),
            Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress)));
        var result = new BodySurfaceTransform[desired.Length];
        for (int i = 0; i < result.Length; i++)
        {
            Matrix4x4 map = motion.Matrix * blended.Matrices[i] * Basis.Matrices[i].inverse;
            result[i] = new BodySurfaceTransform(Vector3.zero, map.GetColumn(3), map.rotation);
        }
        return result;
    }

    /// <summary>同一原模型只更新实际矩阵；足部、头尾和绘制不分开推进。</summary>
    /// <param name="motion">整体运动。</param><param name="bones">已经计算的真实部件。</param>
    /// <param name="terrain">共同地表。</param><param name="maximum">允许的承托上界。</param>
    public BodySurfacePose Surface(in BodySurfaceTransform motion, BodySurfaceTransform[] bones, AbilityHeightField terrain, float maximum)
    {
        var matrices = new Matrix4x4[Basis.Matrices.Length];
        for (int i = 0; i < matrices.Length; i++) matrices[i] = bones[i].Matrix * Basis.Matrices[i];
        Vector3 direction = bones[rootMatrix].Rotation * basisForward;
        return new BodySurfacePose(Parts, matrices, motion.Matrix.MultiplyPoint3x4(Basis.Foot), Basis.Height,
            Mathf.Atan2(direction.y, direction.x)).Deformed(vertices).WithRootMotion(motion);
    }

    /// <summary>身体及足面的真实顶点决定承托，不把原人物根点当作脚掌。</summary>
    /// <param name="bones">同刻实际姿态。</param><param name="terrain">共同地表。</param>
    /// <param name="maximum">承托上界。</param><param name="support">实际接触高度。</param>
    public float Lift(BodySurfaceTransform[] bones, AbilityHeightField terrain, float maximum, out float support)
    {
        float lift = float.NegativeInfinity; support = 0f;
        for (int i = 0; i < meshes.Length; i++)
        foreach (Vector3 original in meshes[i].Points)
        {
            Vector3 point = bones[i].Matrix.MultiplyPoint3x4(original);
            float floor = BodyLocomotion.Floor(terrain, point, maximum, 0f);
            if (floor - point.z <= lift) continue;
            lift = floor - point.z; support = floor;
        }
        return lift;
    }

    /// <summary>各原骨段扫过真实障碍，不扩大为人形胶囊。</summary>
    /// <param name="from">实际前态。</param><param name="to">本步候选。</param><param name="terrain">共同地表。</param>
    /// <param name="obstacles">真实环境。</param><param name="maximum">承托上界。</param>
    public bool Blocked(BodySurfaceTransform[] from, BodySurfaceTransform[] to, AbilityHeightField terrain,
        IReadOnlyList<BodySurfaceObstacle> obstacles, float maximum)
    {
        if (obstacles.Count == 0) return false;
        for (int i = 0; i < meshes.Length; i++)
            if (BodySurfaceSweep.Find(meshes[i], from[i], to[i], terrain, obstacles, out _, ground: false)) return true;
        return false;
    }

    /// <summary>再次受击冻结已确认的真实表面，沿同一原骨架继续。</summary>
    /// <param name="surface">本次真实表面。</param><param name="bones">已经确认的部件。</param>
    public IBodyRecoveryRig Rebase(BodySurfacePose surface, BodySurfaceTransform[] bones) => new BodyJointRecoveryRig(standing, original, surface);
}
