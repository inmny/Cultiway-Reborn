using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cultiway.Content.Skills;

/// <summary>同一组实际表面和镜内坐标留给显示及后续局部作用。</summary>
internal sealed class MirrorReflection : IDisposable
{
    /// <summary>原人物对象和身份，仅由实际受理给出。</summary>
    internal readonly Actor Actor;
    internal readonly long ActorId;

    /// <summary>当前与镜面对应的实际姿态和世界到镜内的变换。</summary>
    internal BodySurfacePose Pose { get; private set; }
    internal Matrix4x4 Projection { get; private set; }

    /// <summary>闲置人物复用采集资料，正在出招的人物读取其实际姿态。</summary>
    private BodySurfaceBinding binding;

    /// <summary>身体、衣甲和较亮的头手细节共用一次投影，各自有五官与衣甲的明暗差。</summary>
    private readonly ReflectionLayer[] layers =
    {
        new("mirror_echo_body"), new("mirror_echo_armor"), new("mirror_echo_detail")
    };

    /// <summary>承接本次已选中的原人物。</summary>
    /// <param name="actor">实际人物。</param>
    internal MirrorReflection(Actor actor)
    {
        Actor = actor;
        ActorId = actor.id;
    }

    /// <summary>更新本轮镜中姿态；原版和 Worldfall 读取同一份几何。</summary>
    /// <param name="mirror">当前完整镜口的地图姿态。</param><param name="rule">编译后的镜口。</param>
    /// <param name="time">共同确认时点。</param><param name="sharedPose">已经确认的受力或出招姿态。</param>
    internal bool Update(Matrix4x4 mirror, CompiledMirrorImage rule, double time, BodySurfacePose sharedPose)
    {
        Pose = sharedPose;
        if (Pose == null)
        {
            if (binding == null || binding.ChangedReason != null) binding = BodySurfaceBinding.Create(Actor);
            if (binding == null) return false;
            ConstructedAbilityService.StartingPose(Actor, out Vector3 foot, out float height);
            binding.Capture(foot, height, time);
            Pose = binding.To;
        }
        if (Pose == null || Pose.Parts.Length == 0) return false;

        Projection = Project(mirror, rule, Pose.Bounds());
        foreach (ReflectionLayer layer in layers) layer.Begin();
        for (int i = 0; i < Pose.Parts.Length; i++)
        {
            BodySurfacePart part = Pose.Parts[i];
            bool details = part.Matches("head") || part.Matches("hand.left") || part.Matches("hand.right");
            bool armor = part.Attachment != null || part.Matches("armor.chest") || part.Matches("armor.helmet") ||
                         part.Matches("armor.boots");
            ReflectionLayer layer = layers[armor ? 1 : details ? 2 : 0];
            Matrix4x4 mapping = Projection * Pose.Matrices[part.TransformIndex < 0 ? i : part.TransformIndex];
            layer.Append(mapping, Pose.Vertices(i), part.Triangles);
        }
        return true;
    }

    /// <summary>左右互换，保留真实高度和薄层深度。整个投影的外接矩形放在实际圆镜内。</summary>
    /// <param name="mirror">当前镜面姿态。</param><param name="rule">固定镜口资料。</param>
    /// <param name="body">原身体的当前实际边界。</param>
    private static Matrix4x4 Project(Matrix4x4 mirror, CompiledMirrorImage rule, Bounds body)
    {
        Vector3 right = mirror.GetColumn(0);
        right.z = 0f;
        right = right.sqrMagnitude > .00001f ? right.normalized : Vector3.right;
        Vector3 depth = Vector3.Cross(Vector3.forward, right);
        float width = Mathf.Abs(right.x) * body.extents.x + Mathf.Abs(right.y) * body.extents.y;
        float thickness = Mathf.Abs(depth.x) * body.extents.x + Mathf.Abs(depth.y) * body.extents.y;
        float scale = rule.Radius * rule.Fill / Mathf.Max(.001f, new Vector2(width, body.extents.z).magnitude);
        float relief = Mathf.Min(scale * .50f, rule.Radius * .095f / Mathf.Max(.001f, thickness));
        Vector3 centre = rule.Centre + Vector3.down * (rule.Radius * .04f + relief * thickness);
        var projection = Matrix4x4.identity;
        projection.SetRow(0, new Vector4(-right.x * scale, -right.y * scale, 0f, centre.x + Vector3.Dot(right, body.center) * scale));
        projection.SetRow(1, new Vector4(depth.x * relief, depth.y * relief, 0f, centre.y - Vector3.Dot(depth, body.center) * relief));
        projection.SetRow(2, new Vector4(0f, 0f, scale, centre.z - body.center.z * scale));
        return projection;
    }

    /// <summary>按相同镜口姿态发布可见层。</summary>
    /// <param name="resource">实际镜框资源。</param><param name="mirror">完整地图姿态。</param>
    /// <param name="terrain">本轮绝对地表。</param><param name="output">共用显示缓冲。</param>
    internal void Append(FormResource resource, Matrix4x4 mirror, AbilityHeightField terrain,
        List<FormVisualInstance> output)
    {
        foreach (ReflectionLayer layer in layers)
            if (layer.Upload())
                output.Add(new FormVisualInstance(resource, layer.Mesh, mirror, null, terrain, layer.Surface));
    }

    /// <summary>结束时销毁本次网格，不修改共享镜框或人物资源。</summary>
    public void Dispose()
    {
        foreach (ReflectionLayer layer in layers)
            if (layer.Mesh != null) Object.Destroy(layer.Mesh);
    }

    /// <summary>一个明暗层复用缓冲和网格；空层不创建网格。</summary>
    private sealed class ReflectionLayer
    {
        /// <summary>当前已上传的网格和共用材质名。</summary>
        internal Mesh Mesh { get; private set; }
        internal readonly string Surface;

        /// <summary>只保留本轮的复制资料，原人物的顶点不改动。</summary>
        private readonly List<Vector3> vertices = new();
        private readonly List<int> indices = new();

        /// <summary>记录所用的材质。</summary><param name="surface">材质名。</param>
        internal ReflectionLayer(string surface) => Surface = surface;

        /// <summary>复用上一轮的容量。</summary>
        internal void Begin() { vertices.Clear(); indices.Clear(); }

        /// <summary>完整复制部位的实际表面，反射后反转三角面朝向。</summary>
        /// <param name="projection">本部位的镜中变换。</param><param name="points">实际局部顶点。</param>
        /// <param name="triangles">实际三角面。</param>
        internal void Append(Matrix4x4 projection, Vector3[] points, int[] triangles)
        {
            int first = vertices.Count;
            foreach (Vector3 point in points) vertices.Add(projection.MultiplyPoint3x4(point));
            for (int i = 0; i < triangles.Length; i += 3)
            {
                indices.Add(first + triangles[i]);
                indices.Add(first + triangles[i + 2]);
                indices.Add(first + triangles[i + 1]);
            }
        }

        /// <summary>上传这一层确实存在的表面。</summary>
        internal bool Upload()
        {
            if (indices.Count == 0) return false;
            if (Mesh == null)
            {
                Mesh = new Mesh { name = "Cultiway " + Surface };
                Mesh.MarkDynamic();
            }
            if (vertices.Count > ushort.MaxValue) Mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            Mesh.SetVertices(vertices);
            Mesh.SetTriangles(indices, 0);
            Mesh.RecalculateNormals();
            return true;
        }
    }
}
