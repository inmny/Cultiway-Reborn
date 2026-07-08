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
    public bool CanBeSelectedByModifier = true;

    /// <summary>
    /// 该轨迹能够给法术提供的方向姿态（按位或）。
    /// 默认 <see cref="TrajectoryOrientation.Horizontal"/>，兼容现有绝大多数水平位移轨迹。
    /// 由 <see cref="SkillModifierLibrary.SetTrajectory"/> 词条在随机选取时与法术的
    /// <see cref="SkillEntityAsset.AcceptedOrientations"/> 取交集过滤。
    /// </summary>
    public TrajectoryOrientation Orientations { get; set; } = TrajectoryOrientation.Horizontal;

    /// <summary>
    /// 流式声明该轨迹支持的方向姿态，便于在 Setup 方法链式调用。
    /// </summary>
    public TrajectoryAsset WithOrientations(TrajectoryOrientation orientations)
    {
        Orientations = orientations;
        return this;
    }
}
