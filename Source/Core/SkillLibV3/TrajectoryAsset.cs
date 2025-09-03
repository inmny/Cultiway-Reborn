using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3;

public delegate void InitDefaultTrajectory(Entity prefab_entity);
public delegate void UpdateTrajectory(ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt);
public class TrajectoryAsset : Asset
{
    public InitDefaultTrajectory OnInit;
    public UpdateTrajectory Action;
}