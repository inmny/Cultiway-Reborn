using Cultiway.Core;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>煞风劫只提供普通构造资料；波次、资格及承伤结果留在劫程服务。</summary>
internal static class BalefulWindConstruction
{
    /// <summary>中央风柱的稳定来源编号。</summary>
    internal const string CenterId = "Cultiway.Tribulation.BalefulWind.Center";

    /// <summary>每股劫风的稳定来源编号。</summary>
    internal const string WaveId = "Cultiway.Tribulation.BalefulWind.Wave";

    /// <summary>沿用原劫风完整元素组成，不用单一风属性替代混合伤害。</summary>
    internal static ElementComposition Element => new(wood: .35f, water: .25f, neg: .25f, entropy: .15f, normalize: true);

    /// <summary>固定环境风柱不伤人，由劫程结束或失败时主动撤销。</summary>
    internal static readonly AbilityDefinition Center = new()
    {
        Id = CenterId, Name = "煞风劫·风柱", Lifetime = 60f,
        Resource = "none", Cost = 0f, Cooldown = 0f, Power = 1f, TargetRange = 10000f,
        Use = new ConstructionUse { Channels = ActiveAbilityChannel.World, Target = ActiveAbilityTargetMode.Point },
        Binding = new ConstructionBinding { Anchor = AbilityAnchor.Target },
        Root = new ConstructionNode
        {
            Id = "center", Form = "baleful_wind_column", Matter = "wind", State = "energy", Scale = 1f,
            ContactRole = ConstructionContactRole.Decoration,
            Operations = new[] { new ConstructionOperation { Kind = ConstructionOperationKind.Animate, Clip = "run" } }
        }
    };

    /// <summary>单股风从实际环境位置飞向固定劫心，只有指定渡劫者能触发一次真实接触。</summary>
    /// <param name="direction">本股从出生点指向固定劫心的世界方向。</param>
    internal static AbilityDefinition Wave(Vector3 direction) => new()
    {
        Id = WaveId, Name = "煞风劫·劫风", Lifetime = 1f,
        Resource = "none", Cost = 0f, Cooldown = 0f, Power = 1f, TargetRange = 10000f,
        Use = new ConstructionUse { Channels = ActiveAbilityChannel.World, Target = ActiveAbilityTargetMode.Object },
        Binding = new ConstructionBinding { Anchor = AbilityAnchor.Source },
        Impact = new ConstructionImpact { SelectedTargetOnly = true, DamageScale = 1f, Element = Element },
        Root = new ConstructionNode
        {
            Id = "wind", Form = "baleful_wind_shear", Matter = "wind", State = "energy", Scale = .35f,
            Contact = new ConstructionContactPolicy { MaximumTotal = 1 },
            Operations = new[]
            {
                new ConstructionOperation { Kind = ConstructionOperationKind.Animate, Clip = "run" },
                new ConstructionOperation { Kind = ConstructionOperationKind.Fly, WorldVelocity = true, InheritVelocity = false,
                    Vector = new[] { direction.x * 32f, direction.y * 32f, direction.z * 32f }, TurnRate = 180f,
                    Goal = new ConstructionFlightGoal { Follow = false, Arrival = ConstructionFlightArrival.End } }
            }
        }
    };
}
