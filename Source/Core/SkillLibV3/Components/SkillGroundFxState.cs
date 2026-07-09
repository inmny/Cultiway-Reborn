using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components;

/// <summary>
/// 飞行地面影响节流：累计位移距离，达到阈值时触发一次 <c>SkillGroundFx.OnFlyOver</c>（如火系烧焦、水系凝冰）。
/// </summary>
public struct SkillGroundFxState : IComponent
{
    /// <summary>自上次触发以来的累计位移（世界单位）。</summary>
    public float DistanceAccumulator;

    /// <summary>上次记录的位置 X，用于增量计算。</summary>
    public float LastX;

    /// <summary>上次记录的位置 Y。</summary>
    public float LastY;
}
