using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using strings;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

/// <summary>推进化神夺舍、本相塑体并清理失效身体锁和本命法器锚点。</summary>
public sealed class YuanshenBodyRecoverySystem : BaseSystem, IWorldStateClearable
{
    /// <summary>本帧需要取消夺舍的人物。</summary>
    private readonly List<ActorExtend> cancelPossessions = new();

    /// <summary>本帧完成引导的夺舍请求。</summary>
    private readonly List<PossessionCompletion> possessionCompletions = new();

    /// <summary>本帧需要取消塑体的人物。</summary>
    private readonly List<ActorExtend> cancelReconstructions = new();

    /// <summary>本帧完成塑体的人物。</summary>
    private readonly List<ReconstructionCompletion> reconstructionCompletions = new();

    /// <summary>本帧检测到失效锚点的人物。</summary>
    private readonly List<ActorExtend> invalidAnchors = new();

    /// <summary>本帧需要移除的宿主运行组件。</summary>
    private readonly List<Entity> staleHostStates = new();

    /// <summary>推进所有已有会话，不寻找任何新宿主或锚点。</summary>
    protected override void OnUpdateGroup()
    {
        base.OnUpdateGroup();
        ClearFrameLists();
        double now = World.world?.getCurWorldTime() ?? 0d;
        CollectPossessions(now);
        CollectReconstructions(now);
        CollectInvalidAnchors();
        CollectStaleHostStates(now);
        SubmitFrameChanges();
    }

    /// <summary>世界切换时丢弃全部帧内请求。</summary>
    void IWorldStateClearable.ClearWorldState()
    {
        ClearFrameLists();
    }

    /// <summary>收集取消或到期的夺舍引导。</summary>
    private void CollectPossessions(double now)
    {
        ModClass.I.W.Query<ActorBinder, YuanshenPossessionState>().ForEachEntity((
            ref ActorBinder binder,
            ref YuanshenPossessionState state,
            Entity sourceEntity) =>
        {
            Actor sourceBase = binder.Actor;
            if (sourceBase == null || sourceBase.isRekt()) return;
            ActorExtend source = sourceBase.GetExtend();
            if (!YuanshenBodyRecoveryService.TryValidatePossession(source, state, out Actor target) ||
                sourceBase.isJustAttacked())
            {
                cancelPossessions.Add(source);
                return;
            }
            if (state.completes_at <= now)
                possessionCompletions.Add(new PossessionCompletion(source, target, state));
        });
    }

    /// <summary>结算本相塑体的时间与灵气供应。</summary>
    private void CollectReconstructions(double now)
    {
        ModClass.I.W.Query<ActorBinder, YuanshenReconstructionState>().ForEachEntity((
            ref ActorBinder binder,
            ref YuanshenReconstructionState state,
            Entity sourceEntity) =>
        {
            Actor bodyless = binder.Actor;
            if (bodyless == null || bodyless.isRekt()) return;
            ActorExtend actor = bodyless.GetExtend();
            if (!YuanshenLifecycleService.IsBodiless(actor) ||
                !YuanshenArtifactAnchorService.TryResolve(actor, out Entity anchorArtifact, out Vector3 anchorPosition) ||
                anchorArtifact.Id != state.anchor_artifact_entity_id ||
                !actor.TryGetComponent(out YuanshenArtifactAnchorState anchorState) ||
                anchorState.generation != state.anchor_token ||
                Vector2.Distance(bodyless.current_position, anchorPosition) >
                YuanshenBodyRecoveryService.PossessionRange)
            {
                cancelReconstructions.Add(actor);
                return;
            }
            if (bodyless.isJustAttacked() || bodyless.has_attack_target)
            {
                state.last_updated_at = now;
                if (now - state.last_interrupted_at < Cultiway.Const.TimeScales.SecPerMonth) return;
                state.progress *= 0.9d;
                state.last_interrupted_at = now;
                CombatStatusEffects.ApplyStatus(
                    bodyless,
                    StatusEffects.SoulTrauma,
                    Cultiway.Const.TimeScales.SecPerMonth,
                    bodyless);
                return;
            }
            double elapsed = Mathf.Clamp(
                (float)(now - state.last_updated_at),
                0f,
                Cultiway.Const.TimeScales.SecPerMonth);
            if (elapsed <= 0d || !actor.HasCultisys<Xian>()) return;
            float required = state.required_wakan * (float)(elapsed / YuanshenBodyRecoveryService.ReconstructionDuration);
            if (!WakanResourceService.TrySpend(actor, required))
            {
                state.last_updated_at = now;
                return;
            }
            state.paid_wakan += required;
            state.progress += elapsed;
            state.last_updated_at = now;
            if (state.progress >= YuanshenBodyRecoveryService.ReconstructionDuration &&
                state.paid_wakan + 0.01f >= state.required_wakan)
                reconstructionCompletions.Add(new ReconstructionCompletion(actor, state));
        });
    }

    /// <summary>收集已经失效但尚未清理的本命法器锚点。</summary>
    private void CollectInvalidAnchors()
    {
        ModClass.I.W.Query<ActorBinder, YuanshenArtifactAnchorState>().ForEachEntity((
            ref ActorBinder binder,
            ref YuanshenArtifactAnchorState anchor,
            Entity actorEntity) =>
        {
            Actor actor = binder.Actor;
            if (actor == null || actor.isRekt()) return;
            ActorExtend extend = actor.GetExtend();
            if (!YuanshenArtifactAnchorService.TryResolve(extend, out _, out _)) invalidAnchors.Add(extend);
        });
    }

    /// <summary>收集来源会话已不存在的宿主锁和到期同意。</summary>
    private void CollectStaleHostStates(double now)
    {
        ModClass.I.W.Query<ActorBinder, YuanshenBodyConsent>().ForEachEntity((
            ref ActorBinder binder,
            ref YuanshenBodyConsent consent,
            Entity entity) =>
        {
            Actor host = binder.Actor;
            if (host != null && !host.isRekt() && consent.expires_at <= now) staleHostStates.Add(entity);
        });
        ModClass.I.W.Query<ActorBinder, YuanshenBodyTransferLock>().ForEachEntity((
            ref ActorBinder binder,
            ref YuanshenBodyTransferLock bodyLock,
            Entity entity) =>
        {
            Actor host = binder.Actor;
            if (host == null || host.isRekt()) return;
            Actor source = World.world?.units?.get(bodyLock.source_actor_id);
            if (source == null || source.isRekt() ||
                !source.GetExtend().TryGetComponent(out YuanshenPossessionState state) ||
                state.token != bodyLock.token || state.target_actor_id != host.data.id)
                staleHostStates.Add(entity);
        });
    }

    /// <summary>离开所有查询后提交结构变更和完成操作。</summary>
    private void SubmitFrameChanges()
    {
        for (var i = 0; i < cancelPossessions.Count; i++)
        {
            ActorExtend actor = cancelPossessions[i];
            if (YuanshenBodyRecoveryService.CancelPossession(actor))
                CombatStatusEffects.ApplyStatus(
                    actor.Base,
                    StatusEffects.SoulTrauma,
                    3f * Cultiway.Const.TimeScales.SecPerMonth,
                    actor.Base);
        }
        for (var i = 0; i < possessionCompletions.Count; i++)
        {
            PossessionCompletion request = possessionCompletions[i];
            YuanshenBodyRecoveryService.ResolvePossession(request.Source, request.Target, request.State);
        }
        for (var i = 0; i < cancelReconstructions.Count; i++)
            YuanshenBodyRecoveryService.CancelReconstruction(cancelReconstructions[i], true);
        for (var i = 0; i < reconstructionCompletions.Count; i++)
        {
            ReconstructionCompletion request = reconstructionCompletions[i];
            if (!YuanshenBodyRecoveryService.CompleteReconstruction(request.Actor, request.State))
                YuanshenBodyRecoveryService.CancelReconstruction(request.Actor, true);
        }
        for (var i = 0; i < invalidAnchors.Count; i++)
            YuanshenArtifactAnchorService.BreakInvalidAnchor(invalidAnchors[i]);
        for (var i = 0; i < staleHostStates.Count; i++)
        {
            Entity entity = staleHostStates[i];
            if (entity.IsNull) continue;
            if (entity.HasComponent<YuanshenBodyConsent>()) entity.RemoveComponent<YuanshenBodyConsent>();
            if (entity.HasComponent<YuanshenBodyTransferLock>()) entity.RemoveComponent<YuanshenBodyTransferLock>();
        }
    }

    /// <summary>清空全部帧内集合。</summary>
    private void ClearFrameLists()
    {
        cancelPossessions.Clear();
        possessionCompletions.Clear();
        cancelReconstructions.Clear();
        reconstructionCompletions.Clear();
        invalidAnchors.Clear();
        staleHostStates.Clear();
    }

    /// <summary>冻结到查询外提交的一次夺舍完成请求。</summary>
    private readonly struct PossessionCompletion
    {
        /// <summary>无身元神。</summary>
        public readonly ActorExtend Source;

        /// <summary>宿主人物。</summary>
        public readonly Actor Target;

        /// <summary>冻结会话。</summary>
        public readonly YuanshenPossessionState State;

        /// <summary>创建夺舍完成请求。</summary>
        public PossessionCompletion(ActorExtend source, Actor target, YuanshenPossessionState state)
        {
            Source = source;
            Target = target;
            State = state;
        }
    }

    /// <summary>冻结到查询外提交的一次塑体完成请求。</summary>
    private readonly struct ReconstructionCompletion
    {
        /// <summary>无身元神。</summary>
        public readonly ActorExtend Actor;

        /// <summary>冻结塑体状态。</summary>
        public readonly YuanshenReconstructionState State;

        /// <summary>创建塑体完成请求。</summary>
        public ReconstructionCompletion(ActorExtend actor, YuanshenReconstructionState state)
        {
            Actor = actor;
            State = state;
        }
    }
}
