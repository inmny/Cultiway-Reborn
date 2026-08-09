using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Const;
using Cultiway.Core.Combat;
using Cultiway.Core;
using Cultiway.Patch;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// 始祖骑士「雷铸神兵」机制（[[knight-forge-design]] 的实现）。
/// <list type="bullet">
/// <item>检测：PatchLightning 将天雷作用域跨事件传到最终伤害结算。</item>
/// <item>重生：致死天雷扣血前阻止伤害，将原单位移动到所属城市的其他安全地块并恢复满血。</item>
/// <item>停滞：重生后的冷却月数内保持昏迷，无法做出任何行动。</item>
/// <item>保留：原单位、id、装备、关系、归属和血脉数据不变，不进入死亡流程。</item>
/// </list>
/// </summary>
public static class KnightForge
{
    private const float StrikeWindowSeconds = 3f;

    /// <summary>注册雷击状态和最终伤害钩子。在 Manager.Init 中、KnightBloodline.Init 之后调用。</summary>
    public static void Init()
    {
        PatchLightning.RegisterActionBeforeSkyLightningDamage(OnSkyLightning);
        ActorExtend.RegisterActionOnFinalDamage(FinalDamageStage.Survival, OnFinalDamage);
    }

    /// <summary>天雷伤害进入结算前记录候选始祖骑士的原版状态。</summary>
    [Hotfixable]
    public static void OnSkyLightning(Vector2Int pPos, int pRad)
    {
        int squaredRadius = pRad * pRad;
        List<Actor> actors = World.world.units.getSimpleList();
        for (int i = 0; i < actors.Count; i++)
        {
            Actor actor = actors[i];
            if (actor.isRekt()) continue;
            if (Toolbox.SquaredDistVec2(actor.current_tile.pos, pPos) > squaredRadius) continue;

            ActorExtend ae = actor.GetExtend();
            if (!ae.HasCultisys<Knight>()) continue;
            if (ae.GetCultisys<Knight>().CurrLevel < KnightSetting.LevelNumber - 1) continue;
            if (!actor.hasCity()) continue;

            actor.addStatusEffect("afterglow", StrikeWindowSeconds, pColorEffect: false);
        }
    }

    /// <summary>近期被天雷命中的始祖骑士遭遇致死伤害时，避免进入原版死亡流程。</summary>
    [Hotfixable]
    private static void OnFinalDamage(
        ActorExtend ae,
        BaseSimObject attacker,
        ElementComposition damageComposition,
        AttackType attackType,
        ref float damage)
    {
        Actor actor = ae.Base;
        if (!actor.hasStatus("afterglow")) return;
        if (!ae.HasCultisys<Knight>()) return;
        if (ae.GetCultisys<Knight>().CurrLevel < KnightSetting.LevelNumber - 1) return;
        if (!actor.hasCity() || actor.city.isRekt()) return;
        if (Mathf.Floor(damage) < actor.data.health) return;

        WorldTile tile = FindRebirthTile(actor);
        if (tile == null) return;

        actor.cancelAllBeh();
        actor.setCurrentTilePosition(tile);
        actor.position_height = 0f;
        actor.restoreHealth((int)actor.getMaxHealth());
        float recoveryDuration = KnightSetting.ForgeCooldownMonths * TimeScales.SecPerMonth;
        actor.addStatusEffect("stunned", recoveryDuration, pColorEffect: false);
        actor.makeWait(recoveryDuration);
        actor.updateStats();
        actor.finishStatusEffect("afterglow");
        damage = 0f;

        ModClass.LogInfo($"Knight {actor.data.id} survived sky lightning and was reforged in place.");
    }

    private static WorldTile FindRebirthTile(Actor actor)
    {
        City city = actor.city;
        WorldTile current = actor.current_tile;
        for (int i = 0; i < city.zones.Count * 2; i++)
        {
            TileZone zone = city.zones.GetRandom();
            if (zone == null || zone.city != city || zone.tiles.Length == 0) continue;

            WorldTile tile = zone.tiles.GetRandom();
            if (IsSafeRebirthTile(tile, current)) return tile;
        }

        foreach (TileZone zone in city.zones)
        {
            if (zone == null || zone.city != city) continue;
            for (int i = 0; i < zone.tiles.Length; i++)
            {
                WorldTile tile = zone.tiles[i];
                if (IsSafeRebirthTile(tile, current)) return tile;
            }
        }

        WorldTile cityTile = city.getTile();
        return IsSafeRebirthTile(cityTile, current) ? cityTile : null;
    }

    private static bool IsSafeRebirthTile(WorldTile tile, WorldTile current)
    {
        return tile != null && tile != current && !tile.IsWater() && !tile.Type.lava && !tile.isOnFire();
    }
}
