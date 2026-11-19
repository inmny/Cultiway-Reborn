using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Visuals;

/// <summary>按世界时间从明确锚点发射空间粒子。</summary>
public static class SkillParticleEmitter
{
    /// <summary>纯视觉种子，不消耗玩法随机数。</summary>
    private static int seed;

    /// <summary>从真实作用位置发射粒子，两种视角读取同一批粒子。</summary>
    /// <param name="position">起点。</param><param name="color">语义色。</param><param name="style">现有粒子样式。</param>
    /// <param name="spread">平面散布。</param><param name="direction">真实方向。</param><param name="directionalSpeed">沿方向速度。</param>
    /// <param name="particleCount">数量，负值使用样式数量。</param>
    public static void EmitBurst(Vector3 position, Color color, SkillParticleStyle style, float spread = .35f,
        Vector3 direction = default, float directionalSpeed = 0f, int particleCount = -1)
    {
        if (!SpatialVisualEffects.IsRelevant(position, spread)) return;
        int count = particleCount >= 0 ? particleCount : style.ParticlesPerEmission;
        color.a *= style.Alpha;
        for (int i = 0; i < count; i++)
        {
            int n = ++seed;
            float angle = n * 2.3999632f;
            Vector3 side = new(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            float varied = .5f + .5f * Mathf.Sin(n * 7.13f);
            Vector3 velocity = direction.normalized * directionalSpeed + side * style.HorizontalDrift * varied +
                Vector3.forward * Mathf.Lerp(style.MinRiseSpeed, style.MaxRiseSpeed, varied);
            SpatialVisualEffects.Particle(position + side * spread * varied, velocity, color,
                Mathf.Lerp(style.MinSize, style.MaxSize, varied), Mathf.Lerp(style.MinLifetime, style.MaxLifetime, varied));
        }
    }

    internal static void ClearWorldState() => seed = 0;
}
