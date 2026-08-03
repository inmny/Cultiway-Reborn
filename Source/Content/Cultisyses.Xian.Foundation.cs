using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Core.Progression;
using NeoModLoader.api.attributes;
using strings;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>仙道筑基境界的三花五气、资质计算、同步与传承规则。</summary>
public partial class Cultisyses
{
    /// <summary>各项来源均达到筑基境界基准时，三花采用的中性资质。</summary>
    private const float FoundationNeutralAptitude = 3f;

    /// <summary>智力没有仙道境界表基准，使用原版常见智慧种族的初始智力作为中性值。</summary>
    private const float FoundationIntelligenceReference = 3f;

    /// <summary>修炼效率以一倍效率作为中性值。</summary>
    private const float FoundationCultivationEfficiencyReference = 1f;

    /// <summary>将三花五气的完成项映射到筑基境界的细分排序区间。</summary>
    [Hotfixable]
    private static float GetFoundationDetailedLevel(ActorExtend actor)
    {
        var result = 0f;
        if (!actor.TryGetComponent(out XianBase xianBase)) return result;

        result += 0.01f;
        if (xianBase.jing > 0) result += 0.01f;
        if (xianBase.qi > 0) result += 0.01f;
        if (xianBase.shen > 0) result += 0.01f;
        if (xianBase.fire > 0) result += 0.01f;
        if (xianBase.wood > 0) result += 0.01f;
        if (xianBase.earth > 0) result += 0.01f;
        if (xianBase.iron > 0) result += 0.01f;
        if (xianBase.water > 0) result += 0.01f;
        return result;
    }

    /// <summary>统计已经完成的三花五气项目数量。</summary>
    private static int CountFoundationParts(ref XianBase xianBase)
    {
        int count = 0;
        if (xianBase.jing != 0f) count++;
        if (xianBase.qi != 0f) count++;
        if (xianBase.shen != 0f) count++;
        if (xianBase.iron != 0f) count++;
        if (xianBase.wood != 0f) count++;
        if (xianBase.water != 0f) count++;
        if (xianBase.fire != 0f) count++;
        if (xianBase.earth != 0f) count++;
        return count;
    }

    /// <summary>筑基未完成时选择逐项筑基，全部筑基项完成后选择结丹。</summary>
    private static ProgressionTransitionAsset<Xian> SelectFoundationTransition(ActorExtend actor,
        CultisysAsset<Xian> cultisys, ref Xian component)
    {
        RealmProgressionAsset<Xian> realm = cultisys.Progression.GetRealm(XianLevels.XianBase);
        return IsFoundationComplete(actor)
            ? realm.GetMajorTransition()
            : realm.GetMinorTransition();
    }

    /// <summary>检查三花与五气是否都已经写入非零筑基强度。</summary>
    private static bool IsFoundationComplete(ActorExtend actor)
    {
        if (!actor.TryGetComponent(out XianBase xianBase)) return false;
        return xianBase.jing != 0f
               && xianBase.qi != 0f
               && xianBase.shen != 0f
               && xianBase.fire != 0f
               && xianBase.wood != 0f
               && xianBase.earth != 0f
               && xianBase.iron != 0f
               && xianBase.water != 0f;
    }

    /// <summary>按固定顺序选择下一筑基项，并使用当前质量确定性直接结算。</summary>
    private static ProgressionResolution ResolveFoundationStep(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                                ref Xian component)
    {
        XianBase xianBase = actor.GetComponent<XianBase>();
        FoundationPart part = GetNextFoundationPart(ref xianBase);
        if (part == FoundationPart.None) return ProgressionResolution.NoProgress();
        float quality = ResolveQiRefinementSample(actor).Quality;
        float value = ResolveFoundationValue(actor, part, quality);
        return ProgressionResolution.Success(new FoundationStepPayload(part, value, quality));
    }

    /// <summary>按精、气、神、火、木、土、金、水顺序取得第一个尚未完成的筑基项。</summary>
    private static FoundationPart GetNextFoundationPart(ref XianBase xianBase)
    {
        if (xianBase.jing == 0f) return FoundationPart.Jing;
        if (xianBase.qi == 0f) return FoundationPart.Qi;
        if (xianBase.shen == 0f) return FoundationPart.Shen;
        if (xianBase.fire == 0f) return FoundationPart.Fire;
        if (xianBase.wood == 0f) return FoundationPart.Wood;
        if (xianBase.earth == 0f) return FoundationPart.Earth;
        if (xianBase.iron == 0f) return FoundationPart.Iron;
        if (xianBase.water == 0f) return FoundationPart.Water;
        return FoundationPart.None;
    }

    /// <summary>三花使用各自的综合资质，五气使用灵根对应元素强度，计算指定筑基项的资质。</summary>
    private static float GetFoundationAptitude(ActorExtend actor, FoundationPart part)
    {
        switch (part)
        {
            case FoundationPart.Jing:
                return ResolveJingFoundationAptitude(actor);
            case FoundationPart.Qi:
                return ResolveQiFoundationAptitude(actor);
            case FoundationPart.Shen:
                return ResolveShenFoundationAptitude(actor);
        }
        if (!actor.HasElementRoot()) return 0f;
        ref ElementRoot root = ref actor.GetElementRoot();
        return part switch
        {
            FoundationPart.Fire => root.Fire,
            FoundationPart.Wood => root.Wood,
            FoundationPart.Earth => root.Earth,
            FoundationPart.Iron => root.Iron,
            FoundationPart.Water => root.Water,
            _ => 0f
        };
    }

    /// <summary>以体魄、寿命和生命恢复等权计算精之花资质。</summary>
    private static float ResolveJingFoundationAptitude(ActorExtend actor)
    {
        float factorSum = ResolveFoundationStatFactor(actor, S.health)
                          + ResolveFoundationStatFactor(actor, S.lifespan)
                          + ResolveFoundationStatFactor(actor, WorldboxGame.BaseStats.HealthRegen.id);
        return CombineFoundationFactors(factorSum, 3);
    }

    /// <summary>以灵气容量和当前实际修炼效率等权计算气之花资质。</summary>
    private static float ResolveQiFoundationAptitude(ActorExtend actor)
    {
        float factorSum = ResolveFoundationStatFactor(actor, BaseStatses.MaxWakan.id)
                          + ResolveFoundationFactor(
                              CultivationEfficiencyResolver.Resolve(actor).FinalMultiplier,
                              FoundationCultivationEfficiencyReference);
        return CombineFoundationFactors(factorSum, 2);
    }

    /// <summary>以智力、神识和元神容量等权计算神之花资质。</summary>
    private static float ResolveShenFoundationAptitude(ActorExtend actor)
    {
        float factorSum = ResolveFoundationFactor(
                              actor.GetStat(S.intelligence),
                              FoundationIntelligenceReference)
                          + ResolveFoundationStatFactor(actor, WorldboxGame.BaseStats.DivineSense.id)
                          + ResolveFoundationStatFactor(actor, WorldboxGame.BaseStats.MaxSoul.id);
        return CombineFoundationFactors(factorSum, 3);
    }

    /// <summary>以筑基境界累计属性作为基准，把角色当前属性换算为无量纲评分。</summary>
    private static float ResolveFoundationStatFactor(ActorExtend actor, string statId)
    {
        float reference = Xian.LevelAccumBaseStats[XianLevels.XianBase][statId];
        return ResolveFoundationFactor(actor.GetStat(statId), reference);
    }

    /// <summary>将来源值按基准作对数压缩；达到基准时返回 1，翻倍后仍有收益但边际递减。</summary>
    private static float ResolveFoundationFactor(float value, float reference)
    {
        return Mathf.Log(1f + Mathf.Max(0f, value) / reference, 2f);
    }

    /// <summary>等权汇总已归一化的来源，并映射回三花沿用的资质尺度。</summary>
    private static float CombineFoundationFactors(float factorSum, int factorCount)
    {
        return FoundationNeutralAptitude * factorSum / factorCount;
    }

    /// <summary>用资质乘以 0.75..1 的当前质量系数，生成无随机筑基步骤强度。</summary>
    private static float ResolveFoundationValue(ActorExtend actor, FoundationPart part, float quality)
    {
        float aptitude = Mathf.Max(0f, GetFoundationAptitude(actor, part));
        return Mathf.Max(0.01f, aptitude * (0.75f + Mathf.Clamp01(quality) * 0.25f));
    }

    /// <summary>构造只用于仙基胚胎结构评分的完整预期三花五气。</summary>
    private static XianBase ResolveFoundationSeed(ActorExtend actor)
    {
        return new XianBase
        {
            jing = GetFoundationAptitude(actor, FoundationPart.Jing),
            qi = GetFoundationAptitude(actor, FoundationPart.Qi),
            shen = GetFoundationAptitude(actor, FoundationPart.Shen),
            fire = GetFoundationAptitude(actor, FoundationPart.Fire),
            wood = GetFoundationAptitude(actor, FoundationPart.Wood),
            earth = GetFoundationAptitude(actor, FoundationPart.Earth),
            iron = GetFoundationAptitude(actor, FoundationPart.Iron),
            water = GetFoundationAptitude(actor, FoundationPart.Water)
        };
    }

    /// <summary>自然熬炼一个筑基项目后消耗全部当前灵气。</summary>
    private static void ApplyFoundationStepCost(
        ActorExtend actor,
        CultisysAsset<Xian> cultisys,
        ref Xian component,
        object payload)
    {
        WakanResourceService.Clear(actor, ref component);
    }

    /// <summary>把成功判定载荷中的强度写入对应 XianBase 筑基字段。</summary>
    private static void ApplyFoundationStep(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                            ref Xian component, object payload)
    {
        var step = (FoundationStepPayload)payload;
        ref XianBase xianBase = ref actor.GetOrAddComponent<XianBase>();
        SetFoundationPart(ref xianBase, step.Part, step.Value);
        CoreFormationComposer.RefineFoundation(actor, ref xianBase, step.Value, step.Quality);
        actor.MarkCultiwayStatsDirty();
        actor.MarkSemanticProfileDirty();
        CoreFormationEffectResolver.Synchronize(actor);
    }

    /// <summary>把一个已结算值写入指定三花五气字段。</summary>
    private static void SetFoundationPart(ref XianBase foundation, FoundationPart part, float value)
    {
        switch (part)
        {
            case FoundationPart.Jing: foundation.jing = value; break;
            case FoundationPart.Qi: foundation.qi = value; break;
            case FoundationPart.Shen: foundation.shen = value; break;
            case FoundationPart.Fire: foundation.fire = value; break;
            case FoundationPart.Wood: foundation.wood = value; break;
            case FoundationPart.Earth: foundation.earth = value; break;
            case FoundationPart.Iron: foundation.iron = value; break;
            case FoundationPart.Water: foundation.water = value; break;
        }
    }

    /// <summary>为明确的同步入口补齐最低九层真气、仙基胚胎和全部三花五气。</summary>
    private static XianBase CompleteFoundationForGrant(ActorExtend actor)
    {
        ref QiRefinementState qi = ref actor.GetOrAddComponent<QiRefinementState>();
        while (qi.CompletedLayers < MinimumFoundationQiLayers)
        {
            QiRefinementSample settlement = ResolveQiRefinementSample(actor);
            CoreFormationComposer.RefineQi(
                actor, ref qi, settlement.Quality, settlement.Composition, settlement.ElementSemantics);
        }

        XianBase xianBase = actor.TryGetComponent(out XianBase existing) ? existing : default;
        if (!xianBase.formation.IsValid)
            xianBase.formation = CoreFormationComposer.ComposeFoundation(
                actor, ResolveFoundationSeed(actor), qi.formation);
        FoundationPart part;
        while ((part = GetNextFoundationPart(ref xianBase)) != FoundationPart.None)
        {
            float quality = ResolveQiRefinementSample(actor).Quality;
            float value = ResolveFoundationValue(actor, part, quality);
            SetFoundationPart(ref xianBase, part, value);
            CoreFormationComposer.RefineFoundation(actor, ref xianBase, value, quality);
        }
        return xianBase;
    }

    /// <summary>同步到筑基境界时补齐真气与仙基，并移除未来境界归档。</summary>
    private static void NormalizeFoundationRealm(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                  ref Xian component, object payload)
    {
        ref QiRefinementState qi = ref actor.GetOrAddComponent<QiRefinementState>();
        while (qi.CompletedLayers < MinimumFoundationQiLayers)
        {
            QiRefinementSample settlement = ResolveQiRefinementSample(actor);
            CoreFormationComposer.RefineQi(
                actor, ref qi, settlement.Quality, settlement.Composition, settlement.ElementSemantics);
        }
        if (!actor.HasComponent<XianBase>())
        {
            actor.AddComponent(new XianBase
            {
                formation = CoreFormationComposer.ComposeFoundation(
                    actor, ResolveFoundationSeed(actor), qi.formation)
            });
        }
        if (actor.HasComponent<Jindan>()) actor.E.RemoveComponent<Jindan>();
        if (actor.HasComponent<Yuanying>()) actor.E.RemoveComponent<Yuanying>();
        actor.MarkSemanticProfileDirty();
    }

    /// <summary>将仙基字段和成果数组深拷贝给目标；来源没有仙基时同步移除。</summary>
    private static void TransferFoundation(ActorExtend source, ActorExtend target)
    {
        if (source.TryGetComponent(out XianBase foundation))
        {
            ref XianBase targetFoundation = ref target.GetOrAddComponent<XianBase>();
            targetFoundation = foundation.DeepClone();
        }
        else if (target.HasComponent<XianBase>())
        {
            target.E.RemoveComponent<XianBase>();
        }
    }

    /// <summary>筑基过程中按顺序填充的三花五气项目。</summary>
    private enum FoundationPart
    {
        /// <summary>所有筑基项目都已完成，没有下一项。</summary>
        None,

        /// <summary>精之花。</summary>
        Jing,

        /// <summary>气之花。</summary>
        Qi,

        /// <summary>神之花。</summary>
        Shen,

        /// <summary>火气。</summary>
        Fire,

        /// <summary>木气。</summary>
        Wood,

        /// <summary>土气。</summary>
        Earth,

        /// <summary>金气，对应组件中的 iron 字段。</summary>
        Iron,

        /// <summary>水气。</summary>
        Water
    }

    /// <summary>一次筑基成功后传给结构变换的不可变数据。</summary>
    private sealed class FoundationStepPayload
    {
        /// <summary>创建一次筑基步骤的项目与结算强度载荷。</summary>
        public FoundationStepPayload(FoundationPart part, float value, float quality)
        {
            Part = part;
            Value = value;
            Quality = Mathf.Clamp01(quality);
        }

        /// <summary>本次完成的三花或五气项目。</summary>
        public FoundationPart Part { get; }

        /// <summary>写入对应 XianBase 字段的筑基强度。</summary>
        public float Value { get; }

        /// <summary>本次熬炼使用的独立品质样本。</summary>
        public float Quality { get; }
    }
}
