using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct SlowModifier : IModifier
{
    public float Duration;
    public float Strength;
    public SkillModifierAsset ModifierAsset => SkillModifiers.Slow;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"持续{Duration:F1}s，减速{Strength:P0}";
}

public struct BurnModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.Burn;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}

public struct FreezeModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.Freeze;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}

public struct PoisonModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.Poison;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}

public struct ExplosionModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.Explosion;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}

public struct HasteModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.Haste;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}

public struct ProficiencyModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.Proficiency;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}

public struct EmpowerModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.Empower;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}

public struct KnockbackModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.Knockback;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}

public struct VolleyModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.Volley;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}

public struct LockOnModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.LockOn;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}

public struct HugeModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.Huge;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}

public struct WeakenModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.Weaken;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}

public struct ArmorBreakModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.ArmorBreak;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}

public struct GravityModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.Gravity;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}

public struct DazeModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.Daze;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}

public struct MercyModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.Mercy;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}

public struct ChaosModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.Chaos;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}

public struct SwapModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.Swap;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}

public struct RandomAffixModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.RandomAffix;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}

public struct BurnoutModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.Burnout;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}

public struct ComboModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.Combo;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}

public struct SilenceModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.Silence;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}

public struct DeathSentenceModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.DeathSentence;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}

public struct ReincarnationTrialModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.ReincarnationTrial;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}

public struct EternalCurseModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.EternalCurse;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}
