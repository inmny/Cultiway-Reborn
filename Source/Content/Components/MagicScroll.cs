using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>
/// 封存一个 mana 技能容器及其释放参数的一次性魔法卷轴。
/// </summary>
public struct MagicScroll : IComponent
{
    /// <summary>卷轴释放时使用的技能强度。</summary>
    public float Strength;

    /// <summary>卷轴制作时固化的力量等级。</summary>
    public float PowerLevel;

    /// <summary>卷轴封存的具体法术版本。</summary>
    public Entity SkillContainer;
}
