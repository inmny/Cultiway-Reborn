using System.IO;
using System.Linq;
using System.Text;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Patch;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content;

public partial class Cultisyses
{
    public static CultisysAsset<Magic> Magic { get; private set; }

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
}
