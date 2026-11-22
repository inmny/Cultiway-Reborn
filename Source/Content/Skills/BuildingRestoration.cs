using Cultiway.Core.SkillLibV3.Effects;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>修缮原有城市建筑，保留其身份、所属城市和原版功能。</summary>
internal static class BuildingRestoration
{
    internal const string ActionId = "building.repair";

    /// <summary>只在建筑的根部修补一座已经建成且仍可使用的建筑。</summary>
    internal static bool CanApply(WorldTile tile)
    {
        Building building = tile?.building;
        return building != null && building.current_tile == tile && building.asset.city_building &&
               building.isUsable() && building.isNormal() && !building.isUnderConstruction() &&
               building.hasHealth() && building.getHealth() < building.getMaxHealth();
    }

    /// <summary>使用原版生命上限限制实际补回的生命。</summary>
    internal static SkillEffectResult Apply(WorldTile tile)
    {
        Building building = tile.building;
        int before = building.getHealth();
        building.changeHealth(Mathf.Max(1, building.getMaxHealth() / 8));
        int gained = building.getHealth() - before;
        return gained > 0
            ? new SkillEffectResult(SkillEffectOutcomeFlags.BuildingRepaired, magnitude: gained)
            : SkillEffectResult.None;
    }
}
