using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components;

/// <summary>
/// 记录法术是否已到达轨迹指定的结算位置，供落点型范围法术独立于单位碰撞完成结算。
/// </summary>
public struct SkillPositionImpactState : IComponent
{
    public bool Requested;
    public bool Resolved;
}
