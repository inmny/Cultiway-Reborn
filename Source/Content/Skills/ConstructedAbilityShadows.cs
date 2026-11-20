using System.Collections.Generic;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    // 一组可见立影只借用真实身体表面，无单位、生命和碰撞体。
    private static readonly List<BodyShadow> shadows = new(16);

    /// <summary>第一次真实作用时采集部位；保持同一次映照，无须每次触碰重新造网格。</summary>
    private static void ShowShadow(RunningCast running, int node, Actor target)
    {
        if (shadows.Exists(value => value.Cast == running && value.Node == node)) return;
        BodySurfacePose body = BodySelectionSurface(target);
        if (body == null) return;
        Mesh mesh = BuildShadowMesh(body);
        if (mesh == null) return;
        if (shadows.Count == 16) RemoveShadow(0);
        Vector3 toward = running.Owner(node).LastFoot - body.Foot;
        toward.z = 0f;
        // 立影离开真实人物的轮廓，沿来向的反面和侧边抬起，两个视角都能认出原身和投影。
        Vector3 side = new(toward.y, -toward.x, 0);
        side = side * .52f - toward;
        if (side.sqrMagnitude < .001f) side = Vector3.right;
        shadows.Add(new BodyShadow(running, node, mesh, body.Foot, side.normalized, simulation.contactTime));
    }

    private static Mesh BuildShadowMesh(BodySurfacePose body)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int i = 0; i < body.Parts.Length; i++)
        {
            BodySurfacePart part = body.Parts[i];
            if (part.Attachment != null) continue;
            Matrix4x4 matrix = body.Matrices[part.TransformIndex < 0 ? i : part.TransformIndex];
            int offset = vertices.Count;
            foreach (Vector3 p in body.Vertices(i)) vertices.Add(matrix.MultiplyPoint3x4(p) - body.Foot);
            foreach (int index in part.Triangles) triangles.Add(index + offset);
        }
        if (triangles.Count == 0) return null;
        var mesh = new Mesh { name = "Cultiway Body Shadow" };
        if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        return mesh;
    }

    /// <summary>立影从脚下抬起；联系无效即开始原地折回，不跟随别的人物。</summary>
    private void AppendShadows(double now)
    {
        if (shadows.Count == 0) return;
        FormResource resource = FormResources.Load("yin_transfer_wrap");
        for (int i = shadows.Count - 1; i >= 0; i--)
        {
            BodyShadow shadow = shadows[i];
            bool live = !shadow.Cast.Replaced && !shadow.Cast.Cast.Finished && shadow.Cast.Owner(shadow.Node).IsLive;
            Actor actor = live ? CorrespondenceReceiver(shadow.Cast, shadow.Node) : null;
            if (actor == null)
            {
                if (!shadow.RetireAt.HasValue) shadow.RetireAt = now;
            }
            else if (!shadow.RetireAt.HasValue) shadow.LastFoot = BodyFoot(actor);

            float leave = shadow.RetireAt.HasValue ? (float)(now - shadow.RetireAt.Value) / .42f : 0f;
            if (leave >= 1f)
            {
                RemoveShadow(i);
                continue;
            }
            float rise = Mathf.SmoothStep(0f, 1f, (float)(now - shadow.At) / .36f);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, leave);
            float lift = rise * fade;
            Matrix4x4 pose = Matrix4x4.identity;
            // 映照只拉宽已有表面，不添加肢体。站起时减少斜切，不能让紧贴的正面看成一道针影。
            pose.m00 = 1f + lift * 1.65f;
            pose.m11 = 1f + lift * .90f;
            pose.m02 = shadow.Side.x * (1.10f - lift * .88f);
            pose.m12 = shadow.Side.y * (1.10f - lift * .88f);
            pose.m22 = .035f + lift * 1.145f;
            Vector3 ground = shadow.LastFoot + shadow.Side * (.14f + lift * 1.06f);
            pose.SetColumn(3, new Vector4(ground.x, ground.y, ground.z + .07f, 1f));
            var tone = new Color(.21f + .09f * lift, .14f + .08f * lift, .36f + .10f * lift, (.63f + .32f * lift) * fade);
            visuals.Add(new FormVisualInstance(resource, shadow.Mesh, pose, tone, terrain, "yin_shadow"));
        }
    }

    private static void RemoveShadow(int index)
    {
        Object.Destroy(shadows[index].Mesh);
        shadows.RemoveAt(index);
    }

    private static void ClearShadows()
    {
        while (shadows.Count > 0) RemoveShadow(shadows.Count - 1);
    }

    /// <summary>真实相交时留下的立影及对应的原施放身份。</summary>
    private sealed class BodyShadow
    {
        internal readonly RunningCast Cast;
        internal readonly int Node;
        internal readonly Mesh Mesh;
        internal readonly Vector3 Side;
        internal readonly double At;
        internal Vector3 LastFoot;
        internal double? RetireAt;

        internal BodyShadow(RunningCast cast, int node, Mesh mesh, Vector3 foot, Vector3 side, double at)
        {
            Cast = cast;
            Node = node;
            Mesh = mesh;
            LastFoot = foot;
            Side = side;
            At = at;
        }
    }
}
