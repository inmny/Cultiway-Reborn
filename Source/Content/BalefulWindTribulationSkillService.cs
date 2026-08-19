using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>创建、命中并清理煞风劫专用的无施法者技能。</summary>
internal static class BalefulWindTribulationSkillService
{
    private const float WaveSpawnRadius = 8f;
    private static Entity centerContainer;
    private static Entity waveContainer;
    private static bool initialized;

    public static void Initialize()
    {
        if (initialized) return;

        centerContainer = new SkillContainerBuilder(SkillEntities.BalefulWindTribulationCenter)
            .UseAnimation(0)
            .Build(SkillContainerBuildMode.SourceGranted);
        waveContainer = new SkillContainerBuilder(SkillEntities.BalefulWindTribulationWave)
            .UseAnimation(0)
            .Build(SkillContainerBuildMode.SourceGranted);
        ActorExtend.RegisterActionOnDeath(OnActorDeath);
        initialized = true;
    }

    public static Entity SpawnCenter(Actor actor)
    {
        if (actor == null || actor.isRekt()) return default;
        Initialize();

        Vector3 targetPosition = actor.GetSimPos();
        Entity skill = ModClass.I.SkillV3.SpawnSourcelessSkill(
            centerContainer,
            targetPosition,
            actor,
            targetPosition,
            0f,
            GetHuashenPowerLevel(),
            runtime_data: SkillCastRuntimeData.Create(1f, DamageOrigin.Primary));
        skill.AddComponent(new BalefulWindTribulationSkill
        {
            target_actor_id = actor.data.id,
            wave = 0,
            kind = BalefulWindTribulationSkillKind.Center
        });
        return skill;
    }

    public static int SpawnWave(Actor actor, int wave, float totalDamage)
    {
        if (actor == null || actor.isRekt() || wave <= 0) return 0;
        Initialize();

        int count = Mathf.Clamp(wave, 1, BalefulWindTribulation.TotalWaves);
        float damagePerTornado = Mathf.Max(0f, totalDamage) / count;
        float angleOffset = ((actor.data.id % 360L) + wave * 137.5f) * Mathf.Deg2Rad;
        float angleStep = Mathf.PI * 2f / count;
        Vector3 targetPosition = actor.GetSimPos();
        float powerLevel = GetHuashenPowerLevel();

        for (int i = 0; i < count; i++)
        {
            float angle = angleOffset + i * angleStep;
            Vector3 outward = new(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector3 sourcePosition = targetPosition + outward * WaveSpawnRadius;
            Entity skill = ModClass.I.SkillV3.SpawnSourcelessSkill(
                waveContainer,
                sourcePosition,
                actor,
                targetPosition,
                damagePerTornado,
                powerLevel,
                runtime_data: SkillCastRuntimeData.Create(1f, DamageOrigin.Primary));
            skill.AddComponent(new BalefulWindTribulationSkill
            {
                target_actor_id = actor.data.id,
                wave = (byte)count,
                kind = BalefulWindTribulationSkillKind.Wave
            });
        }

        return count;
    }

    internal static bool ResolveWaveImpact(ref SkillContext context, Entity skillContainer, Entity skillEntity,
        BaseSimObject target)
    {
        if (target?.isActor() != true) return SkillHitResolver.ResolveProfile(
            ref context, skillContainer, skillEntity, target);

        ActorExtend targetExtend = target.a.GetExtend();
        if (targetExtend.HasComponent<YuanyingSoulState>())
        {
            ModClass.I.CommandBuffer.AddTag<TagRecycle>(skillEntity.Id);
            return false;
        }

        Vector3 direction3 = skillEntity.GetComponent<Rotation>().value;
        Vector2 direction = new(direction3.x, direction3.y);
        if (direction.sqrMagnitude < 0.0001f)
            direction = new Vector2(context.TargetDir.x, context.TargetDir.y);
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
        direction.Normalize();

        int wave = skillEntity.TryGetComponent(out BalefulWindTribulationSkill marker)
            ? marker.wave
            : 1;
        float force = 2.2f + wave * 0.18f;
        targetExtend.GetForce(
            null,
            direction.x * force,
            direction.y * force,
            0f,
            context.PowerLevel);
        return SkillHitResolver.ResolveProfile(ref context, skillContainer, skillEntity, target);
    }

    public static void Fail(Actor actor)
    {
        if (actor == null) return;
        Cleanup(actor.data.id);
        if (!actor.TryGetExtend(out ActorExtend actorExtend) ||
            !actorExtend.TryGetComponent(out BalefulWindTribulation tribulation)) return;

        if (!tribulation.IsPassed) WorldLogUtils.LogBalefulWindTribulationFailed(actor);
        actorExtend.E.RemoveComponent<BalefulWindTribulation>();
    }

    public static void Cleanup(ActorExtend actor)
    {
        if (actor?.Base == null) return;
        Cleanup(actor.Base.data.id);
    }

    public static void Cleanup(long actorId)
    {
        var pending = new List<Entity>();
        ModClass.I.W.Query<BalefulWindTribulationSkill>().ForEachEntity(
            (ref BalefulWindTribulationSkill marker, Entity skill) =>
            {
                if (marker.target_actor_id == actorId) pending.Add(skill);
            });

        for (int i = 0; i < pending.Count; i++)
        {
            Entity skill = pending[i];
            if (!skill.IsNull && !skill.Tags.Has<TagRecycle>()) skill.AddTag<TagRecycle>();
        }
    }

    private static void OnActorDeath(ActorExtend actor)
    {
        if (actor?.Base == null) return;
        bool failed = actor.TryGetComponent(out BalefulWindTribulation tribulation) && !tribulation.IsPassed;
        Cleanup(actor);
        if (!actor.HasComponent<BalefulWindTribulation>()) return;

        if (failed) WorldLogUtils.LogBalefulWindTribulationFailed(actor.Base);
        actor.E.RemoveComponent<BalefulWindTribulation>();
    }

    private static float GetHuashenPowerLevel()
    {
        return Cultisyses.Xian.GetLevelPower(XianLevels.Huashen);
    }
}
