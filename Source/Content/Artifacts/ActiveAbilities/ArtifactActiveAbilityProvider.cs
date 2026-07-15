using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Artifacts.ActiveAbilities;

/// <summary>
/// 将每件已装备法器上的主动能力实例分别暴露给统一主动能力系统。
/// </summary>
internal sealed class ArtifactActiveAbilityProvider : IActiveAbilityProvider
{
    public const string ProviderId = "content.artifact";

    public string Id => ProviderId;

    public void Collect(ActorExtend caster, ICollection<ActiveAbilityHandle> output)
    {
        var relations = caster.E.GetRelations<EquippedArtifactRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            EquippedArtifactRelation relation = relations[i];
            Entity artifact = relation.artifact;
            if (!artifact.IsAvailable() || !artifact.TryGetComponent(out ArtifactAbilitySet abilitySet)) continue;
            for (int j = 0; j < abilitySet.abilities.Length; j++)
            {
                ArtifactAbilityInstance ability = abilitySet.abilities[j];
                ArtifactAbilityAsset asset = Libraries.Manager.ArtifactAbilityLibrary.get(ability.ability_id);
                if (asset?.active_use == null ||
                    !ArtifactAbilityLifecycle.MeetsState(
                        relation.state,
                        asset.lifecycle.active_minimum_state)) continue;
                output.Add(new ActiveAbilityHandle(Id, artifact, ability.instance_id));
            }
        }
    }

    public ActiveAbilityChannel GetChannels(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return TryResolve(caster, handle, out ResolvedAbility resolved)
            ? resolved.Asset.active_use.channels
            : ActiveAbilityChannel.None;
    }

    public ActiveAbilityDescriptor Describe(ActorExtend caster, ActiveAbilityHandle handle)
    {
        ResolvedAbility resolved = Resolve(caster, handle);
        Entity artifact = handle.Source;
        string artifactName = artifact.HasName ? artifact.Name.value : artifact.GetComponent<ItemShape>().Type.id;
        string name = $"{resolved.Asset.GetName()} · {artifactName}";
        ItemShapeAsset shape = artifact.GetComponent<ItemShape>().Type;
        Sprite icon = shape.GetIcon?.Invoke(artifact);
        ArtifactActiveAbilityProfile profile = resolved.Asset.active_use;
        return new ActiveAbilityDescriptor(
            name,
            icon,
            profile.channels,
            profile.target_mode,
            profile.activation_mode);
    }

    public bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        if (!TryResolve(caster, handle, out ResolvedAbility resolved)) return false;
        ArtifactAbilityExecutionContext context = new(caster.E, handle.Source, resolved.Relation.state);
        return resolved.Asset.CanPrepareActive(
            context,
            resolved.Ability,
            resolved.Runtime,
            target);
    }

    public bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target)
    {
        if (!TryResolve(caster, handle, out ResolvedAbility resolved)) return false;
        ArtifactAbilityExecutionContext context = new(caster.E, handle.Source, resolved.Relation.state);
        return resolved.Asset.CanUseActive(
            context,
            resolved.Ability,
            resolved.Runtime,
            target);
    }

    public int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        return TryResolve(caster, handle, out ResolvedAbility resolved)
            ? Mathf.Max(0, resolved.Asset.active_use.ai_weight)
            : 0;
    }

    public float ResolveRange(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        if (!TryResolve(caster, handle, out ResolvedAbility resolved)) return 0f;
        ArtifactAbilityExecutionContext context = new(caster.E, handle.Source, resolved.Relation.state);
        return resolved.Asset.active_use.ResolveRange?.Invoke(context, resolved.Ability) ?? 0f;
    }

    public bool TryUse(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        if (!TryResolve(caster, handle, out ResolvedAbility resolved)) return false;

        ArtifactAbilityRuntime runtime = handle.Source.GetComponent<ArtifactAbilityRuntime>();
        ArtifactAbilityExecutionContext context = new(caster.E, handle.Source, resolved.Relation.state);
        bool used = resolved.Asset.TryUseActive(
            context,
            resolved.Ability,
            ref runtime.abilities[resolved.AbilityIndex],
            target,
            origin);
        handle.Source.GetComponent<ArtifactAbilityRuntime>() = runtime;
        return used;
    }

    private static ResolvedAbility Resolve(ActorExtend caster, ActiveAbilityHandle handle)
    {
        if (!TryResolve(caster, handle, out ResolvedAbility resolved))
        {
            throw new System.InvalidOperationException($"法器主动能力实例不存在: {handle.EntryId}");
        }
        return resolved;
    }

    private static bool TryResolve(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        out ResolvedAbility resolved)
    {
        resolved = default;
        Entity artifact = handle.Source;
        if (artifact.IsNull || !artifact.HasComponent<ArtifactAbilitySet>()) return false;

        var relations = caster.E.GetRelations<EquippedArtifactRelation>();
        EquippedArtifactRelation relation = default;
        for (int i = 0; i < relations.Length; i++)
        {
            if (relations[i].artifact != artifact) continue;
            relation = relations[i];
            break;
        }
        if (relation.artifact != artifact) return false;

        ArtifactAbilitySet abilitySet = artifact.GetComponent<ArtifactAbilitySet>();
        ArtifactAbilityRuntime abilityRuntime = artifact.GetComponent<ArtifactAbilityRuntime>();
        for (int i = 0; i < abilitySet.abilities.Length; i++)
        {
            if (abilitySet.abilities[i].instance_id != handle.EntryId) continue;
            ArtifactAbilityInstance ability = abilitySet.abilities[i];
            ArtifactAbilityAsset asset = Libraries.Manager.ArtifactAbilityLibrary.get(ability.ability_id);
            resolved = new ResolvedAbility(relation, ability, abilityRuntime.abilities[i], asset, i);
            return asset?.active_use != null;
        }
        return false;
    }

    private readonly struct ResolvedAbility
    {
        public readonly EquippedArtifactRelation Relation;
        public readonly ArtifactAbilityInstance Ability;
        public readonly ArtifactAbilityRuntimeEntry Runtime;
        public readonly ArtifactAbilityAsset Asset;
        public readonly int AbilityIndex;

        public ResolvedAbility(
            EquippedArtifactRelation relation,
            ArtifactAbilityInstance ability,
            ArtifactAbilityRuntimeEntry runtime,
            ArtifactAbilityAsset asset,
            int abilityIndex)
        {
            Relation = relation;
            Ability = ability;
            Runtime = runtime;
            Asset = asset;
            AbilityIndex = abilityIndex;
        }
    }
}
