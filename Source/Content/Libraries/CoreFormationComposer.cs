using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Semantics;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils;
using strings;
using UnityEngine;

namespace Cultiway.Content.Libraries;

/// <summary>原子评分时读取的一次性形成上下文。</summary>
internal sealed class CoreFormationContext
{
    private static readonly SemanticQueryPolicy LearnedPolicy = new(SemanticScope.Learned);
    private static readonly SemanticQueryPolicy IntrinsicPolicy = new(SemanticScope.Intrinsic);
    private static readonly SemanticQueryPolicy HistoricalPolicy = new(SemanticScope.Historical);

    private readonly SemanticProfile semanticProfile;
    private readonly CultibookAsset mainCultibook;
    private readonly float mainMastery;

    public ActorExtend Actor { get; }
    public CoreFormationRealm Realm { get; }
    public ElementComposition Composition { get; }
    public float JingRatio { get; }
    public float QiRatio { get; }
    public float ShenRatio { get; }
    public float ThreeHuaBalance { get; }
    public float ElementBalance { get; }
    public float FivePhaseBalance { get; }
    public bool IsDragonSource { get; }

    /// <summary>提取角色根基、元素比例、主修功法和语义资料，构造一次原子评分上下文。</summary>
    public CoreFormationContext(ActorExtend actor, XianBase foundation, ElementComposition composition,
                                CoreFormationRealm realm)
    {
        Actor = actor;
        Realm = realm;
        Composition = composition;

        var threeTotal = Mathf.Max(0f, foundation.jing) + Mathf.Max(0f, foundation.qi) +
                         Mathf.Max(0f, foundation.shen);
        if (threeTotal <= 0f)
        {
            JingRatio = QiRatio = ShenRatio = 1f / 3f;
            ThreeHuaBalance = 1f;
        }
        else
        {
            JingRatio = Mathf.Max(0f, foundation.jing) / threeTotal;
            QiRatio = Mathf.Max(0f, foundation.qi) / threeTotal;
            ShenRatio = Mathf.Max(0f, foundation.shen) / threeTotal;
            var max = Mathf.Max(foundation.jing, Mathf.Max(foundation.qi, foundation.shen));
            var min = Mathf.Min(foundation.jing, Mathf.Min(foundation.qi, foundation.shen));
            ThreeHuaBalance = max <= 0f ? 1f : Mathf.Clamp01(min / max);
        }

        var values = composition.AsArray();
        var maxElement = values.Length == 0 ? 1f : values.Max();
        var activeElements = values.Count(value => value > 0.0001f);
        ElementBalance = activeElements <= 1
            ? 0f
            : Mathf.Clamp01((1f - maxElement) / (1f - 1f / activeElements));
        var fivePhaseMax = values.Take(5).Max();
        var fivePhaseMin = values.Take(5).Min();
        FivePhaseBalance = fivePhaseMax <= 0f ? 0f : Mathf.Clamp01(fivePhaseMin / fivePhaseMax);

        mainCultibook = actor?.GetMainCultibook();
        mainMastery = actor == null ? 0f : Mathf.Clamp01(actor.GetMainCultibookMastery() / 100f);
        semanticProfile = actor?.GetSemanticProfile();

        var actorId = actor?.Base?.asset?.id ?? string.Empty;
        IsDragonSource = actorId.IndexOf("dragon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         RawSemanticScore(CultivationSemantics.Theme.Dragon, IntrinsicPolicy) > 0.15f;
    }

    /// <summary>按主修、其他已学内容、固有来源和炼气历史共同解析语义分数。</summary>
    public float SemanticScore(SemanticAsset semantic)
    {
        if (semantic == null) return 0f;
        var learned = RawSemanticScore(semantic, LearnedPolicy) * 0.25f;
        var intrinsic = RawSemanticScore(semantic, IntrinsicPolicy) * 0.15f;
        var historical = RawSemanticScore(semantic, HistoricalPolicy) * 0.15f;
        var main = mainCultibook?.Semantics?.ContainsExpanded(ModClass.L.SemanticLibrary, semantic) == true
            ? mainMastery * 0.35f
            : 0f;
        return Mathf.Max(0f, learned + intrinsic + historical + main);
    }

    /// <summary>按指定查询策略读取语义画像原始净分；没有画像时返回零。</summary>
    private float RawSemanticScore(SemanticAsset semantic, SemanticQueryPolicy policy)
    {
        return semanticProfile?.GetScore(semantic, policy).Net ?? 0f;
    }
}

/// <summary>负责真气、仙基、金丹与元婴形成、继承、觉醒和派生值重建的确定性组合服务。</summary>
public static class CoreFormationComposer
{
    private const float QiFoundationCompositionWeight = 0.6f;
    private const float FiveQiFoundationCompositionWeight = 0.4f;
    private const float SecondaryAtomReplacementRatio = 1.25f;
    private const float FoundationStepStrengthScale = 0.25f;
    private const int MaxLatentAtoms = 2;
    private const float RepresentativeSkillSemanticWeight = 0.3f;

    private static readonly int[] AwakeningStages = [3, 6];
    private static readonly string[] ArmorStats =
    [
        nameof(WorldboxGame.BaseStats.IronArmor),
        nameof(WorldboxGame.BaseStats.WoodArmor),
        nameof(WorldboxGame.BaseStats.WaterArmor),
        nameof(WorldboxGame.BaseStats.FireArmor),
        nameof(WorldboxGame.BaseStats.EarthArmor),
        nameof(WorldboxGame.BaseStats.NegArmor),
        nameof(WorldboxGame.BaseStats.PosArmor),
        nameof(WorldboxGame.BaseStats.EntropyArmor)
    ];
    private static readonly string[] MasterStats =
    [
        nameof(WorldboxGame.BaseStats.IronMaster),
        nameof(WorldboxGame.BaseStats.WoodMaster),
        nameof(WorldboxGame.BaseStats.WaterMaster),
        nameof(WorldboxGame.BaseStats.FireMaster),
        nameof(WorldboxGame.BaseStats.EarthMaster),
        nameof(WorldboxGame.BaseStats.NegMaster),
        nameof(WorldboxGame.BaseStats.PosMaster),
        nameof(WorldboxGame.BaseStats.EntropyMaster)
    ];
    private static readonly SemanticAsset[] ElementSemantics =
    [
        SkillSemantics.Element.Iron,
        SkillSemantics.Element.Wood,
        SkillSemantics.Element.Water,
        SkillSemantics.Element.Fire,
        SkillSemantics.Element.Earth,
        SkillSemantics.Element.Neg,
        SkillSemantics.Element.Pos,
        SkillSemantics.Element.Entropy
    ];

    /// <summary>凝练一层真气；前九层塑造并定型成果，九层后只增加层数与强度。</summary>
    public static void RefineQi(
        ActorExtend actor,
        ref QiRefinementState state,
        float quality,
        ElementComposition sampleComposition,
        SemanticDescriptor sampleElementSemantics)
    {
        quality = Mathf.Clamp01(quality);
        sampleComposition.Normalize();
        ref CoreFormationSnapshot snapshot = ref state.formation;
        int newLayer = snapshot.IsValid ? Mathf.Max(1, snapshot.refinement + 1) : 1;
        if (snapshot.IsFinalized)
        {
            snapshot.strength += quality / Mathf.Sqrt(newLayer);
            snapshot.refinement = newLayer;
            return;
        }

        float coherence = snapshot.IsValid
            ? Mathf.Clamp01(MathUtils.CosineSimilarity(
                snapshot.composition.AsArray(), sampleComposition.AsArray(), ElementIndex.Count))
            : 1f;
        state.quality_sum += quality;
        state.composition_coherence_sum += coherence;
        state.quality_sample_count++;
        if (state.quality_sample_count > Cultisyses.MinimumFoundationQiLayers)
            throw new InvalidOperationException("真气定型前累计了超过九个品质样本。");

        if (!snapshot.IsValid)
        {
            var context = new CoreFormationContext(
                actor, default, sampleComposition, CoreFormationRealm.QiRefinement);
            snapshot = new CoreFormationSnapshot
            {
                version = CoreFormationSnapshot.CurrentVersion,
                realm = CoreFormationRealm.QiRefinement,
                finalized = false,
                strength = Mathf.Max(0.01f, quality),
                refinement = 1,
                composition = sampleComposition,
                element_semantics = MergeElementSemantics(null, sampleElementSemantics),
                atoms = [],
                stats = [],
                semantics = []
            };
            SetSelectedAtom(ref snapshot, context, CoreFormationAtomCategory.Element, requireMinimum: true);
        }
        else
        {
            snapshot.composition = BlendComposition(snapshot.composition, sampleComposition, 1f / newLayer);
            snapshot.strength += quality / Mathf.Sqrt(newLayer);
            snapshot.refinement = newLayer;
            snapshot.element_semantics = MergeElementSemantics(
                snapshot.element_semantics, sampleElementSemantics);
            var nextContext = new CoreFormationContext(
                actor, default, snapshot.composition, CoreFormationRealm.QiRefinement);
            SetSelectedAtom(ref snapshot, nextContext, CoreFormationAtomCategory.Element, requireMinimum: true);
        }

        if (snapshot.refinement == Cultisyses.MinimumFoundationQiLayers)
            FinalizeQi(ref state);
        else
            RebuildDerived(ref snapshot, snapshot.refinement);
    }

    /// <summary>继承真气的核心身份与特殊效果，并按预期三花五气建立仙基胚胎。</summary>
    public static CoreFormationSnapshot ComposeFoundation(
        ActorExtend actor,
        XianBase foundationSeed,
        CoreFormationSnapshot qi)
    {
        if (!qi.IsFinalized) throw new ArgumentException("筑基需要已经定型的九层真气成果。", nameof(qi));
        var context = new CoreFormationContext(
            actor, foundationSeed, qi.composition, CoreFormationRealm.Foundation);
        List<CoreFormationAtomState> atoms = CopyActiveAtoms(qi, qi.refinement);
        AddSelected(atoms,
            SelectBest(context, CoreFormationAtomCategory.Structure),
            0,
            false);
        var snapshot = new CoreFormationSnapshot
        {
            version = CoreFormationSnapshot.CurrentVersion,
            realm = CoreFormationRealm.Foundation,
            lineage_stem = qi.lineage_stem,
            finalized = false,
            source_signature = qi.signature,
            source_name = qi.canonical_name,
            source_refinement = qi.refinement,
            source_quality_score = qi.quality_score,
            strength = qi.strength,
            refinement = 0,
            composition = qi.composition,
            element_semantics = qi.element_semantics == null
                ? []
                : (SemanticContribution[])qi.element_semantics.Clone(),
            atoms = atoms.ToArray(),
            stats = [],
            semantics = []
        };
        RebuildDerived(ref snapshot, 0);
        return snapshot;
    }

    /// <summary>把一个三花五气步骤熬入仙基，并在新结构优势超过 25% 时演化结构原子。</summary>
    public static void RefineFoundation(
        ActorExtend actor,
        ref XianBase foundation,
        float stepStrength,
        float stepQuality)
    {
        ref CoreFormationSnapshot snapshot = ref foundation.formation;
        if (!snapshot.IsValid)
            throw new InvalidOperationException("筑基步骤需要已经形成的仙基胚胎。");
        if (snapshot.IsFinalized)
            throw new InvalidOperationException("已经定型的仙基不能再次熬炼。");

        foundation.refinement_quality_sum += Mathf.Clamp01(stepQuality);
        foundation.refinement_quality_sample_count++;
        if (foundation.refinement_quality_sample_count > 8)
            throw new InvalidOperationException("仙基定型前累计了超过八个熬炼品质样本。");

        snapshot.refinement = Mathf.Max(snapshot.refinement + 1, CountFoundationParts(foundation));
        snapshot.strength += Mathf.Max(0.01f, stepStrength) * FoundationStepStrengthScale /
                             Mathf.Sqrt(snapshot.refinement);
        snapshot.composition = BuildFoundationComposition(actor, foundation);
        var context = new CoreFormationContext(
            actor, foundation, snapshot.composition, CoreFormationRealm.Foundation);
        TryEvolveSecondaryAtom(ref snapshot, context, CoreFormationAtomCategory.Structure);
        if (snapshot.refinement == 8)
            FinalizeFoundation(ref foundation, context);
        else
            RebuildDerived(ref snapshot, snapshot.refinement);
    }

    /// <summary>继承完整仙基，再由功法与已学内容补入金丹道路和主题原子。</summary>
    public static CoreFormationSnapshot ComposeJindan(ActorExtend actor, XianBase foundation, float strength)
    {
        if (!foundation.formation.IsFinalized)
            throw new ArgumentException("结丹需要已经定型的仙基成果快照。", nameof(foundation));
        var composition = foundation.formation.composition;
        var context = new CoreFormationContext(actor, foundation, composition, CoreFormationRealm.Jindan);
        CoreFormationQualityEvaluator.Evaluation quality = CoreFormationQualityEvaluator.ResolveJindan(
            strength,
            context.ThreeHuaBalance,
            context.ElementBalance);
        List<CoreFormationAtomState> atoms = CopyActiveAtoms(
            foundation.formation, foundation.formation.refinement);

        var latentIndex = 0;
        AddOptional(atoms, context, CoreFormationAtomCategory.Path, ref latentIndex);
        AddOptional(atoms, context, CoreFormationAtomCategory.Theme, ref latentIndex);

        var snapshot = new CoreFormationSnapshot
        {
            version = CoreFormationSnapshot.CurrentVersion,
            realm = CoreFormationRealm.Jindan,
            lineage_stem = foundation.formation.lineage_stem,
            finalized = true,
            source_signature = foundation.formation.signature,
            source_name = foundation.formation.canonical_name,
            source_refinement = foundation.formation.refinement,
            source_quality_score = foundation.formation.quality_score,
            quality = quality.Level,
            quality_score = quality.Score,
            strength = strength,
            refinement = 0,
            composition = composition,
            element_semantics = foundation.formation.element_semantics == null
                ? []
                : (SemanticContribution[])foundation.formation.element_semantics.Clone(),
            atoms = atoms.ToArray(),
            stats = [],
            semantics = []
        };
        RebuildDerived(ref snapshot, 0);
        return snapshot;
    }

    /// <summary>继承已显化的金丹原子，并加入结婴时的显化与蜕变原子。</summary>
    public static CoreFormationSnapshot ComposeYuanying(ActorExtend actor, XianBase foundation,
                                                         CoreFormationSnapshot jindan, int jindanStage,
                                                         float strength)
    {
        if (!jindan.IsFinalized) throw new ArgumentException("结婴需要已经定型的金丹组合快照。", nameof(jindan));
        var context = new CoreFormationContext(actor, foundation, jindan.composition, CoreFormationRealm.Yuanying);
        CoreFormationQualityEvaluator.Evaluation quality = CoreFormationQualityEvaluator.ResolveYuanying(
            strength,
            context.ThreeHuaBalance,
            context.ElementBalance);
        List<CoreFormationAtomState> atoms = new(7);
        foreach (var atom in jindan.atoms ?? [])
        {
            if (!atom.IsActive(jindanStage)) continue;
            var inherited = atom;
            inherited.awakening_stage = 0;
            inherited.inherited = true;
            atoms.Add(inherited);
        }

        AddSelected(atoms, SelectBest(context, CoreFormationAtomCategory.Manifestation, requireMinimum: true),
            0, false);
        var transformation = SelectBest(context, CoreFormationAtomCategory.Transformation);
        if (transformation.asset != null && transformation.score >= transformation.asset.minimum_score)
            AddSelected(atoms, transformation, 0, false);

        var snapshot = new CoreFormationSnapshot
        {
            version = CoreFormationSnapshot.CurrentVersion,
            realm = CoreFormationRealm.Yuanying,
            lineage_stem = jindan.lineage_stem,
            finalized = true,
            source_signature = jindan.signature,
            source_name = jindan.canonical_name,
            source_refinement = jindanStage,
            source_quality_score = jindan.quality_score,
            quality = quality.Level,
            quality_score = quality.Score,
            strength = strength,
            refinement = 0,
            composition = jindan.composition,
            element_semantics = jindan.element_semantics == null
                ? []
                : (SemanticContribution[])jindan.element_semantics.Clone(),
            atoms = atoms.ToArray(),
            stats = [],
            semantics = []
        };
        RebuildDerived(ref snapshot, 0);
        return snapshot;
    }

    /// <summary>处理跨越的三、六、九转节点并重建名称、属性、语义与法术亲和。</summary>
    public static bool EvolveJindan(ref CoreFormationSnapshot snapshot, int previousStage, int currentStage)
    {
        if (!snapshot.IsFinalized || snapshot.realm != CoreFormationRealm.Jindan) return false;
        snapshot.refinement = Mathf.Max(snapshot.refinement, currentStage);
        var changed = false;
        for (var i = 0; i < AwakeningStages.Length; i++)
        {
            var stage = AwakeningStages[i];
            if (previousStage >= stage || currentStage < stage) continue;
            changed = true;
            if ((snapshot.atoms ?? []).Any(atom => atom.awakening_stage == stage)) continue;
            StrengthenPrimaryAtom(ref snapshot);
        }

        if (previousStage < 9 && currentStage >= 9)
        {
            StrengthenAllActiveAtoms(ref snapshot, currentStage);
            changed = true;
        }

        if (changed) RebuildDerived(ref snapshot, currentStage);
        return changed;
    }

    /// <summary>处理元婴跨越的三、六、九层节点，并保留其原始金丹谱系。</summary>
    public static bool EvolveYuanying(ref CoreFormationSnapshot snapshot, int previousStage, int currentStage)
    {
        if (!snapshot.IsFinalized || snapshot.realm != CoreFormationRealm.Yuanying) return false;
        snapshot.refinement = Mathf.Max(snapshot.refinement, currentStage);
        var changed = false;
        for (var i = 0; i < AwakeningStages.Length; i++)
        {
            var stage = AwakeningStages[i];
            if (previousStage >= stage || currentStage < stage) continue;
            changed = true;
            if ((snapshot.atoms ?? []).Any(atom => atom.awakening_stage == stage)) continue;
            StrengthenPrimaryAtom(ref snapshot);
        }

        if (previousStage < 9 && currentStage >= 9)
        {
            StrengthenAllActiveAtoms(ref snapshot, currentStage);
            changed = true;
        }

        if (changed) RebuildDerived(ref snapshot, currentStage);
        return changed;
    }

    /// <summary>组合当前阶段已显化原子的说明文本。</summary>
    public static string GetDescription(CoreFormationSnapshot snapshot, int stage)
    {
        var fragments = GetActiveAtoms(snapshot, stage)
            .Select(atom => atom.GetDescription())
            .Where(text => !string.IsNullOrEmpty(text))
            .Distinct()
            .ToArray();
        return fragments.Length == 0 ? string.Empty : string.Join("；", fragments);
    }

    /// <summary>按快照顺序返回当前阶段已显化的原子名称。</summary>
    public static string[] GetActiveAtomNames(CoreFormationSnapshot snapshot, int stage)
    {
        return GetActiveAtoms(snapshot, stage).Select(atom => atom.GetName()).ToArray();
    }

    /// <summary>返回下一次三、六、九转演化节点；完成九转后返回 -1。</summary>
    public static int GetNextEvolutionStage(int stage)
    {
        if (stage < 3) return 3;
        if (stage < 6) return 6;
        return stage < 9 ? 9 : -1;
    }

    /// <summary>按真气 60% 与已完成五气 40% 合成仙基元素组成。</summary>
    private static ElementComposition BuildFoundationComposition(ActorExtend actor, XianBase foundation)
    {
        var fiveQiRaw = new[]
        {
            foundation.iron, foundation.wood, foundation.water, foundation.fire, foundation.earth, 0f, 0f, 0f
        };
        float fiveQiTotal = fiveQiRaw.Sum(value => Mathf.Max(0f, value));
        CoreFormationSnapshot qiFormation = actor.GetComponent<QiRefinementState>().formation;
        if (fiveQiTotal <= 0f) return qiFormation.composition;
        float[] fiveQi = Normalize(fiveQiRaw, fiveElementFallback: false);
        ElementComposition qi = qiFormation.composition;
        qi.Normalize();
        var result = new float[8];
        for (var i = 0; i < result.Length; i++)
            result[i] = qi[i] * QiFoundationCompositionWeight +
                        fiveQi[i] * FiveQiFoundationCompositionWeight;
        return new ElementComposition(result, normalize: true);
    }

    /// <summary>把非负元素数组归一化；总量为零时按指定范围生成均匀后备分布。</summary>
    private static float[] Normalize(float[] values, bool fiveElementFallback)
    {
        var sum = values.Sum(value => Mathf.Max(0f, value));
        if (sum > 0f)
        {
            for (var i = 0; i < values.Length; i++) values[i] = Mathf.Max(0f, values[i]) / sum;
            return values;
        }

        var count = fiveElementFallback ? 5 : values.Length;
        for (var i = 0; i < count; i++) values[i] = 1f / count;
        return values;
    }

    /// <summary>按给定的新样本权重混合两份组成并重新归一化。</summary>
    private static ElementComposition BlendComposition(
        ElementComposition current,
        ElementComposition sample,
        float sampleWeight)
    {
        current.Normalize();
        sample.Normalize();
        sampleWeight = Mathf.Clamp01(sampleWeight);
        var result = new float[ElementIndex.Count];
        for (var i = 0; i < result.Length; i++)
            result[i] = current[i] * (1f - sampleWeight) + sample[i] * sampleWeight;
        return new ElementComposition(result, normalize: true);
    }

    /// <summary>在第九层固化真气的元素语义、连续品质、品阶、签名和正式名称。</summary>
    private static void FinalizeQi(ref QiRefinementState state)
    {
        ref CoreFormationSnapshot snapshot = ref state.formation;
        if (snapshot.refinement != Cultisyses.MinimumFoundationQiLayers ||
            state.quality_sample_count != Cultisyses.MinimumFoundationQiLayers)
            throw new InvalidOperationException("真气只能在九层且拥有九个品质样本时定型。");

        snapshot.element_semantics = NormalizeElementSemantics(snapshot.element_semantics);
        snapshot.quality_score = CoreFormationQualityEvaluator.ResolveQi(
            state.quality_sum,
            state.composition_coherence_sum,
            state.quality_sample_count);
        snapshot.quality = CoreFormationQualityEvaluator.ResolveItemLevel(snapshot.quality_score);
        snapshot.finalized = true;
        snapshot.lineage_stem = string.Empty;
        RebuildDerived(ref snapshot, snapshot.refinement);
    }

    /// <summary>在三花五气八项完成后固化仙基品质、品阶、签名和正式名称。</summary>
    private static void FinalizeFoundation(ref XianBase foundation, CoreFormationContext context)
    {
        ref CoreFormationSnapshot snapshot = ref foundation.formation;
        if (snapshot.refinement != 8 || foundation.refinement_quality_sample_count != 8)
            throw new InvalidOperationException("仙基只能在八项熬炼全部完成后定型。");

        CoreFormationAtomAsset structure = ResolveActiveStates(snapshot, snapshot.refinement)
            .Where(item => item.asset.category == CoreFormationAtomCategory.Structure)
            .OrderByDescending(item => item.state.weight)
            .ThenBy(item => item.asset.id, StringComparer.Ordinal)
            .Select(item => item.asset)
            .FirstOrDefault();
        if (structure == null) throw new InvalidOperationException("仙基定型时缺少结构原子。");

        float structureQuality = structure.EvaluateQualityFor(context);
        snapshot.quality_score = CoreFormationQualityEvaluator.ResolveFoundation(
            snapshot.source_quality_score,
            foundation.refinement_quality_sum,
            foundation.refinement_quality_sample_count,
            structureQuality);
        snapshot.quality = CoreFormationQualityEvaluator.ResolveItemLevel(snapshot.quality_score);
        snapshot.finalized = true;
        RebuildDerived(ref snapshot, snapshot.refinement);
    }

    /// <summary>把本次凝练的元素语义证据累加到真气草稿中并保持稳定排序。</summary>
    private static SemanticContribution[] MergeElementSemantics(
        SemanticContribution[] current,
        SemanticDescriptor sample)
    {
        var builder = new SemanticDescriptorBuilder();
        if (current is { Length: > 0 }) builder.Add(SemanticDescriptor.Weighted(current));
        if (sample != null) builder.Add(sample);
        return builder.Build().contributions;
    }

    /// <summary>把累计元素语义证据归一化为总正向强度一，供命名和后续谱系继承。</summary>
    private static SemanticContribution[] NormalizeElementSemantics(SemanticContribution[] values)
    {
        SemanticContribution[] positive = (values ?? [])
            .Where(value => value.polarity == SemanticPolarity.Positive && value.strength > 0f)
            .OrderBy(value => value.semantic_id, StringComparer.Ordinal)
            .ToArray();
        float total = positive.Sum(value => value.strength);
        if (total <= 0f) throw new InvalidOperationException("真气定型时缺少元素语义证据。");
        for (var i = 0; i < positive.Length; i++) positive[i].strength /= total;
        return positive;
    }

    /// <summary>在指定互斥分类中写入当前最佳原子，并清理同分类的旧选择。</summary>
    private static void SetSelectedAtom(
        ref CoreFormationSnapshot snapshot,
        CoreFormationContext context,
        CoreFormationAtomCategory category,
        bool requireMinimum)
    {
        (CoreFormationAtomAsset asset, float score) selected = SelectBest(context, category, requireMinimum);
        if (selected.asset == null)
            throw new InvalidOperationException($"核心形成缺少可用原子: realm={context.Realm}, category={category}");

        List<CoreFormationAtomState> atoms = (snapshot.atoms ?? []).ToList();
        int replaceIndex = atoms.FindIndex(state =>
            Manager.CoreFormationAtomLibrary.get(state.atom_id)?.category == category);
        for (int i = atoms.Count - 1; i >= 0; i--)
        {
            CoreFormationAtomAsset asset = Manager.CoreFormationAtomLibrary.get(atoms[i].atom_id);
            if (asset?.category != category) continue;
            atoms.RemoveAt(i);
        }

        var state = new CoreFormationAtomState
        {
            atom_id = selected.asset.id,
            weight = selected.score,
            awakening_stage = 0,
            inherited = false
        };
        if (replaceIndex < 0)
            atoms.Add(state);
        else
            atoms.Insert(Mathf.Min(replaceIndex, atoms.Count), state);
        snapshot.atoms = atoms.ToArray();
    }

    /// <summary>复制来源成果当前已显化的原子，并统一标记为继承。</summary>
    private static List<CoreFormationAtomState> CopyActiveAtoms(
        CoreFormationSnapshot source,
        int stage)
    {
        List<CoreFormationAtomState> result = new((source.atoms ?? []).Length + 2);
        foreach (CoreFormationAtomState atom in source.atoms ?? [])
        {
            if (!atom.IsActive(stage)) continue;
            CoreFormationAtomState inherited = atom;
            inherited.awakening_stage = 0;
            inherited.inherited = true;
            result.Add(inherited);
        }
        return result;
    }

    /// <summary>统计仙基已经完成的三花五气步骤。</summary>
    private static int CountFoundationParts(XianBase foundation)
    {
        var count = 0;
        if (foundation.jing != 0f) count++;
        if (foundation.qi != 0f) count++;
        if (foundation.shen != 0f) count++;
        if (foundation.iron != 0f) count++;
        if (foundation.wood != 0f) count++;
        if (foundation.water != 0f) count++;
        if (foundation.fire != 0f) count++;
        if (foundation.earth != 0f) count++;
        return count;
    }

    /// <summary>将达标的可选原子立即显化，或将部分未达标原子安排到三、六转觉醒。</summary>
    private static void AddOptional(List<CoreFormationAtomState> atoms, CoreFormationContext context,
                                    CoreFormationAtomCategory category, ref int latentIndex)
    {
        var selected = SelectBest(context, category);
        if (selected.asset == null || selected.score <= 0f) return;
        if (selected.score >= selected.asset.minimum_score)
        {
            AddSelected(atoms, selected, 0, false);
            return;
        }

        if (latentIndex >= MaxLatentAtoms || selected.score < selected.asset.minimum_score * 0.35f) return;
        AddSelected(atoms, selected, AwakeningStages[latentIndex], false);
        latentIndex++;
    }

    /// <summary>在境界和分类约束内按分数、优先级及 ID 稳定选出最佳原子。</summary>
    private static (CoreFormationAtomAsset asset, float score) SelectBest(CoreFormationContext context,
                                                                          CoreFormationAtomCategory category,
                                                                          bool requireMinimum = false)
    {
        var realmMask = context.Realm switch
        {
            CoreFormationRealm.QiRefinement => CoreFormationRealmMask.QiRefinement,
            CoreFormationRealm.Foundation => CoreFormationRealmMask.Foundation,
            CoreFormationRealm.Jindan => CoreFormationRealmMask.Jindan,
            _ => CoreFormationRealmMask.Yuanying
        };
        return Manager.CoreFormationAtomLibrary.All
            .Where(atom => atom.category == category && (atom.realms & realmMask) != 0)
            .Select(atom => (asset: atom, score: atom.ScoreFor(context)))
            .Where(item => item.score > 0f && (!requireMinimum || item.score >= item.asset.minimum_score))
            .OrderByDescending(item => item.score)
            .ThenByDescending(item => item.asset.priority)
            .ThenBy(item => item.asset.id, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>把评分结果固化为原子状态，并防止同一资产重复加入。</summary>
    private static void AddSelected(List<CoreFormationAtomState> atoms,
                                    (CoreFormationAtomAsset asset, float score) selected,
                                    int awakeningStage, bool inherited)
    {
        if (selected.asset == null || atoms.Any(atom => atom.atom_id == selected.asset.id)) return;
        atoms.Add(new CoreFormationAtomState
        {
            atom_id = selected.asset.id,
            weight = Mathf.Max(0.01f, selected.score),
            awakening_stage = awakeningStage,
            inherited = inherited
        });
    }

    /// <summary>仅当另一结构评分超过现有结构 25% 时替换，避免谱系在相近候选间抖动。</summary>
    private static void TryEvolveSecondaryAtom(
        ref CoreFormationSnapshot snapshot,
        CoreFormationContext context,
        CoreFormationAtomCategory category)
    {
        var selected = SelectBest(context, category);
        if (selected.asset == null) return;
        CoreFormationAtomState[] atoms = snapshot.atoms ?? [];
        for (var i = 0; i < atoms.Length; i++)
        {
            CoreFormationAtomAsset current = Manager.CoreFormationAtomLibrary.get(atoms[i].atom_id);
            if (current == null || current.category != category) continue;
            if (current.id == selected.asset.id)
            {
                atoms[i].weight = Mathf.Max(atoms[i].weight, selected.score);
                snapshot.atoms = atoms;
                return;
            }
            if (selected.score <= atoms[i].weight * SecondaryAtomReplacementRatio) return;
            atoms[i] = new CoreFormationAtomState
            {
                atom_id = selected.asset.id,
                weight = selected.score,
                awakening_stage = 0,
                inherited = false
            };
            snapshot.atoms = atoms;
            return;
        }
        List<CoreFormationAtomState> expanded = atoms.ToList();
        AddSelected(expanded, selected, 0, false);
        snapshot.atoms = expanded.ToArray();
    }

    /// <summary>强化当前权重最高的已显化非元素原子，用于没有潜在原子的觉醒节点。</summary>
    private static void StrengthenPrimaryAtom(ref CoreFormationSnapshot snapshot)
    {
        var atoms = snapshot.atoms ?? [];
        var best = -1;
        for (var i = 0; i < atoms.Length; i++)
        {
            var asset = Manager.CoreFormationAtomLibrary.get(atoms[i].atom_id);
            if (asset == null || asset.category == CoreFormationAtomCategory.Element || atoms[i].awakening_stage > 0)
                continue;
            if (best < 0 || atoms[i].weight > atoms[best].weight) best = i;
        }
        if (best >= 0) atoms[best].weight *= 1.1f;
        snapshot.atoms = atoms;
    }

    /// <summary>在九转节点按统一倍率强化当前已经显化的全部原子。</summary>
    private static void StrengthenAllActiveAtoms(ref CoreFormationSnapshot snapshot, int stage)
    {
        var atoms = snapshot.atoms ?? [];
        for (var i = 0; i < atoms.Length; i++)
            if (atoms[i].IsActive(stage)) atoms[i].weight *= 1.1f;
        snapshot.atoms = atoms;
    }

    /// <summary>依据当前阶段重新生成属性、语义、签名、规范名称和代表法术。</summary>
    private static void RebuildDerived(ref CoreFormationSnapshot snapshot, int stage)
    {
        var active = ResolveActiveStates(snapshot, stage);
        snapshot.stats = ComposeStats(snapshot, active);
        snapshot.semantics = ComposeSemantics(snapshot, active);
        snapshot.signature = ComposeSignature(snapshot, stage);
        if (snapshot.finalized)
        {
            if (string.IsNullOrEmpty(snapshot.lineage_stem))
                snapshot.lineage_stem = CoreFormationNameComposer.ResolveLineageStem(
                    snapshot, active, NamingRuleUtils.StableHash(snapshot.signature));
            snapshot.canonical_name = CoreFormationNameComposer.Compose(snapshot, active);
        }
        else
        {
            snapshot.canonical_name = string.Empty;
        }
        snapshot.representative_skill_id = ResolveRepresentativeSkill(snapshot);
    }

    /// <summary>解析当前阶段已经显化且仍能找到资产定义的原子状态。</summary>
    private static List<(CoreFormationAtomState state, CoreFormationAtomAsset asset)> ResolveActiveStates(
        CoreFormationSnapshot snapshot, int stage)
    {
        List<(CoreFormationAtomState, CoreFormationAtomAsset)> result = new();
        foreach (var state in snapshot.atoms ?? [])
        {
            if (!state.IsActive(stage)) continue;
            var asset = Manager.CoreFormationAtomLibrary.get(state.atom_id);
            if (asset != null) result.Add((state, asset));
        }
        return result;
    }

    /// <summary>汇总境界基础值、元素抗性/精通与原子模板，生成稳定排序的属性数组。</summary>
    private static CoreFormationStatValue[] ComposeStats(CoreFormationSnapshot snapshot,
        List<(CoreFormationAtomState state, CoreFormationAtomAsset asset)> active)
    {
        float realmScale = snapshot.realm switch
        {
            CoreFormationRealm.QiRefinement => 0.35f,
            CoreFormationRealm.Foundation => 0.60f,
            CoreFormationRealm.Jindan => 1f,
            _ => 1.25f
        };
        Dictionary<string, float> stats = new(StringComparer.Ordinal)
        {
            [S.multiplier_health] = 0.2f * realmScale,
            [S.multiplier_damage] = 0.2f * realmScale
        };

        var composition = snapshot.composition;
        composition.Normalize();
        for (var i = 0; i < 8; i++)
        {
            AddStat(stats, ArmorStats[i], composition[i] * 5f * realmScale);
            AddStat(stats, MasterStats[i], composition[i] * 5f * realmScale);
        }

        var inheritedWeight = active.Where(item => item.state.inherited &&
                                                    item.asset.category != CoreFormationAtomCategory.Element)
            .Sum(item => item.state.weight);
        var newWeight = active.Where(item => !item.state.inherited &&
                                              item.asset.category != CoreFormationAtomCategory.Element)
            .Sum(item => item.state.weight);
        var totalWeight = inheritedWeight + newWeight;

        foreach (var item in active)
        {
            if (item.asset.category == CoreFormationAtomCategory.Element) continue;
            float normalized;
            if (snapshot.realm != CoreFormationRealm.QiRefinement &&
                inheritedWeight > 0f && newWeight > 0f)
                normalized = item.state.inherited
                    ? item.state.weight / inheritedWeight * 0.8f
                    : item.state.weight / newWeight * 0.2f;
            else
                normalized = totalWeight <= 0f ? 0f : item.state.weight / totalWeight;

            foreach (var stat in item.asset.stats ?? [])
                AddStat(stats, stat.stat_id, stat.value * normalized * realmScale);
        }

        return stats.Where(pair => !string.IsNullOrEmpty(pair.Key) && pair.Value != 0f)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new CoreFormationStatValue(pair.Key, pair.Value))
            .ToArray();
    }

    /// <summary>汇总境界、修炼角色、元素组成和原子语义，生成快照语义贡献。</summary>
    private static SemanticContribution[] ComposeSemantics(CoreFormationSnapshot snapshot,
        List<(CoreFormationAtomState state, CoreFormationAtomAsset asset)> active)
    {
        SemanticAsset realmSemantic = snapshot.realm switch
        {
            CoreFormationRealm.QiRefinement => CultivationSemantics.Realm.QiRefinement,
            CoreFormationRealm.Foundation => CultivationSemantics.Realm.Foundation,
            CoreFormationRealm.Jindan => CultivationSemantics.Realm.Jindan,
            _ => CultivationSemantics.Realm.Yuanying
        };
        var builder = new SemanticDescriptorBuilder()
            .Add(realmSemantic)
            .Add(CultivationSemantics.Role.Cultivation);

        var composition = snapshot.composition;
        composition.Normalize();
        float elementEvidenceTotal = (snapshot.element_semantics ?? [])
            .Where(value => value.polarity == SemanticPolarity.Positive)
            .Sum(value => Mathf.Max(0f, value.strength));
        float compositionWeight = elementEvidenceTotal > 0f ? 0.5f : 1f;
        for (var i = 0; i < ElementSemantics.Length; i++)
            if (composition[i] > 0f) builder.Add(ElementSemantics[i], composition[i] * compositionWeight);
        if (elementEvidenceTotal > 0f)
        {
            float evidenceWeight = 0.5f / elementEvidenceTotal;
            foreach (SemanticContribution contribution in snapshot.element_semantics ?? [])
            {
                if (contribution.polarity != SemanticPolarity.Positive || contribution.strength <= 0f) continue;
                builder.Add(contribution, evidenceWeight);
            }
        }

        var total = active.Sum(item => item.state.weight);
        foreach (var item in active)
        {
            var weight = total <= 0f ? 1f : item.state.weight / total;
            builder.Add(item.asset.semantics, weight);
        }
        return builder.Build().contributions;
    }

    /// <summary>将境界、品阶、阶段、元素和原子状态编码后计算稳定的 64 位组合签名。</summary>
    private static string ComposeSignature(CoreFormationSnapshot snapshot, int stage)
    {
        StringBuilder builder = new();
        builder.Append((int)snapshot.realm).Append('|').Append(snapshot.finalized ? 1 : 0).Append('|')
            .Append((int)snapshot.quality).Append('|').Append(Quantize(snapshot.quality_score)).Append('|')
            .Append(stage).Append('|').Append(snapshot.source_signature ?? string.Empty).Append('|')
            .Append(Quantize(snapshot.source_quality_score));
        var composition = snapshot.composition.AsArray();
        for (var i = 0; i < composition.Length; i++)
            builder.Append('|').Append(Quantize(composition[i]));
        foreach (SemanticContribution semantic in (snapshot.element_semantics ?? [])
                     .OrderBy(value => value.semantic_id, StringComparer.Ordinal)
                     .ThenBy(value => value.polarity))
            builder.Append('|').Append(semantic.semantic_id).Append('@').Append(Quantize(semantic.strength))
                .Append(':').Append((int)semantic.polarity);
        foreach (var atom in (snapshot.atoms ?? []).OrderBy(value => value.atom_id, StringComparer.Ordinal))
            builder.Append('|').Append(atom.atom_id).Append('@').Append(Quantize(atom.weight))
                .Append(':').Append(atom.awakening_stage).Append(':').Append(atom.inherited ? 1 : 0);
        return StableHash64(builder.ToString()).ToString("X16", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 优先按元素相似度和非元素语义契合选择代表法术；通用法术只在没有元素候选时兜底。
    /// </summary>
    private static string ResolveRepresentativeSkill(CoreFormationSnapshot snapshot)
    {
        var snapshotSemantics = CollectNonElementSemantics(
            SemanticDescriptor.Weighted(snapshot.semantics ?? []));
        var seed = NamingRuleUtils.StableHash(snapshot.signature);
        SkillEntityAsset bestElemental = null;
        SkillEntityAsset bestGeneric = null;
        var bestElementalScore = float.MinValue;
        var bestGenericScore = float.MinValue;
        foreach (var asset in ModClass.I.SkillV3.SkillLib.list)
        {
            if (asset == null || !asset.CanBeLearned) continue;
            var semanticSimilarity = ResolveNonElementSemanticSimilarity(snapshotSemantics, asset.Semantics);
            var tieBreak = (NamingRuleUtils.StableHash($"{seed}|{asset.id}") % 1000) / 1000000f;
            if (IsGenericSkill(asset))
            {
                var genericScore = semanticSimilarity * RepresentativeSkillSemanticWeight + tieBreak;
                if (genericScore <= bestGenericScore) continue;
                bestGeneric = asset;
                bestGenericScore = genericScore;
                continue;
            }

            var similarity = MathUtils.CosineSimilarity(snapshot.composition.AsArray(), asset.Element.AsArray());
            if (float.IsNaN(similarity) || float.IsInfinity(similarity)) similarity = 0f;
            if (similarity <= 0f) continue;
            var score = similarity + semanticSimilarity * RepresentativeSkillSemanticWeight + tieBreak;
            if (score <= bestElementalScore) continue;
            bestElemental = asset;
            bestElementalScore = score;
        }
        return bestElemental?.id ?? bestGeneric?.id;
    }

    /// <summary>判断法术是否显式声明了通用元素语义。</summary>
    private static bool IsGenericSkill(SkillEntityAsset asset)
    {
        return asset.Semantics.ContainsExpanded(ModClass.L.SemanticLibrary, SkillSemantics.Element.Generic);
    }

    /// <summary>展开描述并移除元素维度，避免元素组成和元素标签被重复计分。</summary>
    private static HashSet<SemanticAsset> CollectNonElementSemantics(SemanticDescriptor descriptor)
    {
        var semantics = SkillSemanticCollector.NewSet();
        SkillSemanticCollector.CollectDescriptorSemantics(descriptor, semantics);
        semantics.RemoveWhere(semantic => semantic.Facet == ModClass.L.SemanticFacetLibrary.Element);
        return semantics;
    }

    /// <summary>使用 Dice 系数计算有界的非元素语义契合度，避免按标签数量线性叠加。</summary>
    private static float ResolveNonElementSemanticSimilarity(
        HashSet<SemanticAsset> snapshotSemantics,
        SemanticDescriptor skillSemantics)
    {
        if (snapshotSemantics.Count == 0 || skillSemantics == null) return 0f;
        var candidateSemantics = CollectNonElementSemantics(skillSemantics);
        if (candidateSemantics.Count == 0) return 0f;

        var intersection = 0;
        foreach (var semantic in candidateSemantics)
        {
            if (snapshotSemantics.Contains(semantic)) intersection++;
        }

        return 2f * intersection / (snapshotSemantics.Count + candidateSemantics.Count);
    }

    /// <summary>惰性枚举当前阶段已显化且定义仍存在的原子资产。</summary>
    private static IEnumerable<CoreFormationAtomAsset> GetActiveAtoms(CoreFormationSnapshot snapshot, int stage)
    {
        foreach (var state in snapshot.atoms ?? [])
        {
            if (!state.IsActive(stage)) continue;
            var atom = Manager.CoreFormationAtomLibrary.get(state.atom_id);
            if (atom != null) yield return atom;
        }
    }

    /// <summary>把浮点值量化到万分位并使用区域无关格式写入签名原文。</summary>
    private static string Quantize(float value)
    {
        return Mathf.Round(value * 10000f).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>使用 FNV-1a 生成跨运行稳定的 64 位非加密哈希。</summary>
    private static ulong StableHash64(string value)
    {
        unchecked
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offset;
            value ??= string.Empty;
            for (var i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= prime;
            }
            return hash;
        }
    }

    /// <summary>把非空、非零属性值累加到指定属性 ID。</summary>
    private static void AddStat(IDictionary<string, float> stats, string id, float value)
    {
        if (string.IsNullOrEmpty(id) || value == 0f) return;
        stats.TryGetValue(id, out var current);
        stats[id] = current + value;
    }
}

/// <summary>由已激活原子生成 4-6 个汉字的稳定规则名称。</summary>
internal static class CoreFormationNameComposer
{
    /// <summary>从品阶和稳定谱系词干提炼规范名称；精炼次数由详情页独立展示。</summary>
    public static string Compose(CoreFormationSnapshot snapshot,
        List<(CoreFormationAtomState state, CoreFormationAtomAsset asset)> active)
    {
        string identity = NamingRuleUtils.LimitNameLength(snapshot.lineage_stem, 2);
        if (snapshot.realm == CoreFormationRealm.QiRefinement) return identity + "真气";
        if (snapshot.realm == CoreFormationRealm.Foundation)
        {
            int foundationSeed = NamingRuleUtils.StableHash(snapshot.signature);
            string structure = NamingRuleUtils.LimitNameLength(
                PickDominant(active, foundationSeed, CoreFormationAtomCategory.Structure), 2);
            return NamingRuleUtils.LimitNameLength(
                NamingRuleUtils.NormalizeName(identity + structure + "仙基"), 6);
        }
        if (snapshot.realm == CoreFormationRealm.Yuanying) return identity + "元婴";

        int seed = NamingRuleUtils.StableHash(snapshot.signature);
        string prefix = snapshot.quality.Stage >= 3
            ? NamingRuleUtils.Pick(seed, "无垢", "太清")
            : snapshot.quality.Stage >= 2
                ? "天元"
                : string.Empty;
        return prefix + identity + "金丹";
    }

    /// <summary>真气九层定型时从累计元素语义中提取贯穿后续境界的稳定短词干。</summary>
    public static string ResolveLineageStem(
        CoreFormationSnapshot snapshot,
        List<(CoreFormationAtomState state, CoreFormationAtomAsset asset)> active,
        int seed)
    {
        var builder = new SemanticProfileBuilder(ModClass.L.SemanticLibrary);
        builder.Add(
            SemanticDescriptor.Weighted(snapshot.element_semantics ?? []),
            1f,
            SemanticScope.Intrinsic,
            new SemanticSourceRef("content.core_formation.naming"));
        SemanticAsset semantic = builder.Build()
            .GetDirectRanked(SemanticQueryPolicy.Default, ModClass.L.SemanticFacetLibrary.Element)
            .Where(rank => rank.semantic.naming_stems is { Length: > 0 })
            .OrderByDescending(rank => rank.score.Net * Mathf.Max(0f, rank.semantic.naming_salience))
            .ThenBy(rank => rank.semantic.id, StringComparer.Ordinal)
            .Select(rank => rank.semantic)
            .FirstOrDefault();
        string stem = semantic == null
            ? PickDominant(active, seed, CoreFormationAtomCategory.Element)
            : NamingRuleUtils.Pick(seed, semantic.naming_stems);
        return NamingRuleUtils.LimitNameLength(string.IsNullOrEmpty(stem) ? "灵元" : stem, 2);
    }

    /// <summary>在指定分类间按原子权重选出最能代表当前组合的稳定名称词干。</summary>
    private static string PickDominant(List<(CoreFormationAtomState state, CoreFormationAtomAsset asset)> active,
                                       int seed, params CoreFormationAtomCategory[] categories)
    {
        return active.Where(item => Array.IndexOf(categories, item.asset.category) >= 0)
            .OrderByDescending(item => item.state.weight)
            .ThenBy(item => Array.IndexOf(categories, item.asset.category))
            .ThenBy(item => item.asset.id, StringComparer.Ordinal)
            .Select(item => item.asset.PickNameStem(seed))
            .FirstOrDefault() ?? string.Empty;
    }

}
