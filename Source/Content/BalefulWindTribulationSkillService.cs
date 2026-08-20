using System.Collections.Generic;
using System.Threading;
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

/// <summary>用一个无施法者序列驱动煞风劫的生成、命中与清理。</summary>
internal static class BalefulWindTribulationSkillService
{
    private const int TotalWaves = 9;

    private const float InitialDelay = 1f;
    private const float EmissionInterval = 0.25f;
    private const float WaveSpawnRadius = 10f;
    private const float BaseDamageRatio = 0.15f;
    private const float DamageGrowthPerWave = 0.01f;
    private const int TotalEmissionCount = TotalWaves * (TotalWaves + 1) / 2;

    private static readonly Dictionary<long, TribulationSequenceSession> sessionsByActor = new();
    private static readonly Dictionary<long, TribulationSequenceSession> sessionsByCorrelation = new();
    private static readonly List<TribulationSequenceSession> sessionsToUpdate = new();
    private static Entity centerContainer;
    private static Entity waveContainer;
    private static long nextCorrelationId;
    private static bool deathHookRegistered;
    private static bool worldClearHookRegistered;

    public static void Initialize()
    {
        if (centerContainer.IsNull)
        {
            centerContainer = new SkillContainerBuilder(SkillEntities.BalefulWindTribulationCenter)
                .UseAnimation(0)
                .Build(SkillContainerBuildMode.SourceGranted);
            SkillContainer container = centerContainer.GetComponent<SkillContainer>();
            container.OnSetup += PrepareCenterEntity;
            container.OnTravel += PinActorFromCenter;
            centerContainer.GetComponent<SkillContainer>() = container;
        }
        if (waveContainer.IsNull)
        {
            waveContainer = new SkillContainerBuilder(SkillEntities.BalefulWindTribulationWave)
                .UseAnimation(0)
                .Build(SkillContainerBuildMode.SourceGranted);
            SkillContainer container = waveContainer.GetComponent<SkillContainer>();
            container.OnSetup += RegisterWindEntity;
            waveContainer.GetComponent<SkillContainer>() = container;
        }
        if (!deathHookRegistered)
        {
            ActorExtend.RegisterActionOnDeath(OnActorDeath);
            deathHookRegistered = true;
        }
        if (worldClearHookRegistered) return;

        Cultiway.Patch.PatchMapBox.RegisterActionOnClearWorld(ClearWorldState);
        worldClearHookRegistered = true;
    }

    public static bool Start(ActorExtend actor)
    {
        if (actor?.Base == null || actor.Base.isRekt()) return false;
        Initialize();
        if (sessionsByActor.ContainsKey(actor.Base.data.id)) return false;

        long correlationId = Interlocked.Increment(ref nextCorrelationId);
        var session = new TribulationSequenceSession(
            actor.Base,
            actor.Base.current_position,
            correlationId);
        SkillCastPlan plan = session.CreatePlan();
        SkillCastRuntimeData runtimeData = SkillCastRuntimeData.Create(1f, DamageOrigin.Primary);
        runtimeData.CorrelationId = correlationId;
        Entity sequence = ModClass.I.SkillV3.StartSourcelessSkillSequence(
            actor,
            waveContainer,
            plan,
            1f,
            GetHuashenPowerLevel(),
            runtime_data: runtimeData,
            options: new SkillCastSequenceOptions
            {
                Hooks = session,
                MaxEmitPerTick = 1
            });
        if (sequence.IsNull) return false;

        session.BindSequence(sequence);
        return true;
    }

    public static bool IsInProgress(ActorExtend actor)
    {
        return TryGetSession(actor, out TribulationSequenceSession session) && session.IsInProgress;
    }

    public static bool IsPassed(ActorExtend actor)
    {
        return TryGetSession(actor, out TribulationSequenceSession session) && session.IsPassed;
    }

    public static bool TryGetProgress(ActorExtend actor, out int wavesSurvived, out int totalWaves)
    {
        totalWaves = TotalWaves;
        if (!TryGetSession(actor, out TribulationSequenceSession session))
        {
            wavesSurvived = 0;
            return false;
        }

        wavesSurvived = session.WavesSurvived;
        return true;
    }

    public static void UpdateAll()
    {
        sessionsToUpdate.Clear();
        foreach (TribulationSequenceSession session in sessionsByActor.Values)
        {
            if (session.IsInProgress) sessionsToUpdate.Add(session);
        }
        for (int i = 0; i < sessionsToUpdate.Count; i++)
        {
            sessionsToUpdate[i].Update();
        }
    }

    internal static bool ResolveWaveImpact(ref SkillContext context, Entity skillContainer, Entity skillEntity,
        BaseSimObject target)
    {
        if (!sessionsByCorrelation.TryGetValue(
                context.RuntimeData.CorrelationId,
                out TribulationSequenceSession session))
        {
            ModClass.I.CommandBuffer.AddTag<TagRecycle>(skillEntity.Id);
            return false;
        }
        return session.ResolveImpact(ref context, skillContainer, skillEntity, target);
    }

    public static void Fail(Actor actor)
    {
        if (actor == null || !sessionsByActor.TryGetValue(
                actor.data.id,
                out TribulationSequenceSession session)) return;

        bool shouldLog = session.IsInProgress;
        session.Close();
        if (shouldLog) WorldLogUtils.LogBalefulWindTribulationFailed(actor);
    }

    public static void Cleanup(ActorExtend actor)
    {
        if (actor?.Base == null) return;
        Cleanup(actor.Base.data.id);
    }

    public static void Cleanup(long actorId)
    {
        if (sessionsByActor.TryGetValue(actorId, out TribulationSequenceSession session))
            session.Close();
    }

    private static bool TryGetSession(ActorExtend actor, out TribulationSequenceSession session)
    {
        session = null;
        return actor?.Base != null && sessionsByActor.TryGetValue(actor.Base.data.id, out session);
    }

    private static void ClearWorldState()
    {
        sessionsToUpdate.Clear();
        sessionsByCorrelation.Clear();
        sessionsByActor.Clear();
    }

    private static void PrepareCenterEntity(Entity skillEntity)
    {
        if (!skillEntity.Tags.Has<TagHasOnTravel>()) skillEntity.AddTag<TagHasOnTravel>();
    }

    private static void PinActorFromCenter(Entity skillEntity)
    {
        SkillContext context = skillEntity.GetComponent<SkillContext>();
        if (sessionsByCorrelation.TryGetValue(
                context.RuntimeData.CorrelationId,
                out TribulationSequenceSession session))
            session.PinActor();
    }

    private static void RegisterWindEntity(Entity skillEntity)
    {
        SkillContext context = skillEntity.GetComponent<SkillContext>();
        if (!sessionsByCorrelation.TryGetValue(
                context.RuntimeData.CorrelationId,
                out TribulationSequenceSession session))
        {
            ModClass.I.CommandBuffer.AddTag<TagRecycle>(skillEntity.Id);
            return;
        }
        session.RegisterWind(skillEntity);
    }

    private static void OnActorDeath(ActorExtend actor)
    {
        if (actor?.Base != null) Fail(actor.Base);
    }

    private static bool IsEligibleBody(Actor actor)
    {
        if (actor == null || actor.isRekt() || !actor.isAlive() || actor.data.health <= 0) return false;
        ActorExtend actorExtend = actor.GetExtend();
        return !actorExtend.HasComponent<YuanyingSoulState>()
               && actorExtend.TryGetComponent(out Xian xian)
               && xian.CurrLevel == XianLevels.Yuanying
               && actorExtend.TryGetComponent(out Yuanying yuanying)
               && yuanying.stage >= Cultisyses.MaximumYuanyingStage;
    }

    private static float GetHuashenPowerLevel()
    {
        return Cultisyses.Xian.GetLevelPower(XianLevels.Huashen);
    }

    private static void ShowCompletion(Actor actor)
    {
        if (!MapBox.isRenderGameplay() || !actor.is_visible) return;
        float scale = Mathf.Max(0.35f, actor.actor_scale);
        EffectsLibrary.spawnAt("fx_teleport_singularity", actor.current_position, scale * 0.9f);
        EffectsLibrary.spawnAt("fx_cast_ground_blue", actor.current_position, scale * 1.4f);
    }

    private sealed class TribulationSequenceSession : ISkillCastSequenceHooks
    {
        private readonly Actor actor;
        private readonly Vector3 anchor;
        private readonly long correlationId;
        private readonly Dictionary<Entity, int> pendingWinds = new();
        private readonly List<Entity> staleWinds = new();
        private readonly int[] resolvedByWave = new int[TotalWaves + 1];
        private readonly float[] damagePerWind = new float[TotalWaves + 1];
        private Entity sequence;
        private Entity center;
        private int assignedEmissionCount;
        private int wavesSurvived;
        private bool emissionsEnded;
        private bool aborted;
        private bool completed;

        public bool IsInProgress => !aborted && !completed;
        public bool IsPassed => completed;
        public int WavesSurvived => wavesSurvived;

        public TribulationSequenceSession(Actor actor, Vector3 anchor, long correlationId)
        {
            this.actor = actor;
            this.anchor = anchor;
            this.correlationId = correlationId;
        }

        public void BindSequence(Entity sequenceEntity)
        {
            sequence = sequenceEntity;
        }

        public SkillCastPlan CreatePlan()
        {
            var plan = new SkillCastPlan();
            float delay = InitialDelay;
            int actorAngle = (int)(actor.data.id % 360L);
            for (int wave = 1; wave <= TotalWaves; wave++)
            {
                float angleOffset = (actorAngle + wave * 137.5f) * Mathf.Deg2Rad;
                float angleStep = Mathf.PI * 2f / wave;
                for (int index = 0; index < wave; index++)
                {
                    float angle = angleOffset + index * angleStep;
                    Vector3 source = anchor + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * WaveSpawnRadius;
                    plan.Steps.Add(SkillCastStep.FromSource(source, actor, anchor, delay));
                    delay += EmissionInterval;
                }
            }
            return plan;
        }

        public bool CanStart(in SkillCastSequenceStartContext context)
        {
            return !aborted && IsActorInProgress() && !sessionsByActor.ContainsKey(actor.data.id);
        }

        public void OnStarted(in SkillCastSequenceStartContext context)
        {
            sessionsByActor.Add(actor.data.id, this);
            sessionsByCorrelation.Add(correlationId, this);
            SpawnCenter();
            PinActor();
        }

        public SkillCastStepDecision PrepareStep(
            in SkillCastSequenceStepContext context,
            in SkillCastStep scheduledStep)
        {
            return aborted || !IsActorInProgress()
                ? SkillCastStepDecision.Cancel()
                : SkillCastStepDecision.Emit(scheduledStep);
        }

        public void OnEnded(in SkillCastSequenceResult result)
        {
            if (aborted) return;
            if (result.Reason != SkillCastSequenceEndReason.Completed)
            {
                Fail(actor);
                return;
            }
            emissionsEnded = true;
            TryComplete();
        }

        public void RegisterWind(Entity skillEntity)
        {
            if (aborted || assignedEmissionCount >= TotalEmissionCount)
            {
                ModClass.I.CommandBuffer.AddTag<TagRecycle>(skillEntity.Id);
                return;
            }

            int wave = ResolveWave(assignedEmissionCount++);
            if (damagePerWind[wave] <= 0f)
            {
                float ratio = BaseDamageRatio + (wave - 1) * DamageGrowthPerWave;
                damagePerWind[wave] = Mathf.Max(1f, actor.getMaxHealth() * ratio) / wave;
            }
            skillEntity.GetComponent<SkillContext>().Strength = damagePerWind[wave];
            pendingWinds.Add(skillEntity, wave);
        }

        public bool ResolveImpact(ref SkillContext context, Entity skillContainer, Entity skillEntity,
            BaseSimObject target)
        {
            if (aborted || target?.isActor() != true || target.a != actor ||
                !pendingWinds.TryGetValue(skillEntity, out int wave) || !IsActorInProgress())
            {
                RemoveWind(skillEntity, false);
                ModClass.I.CommandBuffer.AddTag<TagRecycle>(skillEntity.Id);
                return false;
            }

            bool continueAfterHit = SkillHitResolver.ResolveProfile(
                ref context, skillContainer, skillEntity, target);
            SkillEntityAsset asset = skillEntity.GetComponent<SkillEntity>().Asset;
            float damage = context.Strength * context.EffectScale *
                           asset.ImpactProfile.DamageMultiplier * asset.ImpactTuning.DamageMultiplier;
            if (damage > 0f)
            {
                ElementComposition element = context.ResolveElement(asset.Element);
                actor.GetExtend().GetHit(
                    damage,
                    ref element,
                    null,
                    AttackType.Other,
                    attacker_power_level_override: context.PowerLevel);
            }

            if (!IsActorInProgress())
            {
                Fail(actor);
                return continueAfterHit;
            }

            ResolveWind(skillEntity, wave);
            return continueAfterHit;
        }

        public void PinActor()
        {
            if (aborted || completed || !IsActorInProgress()) return;
            actor.stopMovement();
            actor.current_position = new Vector2(anchor.x, anchor.y);
            actor.dirty_current_tile = true;
            actor.velocity = Vector3.zero;
            actor.velocity_speed = 0f;
            actor.under_forces = false;
        }

        public void Update()
        {
            if (aborted || completed) return;
            if (!IsActorInProgress())
            {
                Fail(actor);
                return;
            }
            PinActor();
            if (!IsCenterOperational()) SpawnCenter();
            staleWinds.Clear();
            foreach (KeyValuePair<Entity, int> entry in pendingWinds)
            {
                Entity wind = entry.Key;
                if (wind.IsNull || wind.Tags.Has<TagRecycle>()) staleWinds.Add(wind);
            }
            for (int i = 0; i < staleWinds.Count; i++)
            {
                if (!pendingWinds.ContainsKey(staleWinds[i])) continue;
                Fail(actor);
                return;
            }
            TryComplete();
        }

        public void Close()
        {
            if (aborted) return;
            aborted = true;
            Recycle(sequence);
            Recycle(center);
            foreach (Entity wind in pendingWinds.Keys)
            {
                Recycle(wind);
            }
            pendingWinds.Clear();
            Unregister();
        }

        private void SpawnCenter()
        {
            Recycle(center);
            SkillCastRuntimeData centerRuntimeData = SkillCastRuntimeData.Create(1f, DamageOrigin.Primary);
            centerRuntimeData.CorrelationId = correlationId;
            center = ModClass.I.SkillV3.SpawnSourcelessSkill(
                centerContainer,
                anchor,
                null,
                anchor,
                0f,
                GetHuashenPowerLevel(),
                runtime_data: centerRuntimeData);
            center.GetComponent<AliveTimeLimit>().value = float.MaxValue;
        }

        private bool IsCenterOperational()
        {
            if (center.IsNull || center.Tags.Has<TagRecycle>()) return false;
            if (!center.TryGetComponent(out SkillAnimationLifecycleState lifecycle)) return true;
            return lifecycle.Phase is SkillAnimationPhase.Appearance or SkillAnimationPhase.Runtime;
        }

        private void ResolveWind(Entity skillEntity, int wave)
        {
            RemoveWind(skillEntity, true);
            resolvedByWave[wave]++;
            AdvanceWaves();
            TryComplete();
        }

        private void RemoveWind(Entity skillEntity, bool recycle)
        {
            pendingWinds.Remove(skillEntity);
            if (recycle) Recycle(skillEntity);
        }

        private void AdvanceWaves()
        {
            while (wavesSurvived < TotalWaves &&
                   resolvedByWave[wavesSurvived + 1] >= wavesSurvived + 1)
            {
                wavesSurvived++;
            }
        }

        private void TryComplete()
        {
            if (aborted || completed || !emissionsEnded || pendingWinds.Count > 0 ||
                wavesSurvived < TotalWaves) return;

            completed = true;
            Recycle(center);
            UnregisterCorrelation();
            WorldLogUtils.LogBalefulWindTribulationSurvived(actor);
            ShowCompletion(actor);
        }

        private bool IsActorInProgress()
        {
            return !aborted && !completed && IsEligibleBody(actor);
        }

        private void Unregister()
        {
            if (sessionsByActor.TryGetValue(actor.data.id, out TribulationSequenceSession actorSession) &&
                ReferenceEquals(actorSession, this))
                sessionsByActor.Remove(actor.data.id);
            UnregisterCorrelation();
        }

        private void UnregisterCorrelation()
        {
            if (sessionsByCorrelation.TryGetValue(correlationId, out TribulationSequenceSession correlationSession) &&
                ReferenceEquals(correlationSession, this))
                sessionsByCorrelation.Remove(correlationId);
        }

        private static int ResolveWave(int emissionIndex)
        {
            int remaining = emissionIndex;
            for (int wave = 1; wave <= TotalWaves; wave++)
            {
                if (remaining < wave) return wave;
                remaining -= wave;
            }
            return TotalWaves;
        }

        private static void Recycle(Entity entity)
        {
            if (!entity.IsNull && !entity.Tags.Has<TagRecycle>()) entity.AddTag<TagRecycle>();
        }
    }
}
