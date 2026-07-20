using System;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Combat;

/// <summary>内容系统共用的状态查询、施加、刷新、净化与驱散原语。</summary>
public static class CombatStatusEffects
{
    /// <summary>判断目标是否持有指定类型且来自指定来源的共享状态。</summary>
    public static bool HasStatus(Actor target, StatusEffectAsset effect, Actor source = null)
    {
        if (target == null || effect == null) return false;
        foreach (Entity status in target.GetExtend().GetStatuses())
        {
            StatusComponent component = status.GetComponent<StatusComponent>();
            if (component.Type == effect && (source == null || component.Source == source)) return true;
        }
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
    public static int RemoveStatus(Actor target, StatusEffectAsset effect, Actor source = null)
    {
        if (target == null || target.isRekt() || effect == null) return 0;
        ActorExtend extend = target.GetExtend();
        int removed = 0;
        foreach (Entity status in extend.GetStatuses())
        {
            StatusComponent component = status.GetComponent<StatusComponent>();
            if (component.Type != effect || source != null && component.Source != source) continue;
            extend.RemoveSharedStatus(status);
            ModClass.I.CommandBuffer.AddTag<TagRecycle>(status.Id);
            removed++;
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
        Actor source)
    {
        ApplyConfiguredStatus(target, effect, source,
            status => ConfigureStatus(status, duration, statId, statValue, source));
    }

    /// <summary>施加只使用状态资产固有属性的共享状态。</summary>
    public static void ApplyStatus(Actor target, StatusEffectAsset effect, float duration, Actor source)
    {
        ApplyConfiguredStatus(target, effect, source,
            status => ConfigureStatusHeader(status, duration, source), effect?.stats);
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
        ApplyConfiguredStatus(target, effect, source, status =>
        {
            ConfigureStatusHeader(status, duration, source);
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
    private static void ApplyConfiguredStatus(
        Actor target,
        StatusEffectAsset effect,
        Actor source,
        Action<Entity> configure,
        BaseStats defaultStats = null)
    {
        if (target == null || target.isRekt() || effect == null || source == null || source.isRekt()) return;
        ActorExtend targetExtend = target.GetExtend();
        foreach (Entity status in targetExtend.GetStatuses())
        {
            StatusComponent component = status.GetComponent<StatusComponent>();
            if (component.Type != effect || component.Source != source) continue;
            configure(status);
            if (defaultStats != null && status.TryGetComponent(out StatusOverwriteStats overwrite))
            {
                overwrite.stats ??= new BaseStats();
                overwrite.stats.clear();
                overwrite.stats.mergeStats(defaultStats);
                status.GetComponent<StatusOverwriteStats>() = overwrite;
            }
            target.setStatsDirty();
            return;
        }

        Entity created = effect.NewEntity();
        configure(created);
        if (!targetExtend.AddSharedStatus(created)) ModClass.I.CommandBuffer.AddTag<TagRecycle>(created.Id);
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

    /// <summary>重置共享状态的持续时间、来源与来源功率等级。</summary>
    private static void ConfigureStatusHeader(Entity status, float duration, Actor source)
    {
        status.GetComponent<AliveTimeLimit>().value = duration;
        status.GetComponent<AliveTimer>().value = 0f;
        ref StatusComponent component = ref status.GetComponent<StatusComponent>();
        component.Source = source;
        component.SourcePowerLevel = source.GetExtend().GetPowerLevel();
    }
}
