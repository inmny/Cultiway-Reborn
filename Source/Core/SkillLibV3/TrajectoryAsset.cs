using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;

namespace Cultiway.Core.SkillLibV3;

public delegate void UpdateTrajectory(ref SkillContext context, ref Position pos, ref Rotation rot);
public class TrajectoryAsset : Asset
{
    public UpdateTrajectory Action;
}