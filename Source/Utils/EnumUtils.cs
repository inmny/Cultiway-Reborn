using Cultiway.Core;

namespace Cultiway.Utils;

public static class EnumUtils
{
    private static readonly ElementComposition[] _attack_type_to_element_compositions =
    {
        new([0, 0.5f, 0.5f, 0, 0, 0, 1, 0], true), // Acid
        new([0, 0, 0, 1f, 0, 0, 1, 0], true), // Fire
        new([0, 1f, 0, 0, 0, 0, 1, 0], true), // Plague
        new([0, 1f, 0, 0, 0, 0, 1, 0], true), // Infection
        new([0, 1f, 0, 0, 0, 0, 1, 0], true), // Tumor
        new([0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0, 1, 0], true), // Other
        new([0, 0, 0.5f, 0.5f, 0, 0, 1, 0], true), // Divine
        new([0, 0, 0, 0.5f, 0.5f, 0, 1, 0], true), // AshFever
        new([0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0, 1, 0], true), // Metamorphosis
        new([0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0, 1, 0], true), // Evolution
        new([0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0, 1, 0], true), // Starvation
        new([0, 0.5f, 0, 0, 0.5f, 0, 1, 0], true), // Eaten
        new([0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0, 1, 0], true), // Age
        new([1f, 0, 0, 0, 0, 0, 1, 0], true), // Weapon
        new([0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0, 1, 0], true), // None
        new([0, 0.5f, 0.5f, 0, 0, 0, 1, 0], true), // Poison
        new([0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0, 1, 0], true), // Gravity
        new([0, 0, 1, 0, 0, 0, 1, 0], true), // Drowning
        new([0, 0, 1, 0, 0, 0, 1, 0], true), // Water
        new([0, 0, 0, 0.5f, 0.5f, 0, 1, 0], true)
    };

    internal static ref ElementComposition DamageCompositionFromDamageType(AttackType attack_type)
    {
        if (attack_type > AttackType.Explosion) return ref ElementComposition.Static.empty;
        return ref _attack_type_to_element_compositions[(int)attack_type];
    }
}