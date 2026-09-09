using System;
using System.Collections.Generic;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;

namespace Cultiway.Core.SkillLibV3.ActiveAbilities;

/// <summary>
/// 汇总所有主动能力来源，并为玩家控制和 AI 战斗提供同一套枚举、校验与释放入口。
/// </summary>
public static class ActiveAbilityService
{
    private static readonly List<IActiveAbilityProvider> Providers = new();
    private static readonly Dictionary<string, IActiveAbilityProvider> ProvidersById =
        new(StringComparer.Ordinal);

    public static void Register(IActiveAbilityProvider provider)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        if (string.IsNullOrWhiteSpace(provider.Id)) throw new ArgumentException("主动能力 Provider 缺少 ID");
        if (ProvidersById.ContainsKey(provider.Id))
        {
            throw new InvalidOperationException($"主动能力 Provider 重复注册: {provider.Id}");
        }

        Providers.Add(provider);
        ProvidersById.Add(provider.Id, provider);
    }

    public static void Collect(ActorExtend caster, ICollection<ActiveAbilityHandle> output)
    {
        output.Clear();
        SkillCasterContext context = SkillCasterContextService.Resolve(caster);
        if (!context.IsValid) return;
        using SkillCasterContextService.Scope scope = SkillCasterContextService.Enter(caster);
        for (int i = 0; i < Providers.Count; i++)
        {
            Providers[i].Collect(context.Owner, output);
        }
    }

    public static ActiveAbilityDescriptor Describe(ActorExtend caster, ActiveAbilityHandle handle)
    {
        SkillCasterContext context = SkillCasterContextService.Resolve(caster);
        if (!context.IsValid) return default;
        using SkillCasterContextService.Scope scope = SkillCasterContextService.Enter(caster);
        return ResolveProvider(handle).Describe(context.Owner, handle);
    }

    public static ActiveAbilityControlState ResolveControlState(
        ActorExtend caster,
        ActiveAbilityHandle handle)
    {
        SkillCasterContext context = SkillCasterContextService.Resolve(caster);
        if (!context.IsValid || !IsAllowedForCarrier(in context, handle) || IsSilenced(in context) ||
            !TryResolveProvider(handle, out IActiveAbilityProvider provider))
        {
            return new ActiveAbilityControlState(ActiveAbilityControlBlockReason.Unavailable);
        }
        using SkillCasterContextService.Scope scope = SkillCasterContextService.Enter(caster);
        return provider.ResolveControlState(context.Owner, handle);
    }

    public static ActiveAbilityChannel GetChannels(ActorExtend caster, ActiveAbilityHandle handle)
    {
        SkillCasterContext context = SkillCasterContextService.Resolve(caster);
        if (!context.IsValid || !IsAllowedForCarrier(in context, handle) ||
            !TryResolveProvider(handle, out IActiveAbilityProvider provider))
            return ActiveAbilityChannel.None;
        using SkillCasterContextService.Scope scope = SkillCasterContextService.Enter(caster);
        return provider.GetChannels(context.Owner, handle);
    }

    public static bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        SkillCasterContext context = SkillCasterContextService.Resolve(caster);
        if (!context.IsValid || !IsAllowedForCarrier(in context, handle) || IsSilenced(in context) ||
            !TryResolveProvider(handle, out IActiveAbilityProvider provider)) return false;
        using SkillCasterContextService.Scope scope = SkillCasterContextService.Enter(caster);
        return provider.CanPrepare(context.Owner, handle, target);
    }

    public static bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target)
    {
        SkillCasterContext context = SkillCasterContextService.Resolve(caster);
        if (!context.IsValid || !IsAllowedForCarrier(in context, handle) || IsSilenced(in context) ||
            !TryResolveProvider(handle, out IActiveAbilityProvider provider)) return false;
        using SkillCasterContextService.Scope scope = SkillCasterContextService.Enter(caster);
        return provider.CanUse(context.Owner, handle, target);
    }

    public static bool TryUse(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        SkillCasterContext context = SkillCasterContextService.Resolve(caster);
        if (!context.IsValid || !IsAllowedForCarrier(in context, handle) || IsSilenced(in context) ||
            !TryResolveProvider(handle, out IActiveAbilityProvider provider)) return false;
        using SkillCasterContextService.Scope scope = SkillCasterContextService.Enter(caster);
        if (!provider.CanUse(context.Owner, handle, target)) return false;
        return provider.TryUse(context.Owner, handle, target, origin);
    }

    public static float ResolveRange(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target = null)
    {
        SkillCasterContext context = SkillCasterContextService.Resolve(caster);
        if (!context.IsValid || !IsAllowedForCarrier(in context, handle) ||
            !TryResolveProvider(handle, out IActiveAbilityProvider provider)) return 0f;
        using SkillCasterContextService.Scope scope = SkillCasterContextService.Enter(caster);
        return Math.Max(0f, provider.ResolveRange(context.Owner, handle, target));
    }

    public static float ResolveEffectRadius(ActorExtend caster, ActiveAbilityHandle handle)
    {
        SkillCasterContext context = SkillCasterContextService.Resolve(caster);
        if (!context.IsValid || !IsAllowedForCarrier(in context, handle) ||
            !TryResolveProvider(handle, out IActiveAbilityProvider provider)) return 0f;
        using SkillCasterContextService.Scope scope = SkillCasterContextService.Enter(caster);
        return Math.Max(0f, provider.ResolveEffectRadius(context.Owner, handle));
    }

    public static int CollectAiCandidates(
        ActorExtend caster,
        BaseSimObject target,
        IList<ActiveAbilityHandle> output,
        IList<int> weights)
    {
        SkillCasterContext context = SkillCasterContextService.Resolve(caster);
        if (!context.IsValid || IsSilenced(in context))
        {
            output.Clear();
            weights.Clear();
            return 0;
        }
        using SkillCasterContextService.Scope scope = SkillCasterContextService.Enter(caster);
        Collect(caster, output);
        weights.Clear();
        int totalWeight = 0;
        int writeIndex = 0;
        int collectedCount = output.Count;
        var useTarget = new ActiveAbilityTarget(
            target,
            target?.GetSimPos() ?? context.Carrier.Base.GetSimPos());
        for (int i = 0; i < collectedCount; i++)
        {
            ActiveAbilityHandle handle = output[i];
            IActiveAbilityProvider provider = ResolveProvider(handle);
            if (!IsAllowedForCarrier(in context, handle) ||
                (provider.GetChannels(context.Owner, handle) & ActiveAbilityChannel.Combat) == 0 ||
                !provider.CanPrepare(context.Owner, handle, target))
            {
                continue;
            }

            int weight = provider.CanUse(context.Owner, handle, useTarget)
                ? Math.Max(0, provider.ResolveAiWeight(context.Owner, handle, target))
                : 0;
            if (weight <= 0) continue;

            output[writeIndex++] = handle;
            weights.Add(weight);
            totalWeight += weight;
        }

        if (writeIndex < collectedCount)
        {
            for (int i = collectedCount - 1; i >= writeIndex; i--)
            {
                output.RemoveAt(i);
            }
        }
        return totalWeight;
    }

    /// <summary>解析主动能力提供者声明的通用战术画像。</summary>
    public static ActiveAbilityTacticalProfile ResolveTacticalProfile(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        BaseSimObject target)
    {
        SkillCasterContext context = SkillCasterContextService.Resolve(caster);
        if (!context.IsValid || !IsAllowedForCarrier(in context, handle) ||
            !TryResolveProvider(handle, out IActiveAbilityProvider provider)) return default;
        using SkillCasterContextService.Scope scope = SkillCasterContextService.Enter(caster);
        return provider.ResolveTacticalProfile(context.Owner, handle, target);
    }

    /// <summary>返回 Provider 为指定战斗上下文声明的基础 AI 权重。</summary>
    public static int ResolveAiWeight(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        BaseSimObject target)
    {
        SkillCasterContext context = SkillCasterContextService.Resolve(caster);
        if (!context.IsValid || !IsAllowedForCarrier(in context, handle) ||
            !TryResolveProvider(handle, out IActiveAbilityProvider provider)) return 0;
        using SkillCasterContextService.Scope scope = SkillCasterContextService.Enter(caster);
        return Math.Max(0, provider.ResolveAiWeight(context.Owner, handle, target));
    }

    /// <summary>请求能力提供者按自身结构化效果选择收益最高的友方目标。</summary>
    public static bool TryResolvePreferredTarget(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        IReadOnlyList<Actor> nearbyAllies,
        out BaseSimObject target)
    {
        SkillCasterContext context = SkillCasterContextService.Resolve(caster);
        if (context.IsValid && TryResolveProvider(handle, out IActiveAbilityProvider provider) &&
            provider is IActiveAbilityTargetAdvisor advisor)
        {
            using SkillCasterContextService.Scope scope = SkillCasterContextService.Enter(caster);
            return advisor.TryResolvePreferredTarget(context.Owner, handle, nearbyAllies, out target);
        }
        target = null;
        return false;
    }

    /// <summary>判断能力提供者是否声明了按具体效果选择目标的顾问。</summary>
    public static bool HasTargetAdvisor(ActiveAbilityHandle handle)
    {
        return TryResolveProvider(handle, out IActiveAbilityProvider provider) &&
               provider is IActiveAbilityTargetAdvisor;
    }

    public static bool TrySelectForAi(
        ActorExtend caster,
        BaseSimObject target,
        IList<ActiveAbilityHandle> candidates,
        IList<int> weights,
        out ActiveAbilityHandle selected)
    {
        int totalWeight = CollectAiCandidates(caster, target, candidates, weights);
        if (totalWeight <= 0)
        {
            selected = default;
            return false;
        }

        int roll = Randy.randomInt(0, totalWeight);
        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= weights[i];
            if (roll >= 0) continue;
            selected = candidates[i];
            return true;
        }

        selected = candidates[candidates.Count - 1];
        return true;
    }

    public static bool HasPreparedCombatAbility(ActorExtend caster, BaseSimObject target)
    {
        SkillCasterContext context = SkillCasterContextService.Resolve(caster);
        if (!context.IsValid || IsSilenced(in context)) return false;
        using SkillCasterContextService.Scope scope = SkillCasterContextService.Enter(caster);
        using var handles = new ListPool<ActiveAbilityHandle>();
        Collect(caster, handles);
        for (int i = 0; i < handles.Count; i++)
        {
            ActiveAbilityHandle handle = handles[i];
            IActiveAbilityProvider provider = ResolveProvider(handle);
            if (IsAllowedForCarrier(in context, handle) &&
                (provider.GetChannels(context.Owner, handle) & ActiveAbilityChannel.Combat) != 0 &&
                provider.CanPrepare(context.Owner, handle, target)) return true;
        }
        return false;
    }

    public static int CountPreparedCombatAbilities(ActorExtend caster, BaseSimObject target)
    {
        SkillCasterContext context = SkillCasterContextService.Resolve(caster);
        if (!context.IsValid || IsSilenced(in context)) return 0;
        using SkillCasterContextService.Scope scope = SkillCasterContextService.Enter(caster);
        using var handles = new ListPool<ActiveAbilityHandle>();
        Collect(caster, handles);
        int count = 0;
        for (int i = 0; i < handles.Count; i++)
        {
            ActiveAbilityHandle handle = handles[i];
            IActiveAbilityProvider provider = ResolveProvider(handle);
            if (IsAllowedForCarrier(in context, handle) &&
                (provider.GetChannels(context.Owner, handle) & ActiveAbilityChannel.Combat) != 0 &&
                provider.CanPrepare(context.Owner, handle, target)) count++;
        }
        return count;
    }

    /// <summary>
    /// 返回当前可准备战斗能力中的最远作用距离，供战斗寻路决定接近目标的停止位置。
    /// </summary>
    public static float ResolveMaxPreparedCombatRange(ActorExtend caster, BaseSimObject target)
    {
        SkillCasterContext context = SkillCasterContextService.Resolve(caster);
        if (!context.IsValid || IsSilenced(in context)) return 0f;
        using SkillCasterContextService.Scope scope = SkillCasterContextService.Enter(caster);
        using var handles = new ListPool<ActiveAbilityHandle>();
        Collect(caster, handles);
        float range = 0f;
        for (int i = 0; i < handles.Count; i++)
        {
            ActiveAbilityHandle handle = handles[i];
            IActiveAbilityProvider provider = ResolveProvider(handle);
            if (!IsAllowedForCarrier(in context, handle) ||
                (provider.GetChannels(context.Owner, handle) & ActiveAbilityChannel.Combat) == 0 ||
                !provider.CanPrepare(context.Owner, handle, target)) continue;

            range = Math.Max(range, provider.ResolveRange(context.Owner, handle, target));
        }
        return Math.Max(0f, range);
    }

    public static int CountUsableCombatAbilities(ActorExtend caster, BaseSimObject target)
    {
        using var handles = new ListPool<ActiveAbilityHandle>();
        using var weights = new ListPool<int>();
        CollectAiCandidates(caster, target, handles, weights);
        return handles.Count;
    }

    private static IActiveAbilityProvider ResolveProvider(ActiveAbilityHandle handle)
    {
        if (TryResolveProvider(handle, out IActiveAbilityProvider provider)) return provider;
        throw new InvalidOperationException($"主动能力 Provider 不存在: {handle.ProviderId}");
    }

    private static bool TryResolveProvider(ActiveAbilityHandle handle, out IActiveAbilityProvider provider)
    {
        if (!string.IsNullOrEmpty(handle.ProviderId) &&
            ProvidersById.TryGetValue(handle.ProviderId, out provider)) return true;
        provider = null;
        return false;
    }

    /// <summary>按技能资产声明判断当前人物载体是否具备执行条件。</summary>
    private static bool IsAllowedForCarrier(
        in SkillCasterContext context,
        ActiveAbilityHandle handle)
    {
        if (context.EffectScale <= 0f) return false;
        if (handle.Source.IsNull || !handle.Source.TryGetComponent(out SkillContainer container) ||
            container.Asset == null) return true;
        return container.Asset.CarrierRequirement switch
        {
            SkillCarrierRequirement.General => true,
            SkillCarrierRequirement.PhysicalBody =>
                context.Kind == SkillCarrierKind.Physical && context.HasPhysicalBody,
            SkillCarrierRequirement.Soul => context.Kind == SkillCarrierKind.Soul,
            _ => false,
        };
    }

    private static bool IsSilenced(in SkillCasterContext context)
    {
        return context.Owner.Base.stats.hasTag(ActorControlTags.Silenced) ||
               context.Carrier.Base.stats.hasTag(ActorControlTags.Silenced);
    }
}
