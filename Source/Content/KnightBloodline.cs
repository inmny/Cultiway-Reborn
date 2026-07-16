using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Progression;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// 骑士血脉系统：
/// - 父母表：makeBaby 时记录 child_id → (parent1_id, parent2_id)，供后代回溯血统。
/// - 9 级快照：骑士突破到 9 级时备份其个人属性（剔除自身继承加成）为 BloodlineAsset。
/// - 后裔加成：骑士按"5 代内衰减后最强的始祖"获得 0.3×0.7^(代数-1)×(等级/9)×快照 的属性加成。
/// - 生命周期：后裔 Master 血脉资产；无后裔时由 IDeleteWhenUnknown 自动回收。
/// </summary>
public static class KnightBloodline
{
    /// <summary>父母表：孩子 ActorData id → (父 id, 母 id)。独立于角色存活，可跨死亡回溯。</summary>
    private static readonly Dictionary<long, (long p1, long p2)> _parents = new();

    /// <summary>makeBaby Postfix 调用：记录一次出生的父母关系。</summary>
    public static void RecordBirth(long child_id, Actor pParent1, Actor pParent2)
    {
        _parents[child_id] = (pParent1?.data.id ?? -1L, pParent2?.data.id ?? -1L);
    }

    /// <summary>注册 9 级快照触发器与后裔加成 stats builder。在 Manager.Init 中调用。</summary>
    public static void Init()
    {
        ProgressionLifecycle.RegisterCommitted(OnKnightCommitted);
        ActorExtend.RegisterCachedStatsBuilder(ApplyBloodlineBonus);
    }

    /// <summary>骑士突破到 9 级 → 备份血脉 + 触发毕业钩子。</summary>
    private static void OnKnightCommitted(ProgressionCommittedEvent evt)
    {
        if (evt.Cultisys != Cultisyses.Knight) return;
        if (evt.ToLevel != KnightSetting.LevelNumber - 1) return; // 到 9 级
        SnapshotAncestor(evt.Actor);
        KnightGraduation.OnGraduated(evt.Actor); // 毕业钩子（当前空操作，未来接"转入其他体系"）
    }

    private static void SnapshotAncestor(ActorExtend ae)
    {
        // 剔除始祖自身继承的血脉加成（防雪崩）
        ComputeBloodlineBonus(ae, out var bh, out var ba, out var bhr, out var bas, out var bc, out var be, out _);

        long ancestor_id = ae.Base.data.id;
        var asset = new BloodlineAsset
        {
            id = $"bloodline_{ancestor_id}",
            ancestor_actor_id = ancestor_id,
            snapshot_health = ae.Base.stats["health"] - bh,
            snapshot_armor = ae.Base.stats["armor"] - ba,
            snapshot_HealthRegen = ae.Base.stats["HealthRegen"] - bhr,
            snapshot_attack_speed = ae.Base.stats["attack_speed"] - bas,
            snapshot_critical_chance = ae.Base.stats["critical_chance"] - bc,
            snapshot_KnightEvasion = ae.Base.stats[BaseStatses.KnightEvasion.id] - be,
            snapshot_power_level = ae.GetPowerLevel(),
        };

        // 同一始祖重复触发：保留更强的快照
        var existing = Libraries.Manager.BloodlineLibrary.get(asset.id);
        if (existing != null)
        {
            if (existing.snapshot_power_level >= asset.snapshot_power_level) return;
            Libraries.Manager.BloodlineLibrary.RemoveAll(new[] { asset.id });
        }
        Libraries.Manager.BloodlineLibrary.AddDynamic(asset);

        // 始祖自己也 Master，在没有后裔之前保活
        ae.Master(asset, 1f);
        ModClass.LogInfo($"Knight {ancestor_id} became a bloodline ancestor.");
    }

    /// <summary>计算某骑士从血脉获得的 7 项加成（同时 Master 命中的血脉以保活）。无血脉则全 0。</summary>
    private static void ComputeBloodlineBonus(ActorExtend ae,
        out float health, out float armor, out float health_regen,
        out float attack_speed, out float critical_chance, out float evasion, out int gen)
    {
        health = armor = health_regen = attack_speed = critical_chance = evasion = 0f;
        gen = -1;
        if (!ae.HasCultisys<Knight>()) return;

        var bloodline = FindStrongestAncestor(ae.Base.data.id, out gen);
        if (bloodline == null) return;

        // Master（幂等）以维持血脉资产存活；后代死亡时 Dispose 会 DeMaster
        ae.Master(bloodline, gen);

        ref var knight = ref ae.GetCultisys<Knight>();
        float scale = KnightSetting.BloodlineBaseCoefficient
                      * Mathf.Pow(KnightSetting.BloodlineDecayPerGen, gen - 1)
                      * (knight.CurrLevel / (float)(KnightSetting.LevelNumber - 1));

        health = scale * bloodline.snapshot_health;
        armor = scale * bloodline.snapshot_armor;
        health_regen = scale * bloodline.snapshot_HealthRegen;
        attack_speed = scale * bloodline.snapshot_attack_speed;
        critical_chance = scale * bloodline.snapshot_critical_chance;
        evasion = scale * bloodline.snapshot_KnightEvasion;
    }

    /// <summary>骑士血脉加成 stats builder：把继承加成叠加到属性缓存。</summary>
    [Hotfixable]
    private static void ApplyBloodlineBonus(ActorExtend ae, BaseStats stats)
    {
        ComputeBloodlineBonus(ae, out var h, out var arm, out var hr, out var aspd, out var crit, out var ev, out _);
        if (h == 0f && arm == 0f && hr == 0f && aspd == 0f && crit == 0f && ev == 0f) return;
        stats["health"] += h;
        stats["armor"] += arm;
        stats["HealthRegen"] += hr;
        stats["attack_speed"] += aspd;
        stats["critical_chance"] += crit;
        stats[BaseStatses.KnightEvasion.id] += ev;
    }

    /// <summary>沿父母表回溯 ≤MaxGenerations 代，取"衰减后实际最强"的单一血脉始祖。</summary>
    private static BloodlineAsset FindStrongestAncestor(long actor_id, out int best_gen)
    {
        best_gen = -1;
        BloodlineAsset best = null;
        float best_eff = -1f;

        var frontier = new List<long> { actor_id };
        for (int g = 1; g <= KnightSetting.BloodlineMaxGenerations; g++)
        {
            var next = new List<long>();
            for (int i = 0; i < frontier.Count; i++)
            {
                if (!_parents.TryGetValue(frontier[i], out var par)) continue;
                best = Consider(par.p1, g, best, ref best_eff, ref best_gen);
                best = Consider(par.p2, g, best, ref best_eff, ref best_gen);
                if (par.p1 > 0) next.Add(par.p1);
                if (par.p2 > 0) next.Add(par.p2);
            }
            frontier = next;
            if (frontier.Count == 0) break;
        }
        return best;
    }

    private static BloodlineAsset Consider(long id, int gen, BloodlineAsset best, ref float best_eff, ref int best_gen)
    {
        if (id <= 0) return best;
        var asset = Libraries.Manager.BloodlineLibrary.get($"bloodline_{id}");
        if (asset == null) return best;
        // 衰减后实际强度：战力 × 衰减^(代数-1)
        float eff = asset.snapshot_power_level * Mathf.Pow(KnightSetting.BloodlineDecayPerGen, gen - 1);
        if (eff > best_eff)
        {
            best_eff = eff;
            best = asset;
            best_gen = gen;
        }
        return best;
    }
}
