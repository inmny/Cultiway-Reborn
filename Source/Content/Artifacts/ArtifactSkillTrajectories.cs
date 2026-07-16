using Cultiway.Abstract;
using Cultiway.Content.Artifacts;
using Cultiway.Core.SkillLibV3;

namespace Cultiway.Content;

/// <summary>
/// Content 提供的法器技能执行轨迹。它们仍由 Core 的 LogicTrajectorySystem 统一调度。
/// </summary>
public sealed class ArtifactSkillTrajectories : ExtendLibrary<TrajectoryAsset, ArtifactSkillTrajectories>
{
    public static TrajectoryAsset FlyingSword { get; private set; }
    public static TrajectoryAsset SwordArray { get; private set; }

    protected override bool AutoRegisterAssets() => true;

    protected override string Prefix() => "Cultiway.ArtifactSkillTrajectory";

    protected override void OnInit()
    {
        FlyingSword.CanBeSelectedByModifier = false;
        FlyingSword.Orientations = TrajectoryOrientation.Horizontal;
        FlyingSword.Action = ArtifactFlyingSwordExecution.Update;

        SwordArray.CanBeSelectedByModifier = false;
        SwordArray.Orientations = TrajectoryOrientation.Appear;
        SwordArray.Action = ArtifactSwordArrayExecution.Update;
    }
}
