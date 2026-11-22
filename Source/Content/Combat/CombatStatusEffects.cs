using System;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.Components;
using Cultiway.Core.Skills;
using Cultiway.Core.Libraries;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Combat;

/// <summary>内容系统共用的状态查询、施加、刷新、净化与驱散原语。</summary>
public static partial class CombatStatusEffects
{
    /// <summary>查询本时点仍可供玩法使用的共享状态，已到期或回收的不参与。</summary>
    /// <param name="target">接收者。</param><param name="effect">已登记状态。</param><param name="active">真实持有的状态。</param>
    internal static bool TryGetActiveStatus(Actor target, StatusEffectAsset effect, out Entity active)
    {
        var relations = target.GetExtend().E.GetRelations<StatusRelation>();
        for (int index = 0; index < relations.Length; index++)
        {
            Entity status = relations[index].status;
            if (!status.IsNull && !status.Tags.Has<TagRecycle>() && status.GetComponent<StatusComponent>().Type == effect &&
                (!status.TryGetComponent(out AliveTimeLimit limit) || status.GetComponent<AliveTimer>().value < limit.value))
            {
                active = status;
                return true;
            }
        }
        active = default;
        return false;
    }

    /// <summary>判断目标是否持有指定类型且来自指定来源的共享状态。</summary>
    public static bool HasStatus(Actor target, StatusEffectAsset effect, Actor source = null)
    {
        if (target == null || effect == null) return false;
        var relations = target.GetExtend().E.GetRelations<StatusRelation>();
        for (var i = 0; i < relations.Length; i++)
        {
            Entity status = relations[i].status;
            StatusComponent component = status.GetComponent<StatusComponent>();
            if (component.Type == effect && (source == null || component.Source == source)) return true;
        }

        return false;
    }

    /// <summary>取得指定类型且来自指定来源的共享状态实体。</summary>
    public static bool TryGetStatus(
        Actor target,
        StatusEffectAsset effect,
        Actor source,
        out Entity statusEntity)
    {
        if (target != null && effect != null)
        {
            var relations = target.GetExtend().E.GetRelations<StatusRelation>();
            for (var i = 0; i < relations.Length; i++)
            {
                Entity status = relations[i].status;
                StatusComponent component = status.GetComponent<StatusComponent>();
                if (component.Type != effect || source != null && component.Source != source) continue;
                statusEntity = status;
                return true;
            }
        }

        statusEntity = default;
        return false;
    }

    /// <summary>移除最多指定数量的负面共享状态；上限小于一表示不限制数量。</summary>
    public static int CleanseNegativeStatuses(Actor target, int maxCount = 0)
    {
        if (target == null || target.isRekt()) return 0;
        ActorExtend extend = target.GetExtend();
        int removed = 0;
        foreach (Entity status in extend.GetStatuses())
        {
            StatusComponent component = status.GetComponent<StatusComponent>();
            if (!component.Type.GetExtend<StatusAssetExtend>().negative) continue;
            extend.RemoveSharedStatus(status);
            ModClass.I.CommandBuffer.AddTag<TagRecycle>(status.Id);
            removed++;
            if (maxCount > 0 && removed >= maxCount) break;
        }

        return removed;
    }

    /// <summary>在共享状态与原版状态中移除优先级最高的一个负面状态。</summary>
    public static bool CleanseHighestPriorityNegativeStatus(Actor target)
    {
        if (target == null || target.isRekt()) return false;
        ActorExtend extend = target.GetExtend();
        Entity selected = default;
        string selectedVanillaId = null;
        int selectedPriority = int.MinValue;
        foreach (Entity status in extend.GetStatuses())
        {
            StatusComponent component = status.GetComponent<StatusComponent>();
            if (!component.Type.GetExtend<StatusAssetExtend>().negative) continue;
            int priority = ResolveCleansePriority(component.Type);
            if (priority <= selectedPriority) continue;
            selected = status;
            selectedPriority = priority;
        }

        foreach (Status status in target.getStatuses())
        {
            StatusAsset asset = status.asset;
            if (status.is_finished || asset == null || !asset.GetExtend<StatusAssetExtend>().negative) continue;
            int priority = ResolveCleansePriority(asset);
            if (priority <= selectedPriority) continue;
            selected = default;
            selectedVanillaId = asset.id;
            selectedPriority = priority;
        }

        if (selectedVanillaId != null)
        {
            target.finishStatusEffect(selectedVanillaId);
            return true;
        }

        if (selected.IsNull) return false;
        extend.RemoveSharedStatus(selected);
        ModClass.I.CommandBuffer.AddTag<TagRecycle>(selected.Id);
        return true;
    }

    /// <summary>返回目标在共享状态与原版状态中的最高净化优先级；没有负面状态时返回零。</summary>
    public static int ResolveHighestNegativePriority(Actor target)
    {
        if (target == null || target.isRekt()) return 0;
        int result = 0;
        foreach (Entity status in target.GetExtend().GetStatuses())
        {
            StatusComponent component = status.GetComponent<StatusComponent>();
            if (!component.Type.GetExtend<StatusAssetExtend>().negative) continue;
            result = Math.Max(result, ResolveCleansePriority(component.Type));
        }

        foreach (Status status in target.getStatuses())
        {
            StatusAsset asset = status.asset;
            if (status.is_finished || asset == null || !asset.GetExtend<StatusAssetExtend>().negative) continue;
            result = Math.Max(result, ResolveCleansePriority(asset));
        }

        return result;
    }

    /// <summary>移除最多指定数量的非负面共享状态；上限小于一表示不限制数量。</summary>
    public static int DispelPositiveStatuses(Actor target, int maxCount = 0)
    {
        if (target == null || target.isRekt()) return 0;
        ActorExtend extend = target.GetExtend();
        int removed = 0;
        foreach (Entity status in extend.GetStatuses())
        {
            StatusComponent component = status.GetComponent<StatusComponent>();
            if (component.Type.GetExtend<StatusAssetExtend>().negative) continue;
            extend.RemoveSharedStatus(status);
            ModClass.I.CommandBuffer.AddTag<TagRecycle>(status.Id);
            removed++;
            if (maxCount > 0 && removed >= maxCount) break;
        }

        return removed;
    }

    /// <summary>移除指定类型的共享状态；提供来源时只移除该来源施加的实例。</summary>
    public static int RemoveStatus(Actor target, StatusEffectAsset effect, Actor source = null) =>
        RemoveStatus(target, effect, source, int.MaxValue);

    /// <summary>按状态现有顺序解除匹配层数，不计已回收的状态。</summary>
    public static int RemoveStatus(Actor target, StatusEffectAsset effect, Actor source, int maximum)
    {
        if (target == null || target.isRekt() || effect == null || maximum <= 0) return 0;
        ActorExtend extend = target.GetExtend();
        int removed = 0;
        foreach (Entity status in extend.GetStatuses())
        {
            StatusComponent component = status.GetComponent<StatusComponent>();
            if (component.Type != effect || status.Tags.Has<TagRecycle>() ||
                source != null && (component.Source != source || component.SourceActorId != source.data.id)) continue;
            extend.RemoveSharedStatus(status);
            ModClass.I.CommandBuffer.AddTag<TagRecycle>(status.Id);
            if (++removed >= maximum) break;
        }

        return removed;
    }

    /// <summary>施加带单项运行时属性覆盖的共享状态。</summary>
    public static void ApplyStatus(
        Actor target,
        StatusEffectAsset effect,
        float duration,
        string statId,
        float statValue,
        Actor source) => ApplyStatusWithStat(target, effect, duration, statId, statValue, source);

    /// <summary>取得实际创建或刷新的状态，免疫与拒绝时返回空。</summary>
    internal static Entity ApplyStatusWithStat(
        Actor target, StatusEffectAsset effect, float duration, string statId, float statValue, Actor source)
    {
        float scale = ResolveCurrentCarrierScale(source);
        statValue *= scale;
        return ApplyConfiguredStatus(target, effect, duration, source,
            (status, resolvedDuration) =>
                ConfigureStatus(status, resolvedDuration, statId, statValue, source));
    }

    /// <summary>施加只使用状态资产固有属性的共享状态。</summary>
    public static void ApplyStatus(Actor target, StatusEffectAsset effect, float duration, Actor source)
    {
        duration *= ResolveCurrentCarrierScale(source);
        ApplyConfiguredStatus(target, effect, duration, source,
            (status, resolvedDuration) => ConfigureStatusHeader(status, resolvedDuration, source), effect?.stats);
    }

    /// <summary>真实接触使用受理时固定的来源境界；同源再次接触不缩短已有控制。</summary>
    /// <param name="target">实际目标。</param><param name="effect">共享状态。</param>
    /// <param name="duration">构造声明的世界秒数。</param><param name="source">仍未被对象池换人的来源，可为空。</param>
    /// <param name="sourcePowerLevel">本次施放保存的来源境界。</param>
    public static Entity ApplyConfirmedStatus(Actor target, StatusEffectAsset effect, float duration, Actor source,
        float sourcePowerLevel) => ApplyConfiguredStatus(target, effect, duration, source, (status, resolved) =>
    {
        ConfigureStatusHeader(status, resolved, source);
        status.GetComponent<StatusComponent>().SourcePowerLevel = sourcePowerLevel;
    }, effect?.stats, sourcePowerLevel, true);

    /// <summary>已确认接触沿用状态的叠加或同类刷新，不重新读取来源当前倍率。</summary>
    /// <param name="target">实际目标。</param><param name="effect">已登记状态。</param>
    /// <param name="duration">本次声明时长。</param><param name="source">本次真实来源。</param>
    /// <param name="sourcePowerLevel">施放时固定的境界。</param><param name="application">已检查的叠加或刷新。</param>
    /// <param name="stats">可选覆盖属性；空值沿用状态自身属性。</param>
    internal static Entity ApplyContactStatus(Actor target, StatusEffectAsset effect, float duration, Actor source,
        float sourcePowerLevel, ConstructionStatusApplication application, BaseStats stats) =>
        ApplyContactStatusValues(target, effect, duration, source, sourcePowerLevel, application, stats);

    /// <summary>普通接触声明复用共享合并和周期；省略周期时恢复模板的零周期量，不遗留前次值。</summary>
    /// <param name="target">真实目标。</param><param name="effect">已经编译的状态。</param>
    /// <param name="source">本次真实来源。</param><param name="sourcePowerLevel">固定来源境界。</param>
    /// <param name="stats">本次已计算的属性。</param><param name="tickValue">按真实余力计算的每秒量。</param>
    internal static Entity ApplyStatusDeclaration(Actor target, CompiledConstructionStatus effect, Actor source,
        float sourcePowerLevel, BaseStats stats, float tickValue) =>
        ApplyContactStatusValues(target, effect.Asset, effect.Duration, source, sourcePowerLevel, effect.Application, stats,
            effect.StackLimit, effect.Tick != null || effect.Asset.SupportsTickValue ? tickValue : null, effect.Tick?.Element ?? default);

    /// <summary>统一配置来源、属性及可选周期值；叠加只改变已有状态的选择规则。</summary>
    /// <param name="target">真实目标。</param><param name="effect">共享状态。</param><param name="duration">未过抗性的时长。</param>
    /// <param name="source">本次来源。</param><param name="sourcePowerLevel">固定境界。</param><param name="application">合并方式。</param>
    /// <param name="stats">覆盖属性。</param><param name="stackLimit">同源有限层数。</param>
    /// <param name="tickValue">可选每秒量。</param><param name="tickElement">周期伤害元素。</param>
    private static Entity ApplyContactStatusValues(Actor target, StatusEffectAsset effect, float duration, Actor source,
        float sourcePowerLevel, ConstructionStatusApplication application, BaseStats stats, int stackLimit = 0,
        float? tickValue = null, ElementComposition tickElement = default)
    {
        return ApplyConfiguredStatus(target, effect, duration, source, (status, resolved) =>
        {
            ConfigureStatusHeader(status, resolved, source);
            status.GetComponent<StatusComponent>().SourcePowerLevel = sourcePowerLevel;
            if (tickValue.HasValue)
            {
                ref var tick = ref status.GetComponent<StatusTickState>();
                tick.Value = tickValue.Value;
                tick.Element = tickElement;
            }
            if (stats == null) status.RemoveComponent<StatusOverwriteStats>();
            else
            {
                if (!status.HasComponent<StatusOverwriteStats>()) status.AddComponent(new StatusOverwriteStats());
                status.GetComponent<StatusOverwriteStats>().stats = stats;
            }
        }, sourcePowerLevel: sourcePowerLevel, independent: application is ConstructionStatusApplication.Stack or ConstructionStatusApplication.SourceStack,
            matchAnySource: application == ConstructionStatusApplication.Refresh, sourceStackLimit: stackLimit);
    }

    /// <summary>
    /// 施加或刷新带自定义运行时组件的共享状态。
    /// 配置委托在通用持续时间和来源信息写入后执行。
    /// </summary>
    public static Entity ApplyStateStatus(
        Actor target,
        StatusEffectAsset effect,
        float duration,
        Actor source,
        Action<Entity> configure)
    {
        duration *= ResolveCurrentCarrierScale(source);
        return ApplyConfiguredStatus(target, effect, duration, source, (status, resolvedDuration) =>
        {
            ConfigureStatusHeader(status, resolvedDuration, source);
            configure?.Invoke(status);
        }, effect?.stats);
    }

    /// <summary>施加带独立周期强度和元素构成的持续状态。</summary>
    public static void ApplyTickingStatus(
        Actor target,
        StatusEffectAsset effect,
        float duration,
        float tickValue,
        ElementComposition element,
        Actor source)
    {
        float scale = ResolveCurrentCarrierScale(source);
        tickValue *= scale;
        ApplyConfiguredStatus(target, effect, duration, source, (status, resolvedDuration) =>
        {
            ConfigureStatusHeader(status, resolvedDuration, source);
            ref StatusTickState tick = ref status.GetComponent<StatusTickState>();
            tick.Value = tickValue;
            tick.Element = element;
        });
    }

    /// <summary>判断目标是否已经持有强度不低于给定值的同类全局状态。</summary>
    public static bool HasEqualOrStrongerStatus(Actor target, StatusEffectAsset effect, float potency)
    {
        return TryGetStrongestStatus(target, effect, out _, out float current) &&
               current + 0.0001f >= potency;
    }

    /// <summary>返回目标指定状态当前剩余的秒数。</summary>
    public static float GetStatusRemaining(Actor target, StatusEffectAsset effect)
    {
        if (!TryGetStrongestStatus(target, effect, out Entity status, out _)) return 0f;
        float duration = status.GetComponent<AliveTimeLimit>().value;
        float elapsed = status.GetComponent<AliveTimer>().value;
        return Mathf.Max(0f, duration - elapsed);
    }

    /// <summary>按全局最强规则施加带运行时属性的状态。</summary>
    public static bool ApplyStrongestStatus(
        Actor target,
        StatusEffectAsset effect,
        float duration,
        float potency,
        Actor source,
        BaseStats stats)
    {
        return ApplyStrongestConfiguredStatus(target, effect, duration, potency, source, status =>
        {
            ref StatusOverwriteStats overwrite = ref status.GetComponent<StatusOverwriteStats>();
            overwrite.stats ??= new BaseStats();
            overwrite.stats.clear();
            if (stats != null) overwrite.stats.mergeStats(stats);
        });
    }

    /// <summary>按全局最强规则施加带周期数值和元素构成的状态。</summary>
    public static bool ApplyStrongestTickingStatus(
        Actor target,
        StatusEffectAsset effect,
        float duration,
        float potency,
        float tickValue,
        ElementComposition element,
        Actor source)
    {
        return ApplyStrongestConfiguredStatus(target, effect, duration, potency, source, status =>
        {
            ref StatusTickState tick = ref status.GetComponent<StatusTickState>();
            tick.Value = tickValue;
            tick.Element = element;
        });
    }

    /// <summary>通过共享状态统一施加禁止移动和行动的空间囚禁。</summary>
    public static void ApplyImprisonment(Actor target, float duration, Actor source)
    {
        ApplyStatus(target, StatusEffects.Imprisoned, duration, source);
        ApplyStatus(target, StatusEffects.Silence, duration, source);
    }

    /// <summary>刷新同源状态或创建新状态，并按需恢复资产默认属性。</summary>
    /// <param name="target">承受状态的目标。</param><param name="effect">状态资料。</param>
    /// <param name="duration">原始秒数。</param><param name="source">来源人物。</param>
    /// <param name="configure">写入实际运行资料。</param><param name="defaultStats">需要恢复的默认属性。</param>
    /// <param name="sourcePowerLevel">可选的已固定来源境界。</param><param name="keepLonger">同源刷新是否保留较长剩余时间。</param>
    /// <param name="independent">每次接触独立加入状态。</param><param name="matchAnySource">刷新同类状态时允许改由本次来源接管。</param>
    private static Entity ApplyConfiguredStatus(
        Actor target,
        StatusEffectAsset effect,
        float duration,
        Actor source,
        Action<Entity, float> configure,
        BaseStats defaultStats = null, float? sourcePowerLevel = null, bool keepLonger = false, bool independent = false, bool matchAnySource = false, int sourceStackLimit = 0)
    {
        if (target == null || target.isRekt() || effect == null ||
            source == null && !sourcePowerLevel.HasValue || source != null && source.isRekt()) return default;
        ActorExtend targetExtend = target.GetExtend();
        var relations = targetExtend.E.GetRelations<StatusRelation>();
        Entity capped = default;
        if (sourceStackLimit > 0)
        {
            // 原有限毒层选择流逝时间最少的一份；不改变周期计时，避免刷新重置每秒触发。
            int count = 0;
            float leastElapsed = float.MaxValue;
            for (int i = 0; i < relations.Length; i++)
            {
                Entity candidate = relations[i].status;
                StatusComponent component = candidate.GetComponent<StatusComponent>();
                if (component.Type != effect || component.Source != source) continue;
                count++;
                float elapsed = candidate.GetComponent<AliveTimer>().value;
                if (elapsed >= leastElapsed) continue;
                leastElapsed = elapsed;
                capped = candidate;
            }
            independent = count < sourceStackLimit;
        }
        for (var i = 0; !independent && i < relations.Length; i++)
        {
            Entity status = relations[i].status;
            if (!capped.IsNull && status != capped) continue;
            StatusComponent component = status.GetComponent<StatusComponent>();
            if (component.Type != effect || !matchAnySource && component.Source != source) continue;
            if (!StatusEffectSuppression.TryResolveDuration(
                    targetExtend, effect, duration, source, sourcePowerLevel,
                    out float resolvedDuration)) return default;
            if (keepLonger)
                resolvedDuration = Mathf.Max(resolvedDuration,
                    status.GetComponent<AliveTimeLimit>().value - status.GetComponent<AliveTimer>().value);
            configure(status, resolvedDuration);
            if (defaultStats != null && status.TryGetComponent(out StatusOverwriteStats overwrite))
            {
                overwrite.stats ??= new BaseStats();
                overwrite.stats.clear();
                overwrite.stats.mergeStats(defaultStats);
                status.GetComponent<StatusOverwriteStats>() = overwrite;
            }

            target.setStatsDirty();
            return status;
        }

        Entity created = effect.NewEntity();
        configure(created, duration);
        if (targetExtend.AddSharedStatus(created)) return created;
        ModClass.I.CommandBuffer.AddTag<TagRecycle>(created.Id);
        return default;
    }

    /// <summary>复用一个全局同类状态实体完成强度比较、覆盖和刷新。</summary>
    private static bool ApplyStrongestConfiguredStatus(
        Actor target,
        StatusEffectAsset effect,
        float duration,
        float potency,
        Actor source,
        Action<Entity> configure)
    {
        if (target == null || target.isRekt() || effect == null || source == null || source.isRekt()) return false;
        potency = Mathf.Max(0f, potency);
        if (TryGetStrongestStatus(target, effect, out Entity status, out float currentPotency))
        {
            if (currentPotency > potency + 0.0001f) return false;
            if (!StatusEffectSuppression.TryResolveDuration(
                    target.GetExtend(), effect, duration, source, null, out float resolvedDuration)) return false;
            ConfigureStatusHeader(status, resolvedDuration, source);
            status.GetComponent<StatusPotency>().Value = potency;
            configure?.Invoke(status);
            target.setStatsDirty();
            return true;
        }

        status = effect.NewEntity();
        ConfigureStatusHeader(status, duration, source);
        status.GetComponent<StatusPotency>().Value = potency;
        configure?.Invoke(status);
        if (target.GetExtend().AddSharedStatus(status)) return true;
        ModClass.I.CommandBuffer.AddTag<TagRecycle>(status.Id);
        return false;
    }

    /// <summary>查找目标身上指定类型的最强状态。</summary>
    private static bool TryGetStrongestStatus(
        Actor target,
        StatusEffectAsset effect,
        out Entity statusEntity,
        out float potency)
    {
        statusEntity = default;
        potency = float.MinValue;
        if (target == null || target.isRekt() || effect == null) return false;
        var relations = target.GetExtend().E.GetRelations<StatusRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            Entity candidate = relations[i].status;
            if (candidate.GetComponent<StatusComponent>().Type != effect) continue;
            float candidatePotency = candidate.TryGetComponent(out StatusPotency value) ? value.Value : 0f;
            if (!statusEntity.IsNull && candidatePotency <= potency) continue;
            statusEntity = candidate;
            potency = candidatePotency;
        }

        return !statusEntity.IsNull;
    }

    /// <summary>把强控制、致命诅咒和普通减益映射到稳定的净化顺序。</summary>
    private static int ResolveCleansePriority(StatusEffectAsset effect)
    {
        if (effect == StatusEffects.Imprisoned) return 100;
        if (effect == StatusEffects.Freeze || effect == StatusEffects.Daze) return 90;
        if (effect == StatusEffects.DeathSentence) return 85;
        if (effect == StatusEffects.Silence) return 80;
        if (effect == StatusEffects.EternalCurse) return 70;
        if (effect == StatusEffects.ArmorBreak || effect == StatusEffects.Weaken) return 60;
        if (effect == StatusEffects.Poison || effect == StatusEffects.Burn) return 50;
        if (effect == StatusEffects.Slow) return 40;
        return 10;
    }

    /// <summary>把仓库已标记的原版负面状态映射到与共享状态一致的净化顺序。</summary>
    private static int ResolveCleansePriority(StatusAsset effect)
    {
        if (effect == WorldboxGame.StatusEffects.Frozen || effect == WorldboxGame.StatusEffects.Stunned) return 90;
        if (effect == WorldboxGame.StatusEffects.SpellSilence) return 80;
        if (effect == WorldboxGame.StatusEffects.Burning) return 50;
        return 10;
    }

    /// <summary>写入共享状态头部和单项属性覆盖。</summary>
    private static void ConfigureStatus(
        Entity status,
        float duration,
        string statId,
        float statValue,
        Actor source)
    {
        ConfigureStatusHeader(status, duration, source);
        BaseStats stats;
        if (status.HasComponent<StatusOverwriteStats>())
        {
            ref StatusOverwriteStats overwrite = ref status.GetComponent<StatusOverwriteStats>();
            stats = overwrite.stats ??= new BaseStats();
            stats.clear();
        }
        else
        {
            stats = new BaseStats();
            status.AddComponent(new StatusOverwriteStats { stats = stats });
        }

        stats[statId] = statValue;
    }

    /// <summary>读取当前主动能力载体的线性心神倍率。</summary>
    private static float ResolveCurrentCarrierScale(Actor source)
    {
        if (source == null || source.isRekt()) return 1f;
        ActorExtend sourceExtend = source.GetExtend();
        if (SkillCasterContextService.TryGetCurrent(sourceExtend, out SkillCasterContext context))
            return context.EffectScale;
        if (sourceExtend.HasComponent<YuanshenSoulCarrierState>())
        {
            context = SkillCasterContextService.Resolve(sourceExtend);
            if (context.IsValid) return context.EffectScale;
        }

        return 1f;
    }

    /// <summary>重置共享状态的持续时间、来源与来源功率等级。</summary>
    private static void ConfigureStatusHeader(Entity status, float duration, Actor source)
    {
        status.GetComponent<AliveTimeLimit>().value = duration;
        status.GetComponent<AliveTimer>().value = 0f;
        ref StatusComponent component = ref status.GetComponent<StatusComponent>();
        component.SetSource(source);
        component.SourcePowerLevel = source?.GetExtend().GetPowerLevel();
    }
}
