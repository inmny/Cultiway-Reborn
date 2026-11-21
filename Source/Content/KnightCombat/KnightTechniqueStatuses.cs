using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Content.Skills;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Core.Skills;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;

namespace Cultiway.Content.KnightCombat;

internal struct KnightBoundWeaponStatus : IComponent
{
    public Item Weapon;
    public KnightTechniqueAsset Technique;
    /// <summary>本次起势的运行编号，旧档与重载只恢复状态及实际兵器。</summary>
    [Newtonsoft.Json.JsonIgnore]
    internal long PreparationCastId;
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

    public static void ApplyBulwark(Actor target, Actor source)
    {
        ApplyGlobal(target, GuardianBulwark, 4f, source);
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

    /// <summary>按共享状态编号识别仍需实际武器及原流派的两种架势。</summary>
    /// <param name="effect">正式起势状态。</param>
    internal static KnightTechniqueAsset RequiredTechnique(StatusEffectAsset effect) =>
        effect == null ? null : effect == GuardStance ? KnightTechniques.GuardStance :
        effect == CounterStance ? KnightTechniques.CounterStance : null;

    /// <summary>起势完成受理时将真正借用的兵器交给状态。</summary>
    /// <param name="status">已经施加的实际状态。</param>
    /// <param name="technique">这个状态所属的原战技。</param>
    /// <param name="weapon">本次实际借用的物件。</param>
    /// <param name="castId">本次起势占用。</param>
    internal static void BindStance(Entity status, KnightTechniqueAsset technique, Item weapon, long castId)
    {
        status.GetComponent<KnightBoundWeaponStatus>() = new KnightBoundWeaponStatus
        {
            Weapon = weapon,
            Technique = technique,
            PreparationCastId = castId,
        };
    }

    /// <summary>架势只豁免这一次起势自己的身体占用，其他强控和换装仍取消。</summary>
    /// <param name="actor">持有状态的实际人物。</param>
    /// <param name="bound">已经绑定的武器及原战技。</param>
    internal static bool IsStanceCurrent(ActorExtend actor, KnightBoundWeaponStatus bound) =>
        bound.Technique != null && KnightTechniqueRuntimeService.IsCurrentTechniqueWeapon(actor, bound.Technique,
            bound.Weapon, BodyControlState.IsBusy(actor.Base.id) &&
                          ConstructedAbilityService.IsBodyControlledBy(actor.Base.id, bound.PreparationCastId));

    private static void ApplyGlobal(
        Actor target,
        StatusEffectAsset effect,
        float duration,
        Actor source)
    {
        if (Combat.CombatStatusEffects.TryGetStatus(target, effect, null, out Entity status))
        {
            float sourcePowerLevel = source.GetExtend().GetPowerLevel();
            if (!StatusEffectSuppression.TryResolveDuration(
                    target.GetExtend(), effect, duration, source, sourcePowerLevel,
                    out float resolvedDuration)) return;
            status.GetComponent<AliveTimer>().value = 0f;
            status.GetComponent<AliveTimeLimit>().value = resolvedDuration;
            ref StatusComponent component = ref status.GetComponent<StatusComponent>();
            component.SetSource(source);
            component.SourcePowerLevel = sourcePowerLevel;
            target.setStatsDirty();
            return;
        }
        Combat.CombatStatusEffects.ApplyStateStatus(target, effect, duration, source, null);
    }
}
