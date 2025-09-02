using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV2.Predefined;
using Cultiway.Core.SkillLibV2.Systems;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV3.Systems;

public class LogicTrajectorySystem : QuerySystem<SkillContext, Position, Rotation, Trajectory>
{
    public LogicTrajectorySystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab>());
    }
    protected override void OnUpdate()
    {
        var dt = Tick.deltaTime;
        Query.ForEach((contexts, positions, rotations, trajectories, entities) =>
        {
            for (int i = 0; i < entities.Length; i++)
            {
                ref var context = ref contexts[i];
                ref var position = ref positions[i];
                ref var rotation = ref rotations[i];
                ref var trajectory = ref trajectories[i];
                trajectory.Asset.Action(ref context, ref position, ref rotation, entities.EntityAt(i), dt);
            }
        }).Run();
    }
}