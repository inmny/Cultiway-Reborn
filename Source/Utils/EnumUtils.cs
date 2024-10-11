using Cultiway.Core;

namespace Cultiway.Utils;

public static class EnumUtils
{
    private static DamageComposition[] attack_type_to_element_compositions = new DamageComposition[]
    {
        new([0, 0.5f, 0.5f, 0, 0, 0]),
        new([0, 0, 0, 1f, 0, 0]),
        new([0, 1f, 0, 0, 0, 0]),
        new([0, 1f, 0, 0, 0, 0]),
        new([0, 1f, 0, 0, 0, 0]),
        new([0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0]),
        new([0, 0, 0.5f, 0.5f, 0, 0]),
        new([0, 0, 0, 0.5f, 0.5f, 0]),
        new([0, 0.5f, 0, 0, 0.5f, 0]),
        new([0.5f, 0, 0, 0.5f, 0, 0]),
        new([1f, 0, 0, 0, 0, 0]),
        new([0.33f, 0.33f, 0, 0, 0.33f, 0]),
        new([0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0]),
        new([0, 0.5f, 0, 0.5f, 0, 0]),
        new([0, 0, 0, 0, 1f, 0]),
    };

    internal static ref DamageComposition DamageCompositionFromDamageType(AttackType attack_type)
    {
        if (attack_type > AttackType.Block) return ref DamageComposition.Static.empty;
        return ref attack_type_to_element_compositions[(int)attack_type];
    }
}