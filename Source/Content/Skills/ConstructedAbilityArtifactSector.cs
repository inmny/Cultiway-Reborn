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

    internal ArtifactAreaSector(Vector3 mouth, Vector2 direction, float angle, string surface)
    {
        Mouth = mouth;
        Direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.up;
        Angle = Mathf.Clamp(angle, 0f, 360f);
        Surface = surface;
        minimumDot = Mathf.Cos(Angle * .5f * Mathf.Deg2Rad);
    }

    /// <summary>在实际接触时按脚下位置沿用原扇形判定。</summary>
    internal bool Contains(Vector2 offset) =>
        offset.sqrMagnitude <= .0001f || Vector2.Dot(Direction, offset.normalized) >= minimumDot;

    /// <summary>从器口扩展成有前缘和数层起伏的薄浪。扇形两侧和外缘使用原角度及距离。</summary>
    internal Mesh CreateMesh()
    {
        int steps = Mathf.Max(12, Mathf.CeilToInt(Angle / 4f));
        Vector2[] profile =
        [
            new(0f, .012f), new(.24f, .02f), new(.43f, .093f), new(.55f, .036f),
            new(.70f, .137f), new(.79f, .054f), new(.93f, .173f), new(1f, .028f),
            new(.98f, .009f), new(0f, .002f)
        ];
        int stride = profile.Length + 1;
        var vertices = new Vector3[(steps + 1) * stride];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[steps * profile.Length * 6];
        int face = 0;
        for (int column = 0; column <= steps; column++)
        {
            float u = (float)column / steps;
            float polar = Mathf.Lerp(-Angle * .5f, Angle * .5f, u) * Mathf.Deg2Rad;
            float across = Mathf.Sin(Mathf.PI * u);
            Vector2 ray = new(Mathf.Sin(polar), Mathf.Cos(polar));
            for (int layer = 0; layer <= profile.Length; layer++)
            {
                Vector2 section = profile[layer % profile.Length];
                float ripple = 1f + across * .26f * Mathf.Cos(u * 35f - section.x * 15f);
                float height = section.y * Mathf.Lerp(.30f, 1f, across) * ripple;
                int a = column * stride + layer;
                vertices[a] = new Vector3(ray.x * section.x, ray.y * section.x, height);
                uv[a] = new Vector2(u, (float)layer / profile.Length);
                if (column == steps || layer == profile.Length) continue;
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
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
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
        running.ArtifactSectorMesh ??= sector.CreateMesh();
        float scale = volume.Transform.MultiplyVector(Vector3.right).magnitude;
        float yaw = Mathf.Atan2(-sector.Direction.x, sector.Direction.y) * Mathf.Rad2Deg;
        var transform = Matrix4x4.TRS(volume.Transform.GetColumn(3), Quaternion.AngleAxis(yaw, Vector3.forward),
            Vector3.one * scale);
        visuals[start] = new FormVisualInstance(volume.Resource, running.ArtifactSectorMesh, transform,
            volume.Dye, volume.Terrain, sector.Surface);
    }
}
