using System.Collections.Generic;
using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>阴阳、五相和混沌分别采用凝结、释放、转相与破裂的运动。</summary>
internal static class CultivationMaterialVisuals
{
    /// <summary>有厚度的裂片；所有混沌招式共用有限模型。</summary>
    private static readonly SpatialVisualMesh fracture = BuildFracture();

    /// <summary>贴地的冷雾向局部凝结，留出人物轮廓。</summary>
    /// <param name="output">共同模型快照。</param>
    /// <param name="center">实际作用中心。</param>
    /// <param name="radius">实际作用范围。</param>
    /// <param name="age">经过的世界秒数。</param>
    /// <param name="alpha">当前阶段的不透明度。</param>
    internal static void Yin(List<SpatialVisualMeshInstance> output, Vector3 center, float radius, float age, float alpha)
    {
        for (int i = 0; i < 9; i++)
        {
            float angle = i * 2.39996f, reach = .25f + i % 3 * .22f;
            float gather = Mathf.Repeat(age * .16f + i * .117f, 1f);
            Vector3 point = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius * reach * (1f - gather * .18f);
            point.z += radius * (.025f + i % 2 * .02f);
            SpatialVisualDraw.Mesh(output, SpatialEnergyGeometry.Cloud, point, Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg),
                new Vector3(radius * .34f, radius * .23f, radius * .055f), new Color(.19f, .26f, .35f, alpha * .2f), true, additive: false);
            float growth = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age * 1.5f - i * .07f));
            SpatialVisualDraw.Mesh(output, SpatialEnergyGeometry.Crystal, point, Quaternion.Euler(12, i * 23f, angle * Mathf.Rad2Deg),
                new Vector3(radius * .07f, radius * .045f, radius * .17f) * growth,
                new Color(.48f, .66f, .71f, alpha * .42f), false, surface: SpatialSurface.Ice);
        }
    }

    /// <summary>白金热流从内部向外释放，短促亮核与低亮外层交错。</summary>
    /// <param name="output">共同模型快照。</param>
    /// <param name="center">实际作用中心。</param>
    /// <param name="radius">实际作用范围。</param>
    /// <param name="age">经过的世界秒数。</param>
    /// <param name="alpha">当前阶段的不透明度。</param>
    internal static void Yang(List<SpatialVisualMeshInstance> output, Vector3 center, float radius, float age, float alpha)
    {
        for (int i = 0; i < 11; i++)
        {
            float angle = i * 2.39996f, pulse = Mathf.Repeat(age * .9f + i * .17f, 1f);
            Vector3 direction = new(Mathf.Cos(angle), Mathf.Sin(angle), .18f + i % 3 * .12f);
            Vector3 point = center + direction * radius * (.12f + pulse * .48f);
            Quaternion facing = Quaternion.FromToRotation(Vector3.forward, direction.normalized);
            SpatialVisualDraw.Mesh(output, SpatialEnergyGeometry.Flame, point, facing,
                new Vector3(radius * .18f, radius * .14f, radius * (.32f + pulse * .35f)),
                new Color(1f, .82f, .38f, alpha * (1f - pulse) * .45f), true, additive: false, surface: SpatialSurface.Energy);
        }
        SpatialVisualDraw.Mesh(output, SpatialEnergyGeometry.Cloud, center + Vector3.forward * radius * .12f, Quaternion.identity,
            new Vector3(radius * .32f, radius * .29f, radius * .24f), new Color(1f, .95f, .72f, alpha * .4f), true, surface: SpatialSurface.Energy);
    }

    /// <summary>不规则实体先聚拢再分离，裂面有体积而不绘制轨道圆环。</summary>
    /// <param name="output">共同模型快照。</param>
    /// <param name="center">实际作用中心。</param>
    /// <param name="radius">实际作用范围。</param>
    /// <param name="age">经过的世界秒数。</param>
    /// <param name="alpha">当前阶段的不透明度。</param>
    internal static void Chaos(List<SpatialVisualMeshInstance> output, Vector3 center, float radius, float age, float alpha)
    {
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 2.39996f, pulse = .5f + .5f * Mathf.Sin(age * 2.3f + i * 1.7f);
            Vector3 point = center + new Vector3(Mathf.Cos(angle) * (.2f + pulse * .32f),
                Mathf.Sin(angle) * (.16f + pulse * .3f), .12f + i % 3 * .13f) * radius;
            Quaternion turn = Quaternion.Euler(15f + i * 19f, Mathf.Sin(age + i) * 16f, angle * Mathf.Rad2Deg + age * (i % 2 == 0 ? 9f : -7f));
            SpatialVisualDraw.Mesh(output, fracture, point, turn, new Vector3(radius * .23f, radius * .18f, radius * (.4f + pulse * .3f)),
                new Color(.18f, .12f, .25f, alpha * .68f), false, surface: SpatialSurface.Stone);
            SpatialVisualDraw.Mesh(output, SpatialEnergyGeometry.Cloud, point, turn,
                new Vector3(radius * .24f, radius * .18f, radius * .15f), new Color(.46f, .31f, .57f, alpha * .12f), true, additive: false);
        }
    }

    /// <summary>只让当前真实相位占主导，上一相的消退由状态剩余时间决定。</summary>
    /// <param name="output">共同模型快照。</param>
    /// <param name="center">实际作用中心。</param>
    /// <param name="radius">实际作用范围。</param>
    /// <param name="phase">实际相位。</param>
    /// <param name="progress">当前相位的时间进度。</param>
    /// <param name="alpha">当前阶段的不透明度。</param>
    internal static void FivePhase(List<SpatialVisualMeshInstance> output, Vector3 center, float radius, int phase, float progress, float alpha)
    {
        float growth = Mathf.SmoothStep(.35f, 1f, Mathf.Clamp01(progress / .2f));
        if (progress < .16f) Phase((phase + 4) % 5, (1f - progress / .16f) * .22f, 1f);
        Phase(phase % 5, alpha, growth);
        void Phase(int value, float opacity, float opening)
        {
            switch (value)
            {
                case 0:
                    for (int i = 0; i < 5; i++)
                        SpatialVisualDraw.Mesh(output, SpatialEnergyGeometry.Sword, center + new Vector3((i - 2) * radius * .23f, 0, radius * .05f),
                            Quaternion.Euler(15, -18, -24 + i * 4), new Vector3(radius * .25f, radius * .25f, radius * .95f) * opening,
                            new Color(.7f, .8f, .85f, opacity * .55f), false, surface: SpatialSurface.Metal);
                    break;
                case 1:
                    RootCluster(output, center, radius, opening, opacity * .75f);
                    break;
                case 2:
                    for (int i = 0; i < 2; i++)
                        SpatialVisualDraw.Mesh(output, SpatialMaterialGeometry.Sample(SpatialMaterialGeometry.Waves, progress),
                            center + new Vector3((i - .5f) * radius * .5f, 0, 0), Quaternion.Euler(0, 0, i * 145f),
                            new Vector3(radius * .45f, radius * .65f, radius * .4f), new Color(.55f, .8f, .87f, opacity * .6f), false);
                    break;
                case 3:
                    for (int i = 0; i < 5; i++)
                    {
                        float angle = i * 2.39996f;
                        SpatialVisualDraw.Mesh(output, SpatialEnergyGeometry.Flame, center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), .1f) * radius * .4f,
                            Quaternion.Euler(0, 0, i * 73), new Vector3(radius * .28f, radius * .25f, radius * .8f) * opening,
                            new Color(1f, .37f, .08f, opacity * .55f), true, additive: false, surface: SpatialSurface.Flame);
                    }
                    break;
                default:
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = i * 2.39996f;
                        SpatialVisualDraw.Mesh(output, SpatialMaterialGeometry.Rocks[i], center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius * .45f,
                            Quaternion.Euler(0, 0, i * 71), new Vector3(radius * .27f, radius * .24f, radius * .5f * opening), new Color(1, 1, 1, opacity * .85f), false);
                    }
                    break;
            }
        }
    }

    /// <summary>建立一块封闭的断裂实体，避免混沌回到椭圆与线框。</summary>
    /// <summary>木系领域以不等长主根和稀疏枝叶建立主体。</summary>
    /// <param name="output">模型。</param><param name="center">固定根部。</param><param name="radius">实际范围。</param>
    /// <param name="growth">生长进度。</param><param name="fade">收尾透明度。</param>
    private static void RootCluster(List<SpatialVisualMeshInstance> output, Vector3 center, float radius, float growth, float fade)
    {
        for (int i = 0; i < 5; i++)
        {
            float phase = Mathf.Clamp01(growth * 1.35f - i * .07f);
            SpatialVisualDraw.Mesh(output, SpatialMaterialGeometry.Sample(SpatialMaterialGeometry.Roots[i % 4], phase), center,
                Quaternion.Euler(0, 0, i * 137.5f + 18f), new Vector3(radius * (.66f + i % 3 * .13f), radius * .85f, radius * .9f),
                new Color(1, 1, 1, fade), false);
        }
    }

    private static SpatialVisualMesh BuildFracture()
    {
        SpatialVisualMeshBuilder builder = new();
        Vector3[] edge = { new(-.7f, -.22f, -.4f), new(.28f, -.4f, -.32f), new(.8f, -.1f, .18f), new(.26f, .17f, .72f), new(-.4f, .3f, .2f) };
        Vector3 front = new(.08f, -.23f, .12f), back = new(-.07f, .23f, .04f);
        for (int i = 0; i < edge.Length; i++)
        {
            builder.AddFace(new[] { edge[i], edge[(i + 1) % edge.Length], front }, new Color(.76f, .7f, .82f));
            builder.AddFace(new[] { edge[(i + 1) % edge.Length], edge[i], back }, new Color(.38f, .34f, .43f));
        }
        return builder.Build("chaos_fracture", SpatialSurface.Stone);
    }
}
