using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components;

/// <summary>
/// 法术实体所有作用半径的统一倍率，包括碰撞、范围伤害、元素地面影响和范围词条。
/// </summary>
public struct EffectRadiusScale : IComponent
{
    public float Value;
}
