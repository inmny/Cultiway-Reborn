using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>通过共用空间显示组合既有能力的真实姿态与作用。</summary>
public class ArtifactGlyphVisualCue : IArtifactVisualCue
{
    /// <summary>稳定的空间样式编号。</summary>
    public string style_key = ArtifactVfxStyles.Arcane;
    /// <summary>真实跟随的作用部位。</summary>
    public ArtifactVisualAnchorRef anchor = ArtifactVisualAnchorKind.Point;
    /// <summary>从能力语义配色中取用的颜色。</summary>
    public ArtifactVisualColorRole color_role = ArtifactVisualColorRole.Primary;
    /// <summary>按当前能力参数求真实半径。</summary>
    public Func<ArtifactAbilityVisualContext, float> ResolveRadius;
    /// <summary>相对实际部位的偏移。</summary>
    public Vector3 offset;
    /// <summary>未提供动态参数时的世界半径。</summary>
    public float radius = 1f;
    /// <summary>固定世界线宽。</summary>
    public float line_width = 0.055f;
    /// <summary>整体透明度。</summary>
    public float alpha = 0.72f;
    /// <summary>内侧记号相对外圈的半径。</summary>
    public float inner_ratio = 0.62f;
    /// <summary>原有显示阶段开始时的尺寸比例。</summary>
    public float start_scale = 0.2f;
    /// <summary>原有显示阶段结束时的尺寸比例。</summary>
    public float end_scale = 1f;
    /// <summary>每世界秒转过的角度。</summary>
    public float rotation_speed = 24f;
    /// <summary>内侧结构相对外侧的反向旋转比例。</summary>
    public float counter_rotation_ratio = -0.65f;
    /// <summary>尺寸的轻微呼吸幅度。</summary>
    public float pulse_amplitude = 0.03f;
    /// <summary>按世界时间计算的呼吸速度。</summary>
    public float pulse_speed = 3f;
    /// <summary>外侧记号数量，受共同装饰预算约束。</summary>
    public int sides = 8;
    /// <summary>是否在原有阶段结束前淡出。</summary>
    public bool fade_out;
    /// <summary>是否随实际载体体型变化。</summary>
    public bool match_actor_scale;

    /// <summary>沿用法宝显示管理器的生命周期，两个视角读取同一租约。</summary>
    /// <param name="context">实际能力快照。</param><param name="now">世界时间。</param><param name="duration">信号时长。</param>
    public IArtifactVisualLease Begin(ArtifactAbilityVisualContext context, double now, float duration) =>
        ArtifactSpatialVisualRuntime.Begin(this, context, now, duration);
}

public sealed class ArtifactDecalVisualCue : ArtifactGlyphVisualCue
{
    public ArtifactDecalVisualCue()
    {
        rotation_speed = 0f;
        counter_rotation_ratio = 0f;
        fade_out = true;
        start_scale = 0.72f;
    }
}
