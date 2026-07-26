using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components;

public struct ColliderConfig : IComponent
{
    public bool Enabled;
    public bool Actor;
    public bool Building;
    public bool Enemy;
    public bool Alias;

    /// <summary>只允许命中施法步骤明确指定的对象，不扫描附近其他单位。</summary>
    public bool ExplicitTargetOnly;
}
