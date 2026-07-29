using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// 始祖骑士「雷铸神兵」机制（[[knight-forge-design]] 的实现）。
/// <list type="bullet">
/// <item>检测：天雷（天气/闪电神力）命中 9 级骑士时打"近期被雷击"标记（见 PatchLightning）。</item>
/// <item>回收：始祖骑士死于雷击 → 快照其数据 + 记村庄与冷却月数，死亡地落一道真实伤害天雷，
///   随后走原版死亡流程（人口−1、掉落、继承等照常）。</item>
/// <item>重生：冷却（游戏内月数）到 → 在所属村庄（仍存活）满血投放新单位，恢复 9 级/斗气/姓名，
///   并在新 id 下重建血脉（使新旧后代都继续继承）。</item>
/// <item>删除：冷却期间村庄被彻底摧毁（city.isRekt）→ 删除待重生数据。</item>
/// </list>
/// 内存态：重载世界后待重生条目丢失（同血脉系统的「存档暂缓」限制）。
/// </summary>
public static class KnightForge
{
    /// <summary>"近期被雷击"标记的有效窗口（秒，实时）：把雷击与紧随其后的死亡关联，过期视为无关死亡。</summary>
    private const float StrikeWindowSeconds = 3f;

    /// <summary>防雷铸闪电级联（同一簇多名始祖骑士）递归过深的护栏。</summary>
    private const int MaxCascadeDepth = 4;

    private static int _cascade_depth;

    /// <summary>近期被天雷命中的候选始祖骑士：actor data id → 命中时刻（Time.unscaledTime）。</summary>
    private static readonly Dictionary<long, float> _strikes = new();

    /// <summary>待重生条目：旧 actor data id → 快照。</summary>
    private static readonly Dictionary<long, ForgeEntry> _pending = new();

    /// <summary>注册死亡钩子。在 Manager.Init 中、KnightBloodline.Init 之后调用。</summary>
    public static void Init()
    {
        ActorExtend.RegisterActionOnDeath(OnActorDeath);
    }

    /// <summary>天雷命中范围内单位时（PatchLightning 调用）：给候选始祖骑士打标记。</summary>
    [Hotfixable]
    public static void OnSkyLightning(Vector2Int pPos, int pRad)
    {
        int sq_rad = pRad * pRad;
        List<Actor> list = World.world.units.getSimpleList();
        for (int i = 0; i < list.Count; i++)
        {
            Actor actor = list[i];
            if (actor.isRekt()) continue;
            if (Toolbox.SquaredDistVec2(actor.current_tile.pos, pPos) > sq_rad) continue;

            ActorExtend ae = actor.GetExtend();
            if (!ae.HasCultisys<Knight>()) continue;
            if (ae.GetCultisys<Knight>().CurrLevel < KnightSetting.LevelNumber - 1) continue; // 仅 9 级（始祖）
            if (!actor.hasCity()) continue; // 无所属村庄则不触发

            _strikes[actor.data.id] = Time.unscaledTime;
        }
    }

    /// <summary>死亡钩子：始祖骑士 + 有村庄 + 近期被雷击 → 触发雷铸回收。</summary>
    [Hotfixable]
    private static void OnActorDeath(ActorExtend ae)
    {
        Actor actor = ae.Base;
        if (actor == null) return;
        if (!ae.HasCultisys<Knight>()) return;
        if (ae.GetCultisys<Knight>().CurrLevel < KnightSetting.LevelNumber - 1) return;
        if (!actor.hasCity()) return;

        if (!_strikes.TryGetValue(actor.data.id, out float strike_time)) return; // 非雷击致死
        _strikes.Remove(actor.data.id);
        if (Time.unscaledTime - strike_time > StrikeWindowSeconds) return; // 标记已过期

        BeginForge(ae);
    }

    /// <summary>快照骑士数据 + 记录待重生条目 + 死亡地落雷。原版死亡流程随后照常执行。</summary>
    [Hotfixable]
    private static void BeginForge(ActorExtend ae)
    {
        if (_cascade_depth > MaxCascadeDepth)
        {
            ModClass.LogWarning("KnightForge: cascade depth exceeded, skipping a forge.");
            return;
        }

        Actor actor = ae.Base;
        long old_id = actor.data.id;

        var entry = new ForgeEntry
        {
            oldActorId = old_id,
            assetId = actor.asset.id,
            name = actor.getName(),
            city = actor.city,
            vigor = ae.GetCultisys<Knight>().vigor,
            remainingMonths = KnightSetting.ForgeCooldownMonths,
        };

        // 血脉快照：复刻旧始祖血脉数据，重生后挂到新 id 下（旧血脉随其原生命周期结束）。
        BloodlineAsset existing = Libraries.Manager.BloodlineLibrary.get($"bloodline_{old_id}");
        if (existing != null)
        {
            entry.hasBloodline = true;
            entry.snap_health = existing.snapshot_health;
            entry.snap_armor = existing.snapshot_armor;
            entry.snap_HealthRegen = existing.snapshot_HealthRegen;
            entry.snap_attack_speed = existing.snapshot_attack_speed;
            entry.snap_critical_chance = existing.snapshot_critical_chance;
            entry.snap_KnightEvasion = existing.snapshot_KnightEvasion;
            entry.snap_power_level = existing.snapshot_power_level;
        }

        _pending[old_id] = entry;

        // 死亡地落一道真实伤害天雷（pActor=null → 仍属天雷，可级联但受 MaxCascadeDepth 约束）。
        _cascade_depth++;
        try
        {
            MapBox.spawnLightningSmall(actor.current_tile, 0.25f, null);
        }
        finally
        {
            _cascade_depth--;
        }

        ModClass.LogInfo(
            $"Knight {old_id} struck down by sky-lightning; reforging in {entry.remainingMonths} months.");
    }

    /// <summary>每月推进待重生条目（ForgePendingSystem 调用）：村庄摧毁→删除；冷却到→重生。</summary>
    [Hotfixable]
    public static void TickPending()
    {
        PruneStaleStrikes();
        if (_pending.Count == 0) return;

        var done = new List<long>();
        foreach (var kv in _pending)
        {
            ForgeEntry entry = kv.Value;
            // 村庄被彻底摧毁 → 删除数据
            if (entry.city == null || entry.city.isRekt())
            {
                done.Add(kv.Key);
                ModClass.LogInfo($"Knight {kv.Key}'s village destroyed; forge data deleted.");
                continue;
            }

            if (entry.remainingMonths <= 1)
            {
                Respawn(entry);
                done.Add(kv.Key);
            }
            else
            {
                entry.remainingMonths--;
            }
        }

        foreach (long id in done) _pending.Remove(id);
    }

    /// <summary>清理过期的雷击标记，防字典无限增长。</summary>
    private static void PruneStaleStrikes()
    {
        if (_strikes.Count == 0) return;
        float now = Time.unscaledTime;
        var stale = new List<long>();
        foreach (var kv in _strikes)
            if (now - kv.Value > StrikeWindowSeconds) stale.Add(kv.Key);
        foreach (long id in stale) _strikes.Remove(id);
    }

    /// <summary>在所属村庄满血投放新单位：归属村庄当前王国，恢复 9 级/斗气/姓名/血脉。</summary>
    [Hotfixable]
    private static void Respawn(ForgeEntry e)
    {
        WorldTile tile = e.city.getTile();
        Actor actor = World.world.units.spawnNewUnit(e.assetId, tile);
        if (actor == null || actor.isRekt())
        {
            ModClass.LogWarning("KnightForge: respawn failed, spawn returned null/rekt.");
            return;
        }

        // 归属：村庄当前王国（若被征服则随新主），姓名保留。
        actor.setKingdom(e.city.kingdom);
        actor.setCity(e.city);
        actor.setName(e.name);

        ActorExtend ae = actor.GetExtend();
        // 0→9 直接授予（Knight 全部 transition 都接了 ResolveGrant，固定成功；Grant 模式不清空斗气）。
        Cultisyses.Knight.GrantToRealm(ae, KnightSetting.LevelNumber - 1);
        ae.GetCultisys<Knight>().vigor = e.vigor;

        // 血脉：在新 id 下重建，使重生后出生的后代也能继承（旧 id 血脉由其后裔按原生命周期保活/回收）。
        if (e.hasBloodline)
        {
            long new_id = actor.data.id;
            string asset_id = $"bloodline_{new_id}";
            BloodlineAsset asset = Libraries.Manager.BloodlineLibrary.get(asset_id);
            if (asset == null)
            {
                asset = new BloodlineAsset
                {
                    id = asset_id,
                    ancestor_actor_id = new_id,
                    snapshot_health = e.snap_health,
                    snapshot_armor = e.snap_armor,
                    snapshot_HealthRegen = e.snap_HealthRegen,
                    snapshot_attack_speed = e.snap_attack_speed,
                    snapshot_critical_chance = e.snap_critical_chance,
                    snapshot_KnightEvasion = e.snap_KnightEvasion,
                    snapshot_power_level = e.snap_power_level,
                };
                Libraries.Manager.BloodlineLibrary.AddDynamic(asset);
            }
            ae.Master(asset, 1f);
        }

        // 满血投放
        actor.restoreHealth((int)actor.getMaxHealth());

        ModClass.LogInfo($"Knight reforged as 雷铸神兵 at village (new actor {actor.data.id}, was {e.oldActorId}).");
    }

    private class ForgeEntry
    {
        public long   oldActorId;
        public string assetId;
        public string name;
        public City   city;
        public float  vigor;
        public int    remainingMonths;
        public bool   hasBloodline;
        public float  snap_health;
        public float  snap_armor;
        public float  snap_HealthRegen;
        public float  snap_attack_speed;
        public float  snap_critical_chance;
        public float  snap_KnightEvasion;
        public float  snap_power_level;
    }
}
