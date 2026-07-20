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

    /// <summary>按主修 60%、其他已学内容 25%、固有来源 15% 解析语义分数。</summary>
    public float SemanticScore(SemanticAsset semantic)
    {
        if (semantic == null) return 0f;
        var learned = RawSemanticScore(semantic, LearnedPolicy) * 0.25f;
        var intrinsic = RawSemanticScore(semantic, IntrinsicPolicy) * 0.15f;
        var main = mainCultibook?.Semantics?.ContainsExpanded(ModClass.L.SemanticLibrary, semantic) == true
            ? mainMastery * 0.35f
            : 0f;
        return Mathf.Max(0f, learned + intrinsic + main);
    }

    /// <summary>按指定查询策略读取语义画像原始净分；没有画像时返回零。</summary>
    private float RawSemanticScore(SemanticAsset semantic, SemanticQueryPolicy policy)
    {
        return semanticProfile?.GetScore(semantic, policy).Net ?? 0f;
    }
}

/// <summary>负责金丹与元婴形成、觉醒和派生值重建的确定性组合服务。</summary>
public static class CoreFormationComposer
{
    private const float FiveQiWeight = 0.7f;
    private const float ElementRootWeight = 0.3f;
    private const int MaxLatentAtoms = 2;

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

    /// <summary>由筑基、灵根、功法和已学法术形成一个新的金丹实例。</summary>
    public static CoreFormationSnapshot ComposeJindan(ActorExtend actor, XianBase foundation, float strength)
    {
        var composition = BuildJindanComposition(actor, foundation);
        var context = new CoreFormationContext(actor, foundation, composition, CoreFormationRealm.Jindan);
        List<CoreFormationAtomState> atoms = new(6);

        AddSelected(atoms, SelectBest(context, CoreFormationAtomCategory.Element), 0, false);
        AddSelected(atoms, SelectBest(context, CoreFormationAtomCategory.Structure, requireMinimum: true),
            0, false);

        var latentIndex = 0;
        AddOptional(atoms, context, CoreFormationAtomCategory.Path, ref latentIndex);
        AddOptional(atoms, context, CoreFormationAtomCategory.Theme, ref latentIndex);

        var snapshot = new CoreFormationSnapshot
        {
            version = CoreFormationSnapshot.CurrentVersion,
            realm = CoreFormationRealm.Jindan,
            quality = ResolveQuality(strength, context),
            composition = composition,
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
        if (!jindan.IsValid) throw new ArgumentException("结婴需要有效的金丹组合快照。", nameof(jindan));
        var context = new CoreFormationContext(actor, foundation, jindan.composition, CoreFormationRealm.Yuanying);
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
            quality = Mathf.Max(jindan.quality, ResolveQuality(strength, context)),
            composition = jindan.composition,
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
        if (!snapshot.IsValid || snapshot.realm != CoreFormationRealm.Jindan) return false;
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
            snapshot.quality = 3;
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

    /// <summary>按筑基五气 70%、灵根 30% 合成并归一化金丹元素组成。</summary>
    private static ElementComposition BuildJindanComposition(ActorExtend actor, XianBase foundation)
    {
        var fiveQi = Normalize([
            foundation.iron, foundation.wood, foundation.water, foundation.fire, foundation.earth, 0f, 0f, 0f
        ], fiveElementFallback: true);
        if (actor == null || !actor.TryGetComponent(out ElementRoot root)) return new ElementComposition(fiveQi);

        var rootRaw = new[]
        {
            root.Iron, root.Wood, root.Water, root.Fire, root.Earth, root.Neg, root.Pos, root.Entropy
        };
        if (rootRaw.Sum(value => Mathf.Max(0f, value)) <= 0f) return new ElementComposition(fiveQi);
        var rootValues = Normalize(rootRaw, fiveElementFallback: false);
        var result = new float[8];
        for (var i = 0; i < result.Length; i++)
            result[i] = fiveQi[i] * FiveQiWeight + rootValues[i] * ElementRootWeight;
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

    /// <summary>综合初始强度、三花均衡和元素均衡，将组合评定为 0-3 级品质。</summary>
    private static int ResolveQuality(float strength, CoreFormationContext context)
    {
        var score = Mathf.Log(Mathf.Max(0f, strength) + 1f, 2f) +
                    context.ThreeHuaBalance + context.ElementBalance * 0.5f;
        if (score >= 4f) return 3;
        if (score >= 2.5f) return 2;
        if (score >= 1.25f) return 1;
        return 0;
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
        var realmMask = context.Realm == CoreFormationRealm.Jindan
            ? CoreFormationRealmMask.Jindan
            : CoreFormationRealmMask.Yuanying;
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
        snapshot.canonical_name = CoreFormationNameComposer.Compose(snapshot, active, stage);
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
        var realmScale = snapshot.realm == CoreFormationRealm.Yuanying ? 1.25f : 1f;
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
            if (snapshot.realm == CoreFormationRealm.Yuanying && inheritedWeight > 0f && newWeight > 0f)
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
        var builder = new SemanticDescriptorBuilder()
            .Add(snapshot.realm == CoreFormationRealm.Jindan
                ? CultivationSemantics.Realm.Jindan
                : CultivationSemantics.Realm.Yuanying)
            .Add(CultivationSemantics.Role.Cultivation);

        var composition = snapshot.composition;
        composition.Normalize();
        for (var i = 0; i < ElementSemantics.Length; i++)
            if (composition[i] > 0f) builder.Add(ElementSemantics[i], composition[i]);

        var total = active.Sum(item => item.state.weight);
        foreach (var item in active)
        {
            var weight = total <= 0f ? 1f : item.state.weight / total;
            builder.Add(item.asset.semantics, weight);
        }
        return builder.Build().contributions;
    }

    /// <summary>将境界、品质、阶段、元素和原子状态编码后计算稳定的 64 位组合签名。</summary>
    private static string ComposeSignature(CoreFormationSnapshot snapshot, int stage)
    {
        StringBuilder builder = new();
        builder.Append((int)snapshot.realm).Append('|').Append(snapshot.quality).Append('|')
            .Append(stage >= 9 ? 3 : stage >= 6 ? 2 : stage >= 3 ? 1 : 0);
        var composition = snapshot.composition.AsArray();
        for (var i = 0; i < composition.Length; i++)
            builder.Append('|').Append(Quantize(composition[i]));
        foreach (var atom in (snapshot.atoms ?? []).OrderBy(value => value.atom_id, StringComparer.Ordinal))
            builder.Append('|').Append(atom.atom_id).Append('@').Append(Quantize(atom.weight))
                .Append(':').Append(atom.awakening_stage).Append(':').Append(atom.inherited ? 1 : 0);
        return StableHash64(builder.ToString()).ToString("X16", CultureInfo.InvariantCulture);
    }

    /// <summary>按元素相似度和语义重合度选择最能代表当前组合的可学习法术。</summary>
    private static string ResolveRepresentativeSkill(CoreFormationSnapshot snapshot)
    {
        var semanticIds = new HashSet<string>((snapshot.semantics ?? []).Select(value => value.semantic_id),
            StringComparer.Ordinal);
        var seed = NamingRuleUtils.StableHash(snapshot.signature);
        SkillEntityAsset best = null;
        var bestScore = float.MinValue;
        foreach (var asset in ModClass.I.SkillV3.SkillLib.list)
        {
            if (asset == null || !asset.CanBeLearned) continue;
            var similarity = MathUtils.CosineSimilarity(snapshot.composition.AsArray(), asset.Element.AsArray());
            if (float.IsNaN(similarity) || float.IsInfinity(similarity)) similarity = 0f;
            var semantics = SkillSemanticCollector.NewSet();
            SkillSemanticCollector.CollectAssetSemantics(asset, semantics);
            var overlap = semantics.Count(value => semanticIds.Contains(value.id));
            if (similarity <= 0f && overlap == 0) continue;
            var score = similarity + overlap * 0.15f +
                        (NamingRuleUtils.StableHash($"{seed}|{asset.id}") % 1000) / 1000000f;
            if (score <= bestScore) continue;
            best = asset;
            bestScore = score;
        }
        return best?.id;
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
    /// <summary>从品质、阶段和主导原子中提炼一个短而稳定的规范名称。</summary>
    public static string Compose(CoreFormationSnapshot snapshot,
        List<(CoreFormationAtomState state, CoreFormationAtomAsset asset)> active, int stage)
    {
        var seed = NamingRuleUtils.StableHash(snapshot.signature);
        var prefix = snapshot.realm == CoreFormationRealm.Jindan
            ? stage >= 9
                ? "九转"
                : snapshot.quality >= 3
                    ? NamingRuleUtils.Pick(seed, "无垢", "太清")
                    : snapshot.quality >= 2
                        ? "天元"
                        : string.Empty
            : string.Empty;

        var element = Pick(active, CoreFormationAtomCategory.Element, seed + 11);
        var lineage = Pick(active, CoreFormationAtomCategory.Theme, seed + 17);
        var path = Pick(active, CoreFormationAtomCategory.Path, seed + 23);
        var structure = Pick(active, CoreFormationAtomCategory.Structure, seed + 29);
        var manifestation = Pick(active, CoreFormationAtomCategory.Manifestation, seed + 31);
        var transformation = Pick(active, CoreFormationAtomCategory.Transformation, seed + 37);
        var suffix = snapshot.realm == CoreFormationRealm.Jindan ? "金丹" : "元婴";
        var core = PickDominant(active, seed + 41,
            CoreFormationAtomCategory.Element, CoreFormationAtomCategory.Structure);
        var identity = snapshot.realm == CoreFormationRealm.Jindan
            ? First(lineage, path, core, element, structure)
            : First(transformation, manifestation, lineage, path, core, element, structure);
        identity = NamingRuleUtils.LimitNameLength(identity, 2);
        return snapshot.realm == CoreFormationRealm.Jindan
            ? prefix + identity + suffix
            : identity + suffix;
    }

    /// <summary>从指定分类中选取权重最高原子的稳定名称词干。</summary>
    private static string Pick(List<(CoreFormationAtomState state, CoreFormationAtomAsset asset)> active,
                               CoreFormationAtomCategory category, int seed)
    {
        return active.Where(item => item.asset.category == category)
            .OrderByDescending(item => item.state.weight)
            .ThenBy(item => item.asset.id, StringComparer.Ordinal)
            .Select(item => item.asset.PickNameStem(seed))
            .FirstOrDefault() ?? string.Empty;
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

    /// <summary>按候选顺序返回首个非空词干。</summary>
    private static string First(params string[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrEmpty(values[i])) return values[i];
        }
        return string.Empty;
    }

}
