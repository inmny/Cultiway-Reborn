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
public class ArtifactTrailVisualCue : IArtifactVisualCue
{
    /// <summary>稳定的空间样式编号。</summary>
    public string style_key = ArtifactVfxStyles.Arcane;
    /// <summary>真实跟随的作用部位。</summary>
    public ArtifactVisualAnchorRef anchor = ArtifactVisualAnchorKind.ActiveExecution;
    /// <summary>从能力语义配色中取用的颜色。</summary>
    public ArtifactVisualColorRole color_role = ArtifactVisualColorRole.Glow;
    /// <summary>相对实际部位的偏移。</summary>
    public Vector3 offset;
    /// <summary>通道或轨迹的世界宽度。</summary>
    public float width = 0.055f;
    /// <summary>整体透明度。</summary>
    public float alpha = 0.82f;
    /// <summary>保留真实运动历史的世界秒数。</summary>
    public float history = 0.28f;
    /// <summary>相邻采样的最小世界间距。</summary>
    public float min_distance = 0.025f;
    /// <summary>每段真实历史的最大采样数量。</summary>
    public int max_points = 24;
    /// <summary>是否随实际载体体型变化。</summary>
    public bool match_actor_scale = true;

    /// <summary>沿用法宝显示管理器的生命周期，两个视角读取同一租约。</summary>
    /// <param name="context">实际能力快照。</param><param name="now">世界时间。</param><param name="duration">信号时长。</param>
    public IArtifactVisualLease Begin(ArtifactAbilityVisualContext context, double now, float duration) =>
        ArtifactSpatialVisualRuntime.Begin(this, context, now, duration);
}

public sealed class ArtifactRibbonVisualCue : ArtifactTrailVisualCue
{
    public ArtifactRibbonVisualCue()
    {
        style_key = ArtifactVfxStyles.Cloth;
        width = 0.14f;
        alpha = 0.58f;
        history = 0.42f;
        max_points = 32;
    }
}
