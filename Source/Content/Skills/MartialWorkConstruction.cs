using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.Skills;

namespace Cultiway.Content.Skills;

/// <summary>把实际落锤改作地形施工，原铁锤和身体动作仍由共用执行持有。</summary>
internal static class MartialWorkConstruction
{
    internal const string Id = "Cultiway.Body.RoadForge";

    internal static void Register()
    {
        var definition = ConstructionEditing.Access.Read("Cultiway.Body.HeavyHammerDrop");
        definition.Id = Id;
        definition.Name = "筑路锤";
        definition.Power = 0f;
        definition.TargetRange = 2f;
        definition.Use = new ConstructionUse { Channels = ActiveAbilityChannel.World, Target = ActiveAbilityTargetMode.Point };
        definition.Body.WeaponGround = false;
        definition.Body.WeaponObstacle = false;
        definition.Body.Impact = new ConstructionImpact { DamageScale = 0f };
        definition.Impact = new ConstructionImpact
        {
            DamageScale = 0f,
            WorldEffects = new[]
            {
                new ConstructionWorldEffect { Action = "terrain.raise", TileCentreOnly = true, MaximumTiles = 12 }
            }
        };
        definition.Root = new ConstructionNode
        {
            Id = "ground",
            Form = "fracture_slab",
            Matter = "physical",
            Surface = "stone",
            Scale = 1.55f,
            DisappearAt = .42f,
            Activation = new ConstructionActivation
            {
                Source = "body",
                Trigger = ConstructionChangeTrigger.WorldContact,
                Placement = ConstructionEventPlacement.Event
            }
        };
        ConstructedAbilityService.Register(definition);
    }
}
