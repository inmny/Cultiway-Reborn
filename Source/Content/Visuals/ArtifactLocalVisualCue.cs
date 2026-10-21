using Cultiway.Content.Libraries;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>器身和目标端的局部表现形式。</summary>
public enum ArtifactLocalVisualKind
{
    /// <summary>局部短闪。</summary>
    Spark,
    /// <summary>向上流动的火舌。</summary>
    Flame,
    /// <summary>受击护持弧。</summary>
    Shield,
    /// <summary>贴地根蔓。</summary>
    Root,
    /// <summary>围绕身体的强化短纹。</summary>
    Blessing
}

/// <summary>局部形态与实际挂点组合，不再依赖整张动画。</summary>
public sealed class ArtifactLocalVisualCue : IArtifactVisualCue
{
    /// <summary>明确选择的局部形态。</summary>
    public readonly ArtifactLocalVisualKind kind;
    /// <summary>实际跟随部位。</summary>
    public ArtifactVisualAnchorRef anchor = ArtifactVisualAnchorKind.Artifact;
    /// <summary>取用的语义颜色。</summary>
    public ArtifactVisualColorRole color_role = ArtifactVisualColorRole.Glow;
    /// <summary>部位偏移。</summary>
    public Vector3 offset;
    /// <summary>相对身体的局部尺度。</summary>
    public float scale = .1f;
    /// <summary>整体透明度。</summary>
    public float alpha = 1f;
    /// <summary>尺寸呼吸幅度。</summary>
    public float scale_pulse_amplitude;
    /// <summary>尺寸呼吸速度。</summary>
    public float scale_pulse_speed = 4f;
    /// <summary>透明呼吸周期。</summary>
    public float alpha_pulse_period;
    /// <summary>透明呼吸的最低比例。</summary>
    public float alpha_pulse_floor;
    /// <summary>是否维持低强度循环。</summary>
    public bool loop = true;
    /// <summary>是否随人物体型变化。</summary>
    public bool match_actor_scale = true;

    /// <summary>选择具体局部形式。</summary>
    /// <param name="kind">局部形式。</param>
    public ArtifactLocalVisualCue(ArtifactLocalVisualKind kind) => this.kind = kind;

    /// <summary>复用已有能力显示生命周期。</summary>
    /// <param name="context">实际快照。</param><param name="now">世界时间。</param><param name="duration">持续时间。</param>
    public IArtifactVisualLease Begin(ArtifactAbilityVisualContext context, double now, float duration) =>
        ArtifactSpatialVisualRuntime.Begin(this, context, now, duration);
}
