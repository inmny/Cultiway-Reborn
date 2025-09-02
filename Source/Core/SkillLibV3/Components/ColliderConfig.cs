using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components;

public struct ColliderConfig : IComponent
{
    public bool Enabled;
    public bool Actor;
    public bool Building;
    public bool Enemy;
    public bool Alias;
}