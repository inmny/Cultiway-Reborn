using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>传送阵从实际落脚格显示充能与抵达。阵眼的画面跟随原行程，迁移仍由行程确认。</summary>
internal static class TeleportArrayConstruction
{
    /// <summary>原阵法受理后到真正迁移之间的世界时间。</summary>
    internal const float ChargeSeconds = .55f;

    /// <summary>原入口和出口共用的充能形体。</summary>
    private static readonly AbilityDefinition charge = Gate("Charge", "传送阵·充能", ChargeSeconds, .27f);

    /// <summary>人物实际落地后，出口留下短暂的光门。</summary>
    private static readonly AbilityDefinition arrival = Gate("Arrival", "传送阵·抵达", .32f, .08f);

    /// <summary>一次充能实际受理的两端，可以随同一旅客的行程撤去。</summary>
    /// <param name="tile">该端实际落脚格。</param><param name="towards">原行程的另一端。</param>
    /// <param name="passenger">原旅客的稳定编号。</param>
    internal static long Charge(WorldTile tile, WorldTile towards, long passenger) => Show(charge, tile, towards, passenger);

    /// <summary>在迁移成功后显示实际出口。</summary>
    /// <param name="tile">原人物现在实际所在的地块。</param><param name="towards">已经离开的入口。</param>
    /// <param name="passenger">已经迁移的原旅客。</param>
    internal static void Arrive(WorldTile tile, WorldTile towards, long passenger) => Show(arrival, tile, towards, passenger);

    /// <summary>把阵门直接立在地块，使用两端的实际方向，不借旅客身份施法。</summary>
    /// <param name="definition">充能或抵达的既定外观。</param><param name="tile">实际落脚格。</param>
    /// <param name="towards">另一端的位置。</param><param name="passenger">本次原旅客编号，只用于关联结果。</param>
    private static long Show(AbilityDefinition definition, WorldTile tile, WorldTile towards, long passenger)
    {
        var target = new ActiveAbilityTarget(null, (Vector2)towards.posV3,
            runtimeData: new SkillCastRuntimeData { CorrelationId = passenger });
        return ConstructedAbilityService.StartWorld(definition, target, (Vector2)tile.posV3, default(GetHitEvent));
    }

    /// <summary>复用已有立体空间门，充能和抵达使用同一位置规则。</summary>
    /// <param name="id">能力阶段编号。</param><param name="name">阶段名称。</param>
    /// <param name="duration">形体存在时长。</param><param name="rise">形体展开时长。</param>
    private static AbilityDefinition Gate(string id, string name, float duration, float rise) => new()
    {
        Id = "Cultiway.Facility.TeleportArray." + id, Name = name, Resource = "none", Cost = 0f, Cooldown = 0f,
        Power = 1f, Lifetime = duration, TargetRange = 10000f,
        Use = new ConstructionUse { Channels = ActiveAbilityChannel.World, Target = ActiveAbilityTargetMode.Point },
        Binding = new ConstructionBinding { Anchor = AbilityAnchor.Source, FaceTarget = true },
        Impact = new ConstructionImpact { DamageScale = 0f },
        Root = new ConstructionNode
        {
            Id = "gate", Form = "space_fold_gate", ContactRole = ConstructionContactRole.Decoration, Volume = 0f,
            Operations = new[]
            {
                new ConstructionOperation { Kind = ConstructionOperationKind.Grow, Seconds = rise }
            }
        }
    };
}
