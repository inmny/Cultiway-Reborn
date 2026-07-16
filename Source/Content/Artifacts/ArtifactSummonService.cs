using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>生成并强化由法器活动维持的真实召唤生物。</summary>
public static class ArtifactSummonService
{
    public static void Init()
    {
        ActorExtend.RegisterCachedStatsBuilder(ContributeSummonStats);
    }

    public static int SummonSpirits(
        ArtifactAbilityExecutionContext context,
        string abilityInstanceId,
        Vector2 position,
        BaseSimObject target,
        int count,
        float duration,
        float strength)
    {
        WorldTile tile = World.world.GetTile(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y));
        if (tile == null) return 0;

        Actor controller = context.controller.GetComponent<ActorBinder>().Actor;
        double expiresAt = World.world.getCurWorldTime() + duration;
        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            Actor spirit = World.world.units.spawnNewUnit(Actors.GhostFire.id, tile, pSpawnHeight: 0f);
            if (spirit == null) continue;
            spirit.joinKingdom(controller.kingdom);
            if (target != null && !target.isRekt() && controller.canAttackTarget(target))
            {
                spirit.setAttackTarget(target);
            }

            ActorExtend extend = spirit.GetExtend();
            extend.AddComponent(new ArtifactSummon
            {
                artifact = context.artifact,
                controller = context.controller,
                ability_instance_id = abilityInstanceId,
                expires_at = expiresAt,
                damage_ratio = 0.12f * strength,
                health_ratio = 0.18f * strength,
                armor_bonus = 0.5f * strength,
            });
            extend.MarkCultiwayStatsDirty();
            spirit.restoreHealth(Mathf.RoundToInt(controller.stats[S.health] * 0.18f * strength));
            spawned++;
        }
        return spawned;
    }

    public static bool IsMaintained(in ArtifactSummon summon)
    {
        if (!summon.artifact.IsAvailable() || !summon.controller.IsAvailable() ||
            !summon.controller.TryGetComponent(out ActorBinder binder) ||
            binder.Actor == null || binder.Actor.isRekt() ||
            !summon.artifact.TryGetComponent(out ArtifactAbilityRuntime runtime)) return false;
        for (int i = 0; i < runtime.abilities.Length; i++)
        {
            ArtifactAbilityRuntimeEntry entry = runtime.abilities[i];
            if (entry.instance_id == summon.ability_instance_id)
            {
                return entry.activity_kind != ArtifactAbilityActivityKind.None;
            }
        }
        return false;
    }

    private static void ContributeSummonStats(ActorExtend extend, BaseStats stats)
    {
        if (!extend.TryGetComponent(out ArtifactSummon summon) || !summon.controller.IsAvailable() ||
            !summon.controller.TryGetComponent(out ActorBinder binder) ||
            binder.Actor == null || binder.Actor.isRekt()) return;
        stats[S.damage] += binder.Actor.stats[S.damage] * summon.damage_ratio;
        stats[S.health] += binder.Actor.stats[S.health] * summon.health_ratio;
        stats[S.armor] += summon.armor_bonus;
    }
}
