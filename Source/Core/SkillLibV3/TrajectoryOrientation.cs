using System;

namespace Cultiway.Core.SkillLibV3;

/// <summary>
/// 轨迹（与法术视觉）可适配的方向姿态类别。
/// 用于约束 <see cref="SkillModifierLibrary.SetTrajectory"/> 词条随机替换轨迹时的方向兼容性，
/// 避免把竖直播放的法术（例如落雷）换成水平位移的轨迹导致视觉穿帮。
/// 一条轨迹可声明多种姿态（按位或），一个法术可接受多种姿态（按位或），交集非空即视为兼容。
/// </summary>
[Flags]
public enum TrajectoryOrientation
{
    /// <summary>无方向姿态，仅作为初值或「永不匹配」哨兵使用。</summary>
    None = 0,

    /// <summary>
    /// 水平位移类：沿施法方向朝目标水平移动。
    /// 例如 TowardsDirection / DriftHoming / SineWave / Zigzag / SpiralHoming / Boomerang / SlowVortex 等。
    /// </summary>
    Horizontal = 1 << 0,

    /// <summary>
    /// 原地显现类：直接出现在目标位置，几乎无可察觉位移。
    /// 适合本身动画即从上到下竖直播放、不应叠加额外位移的法术（例如落雷）。
    /// </summary>
    Appear = 1 << 1,

    /// <summary>
    /// 竖直下落类：从高处沿 z 轴垂直落下。
    /// 例如 FallingStrike / RainFall。
    /// </summary>
    Vertical = 1 << 2,

    /// <summary>
    /// 贴地爬行类：在地面延展行进。
    /// 例如 GroundCrawl。
    /// </summary>
    Ground = 1 << 3,

    /// <summary>
    /// 近身挥砍类：围绕施法者短距离扫掠，不应与独立飞行轨迹互换。
    /// </summary>
    Melee = 1 << 4,
}
