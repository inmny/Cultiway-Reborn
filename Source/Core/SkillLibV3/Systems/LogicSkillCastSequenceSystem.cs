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

    public LogicSkillCastSequenceSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagRecycle>());
    }

    protected override void OnUpdate()
    {
        var dt = Tick.deltaTime;
        _spawnRequests.Clear();
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
                    AttackKingdom = sequence.AttackKingdom
                });
                sequence.EmittedCount++;
                emitted++;
            }

            if (sequence.NextIndex >= sequence.Steps.Length)
            {
                if (sequence.EmittedCount > 0)
                {
                    EventSystemHub.TryPublish(new SkillCastCompletedEvent(sequence.Caster,
                        sequence.SkillContainer, sequence.EmittedCount, sequence.FundingSource));
                    sequence.Caster.OnSkillCastCompleted(
                        sequence.SkillContainer,
                        sequence.EmittedCount,
                        sequence.FundingSource);
                }
                CommandBuffer.AddTag<TagRecycle>(entity.Id);
            }
        });
        foreach (var request in _spawnRequests)
        {
            if (request.SkillContainer.IsNull
            || request.Source.isRekt()
            ) continue;

            ModClass.I.SkillV3.SpawnSkill(request.SkillContainer, request.Source, request.Target, request.TargetPos,
                request.Strength, power_level: request.PowerLevel,
                initial_angle_offset_degrees: request.InitialAngleOffsetDegrees,
                attack_kingdom: request.AttackKingdom);
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
    }
}
