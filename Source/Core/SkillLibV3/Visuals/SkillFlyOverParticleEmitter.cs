using System.Collections.Generic;
using Cultiway.Core.Semantics;
using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Visuals;

/// <summary>按世界时间发射共用空间粒子，保留平面位置和独立升高速度。</summary>
public static class SkillFlyOverParticleEmitter
{
    /// <summary>短暂掠地发射源的上限。</summary>
    private const int MaxSources = 256;
    /// <summary>按地块、元素与语义色合并的发射源。</summary>
    private static readonly Dictionary<(int x, int y, SkillVfxElementAsset element, Color color), Source> sources = new();
    /// <summary>本次待移除的键。</summary>
    private static readonly List<(int x, int y, SkillVfxElementAsset element, Color color)> finished = new();
    /// <summary>纯视觉种子，不消耗玩法随机数。</summary>
    private static int seed;

    /// <summary>登记实际掠过的地块，合并重复发射。</summary>
    /// <param name="tile">实际地块。</param><param name="element">元素样式。</param><param name="colorPalette">语义颜色。</param>
    public static void Activate(WorldTile tile, SkillVfxElementAsset element, SemanticColorPalette colorPalette)
    {
        if (element.FlyOverParticles.ParticlesPerEmission <= 0 || !SpatialVisualEffects.IsRelevant(tile.posV3, 1f)) return;
        Color color = colorPalette.HasColor ? SemanticColorResolver.ToVfxColor(colorPalette.Primary) : element.AccentColor;
        var key = (tile.pos.x, tile.pos.y, element, color);
        double end = World.world.getCurWorldTime() + element.FlyOverParticles.EmissionDuration;
        if (sources.TryGetValue(key, out Source existing)) { existing.End = end; return; }
        if (sources.Count >= MaxSources) return;
        sources.Add(key, new Source { Position = tile.posV3, Color = color, Style = element.FlyOverParticles, End = end });
    }

    /// <summary>从真实作用位置发射粒子，两种视角读取同一批粒子。</summary>
    /// <param name="position">起点。</param><param name="color">语义色。</param><param name="style">现有粒子样式。</param>
    /// <param name="spread">平面散布。</param><param name="direction">真实方向。</param><param name="directionalSpeed">沿方向速度。</param>
    /// <param name="particleCount">数量，负值使用样式数量。</param>
    public static void EmitBurst(Vector3 position, Color color, SkillFlyOverParticleStyle style, float spread = .35f,
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

    /// <summary>推进真实世界时间内仍在发射的源，跳过已经错过的装饰，不补发一串粒子。</summary>
    /// <param name="deltaTime">原有系统提供的世界步长。</param>
    public static void Update(float deltaTime)
    {
        double now = World.world.getCurWorldTime(); finished.Clear();
        foreach (var pair in sources)
        {
            Source source = pair.Value;
            if (now >= source.End) { finished.Add(pair.Key); continue; }
            if (now < source.Next) continue;
            EmitBurst(source.Position, source.Color, source.Style);
            source.Next = now + Mathf.Max(.02f, source.Style.EmissionInterval);
        }
        foreach (var key in finished) sources.Remove(key);
    }

    /// <summary>切换世界释放发射源；已经发出的粒子随共用空间运行时释放。</summary>
    public static void ClearWorldState() { sources.Clear(); finished.Clear(); seed = 0; }

    /// <summary>一个合并后的短期发射源。</summary>
    private sealed class Source
    {
        /// <summary>真实起点。</summary>
        internal Vector3 Position;
        /// <summary>语义颜色。</summary>
        internal Color Color;
        /// <summary>已有样式。</summary>
        internal SkillFlyOverParticleStyle Style;
        /// <summary>结束和下次发射的世界时间。</summary>
        internal double End, Next;
    }
}
