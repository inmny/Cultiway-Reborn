using System;
using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Systems;

/// <summary>先取得本轮序列，再在遍历之外逐步受理和发射；实际成功后才计数及通知。</summary>
public class LogicSkillCastSequenceSystem : QuerySystem<SkillCastSequence>
{
    /// <summary>本轮已有序列的快照，钩子新建的序列留到下一轮。</summary>
    private readonly List<Entity> sequences = new();

    /// <summary>回收中的序列不再推进。</summary>
    public LogicSkillCastSequenceSystem() => Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagRecycle>());

    /// <summary>收集时不执行内容钩子，离开实体遍历后才允许创建、支付和结束。</summary>
    protected override void OnUpdate()
    {
        sequences.Clear();
        Query.ForEachEntity((ref SkillCastSequence sequence, Entity entity) => sequences.Add(entity));
        int generation = ModClass.I.SkillV3.CastGeneration;
        foreach (Entity entity in sequences)
        {
            if (generation != ModClass.I.SkillV3.CastGeneration) break;
            if (!entity.IsAvailable() || !entity.HasComponent<SkillCastSequence>()) continue;
            Advance(entity, entity.GetComponent<SkillCastSequence>(), Tick.deltaTime);
        }
        CommandBuffer.Playback();
    }

    /// <summary>按原世界秒数推进；每次真实受理后保存计数，结束通知在最后一次发射之后。</summary>
    /// <param name="entity">本次序列实体。</param><param name="sequence">独占的本轮数值，避免钩子迁移实体内存后继续持有旧引用。</param>
    /// <param name="seconds">本轮实际世界时间。</param>
    private static void Advance(Entity entity, SkillCastSequence sequence, float seconds)
    {
        Manager manager = ModClass.I.SkillV3;
        SkillCastSequenceEndReason? ending = null;
        bool advanced = false;
        try
        {
            if (!IsValid(sequence)) { ending = SkillCastSequenceEndReason.Invalidated; return; }
            using SkillCasterContextService.Scope scope = SkillCasterContextService.Enter(in sequence.CasterContext);
            sequence.Elapsed += seconds;
            int emitted = 0;
            while (sequence.NextIndex < sequence.Steps.Length && emitted < Math.Max(1, sequence.MaxEmitPerTick))
            {
                if (!IsValid(sequence)) { ending = SkillCastSequenceEndReason.Invalidated; return; }
                SkillCastStep scheduled = sequence.Steps[sequence.NextIndex];
                if (scheduled.Delay > sequence.Elapsed) break;
                SkillCastStepDecision decision = sequence.Options?.Hooks?.PrepareStep(
                    new SkillCastSequenceStepContext(sequence.Caster, sequence.SkillContainer, sequence.NextIndex,
                        sequence.EmittedCount, sequence.Elapsed, sequence.RuntimeData), scheduled) ?? SkillCastStepDecision.Emit(scheduled);
                if (decision.Kind == SkillCastStepDecisionKind.Defer) break;
                if (decision.Kind == SkillCastStepDecisionKind.Cancel) { ending = SkillCastSequenceEndReason.Cancelled; return; }
                sequence.NextIndex++;
                if (decision.Kind == SkillCastStepDecisionKind.Skip || decision.Step.TrackTarget && decision.Step.Target.isRekt()) continue;
                SkillCastStep step = decision.Step;
                using SkillCastPayment payment = sequence.Options?.PaymentTiming == SkillCastPaymentTiming.PerEmission
                    ? SkillCastCost.TryBeginStep(sequence.Caster, sequence.SkillContainer, sequence.FundingSource) : null;
                if (sequence.Options?.PaymentTiming == SkillCastPaymentTiming.PerEmission && payment == null)
                { ending = SkillCastSequenceEndReason.InsufficientResource; return; }
                if (!IsValid(sequence)) { ending = SkillCastSequenceEndReason.Invalidated; return; }
                Vector3 source = step.HasSourcePosition ? step.SourcePos : (sequence.Carrier ?? sequence.Caster).Base.GetSimPos();
                Vector3 position = default;
                bool resolved = !step.HasSourcePosition && !sequence.Sourceless && SkillCastPlanner.TryResolveSourcePosition(sequence.Caster, sequence.SkillContainer, out position);
                if (resolved) source = position;
                var request = new SkillEmission
                {
                    Payment = payment,
                    SkillContainer = sequence.SkillContainer,
                    Sourceless = sequence.Sourceless,
                    Source = sequence.Sourceless ? null : sequence.Caster.Base,
                    SpatialSource = sequence.Sourceless ? null : (sequence.Carrier ?? sequence.Caster).Base,
                    SourcePos = source,
                    HasSourcePosition = step.HasSourcePosition || resolved,
                    Target = step.Target,
                    TargetPos = step.TrackTarget ? step.Target.GetSimPos() : step.TargetPos,
                    Strength = sequence.Strength,
                    PowerLevel = sequence.PowerLevel,
                    StepSeed = unchecked((entity.Id * 397 ^ sequence.NextIndex) * 397 ^ World.world.getCurWorldTime().GetHashCode()),
                    InitialAngleOffsetDegrees = step.InitialAngleOffsetDegrees,
                    AttackKingdom = sequence.AttackKingdom,
                    RuntimeData = sequence.RuntimeData
                };
                var accepted = sequence.Options?.FirstEmissionAccepted;
                if (sequence.EmittedCount == 0 && payment != null && accepted != null)
                    payment.OnCommitted = () => accepted(request);
                if (!manager.EmitSkill(request)) { ending = SkillCastSequenceEndReason.ExecutionRejected; return; }
                if (payment != null && !payment.IsCommitted) payment.Commit();
                sequence.EmittedCount++; emitted++;
                if (sequence.Generation != manager.CastGeneration || entity.IsNull) return;
            }
            if (sequence.NextIndex >= sequence.Steps.Length) ending = SkillCastSequenceEndReason.Completed;
            advanced = true;
        }
        finally
        {
            if (sequence.Generation == manager.CastGeneration && !entity.IsNull)
            {
                if (!advanced && !ending.HasValue) ending = SkillCastSequenceEndReason.ExecutionRejected;
                entity.GetComponent<SkillCastSequence>() = sequence;
                if (ending.HasValue) End(entity, sequence, ending.Value);
            }
        }
    }

    /// <summary>正式结束先解除占用，完成和结束回调均能看到已经产生的最后一批执行体。</summary>
    /// <param name="entity">实际序列。</param><param name="sequence">实际成功计数。</param><param name="reason">真实结束原因。</param>
    private static void End(Entity entity, SkillCastSequence sequence, SkillCastSequenceEndReason reason)
    {
        entity.AddTag<TagRecycle>();
        ModClass.I.SkillV3.ReleaseSkillReservation(sequence.Caster, sequence.SkillContainer);
        if (reason == SkillCastSequenceEndReason.Completed && sequence.EmittedCount > 0 && !sequence.Sourceless && IsValid(sequence))
        {
            using SkillCasterContextService.Scope scope = SkillCasterContextService.Enter(in sequence.CasterContext);
            EventSystemHub.TryPublish(new SkillCastCompletedEvent(sequence.Caster, sequence.SkillContainer,
                sequence.EmittedCount, sequence.FundingSource, sequence.RuntimeData));
            sequence.Caster.OnSkillCastCompleted(sequence.SkillContainer, sequence.EmittedCount, sequence.FundingSource);
        }
        if (sequence.Generation != ModClass.I.SkillV3.CastGeneration) return;
        sequence.Options?.Hooks?.OnEnded(new SkillCastSequenceResult(sequence.Caster, sequence.SkillContainer,
            sequence.EmittedCount, sequence.NextIndex, reason, sequence.RuntimeData));
    }

    /// <summary>每个实际步骤重新核对世界、原人物、空间载体及明确任务边界。</summary>
    /// <param name="sequence">已经受理的实际序列。</param>
    private static bool IsValid(SkillCastSequence sequence) => sequence.Generation == ModClass.I.SkillV3.CastGeneration &&
        sequence.Caster?.Base != null && !sequence.Caster.Base.isRekt() && sequence.Caster.Base.id == sequence.OwnerId &&
        (sequence.Carrier ?? sequence.Caster).Base != null && !(sequence.Carrier ?? sequence.Caster).Base.isRekt() &&
        (sequence.Carrier ?? sequence.Caster).Base.id == sequence.CarrierId && !sequence.SkillContainer.IsNull &&
        sequence.Steps?.Length > 0 && (sequence.RuntimeData.WorldScope == null || sequence.RuntimeData.WorldScope.IsValid());
}
