using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>只恢复仍存活的原版植被，把实际补回的生命留在同一个对象上。</summary>
internal static class LivingPlantRestoration
{
    /// <summary>可与范围、轨迹与频率自由搭配的地块操作。</summary>
    internal const string ActionId = "nature.repair";

    /// <summary>根系所在的原地块可以修复受损的树或植被，砍倒、死亡与普通建筑不适用。</summary>
    /// <param name="tile">已确认接触的地块。</param>
    internal static bool CanApply(WorldTile tile)
    {
        Building plant = tile?.building;
        return plant != null && plant.current_tile == tile && !plant.isRekt() && plant.isAlive() && !plant.chopped &&
               (plant.asset.type is "type_tree" or "type_vegetation" or "type_flower") &&
               plant.hasHealth() && plant.getHealth() < plant.getMaxHealth();
    }

    /// <summary>一次最多补回原生命上限的八分之一，余量由实际生命上限限制。</summary>
    /// <param name="context">已经确认接触的地块与世界时间。</param>
    internal static SkillEffectResult Apply(ConstructionWorldContext context)
    {
        Building plant = context.Tile.building;
        int before = plant.getHealth();
        plant.changeHealth(Mathf.Max(1, plant.getMaxHealth() / 8));
        int gained = plant.getHealth() - before;
        if (gained <= 0) return SkillEffectResult.None;
        ConstructedAbilityService.AddPlantRepair(plant, gained, context.WorldTime);
        return new SkillEffectResult(SkillEffectOutcomeFlags.LivingPlantRepaired, magnitude: gained);
    }
}
