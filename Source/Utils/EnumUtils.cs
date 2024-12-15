using Cultiway.Core;

namespace Cultiway.Utils;

public static class EnumUtils
{
    private static readonly ElementComposition[] _attack_type_to_element_compositions =
    {
        new([0, 0.5f, 0.5f, 0, 0, 0, 1, 0], true),
        new([0, 0, 0, 1f, 0, 0, 1, 0], true),
        new([0, 1f, 0, 0, 0, 0, 1, 0], true),
        new([0, 1f, 0, 0, 0, 0, 1, 0], true),
        new([0, 1f, 0, 0, 0, 0, 1, 0], true),
        new([0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0, 1, 0], true),
        new([0, 0, 0.5f, 0.5f, 0, 0, 1, 0], true),
        new([0, 0, 0, 0.5f, 0.5f, 0, 1, 0], true),
        new([0, 0.5f, 0, 0, 0.5f, 0, 1, 0], true),
        new([0.5f, 0, 0, 0.5f, 0, 0, 1, 0], true),
        new([1f, 0, 0, 0, 0, 0, 1, 0], true),
        new([0.33f, 0.33f, 0, 0, 0.33f, 0, 1, 0], true),
        new([0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0, 1, 0], true),
        new([0, 0.5f, 0, 0.5f, 0, 0, 1, 0], true),
        new([0, 0, 0, 0, 1f, 0, 1, 0], true)
    };

    internal static ref ElementComposition DamageCompositionFromDamageType(AttackType attack_type)
    {
        if (attack_type > AttackType.Block) return ref ElementComposition.Static.empty;
        return ref _attack_type_to_element_compositions[(int)attack_type];
    }
}