using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>恢复时继续使用原真实表面；四条肢链各自保持骨长，持物跟随同一只手。</summary>
internal sealed class BodyRecoveryRig
{
    /// <summary>原身体及左右臂、左右腿的原姿态和自然收势关节。</summary>
    internal readonly BodySurfacePose Basis;
    /// <summary>各条链依次为根、中间关节、末端，均是实际世界坐标。</summary>
    internal readonly Vector3[][] Joints, Rest;
    /// <summary>同一实际部件对应的肢段，零为躯干，随后每条链占三个位置。</summary>
    private readonly int[] owners;
    /// <summary>各肢段冻结的真实三角面，扫掠不重新扩大为包络。</summary>
    private readonly BodySurfaceMesh[] meshes;
    /// <summary>每条链的真实两段长度。</summary>
    private readonly Vector2[] lengths;
    /// <summary>中立骨盆、前方、侧向和起身时允许的屈膝高度。</summary>
    private readonly Vector3 hip, forward, side;
    /// <summary>由真实腿高和撑地臂长确定的屈膝深度，避免短臂身体伸不到地面。</summary>
    private readonly float crouchDrop;
    /// <summary>右手已有持物时，只使用空着的左手撑地。</summary>
    private readonly bool held;
    /// <summary>像素部位共用图片矩阵，恢复时须拆开；真实骨架继续保留原矩阵编号。</summary>
    private readonly bool splitMatrices;
    /// <summary>外部骨架即使暂时被动作模型替换，也要同步其原矩阵位置。</summary>
    private readonly int[] matrixOwners;
    /// <summary>同一次恢复固定的显示布局，不能逐帧创建资源身份。</summary>
    internal readonly BodySurfacePart[] Parts;
    /// <summary>已有变形顶点仍来自原姿态，不逐帧复制几何。</summary>
    private readonly Vector3[][] vertices;

    /// <summary>一次复制关节，按现有部位归属冻结几何，后续步骤只处理数字。</summary>
    /// <param name="basis">实际接手的完整表面。</param><param name="joints">真实原关节。</param>
    /// <param name="rest">同一身体自然收势的关节。</param><param name="splitMatrices">是否拆分共用图片矩阵。</param>
    /// <param name="matrixOwners">原骨架矩阵所属肢段。</param>
    internal BodyRecoveryRig(BodySurfacePose basis, Vector3[][] joints, Vector3[][] rest, bool splitMatrices = false, int[] matrixOwners = null)
    {
        Basis = basis; Joints = joints; Rest = rest; this.splitMatrices = splitMatrices; this.matrixOwners = matrixOwners;
        Parts = new BodySurfacePart[basis.Parts.Length]; vertices = new Vector3[Parts.Length][];
        for (int i = 0; i < Parts.Length; i++)
        {
            BodySurfacePart part = basis.Parts[i];
            Parts[i] = splitMatrices ? new BodySurfacePart(part.Id, part.Vertices, part.Triangles, part.Attachment,
                group: part.Group, imageCoordinates: part.ImageCoordinates, faceColors: part.FaceColors, vertexColors: part.VertexColors).WithSelections(part.Selections) : part;
            vertices[i] = basis.Vertices(i);
        }
        lengths = new Vector2[4];
        for (int i = 0; i < 4; i++) lengths[i] = new Vector2(Vector3.Distance(joints[i][0], joints[i][1]), Vector3.Distance(joints[i][1], joints[i][2]));
        hip = (rest[2][0] + rest[3][0]) * .5f;
        side = rest[3][0] - rest[2][0]; side.z = 0f; side.Normalize();
        forward = Vector3.Cross(Vector3.forward, side).normalized;
        if (splitMatrices)
        {
            forward = new Vector3(Mathf.Cos(basis.Heading), Mathf.Sin(basis.Heading), 0f);
            side = Vector3.Cross(forward, Vector3.forward);
        }
        held = Array.Exists(basis.Parts, part => part.Attachment == "item");
        float hipHeight = Mathf.Max(0f, hip.z - basis.Foot.z);
        crouchDrop = hipHeight * .35f;
        for (int arm = 0; arm < (held ? 1 : 2); arm++)
        {
            Vector3 shoulder = Quaternion.AngleAxis(-80f, side) * (rest[arm][0] - hip);
            float reach = (lengths[arm].x + lengths[arm].y) * .8f, across = Basis.Height * .07f;
            float vertical = Mathf.Sqrt(Mathf.Max(0f, reach * reach - across * across));
            crouchDrop = Mathf.Max(crouchDrop, hipHeight + shoulder.z - Basis.Height * .035f - vertical);
        }
        crouchDrop = Mathf.Min(crouchDrop, hipHeight * .75f);
        owners = new int[basis.Parts.Length];
        var groups = new List<BodySurfacePart>[14];
        for (int i = 0; i < groups.Length; i++) groups[i] = new List<BodySurfacePart>();
        for (int i = 0; i < owners.Length; i++)
        {
            BodySurfacePart part = basis.Parts[i];
            int owner = part.Attachment == "item" ? 6 : 0;
            for (int chain = 0; chain < 4 && owner == 0; chain++)
            {
                string limb = chain < 2 ? "arm." : "leg.", hand = chain < 2 ? "hand." : "foot.";
                string suffix = chain % 2 == 0 ? "left" : "right";
                if (part.Matches(hand + suffix)) owner = chain * 3 + 3;
                else if (part.Matches(limb + suffix + ".lower")) owner = chain * 3 + 2;
                else if (part.Matches(limb + suffix + ".upper")) owner = chain * 3 + 1;
            }
            int matrix = part.TransformIndex < 0 ? i : part.TransformIndex;
            if (matrixOwners != null && matrixOwners[matrix] == 13) owner = 13;
            owners[i] = owner;
            groups[owner].Add(new BodySurfacePart(part.Id, basis.Vertices(i), part.Triangles, part.Attachment,
                part.TransformIndex < 0 ? i : part.TransformIndex));
        }
        meshes = new BodySurfaceMesh[groups.Length];
        for (int i = 0; i < groups.Length; i++) meshes[i] = new BodySurfaceMesh(new BodySurfacePose(groups[i].ToArray(), basis.Matrices, basis.Foot, basis.Height, basis.Heading));
    }

    /// <summary>原骨架制作名称与共同肢段使用同一编号。</summary>
    /// <param name="role">明确的原部位。</param>
    internal static int PartOwner(string role)
    {
        for (int i = 0; i < 4; i++)
        {
            string side = i % 2 == 0 ? "left" : "right", limb = i < 2 ? "arm." : "leg.";
            if (role == limb + side + ".upper") return i * 3 + 1;
            if (role == limb + side + ".lower") return i * 3 + 2;
            if (role == (i < 2 ? "hand." : "foot.") + side) return i * 3 + 3;
        }
        return 0;
    }

    /// <summary>整体旋转仍来自真实滚动，手足只在起身阶段逐渐建立支撑。</summary>
    /// <param name="motion">本次实际整体运动。</param><param name="upright">已确认的直立朝向。</param>
    /// <param name="progress">起身进度，零保留原姿态。</param><param name="terrain">共同地表。</param>
    internal BodySurfaceTransform[] Sample(in BodySurfaceTransform motion, Quaternion upright, float progress, AbilityHeightField terrain)
    {
        var result = new BodySurfaceTransform[14];
        Matrix4x4 motionMap = motion.Matrix;
        Vector3 originalHip = (Joints[2][0] + Joints[3][0]) * .5f;
        result[0] = result[13] = new BodySurfaceTransform(originalHip, motionMap.MultiplyPoint3x4(originalHip), motion.Rotation);
        // 滚动与分段起身使用同一枢轴，插值不能在阶段边界把身体中心当成肩膝关节。
        for (int chain = 0; chain < 4; chain++)
            for (int joint = 0; joint < 3; joint++)
            {
                Vector3 point = Joints[chain][joint];
                result[chain * 3 + joint + 1] = new BodySurfaceTransform(point, motionMap.MultiplyPoint3x4(point), motion.Rotation);
            }
        if (progress <= 0f) return result;
        float t = Mathf.Clamp01(progress), settle = Mathf.SmoothStep(0f, 1f, t);
        float crouch = Mathf.SmoothStep(0f, 1f, t / .35f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.5f, 1f, t)));
        Vector3 bodyShift = (hip - originalHip) * settle - Vector3.forward * crouchDrop * crouch;
        Quaternion lean = Quaternion.AngleAxis(-80f * crouch, side);
        Vector3 hipAt = motion.Matrix.MultiplyPoint3x4(originalHip + bodyShift);
        result[0] = new BodySurfaceTransform(originalHip, hipAt, motion.Rotation * lean);
        result[13] = new BodySurfaceTransform(originalHip, hipAt, motion.Rotation);
        Vector3 foot = motionMap.MultiplyPoint3x4(Basis.Foot);
        Matrix4x4[] rootMaps = splitMatrices ? Array.ConvertAll(result, bone => bone.Matrix) : null;
        for (int chain = 0; chain < 4; chain++)
        {
            Vector3[] source = Joints[chain], rest = Rest[chain];
            Vector3 root = chain >= 2 ? result[13].Matrix.MultiplyPoint3x4(Vector3.Lerp(source[0], rest[0], settle)) :
                splitMatrices ? SkinPoint(0, Vector3.Lerp(source[0], rest[0], settle), rootMaps) : result[0].Matrix.MultiplyPoint3x4(Vector3.Lerp(source[0], rest[0], settle));
            Vector3 initial = motion.Matrix.MultiplyPoint3x4(source[2]);
            Vector3 target = foot + upright * (rest[2] - Basis.Foot);
            float plant = Mathf.SmoothStep(0f, 1f, t / .35f);
            if (chain < 2)
            {
                float support = chain == 0 || !held ? crouch : 0f;
                Vector3 peak = hip - Vector3.forward * crouchDrop +
                    Quaternion.AngleAxis(-80f, side) * (rest[0] - hip);
                Vector3 ground = foot + upright * (peak - Basis.Foot + forward * (Basis.Height * .07f));
                ground.z = BodyRecoveryFloor.Height(terrain, ground, foot.z + Basis.Height * .2f, Basis.Height * .04f) + Basis.Height * .035f;
                target = Vector3.Lerp(target, ground, support);
                plant = settle;
            }
            else target.z = BodyRecoveryFloor.Height(terrain, target, foot.z + Basis.Height * .2f, Basis.Height * .04f);
            target = Vector3.Lerp(initial, target, plant);
            Vector3 pole = chain < 2 ? -forward : forward;
            Vector3 restDirection = (rest[2] - rest[0]).normalized;
            Vector3 restBend = rest[1] - rest[0] - restDirection * Vector3.Dot(rest[1] - rest[0], restDirection);
            if (restBend.sqrMagnitude > .00000001f) pole = Vector3.Slerp(restBend.normalized, pole, crouch);
            Vector3 oldDirection = (source[2] - source[0]).normalized;
            Vector3 oldBend = source[1] - source[0] - oldDirection * Vector3.Dot(source[1] - source[0], oldDirection);
            Vector3 bend = Vector3.Slerp(motion.Rotation * oldBend.normalized, upright * pole, settle);
            int index = chain * 3 + 1;
            float previousHeight = 0f, previousCorrection = 0f;
            for (int pass = 0; pass < 8; pass++)
            {
                BodyJointSolver.Solve(root, target, lengths[chain].x, lengths[chain].y, bend,
                    motion.Rotation * oldDirection, null, 0f, -1f, -1f, out Vector3 joint, out Vector3 end, out _, extension: 1f);
                Quaternion upper = Quaternion.FromToRotation(motion.Rotation * (source[1] - source[0]), joint - root) * motion.Rotation,
                    lower = Quaternion.FromToRotation(motion.Rotation * (source[2] - source[1]), end - joint) * motion.Rotation;
                result[index] = new BodySurfaceTransform(source[0], root, upper);
                result[index + 1] = new BodySurfaceTransform(source[1], joint, lower);
                result[index + 2] = new BodySurfaceTransform(source[2], end,
                    chain < 2 || !splitMatrices ? lower : Quaternion.Slerp(lower, upright, plant));
                if (chain >= 2 ? plant < .999f : crouch < .8f || chain == 1 && held) break;
                // 脚趾或掌面是真正的承托；骨架末端可略高，不能把整个人抬高来避免肢段穿地。
                float correction = float.NegativeInfinity;
                Matrix4x4 endMap = result[index + 2].Matrix;
                foreach (Vector3 point in meshes[index + 2].Points)
                {
                    Vector3 world = endMap.MultiplyPoint3x4(point);
                    correction = Mathf.Max(correction, BodyRecoveryFloor.Height(terrain, world, foot.z + Basis.Height * .2f, Basis.Height * .04f) - world.z);
                }
                if (float.IsNegativeInfinity(correction) || Mathf.Abs(correction) < .00001f) break;
                // 地面倾斜及掌足转动会改变校正量；由前后两次实际表面求收敛步长，避免残余穿地被误用于抬高骨盆。
                float shift = correction;
                if (pass > 0 && Mathf.Abs(correction - previousCorrection) > .000001f)
                    shift = -correction * (target.z - previousHeight) / (correction - previousCorrection);
                previousHeight = target.z; previousCorrection = correction;
                target.z += Mathf.Clamp(shift, -Basis.Height * .25f, Basis.Height * .25f);
            }
        }
        return result;
    }

    /// <summary>原部件可能共用一个图片矩阵，因此逐部件建立自己的变换，不能让另一条腿覆盖它。</summary>
    /// <param name="motion">同一整体根点。</param><param name="bones">同一时点的肢段。</param>
    /// <param name="terrain">共同地面。</param><param name="maximum">当前允许的承托上界。</param>
    internal BodySurfacePose Surface(in BodySurfaceTransform motion, BodySurfaceTransform[] bones, AbilityHeightField terrain, float maximum)
    {
        Matrix4x4[] maps = Array.ConvertAll(bones, bone => bone.Matrix);
        var posedVertices = (Vector3[][])vertices.Clone();
        var matrices = splitMatrices ? new Matrix4x4[Parts.Length] : (Matrix4x4[])Basis.Matrices.Clone();
        if (matrixOwners != null)
            for (int i = 0; i < matrixOwners.Length; i++) matrices[i] = maps[matrixOwners[i]] * Basis.Matrices[i];
        for (int i = 0; i < Parts.Length; i++)
        {
            int index = Basis.Parts[i].TransformIndex < 0 ? i : Basis.Parts[i].TransformIndex;
            matrices[splitMatrices ? i : index] = maps[owners[i]] * Basis.Matrices[index];
            if (Parts[i].Triangles.Length == 0 || !Skinned(owners[i])) continue;
            Matrix4x4 inverse = matrices[splitMatrices ? i : index].inverse;
            posedVertices[i] = Array.ConvertAll(vertices[i], point => inverse.MultiplyPoint3x4(SurfacePoint(owners[i], Basis.Matrices[index].MultiplyPoint3x4(point), maps, terrain, maximum)));
        }
        Vector3 facing = motion.Rotation * new Vector3(Mathf.Cos(Basis.Heading), Mathf.Sin(Basis.Heading), 0f);
        return new BodySurfacePose(Parts, matrices, motion.Matrix.MultiplyPoint3x4(Basis.Foot), Basis.Height,
            Mathf.Atan2(facing.y, facing.x)).Deformed(posedVertices).WithRootMotion(motion);
    }

    /// <summary>承托读取真正移动后的顶点，与显示完全相同。</summary>
    /// <param name="bones">实际肢段。</param><param name="terrain">本轮地面。</param>
    /// <param name="maximum">本轮允许的承托上界。</param><param name="support">实际承托。</param>
    internal float Lift(BodySurfaceTransform[] bones, AbilityHeightField terrain, float maximum, out float support)
    {
        Matrix4x4[] maps = Array.ConvertAll(bones, bone => bone.Matrix);
        float lift = float.NegativeInfinity; support = 0f;
        for (int i = 0; i < meshes.Length; i++)
        foreach (Vector3 point in meshes[i].Points)
        {
            Vector3 world = SurfacePoint(i, point, maps, terrain, maximum);
            float floor = BodyRecoveryFloor.Height(terrain, world, maximum, Basis.Height * .04f);
            if (floor - world.z <= lift) continue;
            lift = floor - world.z; support = floor;
        }
        return lift;
    }

    /// <summary>各实际肢段分别扫过同一环境；旋转中间经过的空间也会被检查。</summary>
    /// <param name="from">已确认肢段。</param><param name="to">候选肢段。</param><param name="terrain">共同地面。</param>
    /// <param name="obstacles">共同环境。</param><param name="maximum">当前允许的承托上界。</param>
    internal bool Blocked(BodySurfaceTransform[] from, BodySurfaceTransform[] to, AbilityHeightField terrain,
        IReadOnlyList<BodySurfaceObstacle> obstacles, float maximum)
    {
        if (obstacles.Count == 0) return false;
        Matrix4x4[] fromMaps = Array.ConvertAll(from, bone => bone.Matrix), toMaps = Array.ConvertAll(to, bone => bone.Matrix);
        for (int i = 0; i < meshes.Length; i++)
        {
            if (meshes[i].Points.Length == 0) continue;
            if (!Skinned(i))
            {
                if (BodySurfaceSweep.Find(meshes[i], from[i], to[i], terrain, obstacles, out _, ground: false)) return true;
                continue;
            }
            Bounds sweep = SweepBounds(i, from, to, fromMaps, toMaps, terrain, maximum, out float angle, out float radius);
            var candidates = BodySurfaceSweep.Candidates(sweep, obstacles);
            if (candidates.Count == 0) continue;
            int slices = Mathf.Max(1, Mathf.CeilToInt(angle * Mathf.Sqrt(radius / (8f * BodySurfaceSweep.Tolerance))));
            var before = Array.ConvertAll(meshes[i].Points, point => SurfacePoint(i, point, fromMaps, terrain, maximum));
            for (int slice = 1; slice <= slices; slice++)
            {
                var bones = new BodySurfaceTransform[from.Length];
                for (int bone = 0; bone < bones.Length; bone++) bones[bone] = BodySurfaceTransform.Lerp(from[bone], to[bone], slice / (float)slices);
                Matrix4x4[] maps = Array.ConvertAll(bones, bone => bone.Matrix);
                var after = Array.ConvertAll(meshes[i].Points, point => SurfacePoint(i, point, maps, terrain, maximum));
                if (BodySurfaceSweep.Deformed(meshes[i], before, after, candidates)) return true;
                before = after;
            }
        }
        return false;
    }

    /// <summary>蒙皮是骨段变换的连续混合；先包住相关骨段的完整旋转弧，再加入衣角可能达到的地面。</summary>
    /// <param name="part">实际部位。</param><param name="from">开始骨段。</param><param name="to">结束骨段。</param>
    /// <param name="fromMaps">开始矩阵。</param><param name="toMaps">结束矩阵。</param><param name="terrain">共同地面。</param>
    /// <param name="maximum">允许的模型承托上界。</param><param name="angle">相关骨段的最大转角。</param><param name="radius">相关骨段的保守转动半径。</param>
    private Bounds SweepBounds(int part, BodySurfaceTransform[] from, BodySurfaceTransform[] to, Matrix4x4[] fromMaps,
        Matrix4x4[] toMaps, AbilityHeightField terrain, float maximum, out float angle, out float radius)
    {
        Bounds source = meshes[part].Bounds, result = new(fromMaps[part].MultiplyPoint3x4(source.center), Vector3.zero);
        angle = radius = 0f;
        float padding = 0f;
        for (int bone = 0; bone < from.Length; bone++)
        {
            bool related = part is 8 or 11 ? bone == part || bone == part + 1 : bone == 0 || bone >= 7;
            if (!related) continue;
            float turn = Quaternion.Angle(from[bone].Rotation, to[bone].Rotation) * Mathf.Deg2Rad, reach = 0f;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = new((corner & 1) == 0 ? source.min.x : source.max.x,
                    (corner & 2) == 0 ? source.min.y : source.max.y, (corner & 4) == 0 ? source.min.z : source.max.z);
                reach = Mathf.Max(reach, Vector3.Distance(point, from[bone].Pivot));
                result.Encapsulate(fromMaps[bone].MultiplyPoint3x4(point));
                result.Encapsulate(toMaps[bone].MultiplyPoint3x4(point));
            }
            radius = Mathf.Max(radius, reach); angle = Mathf.Max(angle, turn);
            padding = Mathf.Max(padding, reach * turn * turn * .125f);
        }
        // 原版投影躯干还含肩部偏移；两次转动的差最多为肩髋距离的两倍。
        if (splitMatrices && part == 0)
        {
            Vector3 chest = (Joints[0][0] + Joints[1][0]) * .5f, origin = (Joints[2][0] + Joints[3][0]) * .5f;
            padding += Vector3.Distance(chest, origin) * 2f;
            radius *= 3f;
        }
        result.Expand(padding * 2f + BodySurfaceSweep.Tolerance * 4f);
        float ground = 0f;
        terrain?.Plane(new Rect(result.min.x, result.min.y, result.size.x, result.size.y), out _, out _, out ground, out _);
        result.Encapsulate(new Vector3(result.center.x, result.center.y, Mathf.Max(maximum + .0001f, ground)));
        return result;
    }

    /// <summary>柔软衣摆边缘可以折在地面上；同一顶点同时用于绘制、承托和环境扫掠，不抬高骨盆代替衣料弯折。</summary>
    /// <param name="owner">实际肢段。</param><param name="point">原世界顶点。</param>
    /// <param name="bones">实际关节。</param><param name="terrain">共同地表。</param><param name="maximum">允许的承托上界。</param>
    private Vector3 SurfacePoint(int owner, Vector3 point, Matrix4x4[] bones, AbilityHeightField terrain, float maximum)
    {
        Vector3 result = SkinPoint(owner, point, bones);
        if (owner is 0 or 13 && point.z < (Joints[2][0].z + Joints[3][0].z) * .5f)
            result.z = Mathf.Max(result.z, BodyRecoveryFloor.Height(terrain, result, maximum, Basis.Height * .04f));
        return result;
    }

    /// <summary>原版厚像素在腰和踝连续衔接，真实骨架自身的刚性部件不重新蒙皮。</summary>
    /// <param name="owner">当前肢段。</param>
    private bool Skinned(int owner) => owner is 0 or 13 || splitMatrices && owner is 8 or 11;

    /// <summary>只混合皮肤顶点，关节和骨长不缩放；显示和地表检查使用同一结果。</summary>
    /// <param name="owner">实际肢段。</param><param name="point">原世界顶点。</param><param name="bones">同一时点骨架。</param>
    private Vector3 SkinPoint(int owner, Vector3 point, Matrix4x4[] bones)
    {
        Vector3 current = bones[owner].MultiplyPoint3x4(point);
        if (!Skinned(owner)) return current;
        if (owner is 0 or 13)
        {
            Vector3 origin = (Joints[2][0] + Joints[3][0]) * .5f, chest = (Joints[0][0] + Joints[1][0]) * .5f;
            float weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(origin.z, chest.z, point.z));
            if (point.z < origin.z)
            {
                // 原部位表中的躯干也可能含长衣摆；腰以下随左右腿连续弯折，不能随骨盆一起压入地面。
                Vector3 a = GarmentPoint(2, point, bones), b = GarmentPoint(3, point, bones);
                Vector3 across = Joints[3][0] - Joints[2][0];
                float right = Mathf.Clamp01(Vector3.Dot(point - Joints[2][0], across) / across.sqrMagnitude);
                return Vector3.Lerp(a, b, right);
            }
            if (!splitMatrices || owner == 13) return current;
            Vector3 shift = bones[0].MultiplyPoint3x4(chest) - bones[13].MultiplyPoint3x4(chest);
            // 原版是投影身体，倾身时肩宽和头形不能跟着纸片的横轴翻倒；三维模型仍使用自己的真实骨架。
            return bones[13].MultiplyPoint3x4(point) + shift * weight;
        }
        int chain = (owner - 2) / 3;
        Vector3 axis = Joints[chain][2] - Joints[chain][1];
        float along = Vector3.Dot(point - Joints[chain][1], axis) / axis.sqrMagnitude;
        float distal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, .95f, along));
        return Vector3.Lerp(current, bones[owner + 1].MultiplyPoint3x4(point), distal);
    }

    /// <summary>衣摆从腰到膝踝依次跟随同侧腿，边界保持连续；只处理原版真实衣料。</summary>
    /// <param name="chain">同侧腿链。</param><param name="point">原衣摆顶点。</param>
    /// <param name="bones">同一时点的真实肢段。</param>
    private Vector3 GarmentPoint(int chain, Vector3 point, Matrix4x4[] bones)
    {
        int first = chain * 3 + 1;
        Vector3[] joints = Joints[chain];
        float upper = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(joints[0].z, joints[1].z, point.z)),
            lower = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(joints[1].z, joints[2].z, point.z));
        Vector3 thigh = Vector3.Lerp(bones[13].MultiplyPoint3x4(point), bones[first].MultiplyPoint3x4(point), upper),
            shin = SkinPoint(first + 1, point, bones);
        return Vector3.Lerp(thigh, shin, lower);
    }

    /// <summary>再受击从已经确认的关节交接，不能退回起身前的冻结全身。</summary>
    /// <param name="surface">实际表面。</param><param name="bones">当刻肢段。</param>
    internal BodyRecoveryRig Rebase(BodySurfacePose surface, BodySurfaceTransform[] bones)
    {
        var joints = new Vector3[4][];
        var rest = new Vector3[4][];
        Quaternion yaw = Quaternion.AngleAxis((surface.Heading - Basis.Heading) * Mathf.Rad2Deg, Vector3.forward);
        for (int i = 0; i < 4; i++)
        {
            int first = i * 3 + 1;
            joints[i] = new[] { bones[first].Matrix.MultiplyPoint3x4(Joints[i][0]),
                bones[first + 1].Matrix.MultiplyPoint3x4(Joints[i][1]), bones[first + 2].Matrix.MultiplyPoint3x4(Joints[i][2]) };
            rest[i] = Array.ConvertAll(Rest[i], point => surface.Foot + yaw * (point - Basis.Foot));
        }
        return new BodyRecoveryRig(surface, joints, rest, splitMatrices, matrixOwners);
    }
}

/// <summary>内容层提供实际承托；恢复数字不反向依赖人物或玩法服务。</summary>
internal static class BodyRecoveryFloor
{
    /// <summary>与共同身体运动共用地表及模型承托，初始化时登记一次。</summary>
    internal static Func<AbilityHeightField, Vector3, float, float, float> Height;
}
