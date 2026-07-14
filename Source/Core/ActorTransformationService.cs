using Cultiway.Content;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Core;

/// <summary>
///     在保留角色实体、原版数据和模组扩展数据的前提下，更换角色的生物形态。
/// </summary>
public static class ActorTransformationService
{
    /// <summary>
    ///     把现有角色原地转换为目标生物。角色身份、关系、物品和全部扩展组件不会迁移或重建。
    /// </summary>
    public static Actor TransformInPlace(Actor actor, ActorAsset targetAsset)
    {
        if (actor == null || actor.isRekt() || targetAsset == null) return null;

        var resources = ResourceRatios.Capture(actor);
        var targetSubspecies = ResolveTargetSubspecies(actor, targetAsset, actor.isSapient());

        actor.setAsset(targetAsset);
        actor.setSubspecies(targetSubspecies);
        ApplyTargetBirthTraits(actor, targetSubspecies);

        actor.data.head = -1;
        if (targetSubspecies.hasPhenotype()) actor.generatePhenotypeAndShade();
        actor.setFlying(targetAsset.flying);
        actor.setShowShadow(targetAsset.shadow);
        actor.clearGraphicsFully();
        actor.setTransformed();

        var actorExtend = actor.GetExtend();
        if (actorExtend != null)
        {
            if (targetAsset.GetExtend<ActorAssetExtend>().must_have_element_root && !actorExtend.HasElementRoot())
            {
                actorExtend.AddComponent(ElementRoot.Roll());
            }
            Cultisyses.RecheckAvailableCultisyses(actorExtend);
            actorExtend.MarkCultiwayStatsDirty(false);
            actorExtend.MarkCultiwaySkillCacheDirty(false);
        }

        if (actor.canUseItems() && !actor.hasWeapon()) actor.generateDefaultSpawnWeapons(false);
        actor.setStatsDirty();
        actor.updateStats();
        resources.Restore(actor);
        actor.city?.setCitizensDirty();
        return actor;
    }

    private static Subspecies ResolveTargetSubspecies(Actor actor, ActorAsset targetAsset, bool requireSapient)
    {
        var subspecies = World.world.subspecies.getNearbySpecies(targetAsset, actor.current_tile, out _,
            requireSapient);
        if (subspecies != null) return subspecies;

        subspecies = World.world.subspecies.newSpecies(targetAsset, actor.current_tile);
        if (requireSapient) subspecies.makeSapient();
        return subspecies;
    }

    private static void ApplyTargetBirthTraits(Actor actor, Subspecies targetSubspecies)
    {
        foreach (var trait in targetSubspecies.getActorBirthTraits().getTraits())
        {
            actor.addTrait(trait, true);
        }
    }

    private readonly struct ResourceRatios
    {
        private readonly float health;
        private readonly float mana;
        private readonly float stamina;
        private readonly float nutrition;
        private readonly float happiness;

        private ResourceRatios(float health, float mana, float stamina, float nutrition, float happiness)
        {
            this.health = health;
            this.mana = mana;
            this.stamina = stamina;
            this.nutrition = nutrition;
            this.happiness = happiness;
        }

        public static ResourceRatios Capture(Actor actor)
        {
            return new ResourceRatios(
                GetRatio(actor.getHealth(), actor.getMaxHealth()),
                GetRatio(actor.getMana(), actor.getMaxMana()),
                GetRatio(actor.getStamina(), actor.getMaxStamina()),
                GetRatio(actor.getNutrition(), actor.getMaxNutrition()),
                Mathf.InverseLerp(actor.getMinHappiness(), actor.getMaxHappiness(), actor.getHappiness()));
        }

        public void Restore(Actor actor)
        {
            actor.setHealth(Scale(health, actor.getMaxHealth()));
            actor.setMana(Scale(mana, actor.getMaxMana()));
            actor.setStamina(Scale(stamina, actor.getMaxStamina()));
            actor.setNutrition(Scale(nutrition, actor.getMaxNutrition()));
            actor.setHappiness(Mathf.RoundToInt(Mathf.Lerp(actor.getMinHappiness(), actor.getMaxHappiness(),
                happiness)));
        }

        private static float GetRatio(int value, int maximum)
        {
            return maximum > 0 ? Mathf.Clamp01((float)value / maximum) : 0f;
        }

        private static int Scale(float ratio, int maximum)
        {
            return Mathf.RoundToInt(ratio * maximum);
        }
    }
}
