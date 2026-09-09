using System;
using System.Collections.Generic;
using Cultiway.Content.CreatureCompositions.Components;
using Cultiway.Content.CreatureCompositions.Models;
using Cultiway.Content.CreatureCompositions.Services;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.SkillLibV3;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.CreatureCompositions.Combat;

/// <summary>
///     全部组合器官共用的被动效果入口。分发器在固定伤害阶段、伤害结算、施放完成、
///     击杀和死亡钩子上按“事件、类别、等级、槽位、器官”的稳定顺序逐器官执行效果。
/// </summary>
public static class CreatureOrganEffectDispatcher
{
    private static bool initialized;

    /// <summary>注册全部全局钩子；只允许模块初始化调用一次。</summary>
    internal static void Initialize()
    {
        if (initialized) return;
        initialized = true;

        ActorExtend.RegisterActionOnFinalDamage(FinalDamageStage.Adaptation, OnAdaptation);
        ActorExtend.RegisterActionOnFinalDamage(FinalDamageStage.Survival, OnSurvival);
        ActorExtend.RegisterActionOnFinalDamage(FinalDamageStage.LastResort, OnLastResort);
        ActorExtend.RegisterActionOnDamageResolved(OnDamageResolved);
        ActorExtend.RegisterActionOnSkillCastCompleted(OnSkillCastCompleted);
        ActorExtend.RegisterActionOnKill(OnKill);
        ActorExtend.RegisterActionOnDeath(OnDeath);
    }

    private static void OnAdaptation(
        ActorExtend self, BaseSimObject attacker, ElementComposition _, AttackType __, ref float damage)
    {
        DispatchFinalDamage(CreatureOrganEventKind.Adaptation, self, attacker, ref damage);
    }

    private static void OnSurvival(
        ActorExtend self, BaseSimObject attacker, ElementComposition _, AttackType __, ref float damage)
    {
        DispatchFinalDamage(CreatureOrganEventKind.Survival, self, attacker, ref damage);
    }

    private static void OnLastResort(
        ActorExtend self, BaseSimObject attacker, ElementComposition _, AttackType __, ref float damage)
    {
        DispatchFinalDamage(CreatureOrganEventKind.LastResort, self, attacker, ref damage);
    }

    private static void OnDamageResolved(
        ActorExtend self, BaseSimObject attacker, float damage, ElementComposition _, AttackType __)
    {
        DispatchSimple(CreatureOrganEventKind.DamageResolved, self, attacker, damage);
    }

    private static void OnSkillCastCompleted(
        ActorExtend self, Entity container, int emittedCount, SkillCastFundingSource _)
    {
        DispatchSkillCast(self, container, emittedCount);
    }

    private static void OnKill(ActorExtend self, Actor victim, Kingdom _)
    {
        DispatchKill(self, victim);
    }

    private static void OnDeath(ActorExtend self)
    {
        DispatchDeath(self);
    }

    /// <summary>保命阶段必须当场生效：逐器官直接改写本次伤害，不经过任何延迟队列。</summary>
    private static void DispatchFinalDamage(
        CreatureOrganEventKind kind, ActorExtend self, BaseSimObject attacker, ref float damage)
    {
        foreach ((CreatureOrganEffectFamily family, CreatureOrganEffectSource source) in
                 CollectEffects(self, kind))
        {
            var context = new CreatureOrganEffectContext
            {
                Owner = self,
                Attacker = attacker,
                Damage = damage,
                EffectFamilyId = source.EffectFamilyId,
                Rank = source.Rank,
                SlotId = source.SlotId,
                OrganId = source.OrganId,
            };
            family.Handler(ref context);
            damage = context.Damage;
            if (damage <= 0f) return;
        }
    }

    /// <summary>伤害结算与击杀事件没有可改写的进行中数值，只通知观察效果。</summary>
    private static void DispatchSimple(
        CreatureOrganEventKind kind, ActorExtend self, BaseSimObject attacker, float damage)
    {
        foreach ((CreatureOrganEffectFamily family, CreatureOrganEffectSource source) in
                 CollectEffects(self, kind))
        {
            var context = new CreatureOrganEffectContext
            {
                Owner = self,
                Attacker = attacker,
                Damage = damage,
                EffectFamilyId = source.EffectFamilyId,
                Rank = source.Rank,
                SlotId = source.SlotId,
                OrganId = source.OrganId,
            };
            family.Handler(ref context);
        }
    }

    /// <summary>技能施放完成事件把容器和发射数量交给效果类别自行解释。</summary>
    private static void DispatchSkillCast(ActorExtend self, Entity container, int emittedCount)
    {
        foreach ((CreatureOrganEffectFamily family, CreatureOrganEffectSource source) in
                 CollectEffects(self, CreatureOrganEventKind.SkillCastCompleted))
        {
            var context = new CreatureOrganEffectContext
            {
                Owner = self,
                SkillContainer = container,
                EmittedCount = emittedCount,
                EffectFamilyId = source.EffectFamilyId,
                Rank = source.Rank,
                SlotId = source.SlotId,
                OrganId = source.OrganId,
            };
            family.Handler(ref context);
        }
    }

    /// <summary>击杀事件携带死亡单位，供吞噬、精华等玩法读取猎物特征。</summary>
    private static void DispatchKill(ActorExtend self, Actor victim)
    {
        foreach ((CreatureOrganEffectFamily family, CreatureOrganEffectSource source) in
                 CollectEffects(self, CreatureOrganEventKind.Kill))
        {
            var context = new CreatureOrganEffectContext
            {
                Owner = self,
                Victim = victim,
                EffectFamilyId = source.EffectFamilyId,
                Rank = source.Rank,
                SlotId = source.SlotId,
                OrganId = source.OrganId,
            };
            family.Handler(ref context);
        }
    }

    /// <summary>死亡事件只通知，不允许效果阻止已经发生的死亡。</summary>
    private static void DispatchDeath(ActorExtend self)
    {
        foreach ((CreatureOrganEffectFamily family, CreatureOrganEffectSource source) in
                 CollectEffects(self, CreatureOrganEventKind.Death))
        {
            var context = new CreatureOrganEffectContext
            {
                Owner = self,
                EffectFamilyId = source.EffectFamilyId,
                Rank = source.Rank,
                SlotId = source.SlotId,
                OrganId = source.OrganId,
            };
            family.Handler(ref context);
        }
    }

    /// <summary>低频生命过程更新；由维护系统按批调用。</summary>
    public static void DispatchUpkeep(ActorExtend self)
    {
        foreach ((CreatureOrganEffectFamily family, CreatureOrganEffectSource source) in
                 CollectEffects(self, CreatureOrganEventKind.Upkeep))
        {
            var context = new CreatureOrganEffectContext
            {
                Owner = self,
                EffectFamilyId = source.EffectFamilyId,
                Rank = source.Rank,
                SlotId = source.SlotId,
                OrganId = source.OrganId,
            };
            family.Handler(ref context);
        }
    }

    /// <summary>
    ///     读取当前身体的整理结果，筛选声明响应该事件的效果类别，
    ///     按“类别编号、等级、槽位、器官编号”的固定顺序返回逐器官执行列表。
    /// </summary>
    private static List<(CreatureOrganEffectFamily family, CreatureOrganEffectSource source)> CollectEffects(
        ActorExtend self, CreatureOrganEventKind kind)
    {
        var ordered = new List<(CreatureOrganEffectFamily, CreatureOrganEffectSource)>();
        if (self?.Base == null || self.Base.isRekt()) return ordered;
        if (!self.TryGetComponent(out CreaturePhenotype phenotype) || !phenotype.IsValid) return ordered;
        if (!CreaturePhenotypeCompiler.TryGetCompiled(
                phenotype.CompiledIndex, phenotype.Signature, out CompiledCreaturePhenotype compiled))
            return ordered;

        var eventMask = (CreatureOrganEventMask)(1 << (int)kind);
        foreach (CompiledCreatureOrgan organ in compiled.OrderedOrgans)
        {
            foreach (Libraries.CreatureEffectRank effect in organ.Rank.EffectRanks ?? Array.Empty<Libraries.CreatureEffectRank>())
            {
                if (string.IsNullOrEmpty(effect.EffectFamilyId)) continue;
                if (!CreatureOrganEffectFamilies.TryGet(effect.EffectFamilyId, out CreatureOrganEffectFamily family))
                    continue;
                if ((family.Events & eventMask) == 0) continue;
                ordered.Add((family, new CreatureOrganEffectSource(
                    effect.EffectFamilyId, effect.Rank, organ.Entry.SlotId, organ.Organ.id)));
            }
        }

        ordered.Sort((left, right) =>
        {
            int byFamily = string.CompareOrdinal(left.Item2.EffectFamilyId, right.Item2.EffectFamilyId);
            if (byFamily != 0) return byFamily;
            int byRank = left.Item2.Rank.CompareTo(right.Item2.Rank);
            if (byRank != 0) return byRank;
            int bySlot = string.CompareOrdinal(left.Item2.SlotId, right.Item2.SlotId);
            return bySlot != 0 ? bySlot : string.CompareOrdinal(left.Item2.OrganId, right.Item2.OrganId);
        });
        return ordered;
    }

    private readonly struct CreatureOrganEffectSource
    {
        internal readonly string EffectFamilyId;
        internal readonly int Rank;
        internal readonly string SlotId;
        internal readonly string OrganId;

        internal CreatureOrganEffectSource(string effectFamilyId, int rank, string slotId, string organId)
        {
            EffectFamilyId = effectFamilyId;
            Rank = rank;
            SlotId = slotId;
            OrganId = organId;
        }
    }
}
