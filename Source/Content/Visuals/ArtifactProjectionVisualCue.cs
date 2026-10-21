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
public sealed class ArtifactProjectionVisualCue : IArtifactVisualCue
{
    /// <summary>真实跟随的作用部位。</summary>
    public ArtifactVisualAnchorRef anchor = ArtifactVisualAnchorKind.Point;
    /// <summary>从能力语义配色中取用的颜色。</summary>
    public ArtifactVisualColorRole color_role = ArtifactVisualColorRole.Glow;
    /// <summary>按当前能力参数求世界尺寸。</summary>
    public Func<ArtifactAbilityVisualContext, float> ResolveWorldSize;
    /// <summary>相对实际部位的偏移。</summary>
    public Vector3 offset;
    /// <summary>相对当前法宝本体的显示尺寸。</summary>
    public float scale = 1f;
    /// <summary>整体透明度。</summary>
    public float alpha = 0.58f;
    /// <summary>原有显示阶段开始时的尺寸比例。</summary>
    public float start_scale = 0.45f;
    /// <summary>原有显示阶段结束时的尺寸比例。</summary>
    public float end_scale = 1f;
    /// <summary>每世界秒转过的角度。</summary>
    public float rotation_speed;
    /// <summary>尺寸的轻微呼吸幅度。</summary>
    public float pulse_amplitude = 0.04f;
    /// <summary>按世界时间计算的呼吸速度。</summary>
    public float pulse_speed = 4f;
    /// <summary>是否在原有阶段结束前淡出。</summary>
    public bool fade_out = true;
    /// <summary>是否跟随当前真实器身朝向。</summary>
    public bool follow_artifact_rotation;
    /// <summary>是否随实际载体体型变化。</summary>
    public bool match_actor_scale;

    /// <summary>沿用法宝显示管理器的生命周期，两个视角读取同一租约。</summary>
    /// <param name="context">实际能力快照。</param><param name="now">世界时间。</param><param name="duration">信号时长。</param>
    public IArtifactVisualLease Begin(ArtifactAbilityVisualContext context, double now, float duration) =>
        ArtifactSpatialVisualRuntime.Begin(this, context, now, duration);
}

/// <summary>通过共用空间显示组合既有能力的真实姿态与作用。</summary>
public sealed class ArtifactOrbitProjectionVisualCue : IArtifactVisualCue
{
    /// <summary>真实跟随的作用部位。</summary>
    public ArtifactVisualAnchorRef anchor = ArtifactVisualAnchorKind.Controller;
    /// <summary>从能力语义配色中取用的颜色。</summary>
    public ArtifactVisualColorRole color_role = ArtifactVisualColorRole.Glow;
    /// <summary>按当前能力参数求投影数量。</summary>
    public Func<ArtifactAbilityVisualContext, int> ResolveCount;
    /// <summary>按当前能力参数求真实半径。</summary>
    public Func<ArtifactAbilityVisualContext, float> ResolveRadius;
    /// <summary>按当前能力参数求世界尺寸。</summary>
    public Func<ArtifactAbilityVisualContext, float> ResolveWorldSize;
    /// <summary>相对实际部位的偏移。</summary>
    public Vector3 offset;
    /// <summary>未提供动态参数时的投影数量。</summary>
    public int count = 3;
    /// <summary>未提供动态参数时的世界半径。</summary>
    public float radius = 0.8f;
    /// <summary>环绕过程中上下起伏相对半径的比例。</summary>
    public float vertical_ratio = 0.42f;
    /// <summary>绕行每世界秒转过的角度。</summary>
    public float angular_speed = 90f;
    /// <summary>投影相对真实模型朝向的角度。</summary>
    public float rotation_offset = -90f;
    /// <summary>局部浮动幅度。</summary>
    public float bob_amplitude = 0.04f;
    /// <summary>局部浮动速度。</summary>
    public float bob_speed = 3f;
    /// <summary>整体透明度。</summary>
    public float alpha = 0.62f;
    /// <summary>尺寸的轻微呼吸幅度。</summary>
    public float pulse_amplitude = 0.04f;
    /// <summary>按世界时间计算的呼吸速度。</summary>
    public float pulse_speed = 4f;
    /// <summary>投影是否朝向环绕半径方向。</summary>
    public bool radial_facing = true;
    /// <summary>是否随实际载体体型变化。</summary>
    public bool match_actor_scale = true;

    /// <summary>沿用法宝显示管理器的生命周期，两个视角读取同一租约。</summary>
    /// <param name="context">实际能力快照。</param><param name="now">世界时间。</param><param name="duration">信号时长。</param>
    public IArtifactVisualLease Begin(ArtifactAbilityVisualContext context, double now, float duration) =>
        ArtifactSpatialVisualRuntime.Begin(this, context, now, duration);
}
