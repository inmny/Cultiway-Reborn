using System.IO;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Content.Skills;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Core.SkillLibV2.Predefined.Modifiers;
using Cultiway.Patch;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;

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

            if (a.HasComponent<XianBase>())
            {
                ref XianBase xian_base = ref a.GetComponent<XianBase>();
                sb.AppendLine("筑基情况:");
                sb.AppendLine($"\t精: {xian_base.jing}");
                sb.AppendLine($"\t气: {xian_base.qi}");
                sb.AppendLine($"\t神: {xian_base.shen}");
                sb.AppendLine($"\t火: {xian_base.fire}");
                sb.AppendLine($"\t木: {xian_base.wood}");
                sb.AppendLine($"\t土: {xian_base.earth}");
                sb.AppendLine($"\t金: {xian_base.iron}");
                sb.AppendLine($"\t水: {xian_base.water}");
            }

            if (a.HasComponent<Jindan>())
            {
                ref Jindan jindan = ref a.GetComponent<Jindan>();
                sb.AppendLine($"金丹: {jindan.Type.GetName()}");
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

    internal static void TakeWakanAndCultivate(ActorExtend actor_extend, ref Xian xian)
    {
        Vector2Int tile_pos = actor_extend.Base.currentTile.pos;
        var to_take = Mathf.Log10(WakanMap.I.map[tile_pos.x, tile_pos.y] + 1);

        var max_wakan = actor_extend.Base.stats[BaseStatses.MaxWakan.id];
        xian.wakan = Mathf.Min(xian.wakan + to_take * actor_extend.GetElementRoot().GetStrength(), max_wakan);
        WakanMap.I.map[tile_pos.x, tile_pos.y] -= to_take;
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
        ElementRoot er = ae.GetElementRoot();
        if (xian_base.jing == 0)
        {
            var intelligence = ae.GetStat(S.intelligence);
            if (!allow_first(intelligence))
            {
                component.wakan = 0;
                ae.EnhanceSkillRandomly(SkillEnhanceSources.SmallUpgradeFailed);
                return false;
            }

            xian_base.jing = sample_first(intelligence);
        }
        else if (xian_base.qi == 0)
        {
            var intelligence = ae.GetStat(S.intelligence);
            if (!allow_first(intelligence))
            {
                component.wakan = 0;
                ae.EnhanceSkillRandomly(SkillEnhanceSources.SmallUpgradeFailed);
                return false;
            }

            xian_base.qi = sample_first(intelligence);
        }
        else if (xian_base.shen == 0)
        {
            var intelligence = ae.GetStat(S.intelligence);
            if (!allow_first(intelligence))
            {
                component.wakan = 0;
                ae.EnhanceSkillRandomly(SkillEnhanceSources.SmallUpgradeFailed);
                return false;
            }

            xian_base.shen = sample_first(intelligence);
        }
        else if (xian_base.fire == 0)
        {
            if (!allow_second(er.Fire))
            {
                component.wakan = 0;
                ae.EnhanceSkillRandomly(SkillEnhanceSources.SmallUpgradeFailed);
                return false;
            }

            xian_base.fire = sample_second(er.Fire);
        }
        else if (xian_base.wood == 0)
        {
            if (!allow_second(er.Wood))
            {
                component.wakan = 0;
                ae.EnhanceSkillRandomly(SkillEnhanceSources.SmallUpgradeFailed);
                return false;
            }

            xian_base.wood = sample_second(er.Wood);
        }
        else if (xian_base.earth == 0)
        {
            if (!allow_second(er.Earth))
            {
                component.wakan = 0;
                ae.EnhanceSkillRandomly(SkillEnhanceSources.SmallUpgradeFailed);
                return false;
            }

            xian_base.earth = sample_second(er.Earth);
        }
        else if (xian_base.iron == 0)
        {
            if (!allow_second(er.Iron))
            {
                component.wakan = 0;
                ae.EnhanceSkillRandomly(SkillEnhanceSources.SmallUpgradeFailed);
                return false;
            }

            xian_base.iron = sample_second(er.Iron);
        }
        else if (xian_base.water == 0)
        {
            if (!allow_second(er.Water))
            {
                component.wakan = 0;
                ae.EnhanceSkillRandomly(SkillEnhanceSources.SmallUpgradeFailed);
                return false;
            }

            xian_base.water = sample_second(er.Water);
        }
        else
        {
            var strength = xian_base.GetFiveQiStrength() * xian_base.GetThreeHuaStrength();
            if (RdUtils.NextNormal_0_6() > strength)
            {
                component.wakan = 0;
                ae.EnhanceSkillRandomly(SkillEnhanceSources.LargeUpgradeFailed);
                return false;
            }

            e.AddComponent(new Jindan
            (
                Libraries.Manager.JindanLibrary.GetJindan(ae, ref xian_base).id,
                strength
            ));
            ae.AddSkillModifier<ScaleModifier, float>(CommonWeaponSkills.StartWeaponSkill.id, new ScaleModifier(Toolbox.randomFloat(1, 4)));
            return true;
        }

        return false;

        bool allow_first(float p)
        {
            var sample = RdUtils.NextNormal_0_6();
            return Mathf.Abs(sample) < p;
        }

        float sample_first(float p)
        {
            return Mathf.Abs(RdUtils.NextStdNormal() * p);
        }

        bool allow_second(float p)
        {
            var sample = RdUtils.NextStdNormal();
            return Mathf.Abs(sample) < p;
        }

        float sample_second(float p)
        {
            return Mathf.Abs(RdUtils.NextStdNormal() * p);
        }
    }
}