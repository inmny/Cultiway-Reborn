using Cultiway.Core.SkillLibV3.Editor;

namespace Cultiway.Core.SkillLibV3;

/// <summary>
/// 法术实体与轨迹的唯一兼容性入口。
/// </summary>
public static class SkillTrajectoryCompatibility
{
    public static bool IsCompatible(SkillEntityAsset entity, TrajectoryAsset trajectory)
    {
        return entity != null &&
               trajectory != null &&
               (entity.AcceptedTrajectoryDomains & trajectory.Domains) != SkillTrajectoryDomain.None;
    }

    public static SkillCompatibilityResult Check(SkillEntityAsset entity, TrajectoryAsset trajectory)
    {
        var result = new SkillCompatibilityResult();
        if (!IsCompatible(entity, trajectory))
        {
            result.AddError("trajectory.domain", trajectory?.id);
        }
        return result;
    }
}
