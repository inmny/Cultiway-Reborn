using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

/// <summary>在世界逻辑中推进煞风劫；镜头是否可见不会影响技能、伤害和结果。</summary>
public sealed class BalefulWindTribulationSystem : QuerySystem<ActorBinder, BalefulWindTribulation>
{
    private const float BaseDamageRatio = 0.15f;
    private const float DamageGrowthPerWave = 0.01f;
    private readonly ArchetypeQuery<BalefulWindTribulationSkill> skillQuery;
    private readonly HashSet<long> activeCenters = new();
    private readonly Dictionary<long, int> activeWaveMasks = new();
    private readonly List<Actor> missingCenterActors = new();
    private readonly List<Actor> dueActors = new();
    private readonly List<Actor> resolvedActors = new();
    private readonly List<Actor> failedActors = new();
    private readonly List<Entity> invalidEntities = new();
    private readonly List<long> invalidActorIds = new();

    public BalefulWindTribulationSystem(EntityStore world)
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());
        var skillFilter = new QueryFilter();
        skillFilter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());
        skillQuery = world.Query<BalefulWindTribulationSkill>(skillFilter);
    }

    protected override void OnUpdate()
    {
        double now = World.world.getCurWorldTime();
        CollectActiveSkills();
        missingCenterActors.Clear();
        dueActors.Clear();
        resolvedActors.Clear();
        failedActors.Clear();
        invalidEntities.Clear();
        invalidActorIds.Clear();

        Query.ForEachEntity((ref ActorBinder binder, ref BalefulWindTribulation tribulation, Entity entity) =>
        {
            Actor actor = binder.Actor;
            if (actor == null || actor.isRekt())
            {
                invalidEntities.Add(entity);
                invalidActorIds.Add(binder.ID);
                return;
            }
            if (!IsEligibleBody(actor))
            {
                failedActors.Add(actor);
                return;
            }
            if (tribulation.IsPassed) return;

            if (!activeCenters.Contains(actor.data.id)) missingCenterActors.Add(actor);
            if (tribulation.active_wave > 0)
            {
                if (!HasActiveWave(actor.data.id, tribulation.active_wave)) resolvedActors.Add(actor);
                return;
            }
            if (now >= tribulation.next_wave_at) dueActors.Add(actor);
        });

        for (int i = 0; i < invalidEntities.Count; i++)
        {
            BalefulWindTribulationSkillService.Cleanup(invalidActorIds[i]);
            Entity entity = invalidEntities[i];
            if (!entity.IsNull && entity.HasComponent<BalefulWindTribulation>())
                entity.RemoveComponent<BalefulWindTribulation>();
        }
        for (int i = 0; i < failedActors.Count; i++)
            BalefulWindTribulationSkillService.Fail(failedActors[i]);
        for (int i = 0; i < missingCenterActors.Count; i++) EnsureCenter(missingCenterActors[i]);
        for (int i = 0; i < resolvedActors.Count; i++) ResolveWave(resolvedActors[i]);
        for (int i = 0; i < dueActors.Count; i++) SpawnWave(dueActors[i], now);
    }

    private void CollectActiveSkills()
    {
        activeCenters.Clear();
        activeWaveMasks.Clear();
        skillQuery.ForEachEntity((ref BalefulWindTribulationSkill marker, Entity _) =>
        {
            if (marker.kind == BalefulWindTribulationSkillKind.Center)
            {
                activeCenters.Add(marker.target_actor_id);
                return;
            }
            if (marker.wave == 0 || marker.wave > BalefulWindTribulation.TotalWaves) return;

            activeWaveMasks.TryGetValue(marker.target_actor_id, out int mask);
            activeWaveMasks[marker.target_actor_id] = mask | 1 << marker.wave;
        });
    }

    private bool HasActiveWave(long actorId, int wave)
    {
        return activeWaveMasks.TryGetValue(actorId, out int mask) && (mask & 1 << wave) != 0;
    }

    private static void EnsureCenter(Actor actor)
    {
        if (!IsEligibleBody(actor)) return;
        ActorExtend actorExtend = actor.GetExtend();
        if (!actorExtend.TryGetComponent(out BalefulWindTribulation tribulation) || tribulation.IsPassed) return;
        BalefulWindTribulationSkillService.SpawnCenter(actor);
    }

    private static void SpawnWave(Actor actor, double now)
    {
        if (!IsEligibleBody(actor)) return;
        ActorExtend actorExtend = actor.GetExtend();
        if (!actorExtend.TryGetComponent(out BalefulWindTribulation current) ||
            current.IsPassed || current.active_wave > 0) return;

        int wave = Mathf.Clamp(current.waves_survived + 1, 1, BalefulWindTribulation.TotalWaves);
        float damageRatio = BaseDamageRatio + (wave - 1) * DamageGrowthPerWave;
        float totalDamage = Mathf.Max(1f, actor.getMaxHealth() * damageRatio);
        int spawned = BalefulWindTribulationSkillService.SpawnWave(actor, wave, totalDamage);
        if (spawned != wave) return;

        ref BalefulWindTribulation tribulation = ref actorExtend.GetComponent<BalefulWindTribulation>();
        tribulation.active_wave = (byte)wave;
        tribulation.next_wave_at = now + BalefulWindTribulation.WaveInterval;
    }

    private static void ResolveWave(Actor actor)
    {
        if (!IsEligibleBody(actor))
        {
            BalefulWindTribulationSkillService.Fail(actor);
            return;
        }

        ActorExtend actorExtend = actor.GetExtend();
        if (!actorExtend.TryGetComponent(out BalefulWindTribulation current) ||
            current.IsPassed || current.active_wave == 0) return;

        int wave = current.active_wave;
        ref BalefulWindTribulation tribulation = ref actorExtend.GetComponent<BalefulWindTribulation>();
        tribulation.waves_survived = (byte)Mathf.Max(tribulation.waves_survived, wave);
        tribulation.active_wave = 0;
        if (wave < BalefulWindTribulation.TotalWaves) return;

        tribulation.outcome = BalefulWindTribulationOutcome.Passed;
        BalefulWindTribulationSkillService.Cleanup(actorExtend);
        WorldLogUtils.LogBalefulWindTribulationSurvived(actor);
        ShowCompletion(actor);
    }

    internal static bool IsEligibleBody(Actor actor)
    {
        if (actor == null || actor.isRekt() || !actor.isAlive() || actor.data.health <= 0) return false;
        ActorExtend actorExtend = actor.GetExtend();
        return !actorExtend.HasComponent<YuanyingSoulState>()
               && actorExtend.TryGetComponent(out Xian xian)
               && xian.CurrLevel == XianLevels.Yuanying
               && actorExtend.TryGetComponent(out Yuanying yuanying)
               && yuanying.stage >= Cultisyses.MaximumYuanyingStage;
    }

    private static void ShowCompletion(Actor actor)
    {
        if (!MapBox.isRenderGameplay() || !actor.is_visible) return;
        float scale = Mathf.Max(0.35f, actor.actor_scale);
        EffectsLibrary.spawnAt("fx_teleport_singularity", actor.current_position, scale * 0.9f);
        EffectsLibrary.spawnAt("fx_cast_ground_blue", actor.current_position, scale * 1.4f);
    }
}
