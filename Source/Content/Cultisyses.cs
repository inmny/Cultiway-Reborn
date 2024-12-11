using System.IO;
using Cultiway.Abstract;
using Cultiway.Content.Const;
using Cultiway.Content.CultisysComponents;
using Cultiway.Content.Skills;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Patch;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;

namespace Cultiway.Content;

[Dependency(typeof(BaseStatses), typeof(CommonWeaponSkills))]
public class Cultisyses : ExtendLibrary<BaseCultisysAsset, Cultisyses>
{
    public static CultisysAsset<Xian> Xian { get; private set; }

    protected override void OnInit()
    {
        Xian = (CultisysAsset<Xian>)Add(new CultisysAsset<Xian>(nameof(Xian), 20, new Xian(),
            [
                null, XianPreCheckUpgrade, XianPreCheckUpgrade, null, null, null, null, null,
                null, null,
                null, null, null, null, null, null, null, null,
                null, null,
            ],
            [
                null, XianCheckUpgrade, CheckUpgradeToJindan, null, null, null, null, null,
                null, null,
                null, null, null, null, null, null, null, null,
                null, null,
            ],
            [
                null, null, null, null, null, null, null, null,
                null, null,
                null, null, null, null, null, null, null, null,
                null, null,
            ],
            [
                null, [CommonWeaponSkills.StartWeaponSkill.id], null, null, null, null, null, null,
                null, null,
                null, null, null, null, null, null, null, null,
                null, null
            ]));
        LoadStatsForXian();


        ActorExtend.RegisterActionOnNewCreature((ae) =>
        {
            if (!ae.HasElementRoot()) return;
            ref var element_root = ref ae.GetElementRoot();
            if (!ContentSetting.AllXian && element_root.Type == ModClass.L.ElementRootLibrary.Common) return;
            ae.NewCultisys(Xian);
            ae.Base.data.favorite = true;
        });
        ActorExtend.RegisterActionOnUpdateStats([Hotfixable](ae) =>
        {
            if (!ae.HasCultisys<Xian>()) return;
            var curr_level = ae.GetCultisys<Xian>().CurrLevel;
            ae.Base.stats.mergeStats(Xian.LevelAccumBaseStats[curr_level]);
            ae.tmp_all_skills.UnionWith(Xian.Skills[curr_level]);
        });
        PatchWindowCreatureInfo.RegisterInfoDisplay((a, sb) =>
        {
            if (a.HasCultisys<Xian>())
            {
                ref var xian_info = ref a.GetCultisys<Xian>();
                sb.AppendLine($"{xian_info.Asset.GetName()}: {xian_info.Asset.GetLevelName(xian_info.CurrLevel)}");
            }
        });
    }

    private void LoadStatsForXian()
    {
        var csv = CSVUtils.ReadCSV(File.ReadAllText(Path.Combine(ModClass.I.GetDeclaration().FolderPath,
            XianSetting.StatsPath)));
        var offset = 0;
        var keys = csv[offset++];
        _ = csv[offset++];
        for (int i = 0; i < Xian.LevelNumber; i++)
        {
            var line = csv[i + offset];
            var stats = Xian.LevelBaseStats[i];
            stats.clear();
            for (int j = 0; j < keys.Length; j++)
            {
                var key = keys[j];
                if (!AssetManager.base_stats_library.Contains(key)) continue;

                stats[key] = float.Parse(line[j]);
            }
        }

        Xian.UpdateAccumStats();
    }

    public override void OnReload()
    {
        LoadStatsForXian();
    }

    private static bool XianPreCheckUpgrade(ActorExtend ae, CultisysAsset<Xian> cultisys, ref Xian component)
    {
        return component.wakan / ae.Base.stats[BaseStatses.MaxWakan.id] > XianSetting.CommonPreUpgradeWakanRatio;
    }

    private static bool XianCheckUpgrade(ActorExtend ae, CultisysAsset<Xian> cultisys, ref Xian component)
    {
        return component.wakan >= ae.Base.stats[BaseStatses.MaxWakan.id] - 0.1f;
    }

    private static bool CheckUpgradeToJindan(ActorExtend ae, CultisysAsset<Xian> cultisys, ref Xian component)
    {
        if (component.wakan < ae.Base.stats[BaseStatses.MaxWakan.id] - 0.1f) return false;

        Entity e = ae.E;
        if (!e.HasComponent<XianBase>()) e.AddComponent<XianBase>();

        ref XianBase xian_base = ref e.GetComponent<XianBase>();

        return true;
    }
}