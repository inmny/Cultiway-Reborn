using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV3.Systems;

public class LogicSkillCastSequenceSystem : QuerySystem<SkillCastSequence>
{
    public LogicSkillCastSequenceSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagRecycle>());
    }

    protected override void OnUpdate()
    {
        var dt = Tick.deltaTime;
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
                if (step.Target == null || step.Target.isRekt()) continue;

                if (!SkillCastCost.TryConsumeStepWakan(sequence.Caster, sequence.SkillContainer))
                {
                    CommandBuffer.AddTag<TagRecycle>(entity.Id);
                    return;
                }

                ModClass.I.SkillV3.SpawnSkill(sequence.SkillContainer, sequence.Caster.Base, step.Target,
                    sequence.Strength);
                emitted++;
            }

            if (sequence.NextIndex >= sequence.Steps.Length)
            {
                CommandBuffer.AddTag<TagRecycle>(entity.Id);
            }
        });
        CommandBuffer.Playback();
    }

    private static bool IsSequenceValid(ref SkillCastSequence sequence)
    {
        if (sequence.Caster == null) return false;
        if (sequence.Caster.Base == null || sequence.Caster.Base.isRekt()) return false;
        if (sequence.SkillContainer.IsNull) return false;
        return sequence.Steps != null && sequence.Steps.Length > 0;
    }
}
