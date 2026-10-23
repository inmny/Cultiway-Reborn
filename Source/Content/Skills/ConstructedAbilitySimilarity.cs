using System;
using System.Collections.Generic;
using Cultiway.Core;
using Cultiway.Core.Skills;

namespace Cultiway.Content.Skills;

/// <summary>只限制自动创法新增，明确复制、改造和历史恢复仍完整登记。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>每份正式构造只保留一份比较数值。</summary>
    private static readonly Dictionary<string, ConstructionSimilarity> similarities = new(StringComparer.Ordinal);

    /// <summary>世界资料按结构及参数阶梯索引；同组保留所有旧编号，不合并人物来源。</summary>
    private static readonly Dictionary<string, SortedSet<string>> similarityBuckets = new(StringComparer.Ordinal);

    /// <summary>正式登记和替换时一次更新，避免创法遍历整个世界目录。</summary>
    /// <param name="definition">已经验证并登记的完整副本。</param>
    /// <param name="plan">该次正式登记已经编译的计划。</param>
    private static void IndexConstruction(AbilityDefinition definition, CompiledAbility plan)
    {
        if (similarities.TryGetValue(definition.Id, out ConstructionSimilarity old))
        {
            SortedSet<string> bucket = similarityBuckets[old.Bucket];
            bucket.Remove(definition.Id);
            if (bucket.Count == 0) similarityBuckets.Remove(old.Bucket);
        }

        ConstructionSimilarity profile = ConstructionSimilarity.Describe(definition, plan);
        similarities[definition.Id] = profile;
        if (!similarityBuckets.TryGetValue(profile.Bucket, out SortedSet<string> ids))
            similarityBuckets.Add(profile.Bucket, ids = new SortedSet<string>(StringComparer.Ordinal));
        ids.Add(definition.Id);
    }

    /// <summary>自身最多128项逐项比较，世界只查同阶梯索引，不把另一人的原术冒充本次新作者。</summary>
    /// <param name="actor">真实创造者。</param>
    /// <param name="definition">本次尚未登记的结果。</param>
    /// <param name="plan">该次已经完整检查的计划。</param>
    private static string FindSimilarConstruction(ActorExtend actor, AbilityDefinition definition, CompiledAbility plan)
    {
        ConstructionSimilarity profile = ConstructionSimilarity.Describe(definition, plan);
        if (actor.E.TryGetComponent(out ConstructedKnowledge known))
            foreach (string id in known.Ids)
                if (similarities.TryGetValue(id, out ConstructionSimilarity previous) && profile.Near(previous))
                    return id;
        return similarityBuckets.TryGetValue(profile.Bucket, out SortedSet<string> ids) ? ids.Min : null;
    }

    /// <summary>切世界重建索引，不把另一世界的能力误认成本世界已有。</summary>
    private static void ClearConstructionIndex()
    {
        similarities.Clear();
        similarityBuckets.Clear();
    }

    /// <summary>资源集合成功改变后重新读取实际展开和接触规则，旧施放仍保留原计划。</summary>
    private static void RebuildConstructionIndex()
    {
        ClearConstructionIndex();
        foreach (AbilityDefinition definition in definitions.Values)
            IndexConstruction(definition, PlanFor(definition.Id));
    }
}