using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>用同一次施放冻结的开角制作实体扇面并校验定时接触。</summary>
internal readonly struct ArtifactAreaSector
{
    internal readonly Vector3 Mouth;
    internal readonly Vector2 Direction;
    internal readonly float Angle;
    internal readonly string Surface;
    private readonly float minimumDot;
    private readonly Vector2[] profile;

    private static readonly Vector2[] PouringProfile =
    [
        new(0f, .012f), new(.24f, .02f), new(.43f, .093f), new(.55f, .036f),
        new(.70f, .137f), new(.79f, .054f), new(.93f, .173f), new(1f, .028f),
        new(.98f, .009f), new(0f, .002f)
    ];

    internal ArtifactAreaSector(Vector3 mouth, Vector2 direction, float angle, string surface, Vector2[] profile = null)
    {
        Mouth = mouth;
        Direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.up;
        Angle = Mathf.Clamp(angle, 0f, 360f);
        Surface = surface;
        this.profile = profile;
        minimumDot = Mathf.Cos(Angle * .5f * Mathf.Deg2Rad);
    }

    /// <summary>在实际接触时按脚下位置沿用原扇形判定。</summary>
    internal bool Contains(Vector2 offset) =>
        offset.sqrMagnitude <= .0001f || Vector2.Dot(Direction, offset.normalized) >= minimumDot;

    /// <summary>从器口扩展成有前缘和数层起伏的薄浪。扇形两侧和外缘使用原角度及距离。</summary>
    internal ArtifactSectorView CreateView(float radius)
    {
        int steps = Mathf.Max(12, Mathf.CeilToInt(Angle / 4f));
        Vector2[] outline = profile ?? PouringProfile;
        // 放宽渲染跨度时加密径向格网，避免大三角面直接穿过相邻坡顶。
        var sections = new System.Collections.Generic.List<Vector2>(outline.Length);
        for (int i = 0; i < outline.Length; i++)
        {
            Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
            int count = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(b.x - a.x) * radius / 2f), 1, 24);
            for (int j = 0; j < count; j++) sections.Add(Vector2.Lerp(a, b, (float)j / count));
        }
        Vector2[] shape = sections.ToArray();
        int stride = shape.Length + 1;
        var vertices = new Vector3[(steps + 1) * stride];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[steps * shape.Length * 6];
        int face = 0;
        for (int column = 0; column <= steps; column++)
        {
            float u = (float)column / steps;
            float polar = Mathf.Lerp(-Angle * .5f, Angle * .5f, u) * Mathf.Deg2Rad;
            float across = Mathf.Sin(Mathf.PI * u);
            Vector2 ray = new(Mathf.Sin(polar), Mathf.Cos(polar));
            for (int layer = 0; layer <= shape.Length; layer++)
            {
                Vector2 section = shape[layer % shape.Length];
                float ripple = 1f + across * .26f * Mathf.Cos(u * 35f - section.x * 15f);
                float height = section.y * Mathf.Lerp(.30f, 1f, across) * ripple;
                int a = column * stride + layer;
                vertices[a] = new Vector3(ray.x * section.x, ray.y * section.x, height);
                uv[a] = new Vector2(u, (float)layer / shape.Length);
                if (column == steps || layer == shape.Length) continue;
                int b = a + stride;
                triangles[face++] = a;
                triangles[face++] = b;
                triangles[face++] = a + 1;
                triangles[face++] = a + 1;
                triangles[face++] = b;
                triangles[face++] = b + 1;
            }
        }

        var mesh = new Mesh
        {
            name = "Cultiway Artifact Sector", hideFlags = HideFlags.DontSave,
            vertices = vertices, uv = uv, triangles = triangles
        };
        return new ArtifactSectorView(mesh, vertices);
    }
}

/// <summary>只在展开或地面采样改变时校正网格高度，水平形状保持不变。</summary>
internal sealed class ArtifactSectorView
{
    internal readonly Mesh Mesh;
    private readonly Vector3[] original, adjusted;
    private AbilityHeightField previousTerrain;
    private Matrix4x4 previousTransform;

    internal ArtifactSectorView(Mesh mesh, Vector3[] vertices)
    {
        Mesh = mesh;
        original = vertices;
        adjusted = new Vector3[vertices.Length];
        Mesh.MarkDynamic();
        Mesh.RecalculateNormals();
        Mesh.RecalculateBounds();
    }

    internal void SetTerrain(Matrix4x4 transform, AbilityHeightField terrain, float scale)
    {
        if (ReferenceEquals(terrain, previousTerrain) && (terrain == null || transform == previousTransform)) return;
        previousTerrain = terrain;
        previousTransform = transform;
        float start = terrain?.Sample(transform.m03, transform.m13) ?? 0f;
        for (int i = 0; i < original.Length; i++)
        {
            Vector3 local = original[i];
            Vector3 world = transform.MultiplyPoint3x4(local);
            if (terrain != null)
                local.z += Mathf.Max(0f, terrain.Sample(world.x, world.y) - start) / scale;
            adjusted[i] = local;
        }
        Mesh.vertices = adjusted;
        Mesh.RecalculateNormals();
        Mesh.RecalculateBounds();
    }
}

public sealed partial class ConstructedAbilityService
{
    /// <summary>扇面保留原施放的起点、展开和收束，两个视角读取同一份实体。</summary>
    private void AppendArtifactAreaVisuals(RunningCast running)
    {
        int start = visuals.Count;
        running.Cast.AppendVisuals(visuals);
        if (running.ArtifactArea?.Sector is not { } sector || visuals.Count == start) return;
        FormVisualInstance volume = visuals[start];
        float scale = volume.Transform.MultiplyVector(Vector3.right).magnitude;
        float yaw = Mathf.Atan2(-sector.Direction.x, sector.Direction.y) * Mathf.Rad2Deg;
        var transform = Matrix4x4.TRS(volume.Transform.GetColumn(3), Quaternion.AngleAxis(yaw, Vector3.forward),
            Vector3.one * scale);
        running.ArtifactSectorView ??= sector.CreateView(running.Cast.Plan.Nodes[0].Scale);
        if (scale > .0001f) running.ArtifactSectorView.SetTerrain(transform, volume.Terrain, scale);
        visuals[start] = new FormVisualInstance(volume.Resource, running.ArtifactSectorView.Mesh, transform,
            volume.Dye, volume.Terrain, sector.Surface);
    }
}
