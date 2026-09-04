using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components.TrajParams;

/// <summary>
/// 技能实体使用的轨迹种类。同一实体同一时刻只携带一种轨迹参数。
/// </summary>
public enum TrajectoryKind : byte
{
    /// <summary>未指定轨迹。</summary>
    None,

    /// <summary>抛物线弹道，见 <see cref="TrajectoryParams.Duration"/> 与 <see cref="TrajectoryParams.Height"/>。</summary>
    Arc,

    /// <summary>回旋镖：飞出后返回施法者。</summary>
    Boomerang,

    /// <summary>从空中坠落打击。</summary>
    Falling,

    /// <summary>折线闪击：分段瞬移逼近目标。</summary>
    LightningSnap,

    /// <summary>近身挥砍：围绕施法者的扇面扫过。</summary>
    MeleeSweep,

    /// <summary>环绕目标收缩。</summary>
    Orbit,

    /// <summary>在目标区域上空随机散布落下。</summary>
    RainFall,

    /// <summary>螺旋追踪。</summary>
    Spiral,

    /// <summary>缓慢涡旋推进。</summary>
    Vortex,

    /// <summary>正弦波形推进。</summary>
    Wave,

    /// <summary>锯齿形推进。</summary>
    Zigzag
}

/// <summary>
/// 全部轨迹种类的参数并集。同一实体只按 <see cref="Kind"/> 解释对应的字段；
/// 不同种类共用同名字段（例如 <see cref="Radius"/> 由挥砍、螺旋和涡旋共用）。
/// </summary>
public struct TrajectoryParams : IComponent
{
    /// <summary>当前参数对应的轨迹种类。</summary>
    public TrajectoryKind Kind;

    /// <summary>抛物线弹道或近身挥砍的总时长（秒）。</summary>
    public float Duration;

    /// <summary>抛物线弹道的弧顶高度。</summary>
    public float Height;

    /// <summary>回旋镖向外飞行的距离。</summary>
    public float OutDistance;

    /// <summary>回旋镖返程的转向速度（度/秒）。</summary>
    public float ReturnTurnRate;

    /// <summary>回旋镖的最长存在时间（秒）。</summary>
    public float MaxLifetime;

    /// <summary>坠落类轨迹的起始高度。</summary>
    public float StartHeight;

    /// <summary>坠落类轨迹的下落速度。</summary>
    public float FallSpeed;

    /// <summary>坠落类轨迹的水平漂移速度。</summary>
    public float DriftSpeed;

    /// <summary>坠落类轨迹判定落地的高度余量。</summary>
    public float ImpactHeight;

    /// <summary>折线闪击每一步的时间间隔（秒）。</summary>
    public float StepInterval;

    /// <summary>折线闪击每一步推进的距离。</summary>
    public float StepDistance;

    /// <summary>折线闪击每一步的横向抖动半径。</summary>
    public float JitterRadius;

    /// <summary>折线闪击判定命中目标的距离。</summary>
    public float HitDistance;

    /// <summary>近身挥砍半径、螺旋半径或涡旋基准半径。</summary>
    public float Radius;

    /// <summary>近身挥砍的起始角度（度）。</summary>
    public float StartAngle;

    /// <summary>近身挥砍的结束角度（度）。</summary>
    public float EndAngle;

    /// <summary>环绕轨迹的起始半径。</summary>
    public float StartRadius;

    /// <summary>环绕或涡旋的角速度（度/秒）。</summary>
    public float AngularSpeed;

    /// <summary>环绕轨迹的半径收缩速度。</summary>
    public float ShrinkSpeed;

    /// <summary>环绕或螺旋轨迹的追踪强度，范围 0 到 1。</summary>
    public float HomingStrength;

    /// <summary>螺旋或波形轨迹的频率。</summary>
    public float Frequency;

    /// <summary>螺旋轨迹的半径衰减速率。</summary>
    public float RadiusDamping;

    /// <summary>涡旋的基准前进速度。</summary>
    public float ForwardSpeed;

    /// <summary>涡旋的半径脉动幅度。</summary>
    public float PulseAmplitude;

    /// <summary>涡旋的半径脉动频率。</summary>
    public float PulseFrequency;

    /// <summary>波形轨迹的横向摆动幅度。</summary>
    public float Amplitude;

    /// <summary>波形轨迹的初始相位。</summary>
    public float Phase;

    /// <summary>锯齿轨迹的横向摆动幅度。</summary>
    public float SideAmplitude;

    /// <summary>锯齿轨迹每段往复的时长（秒）。</summary>
    public float SegmentDuration;
}
