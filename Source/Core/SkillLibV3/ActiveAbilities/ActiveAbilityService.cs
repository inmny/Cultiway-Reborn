using System;
using System.Collections.Generic;
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
        if (caster == null || caster.Base.isRekt()) return;

        for (int i = 0; i < Providers.Count; i++)
        {
            Providers[i].Collect(caster, output);
        }
    }

    public static ActiveAbilityDescriptor Describe(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return ResolveProvider(handle).Describe(caster, handle);
    }

    public static ActiveAbilityChannel GetChannels(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return TryResolveProvider(handle, out IActiveAbilityProvider provider)
            ? provider.GetChannels(caster, handle)
            : ActiveAbilityChannel.None;
    }

    public static bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        return TryResolveProvider(handle, out IActiveAbilityProvider provider) &&
               provider.CanPrepare(caster, handle, target);
    }

    public static bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target)
    {
        return TryResolveProvider(handle, out IActiveAbilityProvider provider) &&
               provider.CanUse(caster, handle, target);
    }

    public static bool TryUse(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        if (!TryResolveProvider(handle, out IActiveAbilityProvider provider)) return false;
        if (!provider.CanUse(caster, handle, target)) return false;
        return provider.TryUse(caster, handle, target, origin);
    }

    public static float ResolveRange(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target = null)
    {
        return TryResolveProvider(handle, out IActiveAbilityProvider provider)
            ? Math.Max(0f, provider.ResolveRange(caster, handle, target))
            : 0f;
    }

    public static float ResolveEffectRadius(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return TryResolveProvider(handle, out IActiveAbilityProvider provider)
            ? Math.Max(0f, provider.ResolveEffectRadius(caster, handle))
            : 0f;
    }

    public static int CollectAiCandidates(
        ActorExtend caster,
        BaseSimObject target,
        IList<ActiveAbilityHandle> output,
        IList<int> weights)
    {
        Collect(caster, output);
        weights.Clear();
        int totalWeight = 0;
        int writeIndex = 0;
        int collectedCount = output.Count;
        var useTarget = new ActiveAbilityTarget(target, target?.GetSimPos() ?? caster.Base.GetSimPos());
        for (int i = 0; i < collectedCount; i++)
        {
            ActiveAbilityHandle handle = output[i];
            IActiveAbilityProvider provider = ResolveProvider(handle);
            if ((provider.GetChannels(caster, handle) & ActiveAbilityChannel.Combat) == 0 ||
                !provider.CanPrepare(caster, handle, target))
            {
                continue;
            }

            int weight = provider.CanUse(caster, handle, useTarget)
                ? Math.Max(0, provider.ResolveAiWeight(caster, handle, target))
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
        using var handles = new ListPool<ActiveAbilityHandle>();
        Collect(caster, handles);
        for (int i = 0; i < handles.Count; i++)
        {
            ActiveAbilityHandle handle = handles[i];
            IActiveAbilityProvider provider = ResolveProvider(handle);
            if ((provider.GetChannels(caster, handle) & ActiveAbilityChannel.Combat) != 0 &&
                provider.CanPrepare(caster, handle, target)) return true;
        }
        return false;
    }

    public static int CountPreparedCombatAbilities(ActorExtend caster, BaseSimObject target)
    {
        using var handles = new ListPool<ActiveAbilityHandle>();
        Collect(caster, handles);
        int count = 0;
        for (int i = 0; i < handles.Count; i++)
        {
            ActiveAbilityHandle handle = handles[i];
            IActiveAbilityProvider provider = ResolveProvider(handle);
            if ((provider.GetChannels(caster, handle) & ActiveAbilityChannel.Combat) != 0 &&
                provider.CanPrepare(caster, handle, target)) count++;
        }
        return count;
    }

    /// <summary>
    /// 返回当前可准备战斗能力中的最远作用距离，供战斗寻路决定接近目标的停止位置。
    /// </summary>
    public static float ResolveMaxPreparedCombatRange(ActorExtend caster, BaseSimObject target)
    {
        using var handles = new ListPool<ActiveAbilityHandle>();
        Collect(caster, handles);
        float range = 0f;
        for (int i = 0; i < handles.Count; i++)
        {
            ActiveAbilityHandle handle = handles[i];
            IActiveAbilityProvider provider = ResolveProvider(handle);
            if ((provider.GetChannels(caster, handle) & ActiveAbilityChannel.Combat) == 0 ||
                !provider.CanPrepare(caster, handle, target)) continue;

            range = Math.Max(range, provider.ResolveRange(caster, handle, target));
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
}
