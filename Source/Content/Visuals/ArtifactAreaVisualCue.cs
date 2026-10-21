using System;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>通过共用空间显示组合既有能力的真实姿态与作用。</summary>
public sealed class ArtifactAreaVisualCue : IArtifactVisualCue
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
    /// <summary>边界透明度。</summary>
    public float line_alpha = 0.55f;
    /// <summary>内部流动的透明度。</summary>
    public float fill_alpha = 0.06f;
    /// <summary>固定世界线宽。</summary>
    public float line_width = 0.08f;
    /// <summary>内侧结构的半径比例。</summary>
    public float inner_radius_ratio = 0.72f;
    /// <summary>内侧结构每世界秒转过的角度。</summary>
    public float inner_rotation_speed = 18f;
    /// <summary>尺寸的轻微呼吸幅度。</summary>
    public float pulse_amplitude = 0.025f;
    /// <summary>按世界时间计算的呼吸速度。</summary>
    public float pulse_speed = 3f;
    /// <summary>原有显示阶段开始时的尺寸比例。</summary>
    public float start_scale = 1f;
    /// <summary>原有显示阶段结束时的尺寸比例。</summary>
    public float end_scale = 1f;
    /// <summary>是否在原有阶段结束前淡出。</summary>
    public bool fade_out;
    /// <summary>是否保留内侧结构。</summary>
    public bool show_inner_ring = true;

    /// <summary>沿用法宝显示管理器的生命周期，两个视角读取同一租约。</summary>
    /// <param name="context">实际能力快照。</param><param name="now">世界时间。</param><param name="duration">信号时长。</param>
    public IArtifactVisualLease Begin(ArtifactAbilityVisualContext context, double now, float duration) =>
        ArtifactSpatialVisualRuntime.Begin(this, context, now, duration);
}
