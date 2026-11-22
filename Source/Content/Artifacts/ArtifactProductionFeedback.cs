using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Artifacts;

/// <summary>保留实际贡献者，等生产行为写入进度或成品后再发出对应表现。</summary>
internal struct ArtifactProductionFeedback
{
    private List<(ArtifactAbilityExecutionContext Context, ArtifactAbilityInstance Ability,
        ArtifactAbilityRuntimeEntry Runtime, float Intensity)> contributions;

    internal void Record(ArtifactAbilityExecutionContext context, ArtifactAbilityInstance ability,
        ArtifactAbilityRuntimeEntry runtime, float intensity) =>
        (contributions ??= new()).Add((context, ability, runtime, intensity));

    internal void Publish(string channel, float duration = 0f, Entity? craftingProduct = null)
    {
        if (contributions == null) return;
        foreach (var entry in contributions)
            ArtifactAbilityVisuals.EmitProduction(entry.Context, entry.Ability, entry.Runtime, channel,
                entry.Intensity, duration, craftingProduct);
        contributions.Clear();
    }

}
