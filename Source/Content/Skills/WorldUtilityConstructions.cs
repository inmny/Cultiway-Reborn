using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.Skills;

namespace Cultiway.Content.Skills;

/// <summary>万法阁的城市净土和全图防火底稿，共用已登记的世界操作。</summary>
internal static class WorldUtilityConstructions
{
    internal static void Register()
    {
        ConstructedAbilityService.Register(new AbilityDefinition
        {
            Id = "Cultiway.WorldUtility.CityCleanse",
            Name = "镇域净土",
            Resource = "wakan",
            Cost = 100f,
            Cooldown = 18f,
            Lifetime = 1.2f,
            Power = 0f,
            TargetRange = 48f,
            Use = new ConstructionUse { Channels = ActiveAbilityChannel.World, Target = ActiveAbilityTargetMode.Point },
            Binding = new ConstructionBinding { Anchor = AbilityAnchor.Target },
            Impact = new ConstructionImpact
            {
                DamageScale = 0f,
                WorldEffects = new[]
                {
                    new ConstructionWorldEffect { Action = "land.cleanse", TileCentreOnly = true, MaximumTiles = 8192 }
                }
            },
            Root = new ConstructionNode
            {
                Id = "ground", Form = "world_domain", Matter = "water",
                Scale = 48f, ActiveFrom = .18f, ActiveUntil = .26f
            }
        });

        ConstructedAbilityService.Register(new AbilityDefinition
        {
            Id = "Cultiway.WorldUtility.FireEdict",
            Name = "息焰敕令",
            Resource = "wakan",
            Cost = 500f,
            Cooldown = 32f,
            Lifetime = 8f,
            Power = 0f,
            Use = new ConstructionUse { Channels = ActiveAbilityChannel.World, Target = ActiveAbilityTargetMode.Self },
            Binding = new ConstructionBinding { Anchor = AbilityAnchor.Source },
            Impact = new ConstructionImpact { DamageScale = 0f },
            WorldCondition = new ConstructionWorldCondition { EntireWorld = true },
            Root = new ConstructionNode
            {
                Id = "edict", Form = "truth_mirror", Matter = "water",
                Scale = 1.5f, ContactRole = ConstructionContactRole.Decoration
            }
        });
    }
}
