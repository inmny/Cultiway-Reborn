using System;
using System.Linq;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>沿原身体的真实父子关节接回自然姿态，保留原几何和每段实际尺寸。</summary>
internal sealed class BodyJointSkeleton
{
    /// <summary>各真实骨段的矩阵、父段及其原局部关节。</summary>
    private readonly int[] matrices, parents, owners;
    /// <summary>本身体真实关节在各自原部件中的位置。</summary>
    private readonly Vector3[] pivots;
    /// <summary>本身体真实骨段名称，与矩阵一一对应。</summary>
    private readonly string[] names;

    /// <summary>从已经采集的实际身体建立一次对应，不预设四肢数量。</summary>
    /// <param name="structure">原身体实际结构。</param><param name="attachUnmapped">整身恢复时，未独立分骨的附着继续沿实际身体根部。</param>
    internal BodyJointSkeleton(BodyStructure structure, bool attachUnmapped = false)
    {
        BodyStructureBone[] bones = structure.Bones.Values.ToArray();
        names = Array.ConvertAll(bones, bone => bone.Part);
        matrices = new int[bones.Length]; parents = new int[bones.Length]; pivots = new Vector3[bones.Length];
        for (int i = 0; i < bones.Length; i++)
        {
            int piece = Array.FindIndex(structure.Surface.Parts, part => part.Matches(bones[i].Part));
            int matrix = structure.Surface.Parts[piece].TransformIndex;
            matrices[i] = matrix < 0 ? piece : matrix;
            parents[i] = Array.FindIndex(bones, parent => parent.Part == bones[i].Parent);
            pivots[i] = structure.Surface.Matrices[matrices[i]].inverse.MultiplyPoint3x4(bones[i].Pivot);
        }
        owners = new int[structure.Surface.Matrices.Length];
        for (int i = 0; i < owners.Length; i++) owners[i] = -1;
        for (int i = 0; i < structure.Surface.Parts.Length; i++)
        {
            BodySurfacePart part = structure.Surface.Parts[i]; int matrix = part.TransformIndex < 0 ? i : part.TransformIndex;
            for (int bone = 0; bone < bones.Length; bone++)
                if ((part.Matches(bones[bone].Part) || bones[bone].Part.StartsWith("part:", StringComparison.Ordinal) && part.Matches(bones[bone].Part.Substring(5))) &&
                    (owners[matrix] < 0 || structure.IsWithin(bones[bone].Part, bones[owners[matrix]].Part))) owners[matrix] = bone;
        }
        if (attachUnmapped)
            for (int i = 0; i < owners.Length; i++)
                if (owners[i] < 0) owners[i] = Array.FindIndex(parents, parent => parent < 0);
    }

    /// <summary>读取实际父骨矩阵，恢复中的其他部件继续保持相对安装。</summary>
    /// <param name="matrix">真实部件矩阵。</param>
    internal int OwnerMatrix(int matrix) => matrices[owners[matrix]];

    /// <summary>实际发力骨段仍沿自己的原矩阵读取挂点和朝向。</summary>
    /// <param name="part">已经绑定的原骨段。</param>
    internal int MatrixFor(string part) => matrices[Array.IndexOf(names, part)];

    /// <summary>交接后的承重点继续读取真实根关节。</summary>
    /// <param name="surface">当前共同身体。</param>
    internal Vector3 Center(BodySurfacePose surface)
    {
        int root = Array.FindIndex(parents, parent => parent < 0);
        return surface.Matrices[matrices[root]].MultiplyPoint3x4(pivots[root]);
    }

    /// <summary>关节跟随已经混合的真实父段，部件通过刚性变化接回目标，不缩扁原几何。</summary>
    /// <param name="from">本时刻实际动作。</param><param name="to">同一身体的自然姿态。</param><param name="weight">收势进度。</param>
    internal BodySurfacePose Blend(BodySurfacePose from, BodySurfacePose to, float weight)
    {
        if (weight <= 0f) return from;
        if (weight >= 1f) return to;
        var result = new Matrix4x4[from.Matrices.Length]; var done = new bool[matrices.Length];
        for (int i = 0; i < matrices.Length; i++) Solve(i);
        for (int i = 0; i < result.Length; i++)
        {
            int owner = owners[i];
            if (owner < 0) result[i] = Rigid(from.Matrices[i], to.Matrices[i], Vector3.zero, weight);
            else if (matrices[owner] != i)
            {
                Matrix4x4 a = from.Matrices[matrices[owner]].inverse * from.Matrices[i],
                    b = to.Matrices[matrices[owner]].inverse * to.Matrices[i];
                result[i] = result[matrices[owner]] * Rigid(a, b, Vector3.zero, weight);
            }
        }
        var surface = new BodySurfacePose(from.Parts, result, Vector3.Lerp(from.Foot, to.Foot, weight),
            from.Height, Mathf.LerpAngle(from.Heading * Mathf.Rad2Deg, to.Heading * Mathf.Rad2Deg, weight) * Mathf.Deg2Rad);
        if (from.Opaque != null)
        {
            var shadows = (Matrix4x4[])from.Opaque.Matrices.Clone();
            for (int i = 0; i < from.Opaque.Parts.Length; i++)
            {
                BodySurfacePart part = from.Opaque.Parts[i];
                int source = Array.FindIndex(from.Parts, piece => piece.Id == part.Id);
                if (source < 0) continue;
                int matrix = from.Parts[source].TransformIndex < 0 ? source : from.Parts[source].TransformIndex;
                shadows[part.TransformIndex < 0 ? i : part.TransformIndex] = result[matrix] * from.Matrices[matrix].inverse *
                    from.Opaque.Matrices[part.TransformIndex < 0 ? i : part.TransformIndex];
            }
            surface = surface.WithOpaque(new BodySurfacePose(from.Opaque.Parts, shadows, surface.Foot, surface.Height, surface.Heading));
        }
        return surface;

        /// <summary>父段先完成，子关节继续处于原父段的实际安装位置。</summary>
        /// <param name="bone">原骨段编号。</param>
        void Solve(int bone)
        {
            if (done[bone]) return;
            int matrix = matrices[bone], parent = parents[bone];
            Vector3 a = from.Matrices[matrix].MultiplyPoint3x4(pivots[bone]), b = to.Matrices[matrix].MultiplyPoint3x4(pivots[bone]);
            Vector3 target = Vector3.Lerp(a, b, weight);
            if (parent >= 0)
            {
                Solve(parent); int parentMatrix = matrices[parent];
                target = result[parentMatrix].MultiplyPoint3x4(Vector3.Lerp(from.Matrices[parentMatrix].inverse.MultiplyPoint3x4(a),
                    to.Matrices[parentMatrix].inverse.MultiplyPoint3x4(b), weight));
            }
            result[matrix] = Rigid(from.Matrices[matrix], to.Matrices[matrix], pivots[bone], weight, target);
            done[bone] = true;
        }
    }

    /// <summary>两端共享原比例与原几何，插值只施加真实相对旋转和关节位移。</summary>
    /// <param name="from">原变换。</param><param name="to">目标变换。</param><param name="pivot">原局部关节。</param>
    /// <param name="weight">收势进度。</param><param name="target">父段确定的关节位置，空值按两端插值。</param>
    private static Matrix4x4 Rigid(Matrix4x4 from, Matrix4x4 to, Vector3 pivot, float weight, Vector3? target = null)
    {
        Vector3 a = from.MultiplyPoint3x4(pivot), b = target ?? Vector3.Lerp(a, to.MultiplyPoint3x4(pivot), weight);
        Quaternion rotation = Quaternion.Slerp(Quaternion.identity, (to * from.inverse).rotation, weight);
        return Matrix4x4.TRS(b, rotation, Vector3.one) * Matrix4x4.Translate(-a) * from;
    }
}
