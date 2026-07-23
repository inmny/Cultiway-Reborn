using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components;

public enum SkillPersistentKind
{
    Field,
    Barrier,
    Shield
}

/// <summary>
/// 场域、屏障和护盾的运行时占位、数量上限与耐久状态。
/// </summary>
public struct SkillPersistentState : IComponent
{
    public SkillPersistentKind Kind;
    public int MaxInstances;
    public float Durability;
    public float Length;
    public float Width;
}
