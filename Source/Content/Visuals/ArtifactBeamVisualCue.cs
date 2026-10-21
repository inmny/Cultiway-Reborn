using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Libraries;
using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>通过共用空间显示组合既有能力的真实姿态与作用。</summary>
public class ArtifactBeamVisualCue : IArtifactVisualCue
{
    /// <summary>稳定的空间样式编号。</summary>
    public string style_key = ArtifactVfxStyles.Arcane;
    /// <summary>实际通道起点。</summary>
    public ArtifactVisualAnchorRef from = ArtifactVisualAnchorKind.Artifact;
    /// <summary>实际通道终点。</summary>
    public ArtifactVisualAnchorRef to = ArtifactVisualAnchorKind.Target;
    /// <summary>从能力语义配色中取用的颜色。</summary>
    public ArtifactVisualColorRole color_role = ArtifactVisualColorRole.Primary;
    /// <summary>实际起点的局部偏移。</summary>
    public Vector3 from_offset;
    /// <summary>实际终点的局部偏移。</summary>
    public Vector3 to_offset;
    /// <summary>通道或轨迹的世界宽度。</summary>
    public float width = 0.045f;
    /// <summary>通道柔光相对主体宽度的倍数。</summary>
    public float glow_width_multiplier = 2.4f;
    /// <summary>整体透明度。</summary>
    public float alpha = 0.9f;
    /// <summary>通道柔光透明度。</summary>
    public float glow_alpha = 0.32f;
    /// <summary>两端重合时用于显示方向的短通道长度。</summary>
    public float fallback_length = 1f;
    /// <summary>通道相对直线的上拱幅度。</summary>
    public float curvature;
    /// <summary>通道横向摆动幅度。</summary>
    public float wave_amplitude;
    /// <summary>整条通道的波动周期数。</summary>
    public float wave_frequency = 2f;
    /// <summary>波动按世界时间流动的速度。</summary>
    public float wave_speed = 5f;
    /// <summary>通道宽度的呼吸幅度。</summary>
    public float width_pulse_amplitude = 0.08f;
    /// <summary>通道宽度的呼吸速度。</summary>
    public float width_pulse_speed = 6f;
    /// <summary>是否随实际载体体型变化。</summary>
    public bool match_actor_scale = true;

    /// <summary>沿用法宝显示管理器的生命周期，两个视角读取同一租约。</summary>
    /// <param name="context">实际能力快照。</param><param name="now">世界时间。</param><param name="duration">信号时长。</param>
    public IArtifactVisualLease Begin(ArtifactAbilityVisualContext context, double now, float duration) =>
        ArtifactSpatialVisualRuntime.Begin(this, context, now, duration);
}

public sealed class ArtifactTetherVisualCue : ArtifactBeamVisualCue
{
    public ArtifactTetherVisualCue()
    {
        curvature = -0.16f;
        wave_amplitude = 0.025f;
        wave_frequency = 1.5f;
        width = 0.032f;
    }
}
