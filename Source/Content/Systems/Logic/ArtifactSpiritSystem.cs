using System;
using System.Collections.Generic;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using strings;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

/// <summary>根据法器持久状态和控制状态显化、维持或收回唯一的真实器灵生物。</summary>
public sealed class ArtifactSpiritSystem
    : QuerySystem<Artifact, ArtifactSpiritState, ArtifactAbilitySet, ArtifactAbilityRuntime>
{
    private readonly List<SpawnRequest> spawns = new();
    private readonly List<AvatarRequest> dismissals = new();
    private readonly List<AvatarRequest> maintenance = new();
    private readonly List<RelationRequest> relationRemovals = new();

    public ArtifactSpiritSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagRecycle>());
    }

    protected override void OnUpdate()
    {
        double now = World.world.getCurWorldTime();
        spawns.Clear();
        dismissals.Clear();
        maintenance.Clear();
        relationRemovals.Clear();

        Query.ForEachEntity((
            ref Artifact _,
            ref ArtifactSpiritState state,
            ref ArtifactAbilitySet abilitySet,
            ref ArtifactAbilityRuntime runtime,
            Entity artifact) =>
        {
            SpiritBinding binding = default;
            bool desired = state.awakened && runtime.attached && runtime.controller.IsAvailable() &&
                           TryResolveBinding(abilitySet, runtime, state, out binding);
            Entity avatar = ResolveAvatar(artifact);
            if (!avatar.IsNull && !IsLivingAvatar(avatar))
            {
                relationRemovals.Add(new RelationRequest(artifact, avatar));
                avatar = default;
            }

            if (!desired)
            {
                if (!avatar.IsNull) dismissals.Add(new AvatarRequest(artifact, avatar));
                return;
            }

            if (!avatar.IsNull)
            {
                ArtifactSpiritAvatar link = avatar.GetComponent<ArtifactSpiritAvatar>();
                if (link.controller == runtime.controller &&
                    link.ability_instance_id == binding.ability.instance_id)
                {
                    maintenance.Add(new AvatarRequest(artifact, avatar, runtime.controller, binding, state));
                    return;
                }
                dismissals.Add(new AvatarRequest(artifact, avatar));
            }

            if (now >= state.recovery_until)
            {
                spawns.Add(new SpawnRequest(
                    artifact,
                    runtime.controller,
                    binding,
                    state,
                    runtime.control_state));
            }
        });

        ApplyRelationRemovals();
        for (int i = 0; i < dismissals.Count; i++) Dismiss(dismissals[i]);
        for (int i = 0; i < maintenance.Count; i++) Maintain(maintenance[i]);
        for (int i = 0; i < spawns.Count; i++) Spawn(spawns[i]);
    }

    private static bool TryResolveBinding(
        ArtifactAbilitySet abilitySet,
        ArtifactAbilityRuntime runtime,
        ArtifactSpiritState state,
        out SpiritBinding result)
    {
        result = default;
        float bestScore = float.MinValue;
        bool found = false;
        for (int i = 0; i < abilitySet.abilities.Length; i++)
        {
            ArtifactAbilityInstance ability = abilitySet.abilities[i];
            ArtifactAbilityAsset asset = Libraries.Manager.ArtifactAbilityLibrary.get(ability.ability_id);
            ArtifactSpiritAbilityProfile profile = asset.spirit_use;
            if (profile == null ||
                !ArtifactAbilityLifecycle.MeetsState(runtime.control_state, profile.minimum_state)) continue;

            float score = (profile.ResolveDamageRatio?.Invoke(ability, state) ?? 0f) * 2f +
                          (profile.ResolveHealthRatio?.Invoke(ability, state) ?? 0f) +
                          (profile.ResolveArmorBonus?.Invoke(ability, state) ?? 0f) * 0.05f;
            if (found && (score < bestScore ||
                          score == bestScore && string.CompareOrdinal(
                              ability.instance_id,
                              result.ability.instance_id) >= 0)) continue;
            result = new SpiritBinding(ability, runtime.abilities[i], asset, profile);
            bestScore = score;
            found = true;
        }
        return found;
    }

    private Entity ResolveAvatar(Entity artifact)
    {
        Entity selected = default;
        var relations = artifact.GetRelations<ArtifactSpiritAvatarRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            Entity avatar = relations[i].avatar;
            if (!avatar.IsAvailable())
            {
                relationRemovals.Add(new RelationRequest(artifact, avatar));
                continue;
            }
            if (selected.IsNull || avatar.Id < selected.Id)
            {
                if (!selected.IsNull) dismissals.Add(new AvatarRequest(artifact, selected));
                selected = avatar;
            }
            else
            {
                dismissals.Add(new AvatarRequest(artifact, avatar));
            }
        }
        return selected;
    }

    private void ApplyRelationRemovals()
    {
        for (int i = 0; i < relationRemovals.Count; i++)
        {
            RelationRequest request = relationRemovals[i];
            if (request.artifact.IsAvailable())
            {
                request.artifact.RemoveRelation<ArtifactSpiritAvatarRelation>(request.avatar);
            }
        }
    }

    private static void Dismiss(AvatarRequest request)
    {
        if (request.artifact.IsAvailable())
        {
            request.artifact.RemoveRelation<ArtifactSpiritAvatarRelation>(request.avatar);
        }
        if (!IsLivingAvatar(request.avatar)) return;
        ref ArtifactSpiritAvatar avatar = ref request.avatar.GetComponent<ArtifactSpiritAvatar>();
        avatar.recover_on_death = false;
        request.avatar.GetComponent<ActorBinder>().Actor.dieAndDestroy(AttackType.None);
    }

    private static void Maintain(AvatarRequest request)
    {
        if (!IsLivingAvatar(request.avatar) || !request.controller.IsAvailable()) return;
        Actor spirit = request.avatar.GetComponent<ActorBinder>().Actor;
        Actor controller = request.controller.GetComponent<ActorBinder>().Actor;
        float levelScale = ArtifactSpiritService.ResolveLevelScale(request.state);
        ref ArtifactSpiritAvatar avatar = ref request.avatar.GetComponent<ArtifactSpiritAvatar>();
        float damageRatio = Mathf.Max(0f,
            request.binding.profile.ResolveDamageRatio?.Invoke(request.binding.ability, request.state) ?? 0f) *
                            levelScale;
        float healthRatio = Mathf.Max(0f,
            request.binding.profile.ResolveHealthRatio?.Invoke(request.binding.ability, request.state) ?? 0f) *
                            levelScale;
        float armorBonus = Mathf.Max(0f,
            request.binding.profile.ResolveArmorBonus?.Invoke(request.binding.ability, request.state) ?? 0f) *
                           levelScale;
        if (!Mathf.Approximately(avatar.damage_ratio, damageRatio) ||
            !Mathf.Approximately(avatar.health_ratio, healthRatio) ||
            !Mathf.Approximately(avatar.armor_bonus, armorBonus))
        {
            avatar.damage_ratio = damageRatio;
            avatar.health_ratio = healthRatio;
            avatar.armor_bonus = armorBonus;
            spirit.GetExtend().MarkCultiwayStatsDirty();
        }

        if (spirit.kingdom != controller.kingdom) spirit.joinKingdom(controller.kingdom);
        if (controller.has_attack_target && controller.isEnemyTargetAlive() &&
            spirit.canAttackTarget(controller.attack_target))
        {
            spirit.setAttackTarget(controller.attack_target);
            return;
        }

        float distance = Toolbox.SquaredDistVec2Float(spirit.current_position, controller.current_position);
        if (distance > 100f)
        {
            spirit.setCurrentTilePosition(controller.current_tile);
            spirit.stopMovement();
        }
        else if (distance > 9f && !spirit.has_attack_target)
        {
            spirit.moveTo(controller.current_tile);
        }
    }

    private static void Spawn(SpawnRequest request)
    {
        if (!request.artifact.IsAvailable() || !request.controller.IsAvailable()) return;
        Actor controller = request.controller.GetComponent<ActorBinder>().Actor;
        if (controller == null || !controller.isAlive()) return;
        Actor spirit = World.world.units.spawnNewUnit(
            Actors.GhostFire.id,
            controller.current_tile,
            pSpawnHeight: 0.25f);
        if (spirit == null) return;

        spirit.joinKingdom(controller.kingdom);
        float levelScale = ArtifactSpiritService.ResolveLevelScale(request.state);
        ArtifactSpiritAvatar avatar = new()
        {
            artifact = request.artifact,
            controller = request.controller,
            ability_instance_id = request.binding.ability.instance_id,
            damage_ratio = Mathf.Max(0f,
                request.binding.profile.ResolveDamageRatio?.Invoke(request.binding.ability, request.state) ?? 0f) *
                           levelScale,
            health_ratio = Mathf.Max(0f,
                request.binding.profile.ResolveHealthRatio?.Invoke(request.binding.ability, request.state) ?? 0f) *
                           levelScale,
            armor_bonus = Mathf.Max(0f,
                request.binding.profile.ResolveArmorBonus?.Invoke(request.binding.ability, request.state) ?? 0f) *
                          levelScale,
            recovery_duration = Mathf.Max(1f,
                request.binding.profile.ResolveRecoveryDuration?.Invoke(request.binding.ability, request.state) ?? 30f),
            recover_on_death = true,
        };
        Entity avatarEntity = spirit.GetExtend().E;
        avatarEntity.AddComponent(avatar);
        request.artifact.AddRelation(new ArtifactSpiritAvatarRelation { avatar = avatarEntity });
        spirit.GetExtend().MarkCultiwayStatsDirty();
        spirit.restoreHealth(Mathf.RoundToInt(controller.stats[S.health] * avatar.health_ratio));

        ArtifactAbilityVisuals.Emit(
            new ArtifactAbilityExecutionContext(
                request.controller,
                request.artifact,
                request.control_state),
            request.binding.ability,
            request.binding.runtime,
            "spirit_manifest",
            spirit.current_position,
            target: spirit,
            intensity: levelScale);
    }

    private static bool IsLivingAvatar(Entity avatar)
    {
        return avatar.IsAvailable() && avatar.TryGetComponent(out ActorBinder binder) &&
               binder.Actor != null && binder.Actor.isAlive();
    }

    private readonly struct SpiritBinding
    {
        public readonly ArtifactAbilityInstance ability;
        public readonly ArtifactAbilityRuntimeEntry runtime;
        public readonly ArtifactAbilityAsset asset;
        public readonly ArtifactSpiritAbilityProfile profile;

        public SpiritBinding(
            ArtifactAbilityInstance ability,
            ArtifactAbilityRuntimeEntry runtime,
            ArtifactAbilityAsset asset,
            ArtifactSpiritAbilityProfile profile)
        {
            this.ability = ability;
            this.runtime = runtime;
            this.asset = asset;
            this.profile = profile;
        }
    }

    private readonly struct SpawnRequest
    {
        public readonly Entity artifact;
        public readonly Entity controller;
        public readonly SpiritBinding binding;
        public readonly ArtifactSpiritState state;
        public readonly ArtifactControlState control_state;

        public SpawnRequest(
            Entity artifact,
            Entity controller,
            SpiritBinding binding,
            ArtifactSpiritState state,
            ArtifactControlState controlState)
        {
            this.artifact = artifact;
            this.controller = controller;
            this.binding = binding;
            this.state = state;
            control_state = controlState;
        }
    }

    private readonly struct AvatarRequest
    {
        public readonly Entity artifact;
        public readonly Entity avatar;
        public readonly Entity controller;
        public readonly SpiritBinding binding;
        public readonly ArtifactSpiritState state;

        public AvatarRequest(Entity artifact, Entity avatar)
            : this(artifact, avatar, default, default, default)
        {
        }

        public AvatarRequest(
            Entity artifact,
            Entity avatar,
            Entity controller,
            SpiritBinding binding,
            ArtifactSpiritState state)
        {
            this.artifact = artifact;
            this.avatar = avatar;
            this.controller = controller;
            this.binding = binding;
            this.state = state;
        }
    }

    private readonly struct RelationRequest
    {
        public readonly Entity artifact;
        public readonly Entity avatar;

        public RelationRequest(Entity artifact, Entity avatar)
        {
            this.artifact = artifact;
            this.avatar = avatar;
        }
    }
}

/// <summary>当法器或驾驭者先于器灵消失时，回收失去来源的显化生物。</summary>
