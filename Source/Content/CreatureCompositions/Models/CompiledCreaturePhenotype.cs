using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Cultiway.Content.CreatureCompositions.Libraries;
using Cultiway.Core.Semantics;

namespace Cultiway.Content.CreatureCompositions.Models;

/// <summary>身体整理器生成并由当前世界缓存共享的只读结果。</summary>
public sealed class CompiledCreaturePhenotype
{
    private readonly ReadOnlyCollection<CompiledCreatureOrgan> orderedOrgans;
    private readonly ReadOnlyCollection<CreatureStatValue> stats;
    private readonly ReadOnlyCollection<SemanticContribution> semantics;
    private readonly ReadOnlyCollection<string> activeAbilityIds;
    private readonly ReadOnlyCollection<CreatureEffectRank> passiveEffects;
    private readonly ReadOnlyCollection<CompiledCreatureVisualLayer> visualLayers;

    public int CompiledIndex { get; }
    public string Signature { get; }
    public CreatureBodyPlanAsset BodyPlan { get; }
    public CreatureMorphAsset Morph { get; }
    public IReadOnlyList<CompiledCreatureOrgan> OrderedOrgans => orderedOrgans;
    public IReadOnlyList<CreatureStatValue> Stats => stats;
    public IReadOnlyList<SemanticContribution> Semantics => semantics;
    public IReadOnlyList<string> ActiveAbilities => activeAbilityIds;
    public IReadOnlyList<CreatureEffectRank> PassiveEffects => passiveEffects;
    public IReadOnlyList<CompiledCreatureVisualLayer> VisualLayers => visualLayers;
    public int ComplexityUsed { get; }

    internal CompiledCreaturePhenotype(
        int compiledIndex,
        string signature,
        CreatureBodyPlanAsset bodyPlan,
        CreatureMorphAsset morph,
        CompiledCreatureOrgan[] orderedOrgans,
        CreatureStatValue[] stats,
        SemanticContribution[] semantics,
        string[] activeAbilityIds,
        CreatureEffectRank[] passiveEffects,
        CompiledCreatureVisualLayer[] visualLayers,
        int complexityUsed)
    {
        CompiledIndex = compiledIndex;
        Signature = signature;
        BodyPlan = bodyPlan;
        Morph = morph;
        this.orderedOrgans = Array.AsReadOnly(orderedOrgans ?? Array.Empty<CompiledCreatureOrgan>());
        this.stats = Array.AsReadOnly(stats ?? Array.Empty<CreatureStatValue>());
        this.semantics = Array.AsReadOnly(semantics ?? Array.Empty<SemanticContribution>());
        this.activeAbilityIds = Array.AsReadOnly(activeAbilityIds ?? Array.Empty<string>());
        this.passiveEffects = Array.AsReadOnly(passiveEffects ?? Array.Empty<CreatureEffectRank>());
        this.visualLayers = Array.AsReadOnly(visualLayers ?? Array.Empty<CompiledCreatureVisualLayer>());
        ComplexityUsed = complexityUsed;
    }
}
