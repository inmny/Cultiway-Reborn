using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;

namespace Cultiway.Content.KnightCombat;

internal struct KnightBoundWeaponStatus : IComponent
{
    public Item Weapon;
    public KnightTechniqueAsset Technique;
}

/// <summary>注册并操作骑士战技使用的共享状态。</summary>
internal static class KnightTechniqueStatuses
{
    public static StatusEffectAsset GuardStance { get; private set; }
    public static StatusEffectAsset GuardianBulwark { get; private set; }
    public static StatusEffectAsset ArmorBreak { get; private set; }
    public static StatusEffectAsset CounterStance { get; private set; }

    public static void Init()
    {
        GuardStance = BuildBoundStatus(
            "KnightGuardStance",
            KnightTechniques.GuardStance,
            2.5f);
        GuardianBulwark = StatusEffectAsset.StartBuild("KnightGuardianBulwark")
            .SetName(KnightTechniques.GuardianBulwark.ResolveName())
            .SetDescription(KnightTechniques.GuardianBulwark.ResolveDescription())
            .SetIconPath(KnightTechniques.GuardianBulwark.IconPath)
            .SetDuration(4f)
            .Build();
        ArmorBreak = StatusEffectAsset.StartBuild("KnightArmorBreak")
            .SetName(KnightTechniques.ArmorPiercingThrust.ResolveName())
            .SetDescription(KnightTechniques.ArmorPiercingThrust.ResolveDescription())
            .SetIconPath(KnightTechniques.ArmorPiercingThrust.IconPath)
            .SetNegative()
            .SetDuration(3f)
            .SetStats(new BaseStats { [S.armor] = -0.2f })
            .Build();
        CounterStance = BuildBoundStatus(
            "KnightCounterStance",
            KnightTechniques.CounterStance,
            3f);
    }

    public static void ApplyGuard(KnightTechniqueContext context)
    {
        ApplyBound(context, GuardStance, 2.5f);
    }

    public static void ApplyCounter(KnightTechniqueContext context)
    {
        ApplyBound(context, CounterStance, 3f);
    }

    public static void ApplyBulwark(Actor target, Actor source)
    {
        ApplyGlobal(target, GuardianBulwark, 4f, source);
    }

    public static void ApplyArmorBreak(Actor target, Actor source)
    {
        ApplyGlobal(target, ArmorBreak, 3f, source);
    }

    public static bool TryGetBound(
        Actor target,
        StatusEffectAsset effect,
        out Entity status,
        out KnightBoundWeaponStatus bound)
    {
        if (Combat.CombatStatusEffects.TryGetStatus(target, effect, null, out status) &&
            status.TryGetComponent(out bound)) return true;
        bound = default;
        return false;
    }

    public static bool Has(Actor target, StatusEffectAsset effect)
    {
        return Combat.CombatStatusEffects.HasStatus(target, effect);
    }

    public static void Remove(Actor target, StatusEffectAsset effect)
    {
        Combat.CombatStatusEffects.RemoveStatus(target, effect);
    }

    private static StatusEffectAsset BuildBoundStatus(
        string id,
        KnightTechniqueAsset technique,
        float duration)
    {
        return StatusEffectAsset.StartBuild(id)
            .SetName(technique.ResolveName())
            .SetDescription(technique.ResolveDescription())
            .SetIconPath(technique.IconPath)
            .SetDuration(duration)
            .AddComponent(new KnightBoundWeaponStatus())
            .Build();
    }

    private static void ApplyBound(
        KnightTechniqueContext context,
        StatusEffectAsset effect,
        float duration)
    {
        Combat.CombatStatusEffects.ApplyStateStatus(
            context.Caster.Base,
            effect,
            duration,
            context.Caster.Base,
            status => status.GetComponent<KnightBoundWeaponStatus>() = new KnightBoundWeaponStatus
            {
                Weapon = context.Weapon,
                Technique = context.Technique,
            });
    }

    private static void ApplyGlobal(
        Actor target,
        StatusEffectAsset effect,
        float duration,
        Actor source)
    {
        if (Combat.CombatStatusEffects.TryGetStatus(target, effect, null, out Entity status))
        {
            status.GetComponent<AliveTimer>().value = 0f;
            status.GetComponent<AliveTimeLimit>().value = duration;
            ref StatusComponent component = ref status.GetComponent<StatusComponent>();
            component.Source = source;
            component.SourcePowerLevel = source.GetExtend().GetPowerLevel();
            target.setStatsDirty();
            return;
        }
        Combat.CombatStatusEffects.ApplyStateStatus(target, effect, duration, source, null);
    }
}
