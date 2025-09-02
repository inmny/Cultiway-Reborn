using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Modifiers;

public struct Trajectory : IModifier, IComponent
{
    public string ID;

    public TrajectoryAsset Asset
    {
        get
        {
            _asset ??= ModClass.I.SkillV3.TrajLib.get(ID);
            return _asset;
        }
    }

    private TrajectoryAsset _asset;
}