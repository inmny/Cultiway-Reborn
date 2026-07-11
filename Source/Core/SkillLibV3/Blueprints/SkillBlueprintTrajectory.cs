using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Blueprints;

public static class SkillBlueprintTrajectory
{
    public static string ResolveEffectiveId(Entity containerEntity)
    {
        if (containerEntity.TryGetComponent(out Trajectory overrideTrajectory))
        {
            return overrideTrajectory.ID;
        }

        var container = containerEntity.GetComponent<SkillContainer>();
        return ResolveDefaultId(container.Asset);
    }

    public static string ResolveDefaultId(SkillEntityAsset entityAsset)
    {
        return entityAsset.PrefabEntity.TryGetComponent(out Trajectory trajectory) ? trajectory.ID : string.Empty;
    }
}
