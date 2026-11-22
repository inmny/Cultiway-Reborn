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
    Blessing,
    /// <summary>沿实际身体展开的半透明衣面。</summary>
    Veil,
    /// <summary>随身体出现的护体折面。</summary>
    FoldedShield,
    /// <summary>一次确认吸收的局部亮面。</summary>
    ShieldImpact,
    /// <summary>卸力起点顺势打开的宽风面。</summary>
    WindFold,
    /// <summary>真元有余量时在法宝上聚集的灵光。</summary>
    GatheredWakan,
    /// <summary>护甲实际削弱后从接触位置散开的碎甲。</summary>
    ArmorFragments,
    /// <summary>实际收魂时从击杀点进入原件的短暂魂影。</summary>
    SoulGathering,
    /// <summary>从原器身的实际部位浮起的器灵轮廓。</summary>
    SpiritSilhouette
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
