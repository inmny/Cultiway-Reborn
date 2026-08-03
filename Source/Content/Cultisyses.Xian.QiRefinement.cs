using System;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Core.Progression;
using Cultiway.Core.Semantics;
using NeoModLoader.api.attributes;
using strings;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>仙道炼气境界的成果形成、进阶决策、同步与传承规则。</summary>
public partial class Cultisyses
{
    /// <summary>允许从炼气进入筑基所需的最低真气层数，也是后续层数的里程碑间隔。</summary>
    internal const int MinimumFoundationQiLayers = 9;

    /// <summary>九层后继续凝练下一层真气所需的基础边际收益。</summary>
    private const float FurtherQiRefinementThreshold = 0.17f;

    /// <summary>下一层恰好达到九层倍数时追加的里程碑收益。</summary>
    private const float QiRefinementMilestoneBenefit = 0.025f;

    /// <summary>将无上限真气层数映射到不会越过下一大境界的细分排序区间。</summary>
    [Hotfixable]
    private static float GetQiRefinementDetailedLevel(ActorExtend actor)
    {
        int layers = actor.GetComponent<QiRefinementState>().CompletedLayers;
        return layers <= 0 ? 0f : 1f - 1f / (layers + 1f);
    }

    /// <summary>角色首次获得仙道时同步创建尚未凝成真气的成果槽。</summary>
    private static void InitializeXianState(ActorExtend actor, CultisysAsset<Xian> cultisys, ref Xian component)
    {
        actor.AddComponent(new QiRefinementState());
    }

    /// <summary>按角色当前来源解析下一层真气，并直接返回确定性成功结算。</summary>
    private static ProgressionResolution ResolveQiCirculation(
        ActorExtend actor,
        CultisysAsset<Xian> cultisys,
        ref Xian component)
    {
        return ProgressionResolution.Success(ResolveQiRefinementSample(actor));
    }

    /// <summary>把成功凝练提交为同名真气的下一层。</summary>
    private static void ApplyQiCirculation(
        ActorExtend actor,
        CultisysAsset<Xian> cultisys,
        ref Xian component,
        object payload)
    {
        QiRefinementSample sample = (QiRefinementSample)payload;
        ref QiRefinementState state = ref actor.GetComponent<QiRefinementState>();
        CoreFormationComposer.RefineQi(
            actor,
            ref state,
            sample.Quality,
            sample.Composition,
            sample.ElementSemantics);
        actor.MarkCultiwayStatsDirty();
        actor.MarkSemanticProfileDirty();
        CoreFormationEffectResolver.Synchronize(actor);
    }

    /// <summary>自然凝成下一层真气时消耗全部当前灵气。</summary>
    private static void ApplyQiLayerCost(
        ActorExtend actor,
        CultisysAsset<Xian> cultisys,
        ref Xian component,
        object payload)
    {
        WakanResourceService.Clear(actor, ref component);
    }

    /// <summary>筑基前消费最后一次蓄满的灵气，但保留命名真气成果。</summary>
    private static void ApplyQiFoundationCost(
        ActorExtend actor,
        CultisysAsset<Xian> cultisys,
        ref Xian component,
        object payload)
    {
        WakanResourceService.Clear(actor, ref component);
    }

    /// <summary>进入筑基时从真气谱系创建仙基胚胎，并保留真气组件作为归档页。</summary>
    private static void CreateFoundationEmbryo(
        ActorExtend actor,
        CultisysAsset<Xian> cultisys,
        ref Xian component,
        object payload)
    {
        CoreFormationSnapshot qi = actor.GetComponent<QiRefinementState>().formation;
        if (!qi.IsFinalized) throw new InvalidOperationException("进入筑基前真气尚未完成九层定型。");
        XianBase seed = ResolveFoundationSeed(actor);
        ref XianBase foundation = ref actor.GetOrAddComponent<XianBase>();
        foundation = new XianBase
        {
            formation = CoreFormationComposer.ComposeFoundation(actor, seed, qi)
        };
        actor.MarkCultiwayStatsDirty();
        actor.MarkSemanticProfileDirty();
    }

    /// <summary>不足九层必定凝气，达到后由边际收益决策继续凝气或筑基。</summary>
    private static ProgressionTransitionAsset<Xian> SelectQiRefinementTransition(
        ActorExtend actor,
        CultisysAsset<Xian> cultisys,
        ref Xian component)
    {
        RealmProgressionAsset<Xian> realm = cultisys.Progression.GetRealm(XianLevels.QiRefinement);
        return ShouldRefineQiFurther(actor)
            ? realm.Transitions.Find(transition => transition.Id == "xian.circulate_qi")
            : realm.GetMajorTransition();
    }

    /// <summary>达到九层后按边际收益、里程碑、寿命压力和性格决定继续炼气还是直接筑基。</summary>
    private static bool ShouldRefineQiFurther(ActorExtend actor)
    {
        int completed = actor.GetComponent<QiRefinementState>().CompletedLayers;
        if (completed < MinimumFoundationQiLayers) return true;

        QiRefinementSample sample = ResolveQiRefinementSample(actor);
        int nextLayer = completed + 1;
        float benefit = sample.Quality / Mathf.Sqrt(nextLayer);
        if (nextLayer % MinimumFoundationQiLayers == 0) benefit += QiRefinementMilestoneBenefit;

        float threshold = FurtherQiRefinementThreshold;
        if (actor.Base.hasTrait(WorldboxGame.ActorTraits.Ambitious.id)) threshold -= 0.035f;
        float rationality = Mathf.Clamp(actor.Base.stats["personality_rationality"], -1f, 1f);
        threshold += rationality * 0.02f;

        if (!actor.Base.hasTrait(ActorTraits.Immortal.id))
        {
            float lifespan = actor.Base.stats[S.lifespan];
            float ageRatio = lifespan > 0f ? actor.Base.data.getAge() / lifespan : 0f;
            threshold += Mathf.Max(0f, ageRatio - 0.60f) * 0.30f;
        }
        return benefit >= Mathf.Clamp(threshold, 0.10f, 0.30f);
    }

    /// <summary>直接授予大境界时只把真气补到最低九层，不赠送额外层数。</summary>
    private static ProgressionTransitionAsset<Xian> SelectQiRefinementGrantTransition(
        ActorExtend actor,
        CultisysAsset<Xian> cultisys,
        ref Xian component)
    {
        RealmProgressionAsset<Xian> realm = cultisys.Progression.GetRealm(XianLevels.QiRefinement);
        if (actor.GetComponent<QiRefinementState>().CompletedLayers < MinimumFoundationQiLayers)
            return realm.Transitions.Find(transition => transition.Id == "xian.circulate_qi");
        return realm.GetMajorTransition();
    }

    /// <summary>自然筑基只要求命名真气达到九层最低线。</summary>
    private static ProgressionGateResult RequireQiFoundationReady(
        ActorExtend actor,
        CultisysAsset<Xian> cultisys,
        ref Xian component)
    {
        QiRefinementState state = actor.GetComponent<QiRefinementState>();
        return state.CompletedLayers >= MinimumFoundationQiLayers && state.formation.IsFinalized
            ? ProgressionGateResult.Satisfied
            : ProgressionGateResult.NotReady("xian.qi_foundation_not_ready");
    }

    /// <summary>同步到炼气境界时只保留真气成果并移除未来境界归档。</summary>
    private static void NormalizeQiRefinementRealm(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                     ref Xian component, object payload)
    {
        if (actor.HasComponent<XianBase>()) actor.E.RemoveComponent<XianBase>();
        if (actor.HasComponent<Jindan>()) actor.E.RemoveComponent<Jindan>();
        if (actor.HasComponent<Yuanying>()) actor.E.RemoveComponent<Yuanying>();
        actor.MarkSemanticProfileDirty();
    }

    /// <summary>深拷贝命名真气成果。</summary>
    private static void TransferQiRefinementState(ActorExtend source, ActorExtend target)
    {
        ref QiRefinementState targetState = ref target.GetOrAddComponent<QiRefinementState>();
        targetState = source.GetComponent<QiRefinementState>().DeepClone();
    }

    /// <summary>按角色当前灵根、主修功法和修炼方式解析本次真气质量与元素组成。</summary>
    private static QiRefinementSample ResolveQiRefinementSample(ActorExtend actor)
    {
        CultibookAsset cultibook = actor.GetMainCultibook();
        CultivateMethodAsset method = cultibook?.GetCultivateMethod() ?? CultivateMethods.Standard;
        CultivationEfficiencyResult efficiency = CultivationEfficiencyResolver.Resolve(actor, cultibook, method);
        float methodQuality = NormalizeQiRefinementMethodQuality(efficiency.MethodMultiplier);
        float quality = efficiency.Intensity * 0.30f +
                        efficiency.Purity * 0.25f +
                        efficiency.MainCultibookAffinity * 0.30f +
                        methodQuality * 0.15f;
        return new QiRefinementSample(
            quality,
            ResolveQiRefinementComposition(actor, cultibook, method),
            ResolveQiRefinementElementSemantics(actor, cultibook, method));
    }

    /// <summary>把修炼方式倍率按二倍增益映射压缩到 0..1。</summary>
    private static float NormalizeQiRefinementMethodQuality(float multiplier)
    {
        return Mathf.Clamp01(0.5f + 0.25f * Mathf.Log(Mathf.Max(0.25f, multiplier), 2f));
    }

    /// <summary>按灵根 50%、主修功法 30%、修炼方式 20% 合成元素组成。</summary>
    private static ElementComposition ResolveQiRefinementComposition(
        ActorExtend actor,
        CultibookAsset cultibook,
        CultivateMethodAsset method)
    {
        const float rootWeight = 0.5f;
        const float cultibookWeight = 0.3f;
        const float methodWeight = 0.2f;
        ElementComposition result = default;
        var totalWeight = 0f;
        if (actor.HasElementRoot())
        {
            AddQiRefinementComposition(ref result,
                ElementSemanticProfileService.ToComposition(actor.GetElementRoot()), rootWeight);
            totalWeight += rootWeight;
        }

        if (TryResolveQiRefinementCultibookComposition(cultibook, out ElementComposition cultibookComposition))
        {
            AddQiRefinementComposition(ref result, cultibookComposition, cultibookWeight);
            totalWeight += cultibookWeight;
        }

        if (ElementSemanticProfileService.TryResolveComposition(method?.Semantics,
                out ElementComposition methodComposition))
        {
            AddQiRefinementComposition(ref result, methodComposition, methodWeight);
            totalWeight += methodWeight;
        }

        if (totalWeight <= 0.0001f) return ElementComposition.Static.empty;
        for (var i = 0; i < ElementIndex.Count; i++) result[i] /= totalWeight;
        result.Normalize();
        return result;
    }

    /// <summary>优先从功法元素需求读取组成，未声明需求时再解析功法语义。</summary>
    private static bool TryResolveQiRefinementCultibookComposition(
        CultibookAsset cultibook,
        out ElementComposition composition)
    {
        composition = default;
        if (cultibook == null) return false;
        ElementRequirement requirement = cultibook.ElementReq;
        composition = new ElementComposition(
            requirement.MinIron,
            requirement.MinWood,
            requirement.MinWater,
            requirement.MinFire,
            requirement.MinEarth,
            requirement.MinNeg,
            requirement.MinPos,
            requirement.MinEntropy);
        var total = 0f;
        for (var i = 0; i < ElementIndex.Count; i++) total += Mathf.Max(0f, composition[i]);
        if (total > 0.0001f)
        {
            composition.Normalize();
            return true;
        }
        return ElementSemanticProfileService.TryResolveComposition(cultibook.Semantics, out composition);
    }

    /// <summary>把归一化组成按给定权重累加到目标。</summary>
    private static void AddQiRefinementComposition(
        ref ElementComposition target,
        ElementComposition source,
        float weight)
    {
        source.Normalize();
        for (var i = 0; i < ElementIndex.Count; i++) target[i] += source[i] * weight;
    }

    /// <summary>按灵根、主修功法和修炼方式的同一组权重构造本层元素语义证据。</summary>
    private static SemanticDescriptor ResolveQiRefinementElementSemantics(
        ActorExtend actor,
        CultibookAsset cultibook,
        CultivateMethodAsset method)
    {
        var builder = new SemanticDescriptorBuilder();
        ref ElementRoot root = ref actor.GetElementRoot();
        AddQiRefinementElementEvidence(
            builder,
            ElementSemanticProfileService.ToComposition(root),
            root.Type.Semantics,
            0.5f);

        if (cultibook != null)
        {
            TryResolveQiRefinementCultibookComposition(cultibook, out ElementComposition composition);
            AddQiRefinementElementEvidence(builder, composition, cultibook.Semantics, 0.3f);
        }

        if (method != null)
        {
            ElementSemanticProfileService.TryResolveComposition(method.Semantics, out ElementComposition composition);
            AddQiRefinementElementEvidence(builder, composition, method.Semantics, 0.2f);
        }
        return builder.Build();
    }

    /// <summary>把一个来源内部的元素语义归一化后按来源权重加入凝练样本。</summary>
    private static void AddQiRefinementElementEvidence(
        SemanticDescriptorBuilder target,
        ElementComposition composition,
        SemanticDescriptor descriptor,
        float sourceWeight)
    {
        SemanticProfile profile = ElementSemanticProfileService.Build(composition, descriptor);
        var ranked = profile.GetDirectRanked(
            SemanticQueryPolicy.Default,
            ModClass.L.SemanticFacetLibrary.Element);
        float total = 0f;
        for (var i = 0; i < ranked.Count; i++) total += Mathf.Max(0f, ranked[i].score.Net);
        if (total <= 0f) throw new InvalidOperationException("凝练来源缺少可解析的元素语义。");
        for (var i = 0; i < ranked.Count; i++)
        {
            float score = Mathf.Max(0f, ranked[i].score.Net);
            if (score > 0f) target.Add(ranked[i].semantic, sourceWeight * score / total);
        }
    }

    /// <summary>一次真气凝练使用的冻结质量与元素组成。</summary>
    private readonly struct QiRefinementSample
    {
        /// <summary>创建一份已经归一化的真气凝练样本。</summary>
        public QiRefinementSample(
            float quality,
            ElementComposition composition,
            SemanticDescriptor elementSemantics)
        {
            Quality = Mathf.Clamp01(quality);
            Composition = composition;
            ElementSemantics = elementSemantics;
        }

        /// <summary>灵根强度、纯度、功法契合与修炼方式共同决定的 0..1 质量。</summary>
        public float Quality { get; }

        /// <summary>灵根五成、主修功法三成、修炼方式两成形成的八元素组成。</summary>
        public ElementComposition Composition { get; }

        /// <summary>与元素组成采用相同来源权重、同时保留风雷冰毒等具名元素的语义证据。</summary>
        public SemanticDescriptor ElementSemantics { get; }
    }
}
