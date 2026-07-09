using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV3.Systems;

public class LogicTrajectorySystem : QuerySystem<SkillContext, PrevPosition, Position, Rotation, Trajectory>
{
    public LogicTrajectorySystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive>());
    }
    protected override void OnUpdate()
    {
        var dt = Tick.deltaTime;
        Query.ForEach((contexts, prevPositions, positions, rotations, trajectories, entities) =>
        {
            for (int i = 0; i < entities.Length; i++)
            {
                ref var context = ref contexts[i];
                ref var prevPos = ref prevPositions[i];
                ref var position = ref positions[i];
                ref var rotation = ref rotations[i];
                ref var trajectory = ref trajectories[i];

                // 在轨迹更新前记录上一帧位置，供扫掠碰撞做线段检测
                prevPos.Value = position.v2;

                trajectory.Asset.Action(ref context, ref position, ref rotation, entities.EntityAt(i), dt);
            }
        }).Run();
    }
}
