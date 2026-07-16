using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components;

/// <summary>
/// 将 <see cref="ColliderSphere"/> 沿实体运动方向扩展为胶囊体。
/// 前后长度不包含球体本身的半径，适用于飞剑、箭矢等长条形高速实体。
/// </summary>
public struct ColliderLinearExtent : IComponent
{
    public float Forward;
    public float Backward;
}
