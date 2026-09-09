using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV3.Systems;

public class LogicSkillCastSequenceSystem : QuerySystem<SkillCastSequence>
{
    private readonly List<SpawnSkillRequest> _spawnRequests = new();
    private readonly List<SkillCastCompletedRequest> _completedRequests = new();
    private readonly List<SkillCastSequenceEndRequest> _endRequests = new();

    public LogicSkillCastSequenceSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagRecycle>());
    }

    protected override void OnUpdate()
    {
        var dt = Tick.deltaTime;
        _spawnRequests.Clear();
        _completedRequests.Clear();
        _endRequests.Clear();
        Query.ForEachEntity((ref SkillCastSequence sequence, Entity entity) =>
        {
            if (!IsSequenceValid(ref sequence))
            {
                QueueEnd(ref sequence, SkillCastSequenceEndReason.Invalidated);
                CommandBuffer.AddTag<TagRecycle>(entity.Id);
                return;
            }

            sequence.Elapsed += dt;
            var emitted = 0;
            var maxEmitPerTick = sequence.MaxEmitPerTick <= 0 ? 1 : sequence.MaxEmitPerTick;
            bool ended = false;
            while (sequence.NextIndex < sequence.Steps.Length && emitted < maxEmitPerTick)
            {
                SkillCastStep scheduledStep = sequence.Steps[sequence.NextIndex];
                if (scheduledStep.Delay > sequence.Elapsed) break;

                SkillCastStepDecision decision = sequence.Options?.Hooks == null
                    ? SkillCastStepDecision.Emit(scheduledStep)
                    : sequence.Options.Hooks.PrepareStep(
                        new SkillCastSequenceStepContext(
                            sequence.Caster,
                            sequence.SkillContainer,
                            sequence.NextIndex,
                            sequence.EmittedCount,
                            sequence.Elapsed,
                            sequence.RuntimeData),
                        scheduledStep);

                if (decision.Kind == SkillCastStepDecisionKind.Defer) break;
                if (decision.Kind == SkillCastStepDecisionKind.Cancel)
                {
                    QueueEnd(ref sequence, SkillCastSequenceEndReason.Cancelled);
                    CommandBuffer.AddTag<TagRecycle>(entity.Id);
                    ended = true;
                    break;
                }

                sequence.NextIndex++;
                if (decision.Kind == SkillCastStepDecisionKind.Skip) continue;

                SkillCastStep step = decision.Step;
                if (step.TrackTarget && step.Target.isRekt()) continue;

                if (sequence.Options?.PaymentTiming == SkillCastPaymentTiming.PerEmission &&
                    !SkillCastCost.TryPayStep(
                        sequence.Caster,
                        sequence.SkillContainer,
                        sequence.FundingSource))
                {
                    QueueEnd(ref sequence, SkillCastSequenceEndReason.InsufficientResource);
                    CommandBuffer.AddTag<TagRecycle>(entity.Id);
                    ended = true;
                    break;
                }

                _spawnRequests.Add(new SpawnSkillRequest
                {
                    SkillContainer = sequence.SkillContainer,
                    Sourceless = sequence.Sourceless,
                    Source = sequence.Sourceless ? null : sequence.Caster.Base,
                    SpatialSource = sequence.Sourceless
                        ? null
                        : (sequence.Carrier ?? sequence.Caster).Base,
                    SourcePos = step.HasSourcePosition
                        ? step.SourcePos
                        : (sequence.Carrier ?? sequence.Caster).Base.GetSimPos(),
                    HasSourcePosition = step.HasSourcePosition,
                    Target = step.Target,
                    TargetPos = step.TrackTarget ? step.Target.GetSimPos() : step.TargetPos,
                    Strength = sequence.Strength,
                    PowerLevel = sequence.PowerLevel,
                    InitialAngleOffsetDegrees = step.InitialAngleOffsetDegrees,
                    AttackKingdom = sequence.AttackKingdom,
                    RuntimeData = sequence.RuntimeData,
                });
                sequence.EmittedCount++;
                emitted++;
            }

            if (!ended && sequence.NextIndex >= sequence.Steps.Length)
            {
                if (sequence.EmittedCount > 0 && !sequence.Sourceless)
                {
                    _completedRequests.Add(new SkillCastCompletedRequest
                    {
                        Caster = sequence.Caster,
                        SkillContainer = sequence.SkillContainer,
                        EmittedCount = sequence.EmittedCount,
                        FundingSource = sequence.FundingSource,
                        RuntimeData = sequence.RuntimeData,
                        CasterContext = sequence.CasterContext,
                    });
                }
                QueueEnd(ref sequence, SkillCastSequenceEndReason.Completed);
                CommandBuffer.AddTag<TagRecycle>(entity.Id);
            }
        });
        // 完成回调允许创建状态、回响技能等实体，必须离开查询迭代后再执行。
        foreach (var request in _completedRequests)
        {
            if (request.Caster?.Base == null || request.Caster.Base.isRekt()) continue;
            using SkillCasterContextService.Scope scope =
                SkillCasterContextService.Enter(in request.CasterContext);
            EventSystemHub.TryPublish(new SkillCastCompletedEvent(
                request.Caster,
                request.SkillContainer,
                request.EmittedCount,
                request.FundingSource,
                request.RuntimeData));
            request.Caster.OnSkillCastCompleted(
                request.SkillContainer,
                request.EmittedCount,
                request.FundingSource);
        }
        foreach (var request in _spawnRequests)
        {
            if (request.SkillContainer.IsNull ||
                !request.Sourceless && request.Source.isRekt()) continue;

            if (request.Sourceless)
            {
                ModClass.I.SkillV3.SpawnSourcelessSkill(
                    request.SkillContainer,
                    request.SourcePos,
                    request.Target,
                    request.TargetPos,
                    request.Strength,
                    request.PowerLevel,
                    initial_angle_offset_degrees: request.InitialAngleOffsetDegrees,
                    attack_kingdom: request.AttackKingdom,
                    runtime_data: request.RuntimeData);
                continue;
            }

            if (request.HasSourcePosition)
            {
                ModClass.I.SkillV3.SpawnSkillAtPosition(
                    request.SkillContainer,
                    request.Source,
                    request.SourcePos,
                    request.Target,
                    request.TargetPos,
                    request.Strength,
                    request.PowerLevel,
                    request.InitialAngleOffsetDegrees,
                    request.AttackKingdom,
                    request.RuntimeData,
                    request.SpatialSource);
                continue;
            }
            ModClass.I.SkillV3.SpawnSkill(request.SkillContainer, request.Source, request.SpatialSource,
                request.Target, request.TargetPos, request.Strength, power_level: request.PowerLevel,
                initial_angle_offset_degrees: request.InitialAngleOffsetDegrees,
                attack_kingdom: request.AttackKingdom,
                runtime_data: request.RuntimeData);
        }
        // 结束钩子可能创建返程动画或清理角色侧组件，必须在查询和最后一批实体生成之后执行。
        foreach (SkillCastSequenceEndRequest request in _endRequests)
        {
            request.Hooks?.OnEnded(request.Result);
        }
        CommandBuffer.Playback();
    }

    /// <summary>缓存一次序列结束通知，延迟到 ECS 查询和实体生成阶段之后执行。</summary>
    private void QueueEnd(ref SkillCastSequence sequence, SkillCastSequenceEndReason reason)
    {
        ModClass.I.SkillV3.ReleaseSkillReservation(sequence.Caster, sequence.SkillContainer);
        if (sequence.Options?.Hooks == null) return;
        _endRequests.Add(new SkillCastSequenceEndRequest
        {
            Hooks = sequence.Options.Hooks,
            Result = new SkillCastSequenceResult(
                sequence.Caster,
                sequence.SkillContainer,
                sequence.EmittedCount,
                sequence.NextIndex,
                reason,
                sequence.RuntimeData),
        });
    }

    private static bool IsSequenceValid(ref SkillCastSequence sequence)
    {
        if (sequence.Caster == null) return false;
        if (sequence.Caster.Base == null || sequence.Caster.Base.isRekt()) return false;
        ActorExtend carrier = sequence.Carrier ?? sequence.Caster;
        if (carrier.Base == null || carrier.Base.isRekt()) return false;
        if (sequence.SkillContainer.IsNull) return false;
        return sequence.Steps != null && sequence.Steps.Length > 0;
    }

    private struct SpawnSkillRequest
    {
        public Entity SkillContainer;
        public bool Sourceless;
        public BaseSimObject Source;
        public BaseSimObject SpatialSource;
        public UnityEngine.Vector3 SourcePos;
        public bool HasSourcePosition;
        public BaseSimObject Target;
        public UnityEngine.Vector3 TargetPos;
        public float Strength;
        public float PowerLevel;
        public float InitialAngleOffsetDegrees;
        public Kingdom AttackKingdom;
        public SkillCastRuntimeData RuntimeData;
    }

    private struct SkillCastCompletedRequest
    {
        public ActorExtend Caster;
        public Entity SkillContainer;
        public int EmittedCount;
        public SkillCastFundingSource FundingSource;
        public SkillCastRuntimeData RuntimeData;
        /// <summary>完成回调需要恢复的冻结载体上下文。</summary>
        public SkillCasterContext CasterContext;
    }

    /// <summary>离开查询后执行的内容侧序列结束通知。</summary>
    private struct SkillCastSequenceEndRequest
    {
        public ISkillCastSequenceHooks Hooks;
        public SkillCastSequenceResult Result;
    }
}
