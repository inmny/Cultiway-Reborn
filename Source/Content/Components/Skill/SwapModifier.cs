using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct SwapModifier : IModifier
{
    public float Chance;
    public SkillModifierAsset ModifierAsset => SkillModifiers.Swap;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"命中{Chance:P0}概率交换位置";
}
