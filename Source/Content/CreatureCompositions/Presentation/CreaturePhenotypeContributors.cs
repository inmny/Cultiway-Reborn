using Cultiway.Content.CreatureCompositions.Components;
using Cultiway.Content.CreatureCompositions.Models;
using Cultiway.Content.CreatureCompositions.Services;
using Cultiway.Core;
using Cultiway.Core.Semantics;

namespace Cultiway.Content.CreatureCompositions.Presentation;

/// <summary>把整理好的身体属性按稳定顺序合并进角色属性缓存。</summary>
public static class CreaturePhenotypeStatsContributor
{
    /// <summary>读取当前身体并累加器官属性；没有身体或整理结果失效时不产生任何属性。</summary>
    public static void Contribute(ActorExtend actor, BaseStats stats)
    {
        if (!actor.TryGetComponent(out CreaturePhenotype phenotype) || !phenotype.IsValid) return;
        if (!CreaturePhenotypeCompiler.TryGetCompiled(
                phenotype.CompiledIndex, phenotype.Signature, out CompiledCreaturePhenotype compiled))
            return;

        foreach (Libraries.CreatureStatValue stat in compiled.Stats)
        {
            if (string.IsNullOrEmpty(stat.StatId) ||
                AssetManager.base_stats_library.get(stat.StatId) == null) continue;
            stats[stat.StatId] += stat.Value;
        }
    }
}

/// <summary>把器官提供的长期生物特征写入角色语义档案，每条特征都能追查到具体器官。</summary>
public sealed class CreaturePhenotypeSemanticContributor : IActorSemanticContributor
{
    /// <summary>主动能力、语义来源与属性贡献共同使用的稳定贡献者编号。</summary>
    public const string ContributorId = "content.creature_phenotype";

    /// <summary>排在玩法临时语义之前、原版种族语义之后的稳定执行顺序。</summary>
    public int Priority => 50;

    /// <summary>语义贡献者的稳定编号。</summary>
    public string Id => ContributorId;

    /// <summary>按身体位置顺序逐个器官写入先天特征。</summary>
    public void Contribute(ActorExtend actor, SemanticProfileBuilder builder)
    {
        if (!actor.TryGetComponent(out CreaturePhenotype phenotype) || !phenotype.IsValid) return;
        if (!CreaturePhenotypeCompiler.TryGetCompiled(
                phenotype.CompiledIndex, phenotype.Signature, out CompiledCreaturePhenotype compiled))
            return;

        string prefix = $"{compiled.BodyPlan.id}/{compiled.Morph.id}/";
        foreach (CompiledCreatureOrgan organ in compiled.OrderedOrgans)
        {
            if (organ.Organ.Semantics == null) continue;
            var source = new SemanticSourceRef(
                ContributorId, $"{prefix}{organ.Entry.SlotId}/{organ.Organ.id}");
            builder.Add(organ.Organ.Semantics, 1f, SemanticScope.Intrinsic, source);
        }
    }
}
