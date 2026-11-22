using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.Skills;

namespace Cultiway.Content.Skills;

/// <summary>供万法阁取用的两种地脉传力。实际接地和地面波沿用普通构造。</summary>
internal static class GroundScaleConstruction
{
    internal const string CityId = "Cultiway.GroundPulse.City";
    internal const string WorldId = "Cultiway.GroundPulse.World";

    /// <summary>保留已验收的两个尺度，玩家另创时得到可独立编辑的副本。</summary>
    internal static void Register()
    {
        ConstructedAbilityService.Register(
            Create(CityId, "城域地脉传力", 100f, 18f, 48f, 16f, .8f, 2f, 8192));
        ConstructedAbilityService.Register(
            Create(WorldId, "全域地脉传力", 500f, 32f, 512f, 70f, .2f, 5f, 65536));
    }

    private static AbilityDefinition Create(string id, string name, float cost, float cooldown, float range,
                                            float speed, float thickness, float height, int cells)
    {
        var definition = BodyActionCatalog.Create(id, name,
            new BodyActionRequest { Kind = BodyActionKind.Guard, Effector = "foot.right", Seconds = 1.2f });
        definition.Use = new ConstructionUse
        {
            Channels = ActiveAbilityChannel.World,
            Target = ActiveAbilityTargetMode.Point
        };
        definition.Resource = "wakan";
        definition.Cost = cost;
        definition.Cooldown = cooldown;
        definition.Power = 100f;
        definition.Lifetime = 12f;
        definition.Body.MatchDistance = false;
        definition.Body.ActiveFrom = .45f;
        definition.Body.ActiveUntil = .65f;
        definition.Body.Impact = new ConstructionImpact { DamageScale = 0f };
        foreach (var key in definition.Body.Keys)
        {
            key.Foot = new[] { .1f, key.Foot[1], 0f };
            key.Torso = new[] { 0f, 0f, -.12f };
        }

        definition.Impact = new ConstructionImpact
        {
            DamageScale = .005f,
            WorldEffects = new[]
            {
                new ConstructionWorldEffect { Action = "land.cleanse", TileCentreOnly = true, MaximumTiles = cells }
            }
        };
        definition.Root = new ConstructionNode
        {
            Id = "front",
            Form = "fracture_slab",
            Matter = "physical",
            Surface = "stone",
            Activation = new ConstructionActivation
            {
                Source = "body",
                Trigger = ConstructionChangeTrigger.WorldContact,
                Placement = ConstructionEventPlacement.Event
            },
            Operations = new[]
            {
                new ConstructionOperation
                {
                    Kind = ConstructionOperationKind.Propagate,
                    Propagation = new ConstructionPropagation
                    {
                        Range = range, Speed = speed, Seconds = thickness, Height = height, InnerRadius = 2f,
                        Angle = 360f, Attenuation = .004f, MaximumCells = cells
                    }
                }
            }
        };
        return definition;
    }
}
