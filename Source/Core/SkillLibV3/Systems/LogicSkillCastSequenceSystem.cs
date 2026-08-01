using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV3.Systems;

public class LogicSkillCastSequenceSystem : QuerySystem<SkillCastSequence>
{
    private readonly List<SpawnSkillRequest> _spawnRequests = new();
    private readonly List<SkillCastCompletedRequest> _completedRequests = new();

    public LogicSkillCastSequenceSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagRecycle>());
    }

    protected override void OnUpdate()
    {
        var dt = Tick.deltaTime;
        _spawnRequests.Clear();
        _completedRequests.Clear();
        Query.ForEachEntity((ref SkillCastSequence sequence, Entity entity) =>
        {
            if (!IsSequenceValid(ref sequence))
            {
                CommandBuffer.AddTag<TagRecycle>(entity.Id);
                return;
            }

            sequence.Elapsed += dt;
            var emitted = 0;
            var maxEmitPerTick = sequence.MaxEmitPerTick <= 0 ? 1 : sequence.MaxEmitPerTick;
            while (sequence.NextIndex < sequence.Steps.Length && emitted < maxEmitPerTick)
            {
                var step = sequence.Steps[sequence.NextIndex];
                if (step.Delay > sequence.Elapsed) break;

                sequence.NextIndex++;
                if (step.TrackTarget && step.Target.isRekt()) continue;

                _spawnRequests.Add(new SpawnSkillRequest
                {
                    SkillContainer = sequence.SkillContainer,
                    Source = sequence.Caster.Base,
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

            if (sequence.NextIndex >= sequence.Steps.Length)
            {
                if (sequence.EmittedCount > 0)
                {
                    _completedRequests.Add(new SkillCastCompletedRequest
                    {
                        Caster = sequence.Caster,
                        SkillContainer = sequence.SkillContainer,
                        EmittedCount = sequence.EmittedCount,
                        FundingSource = sequence.FundingSource,
                        RuntimeData = sequence.RuntimeData,
                    });
                }
                CommandBuffer.AddTag<TagRecycle>(entity.Id);
            }
        });
        // 完成回调允许创建状态、回响技能等实体，必须离开查询迭代后再执行。
        foreach (var request in _completedRequests)
        {
            if (request.Caster?.Base == null || request.Caster.Base.isRekt()) continue;
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
            if (request.SkillContainer.IsNull
            || request.Source.isRekt()
            ) continue;

            ModClass.I.SkillV3.SpawnSkill(request.SkillContainer, request.Source, request.Target, request.TargetPos,
                request.Strength, power_level: request.PowerLevel,
                initial_angle_offset_degrees: request.InitialAngleOffsetDegrees,
                attack_kingdom: request.AttackKingdom,
                runtime_data: request.RuntimeData);
        }
        CommandBuffer.Playback();
    }

    private static bool IsSequenceValid(ref SkillCastSequence sequence)
    {
        if (sequence.Caster == null) return false;
        if (sequence.Caster.Base == null || sequence.Caster.Base.isRekt()) return false;
        if (sequence.SkillContainer.IsNull) return false;
        return sequence.Steps != null && sequence.Steps.Length > 0;
    }

    private struct SpawnSkillRequest
    {
        public Entity SkillContainer;
        public BaseSimObject Source;
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
    }
}
