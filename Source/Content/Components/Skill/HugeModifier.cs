using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct HugeModifier : IModifier
{
    public float ScaleMultiplier;
    public float ColliderMultiplier;
    public SkillModifierAsset ModifierAsset => SkillModifiers.Huge;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"体型×{ScaleMultiplier:F1}，碰撞体×{ColliderMultiplier:F1}";
}
