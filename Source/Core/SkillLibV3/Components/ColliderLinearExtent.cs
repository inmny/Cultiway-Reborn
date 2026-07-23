using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components;

/// <summary>
/// 将 <see cref="ColliderSphere"/> 沿运动方向或实体朝向扩展为胶囊体。
/// 前后长度不包含球体本身的半径；固定线性几何应启用 <see cref="UseEntityRotation"/>。
/// </summary>
public struct ColliderLinearExtent : IComponent
{
    public float Forward;
    public float Backward;
    public bool UseEntityRotation;
}
