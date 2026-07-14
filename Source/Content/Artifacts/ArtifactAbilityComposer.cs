using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;

namespace Cultiway.Content.Artifacts;

/// <summary>
/// 炼器阶段生成的能力定义与对应初始运行状态。
/// </summary>
public sealed class ArtifactAbilityComposition
{
    public ArtifactAbilitySet ability_set;
    public ArtifactAbilityRuntime runtime;
}

/// <summary>
/// 根据器形、atom 和配方语义，从注册表中组合所有满足条件的法器能力。
/// </summary>
public static class ArtifactAbilityComposer
{
    public static ArtifactAbilityComposition Compose(
        ArtifactRecipeContext recipe,
        ArtifactShapeAsset shape,
        ArtifactAtomAsset[] atoms,
        string seed)
    {
        ArtifactAbilityComposeContext context = new()
        {
            recipe = recipe,
            shape = shape,
            atoms = atoms,
            seed = seed,
        };
        var candidates = Cultiway.Content.Libraries.Manager.ArtifactAbilityLibrary.All
            .Select(ability => new
            {
                Ability = ability,
                Score = ability.ScoreFor(context),
                TieBreak = StableTieBreak(seed, ability.id),
            })
            .Where(candidate => candidate.Score > 0f && candidate.Score >= candidate.Ability.minimum_score)
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.TieBreak)
            .ThenBy(candidate => candidate.Ability.id)
            .ToArray();

        HashSet<string> selectedGroups = new(StringComparer.Ordinal);
        List<ArtifactAbilityInstance> abilities = new(candidates.Length);
        List<ArtifactAbilityRuntimeEntry> runtime = new(candidates.Length);
        for (int i = 0; i < candidates.Length; i++)
        {
            ArtifactAbilityAsset ability = candidates[i].Ability;
            if (!string.IsNullOrEmpty(ability.exclusive_group) &&
                !selectedGroups.Add(ability.exclusive_group))
            {
                continue;
            }
            abilities.Add(ability.ComposeInstance(context));
            runtime.Add(ability.ComposeRuntime(context));
        }

        return new ArtifactAbilityComposition
        {
            ability_set = new ArtifactAbilitySet { abilities = abilities.ToArray() },
            runtime = new ArtifactAbilityRuntime { abilities = runtime.ToArray() },
        };
    }

    private static int StableTieBreak(string seed, string abilityId)
    {
        unchecked
        {
            int hash = 17;
            string value = $"{seed}|{abilityId}";
            for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
            return hash;
        }
    }
}
