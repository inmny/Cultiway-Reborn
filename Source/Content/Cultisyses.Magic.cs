using System.IO;
using System.Linq;
using System.Text;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Patch;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content;

public partial class Cultisyses
{
    public static CultisysAsset<Magic> Magic { get; private set; }

    private static readonly int[] _maxSpellRingByLevel = new int[MagicSetting.LevelNumber];
    private static readonly int[] _knownSpellCapacityByLevel = new int[MagicSetting.LevelNumber];

    /// <summary>取得指定魔法等级能够理解的最高法术环级。</summary>
    public static int GetMaxSpellRing(int level) => _maxSpellRingByLevel[level];

    /// <summary>取得指定魔法等级能够长期掌握的法术数量。</summary>
    public static int GetKnownSpellCapacity(int level) => _knownSpellCapacityByLevel[level];

    private void InitMagic()
    {
        Magic = (CultisysAsset<Magic>)Add(new CultisysAsset<Magic>(nameof(Magic), MagicSetting.LevelNumber, new Magic(),
            Enumerable.Range(0, MagicSetting.LevelNumber).Select(_ => (CultisysAsset<Magic>.CheckUpgrade)MagicPreCheckUpgrade).ToArray(),
            Enumerable.Range(0, MagicSetting.LevelNumber).Select(_ => (CultisysAsset<Magic>.CheckUpgrade)MagicCheckUpgrade).ToArray(),
            null, null, null));
        SetupMagicDisplayStyle();
        LoadStatsForMagic();

        ActorExtend.RegisterActionOnNewCreature((ae) =>
        {
            if (!ae.HasElementRoot()) return;
            if (!GetAvailableCultisysIds(ae).Contains(nameof(Magic))) return;
            ae.NewCultisys(Magic);
            ModClass.I.WorldRecord.CheckAndLogFirstLevelup(Magic.id, ae, ref ae.GetCultisys<Magic>());
        });

        ActorExtend.RegisterCachedStatsBuilder([Hotfixable](ae, stats) =>
        {
            if (!ae.TryGetComponent(out Magic magic)) return;
            stats.mergeStats(Magic.LevelAccumBaseStats[magic.CurrLevel]);
        });

        ActorExtend.RegisterDefensePowerLevelResolver(MagicDefensePowerLevelResolver);

        PatchWindowCreatureInfo.RegisterInfoDisplay((a, sb) =>
        {
            if (!a.HasCultisys<Magic>()) return;
            ref var magic_info = ref a.GetCultisys<Magic>();
            sb.AppendLine($"{magic_info.Asset.GetName()}: {magic_info.Asset.GetLevelName(magic_info.CurrLevel)}");
            sb.AppendLine($"精神力: {magic_info.spirit} / {a.Base.stats[BaseStatses.MaxSpirit.id]}");
        });
    }

    private static void SetupMagicDisplayStyle()
    {
        Magic.DisplayStyle = new ElementRootDisplayStyle
        {
            category_label_key   = "Cultiway.ERStyle.Magic.Category",
            components_label_key = "Cultiway.ERStyle.Magic.Components",
            overall_label_key    = "Cultiway.ERStyle.Magic.Overall",
            page_title_key       = "Cultiway.ERStyle.Magic.PageTitle",
            stage_count          = 12,
            level_per_stage      = 3,
            stage_name_keys      = Enumerable.Range(0, 12)
                .Select(i => $"Cultiway.ERStyle.Magic.Stage.{i}").ToArray(),
            level_name_keys      =
            [
                "Cultiway.ERStyle.Magic.Level.0",
                "Cultiway.ERStyle.Magic.Level.1",
                "Cultiway.ERStyle.Magic.Level.2"
            ],
            level_format         = "{stage}{level}阶",
            element_root_name_prefix = "Cultiway.ER.Magic",
            element_root_desc_prefix = "Cultiway.ER.Magic"
        };
    }

    private void LoadStatsForMagic()
    {
        var csv = CSVUtils.ReadCSV(File.ReadAllText(Path.Combine(ModClass.I.GetDeclaration().FolderPath,
            MagicSetting.StatsPath)));
        var keys = csv[0];
        _ = csv[1];

        // 定位不属于 BaseStats 的魔法体系规则列。
        int maxSpellRingCol = -1;
        int knownSpellCapacityCol = -1;
        for (int j = 0; j < keys.Length; j++)
        {
            switch (keys[j])
            {
                case "MaxSpellRing":
                    maxSpellRingCol = j;
                    break;
                case "KnownSpellCapacity":
                    knownSpellCapacityCol = j;
                    break;
            }
        }

        for (int i = 0; i < Magic.LevelNumber; i++)
        {
            var line = csv[i + 2];
            var stats = Magic.LevelBaseStats[i];
            stats.clear();
            for (int j = 0; j < keys.Length; j++)
            {
                var key = keys[j];
                if (!AssetManager.base_stats_library.Contains(key)) continue;
                stats[key] = float.Parse(line[j]);
            }
            if (maxSpellRingCol >= 0)
            {
                _maxSpellRingByLevel[i] = int.Parse(line[maxSpellRingCol]);
            }
            if (knownSpellCapacityCol >= 0)
            {
                _knownSpellCapacityByLevel[i] = int.Parse(line[knownSpellCapacityCol]);
            }
        }

        Magic.UpdateAccumStats();
    }

    private static bool MagicPreCheckUpgrade(ActorExtend ae, CultisysAsset<Magic> cultisys, ref Magic component)
    {
        return component.spirit / ae.Base.stats[BaseStatses.MaxSpirit.id] > MagicSetting.CommonPreUpgradeSpiritRatio;
    }

    private static bool MagicCheckUpgrade(ActorExtend ae, CultisysAsset<Magic> cultisys, ref Magic component)
    {
        return component.spirit >= ae.Base.stats[BaseStatses.MaxSpirit.id] - 0.1f;
    }

    /// <summary>
    /// 魔法 mana 护盾：PowerLevel 防御需消耗 mana 维持。
    /// mana 足够时正常防御（返回真实 PL），不足时完全取消防御（返回 0）。
    /// 非魔法生物或 gap<=0 时返回 null（不处理，回退默认）。
    /// </summary>
    private static float? MagicDefensePowerLevelResolver(ActorExtend target, float attacker_power_level, float damage)
    {
        if (!target.E.HasComponent<Magic>()) return null;

        var power_level = target.GetPowerLevel();
        var gap = power_level - attacker_power_level + 1;
        if (gap <= 0) return null;

        // 按豁免量消耗 mana
        var absorbed = damage - Mathf.Log(Mathf.Max(damage, 1),
            Mathf.Pow(DamageCalcHyperParameters.PowerBase, gap));
        var mana_cost = Mathf.CeilToInt(absorbed * Mathf.Pow(MagicSetting.ManaShieldCostRatio, gap - 1));
        if (mana_cost > 0 && target.Base.getMana() >= mana_cost)
        {
            target.Base.setMana(target.Base.getMana() - mana_cost);
            // 减免成功，触发护盾受击特效
            if (target.Base.is_visible)
            {
                var hit_fx = EffectsLibrary.spawnAt("fx_shield_hit", target.Base.current_position, 1f);
                hit_fx?.attachTo(target.Base.a);
            }
            return power_level;
        }
        return 0;
    }
}
