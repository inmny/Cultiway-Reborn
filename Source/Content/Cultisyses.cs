using System.IO;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.AIGC;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
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
using strings;
using UnityEngine;

namespace Cultiway.Content;

[Dependency(typeof(BaseStatses), typeof(WrappedSkills))]
public class Cultisyses : ExtendLibrary<BaseCultisysAsset, Cultisyses>
{
    public static CultisysAsset<Xian> Xian { get; private set; }

    protected override void OnInit()
    {
        Xian = (CultisysAsset<Xian>)Add(new CultisysAsset<Xian>(nameof(Xian), 20, new Xian(),
            [
                null, XianPreCheckUpgrade, XianPreCheckUpgrade, XianPreCheckUpgrade, null, null, null, null,
                null, null,
                null, null, null, null, null, null, null, null,
                null, null,
            ],
            [
                null, XianCheckUpgrade, CheckUpgradeToJindan, CheckUpgradeToYuanying, null, null, null, null,
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
                null, [WrappedSkills.StartWeaponSkill.id], null, null, null, null, null, null,
                null, null,
                null, null, null, null, null, null, null, null,
                null, null
            ],
            null,
            [
                null, [Hotfixable](ae) =>
                {
                    var res = 0f;
                    if (ae.HasComponent<XianBase>())
                    {
                        res += 0.01f;
                        var xian_base = ae.GetComponent<XianBase>();
                        if (xian_base.jing > 0) res += 0.01f;
                        if (xian_base.qi > 0) res += 0.01f;
                        if (xian_base.shen > 0) res += 0.01f;
                        if (xian_base.fire > 0) res += 0.01f;
                        if (xian_base.wood > 0) res += 0.01f;
                        if (xian_base.earth > 0) res += 0.01f;
                        if (xian_base.iron > 0) res += 0.01f;
                        if (xian_base.water > 0) res += 0.01f;
                    }
                    return res;
                },
                [Hotfixable](ae) =>
                {
                    var res = 0f;
                    if (ae.HasComponent<Jindan>())
                    {
                        res += 0.01f;
                        var jindan = ae.GetComponent<Jindan>();
                        res += 0.9f * (1 - 1f / (jindan.stage + 1));
                    }
                    return res;
                }, null, null, null, null, null,
                null, null,
                null, null, null, null, null, null, null, null,
                null, null,
            ]));
        LoadStatsForXian();


        ActorExtend.RegisterActionOnNewCreature((ae) =>
        {
            if (!ae.HasElementRoot()) return;
            ref var element_root = ref ae.GetElementRoot();
            if (!ContentSetting.AllXian && element_root.Type == ModClass.L.ElementRootLibrary.Common) return;
            ae.NewCultisys(Xian);
            
            ModClass.I.WorldRecord.CheckAndLogFirstLevelup(Xian.id, ae, ref ae.GetCultisys<Xian>());
            if (ae.Base.asset == Actors.Plant)
            {
                ae.Base.setName(PlantNameGenerator.Instance.GenerateName([element_root.Type.GetName()]));
            }
        });
        ActorExtend.RegisterActionOnUpdateStats([Hotfixable](ae) =>
        {
            if (!ae.TryGetComponent(out Xian xian)) return;
            var curr_level = xian.CurrLevel;
            var stats = ae.Base.stats;
            stats.mergeStats(Xian.LevelAccumBaseStats[curr_level]);
            if (ae.TryGetComponent(out XianBase xian_base))
            {
                
            }
            if (ae.TryGetComponent(out Jindan jindan))
            {
                var jindan_asset = jindan.Type;
                stats.MergeStats(jindan_asset.Stats, jindan.strength);
                if (!string.IsNullOrEmpty(jindan_asset.wrapped_skill_id))
                {
                    ae.tmp_all_skills.Add(jindan_asset.wrapped_skill_id);
                    return;
                }
            }

            if (ae.HasCultibook())
            {
                foreach (var cultibook_master in ae.GetAllMaster<CultibookAsset>())
                {
                    stats.MergeStats(cultibook_master.Item1.FinalStats, cultibook_master.Item2 / 100f);
                }
            }
            
            
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

            if (a.HasComponent<Yuanying>())
            {
                ref Yuanying yuanying = ref a.GetComponent<Yuanying>();
                sb.AppendLine($"元婴: {yuanying.Type.GetName()}");
            }
        });
    }
    [Hotfixable]
    private bool CheckUpgradeToYuanying(ActorExtend ae, CultisysAsset<Xian> cultisys, ref Xian component)
    {
        var a = ae.Base;
        if (component.wakan < a.stats[BaseStatses.MaxWakan.id] - 0.1f) return false;

        Entity e = ae.E;
        
        ref var jindan = ref e.GetComponent<Jindan>();
        var intelligence = ae.GetStat(S.intelligence);
        if (
            // 达到九转有概率选择突破元婴，有野心的会倾向于打磨金丹
            (jindan.stage >= 9 && Randy.randomChance(a.hasTrait(WorldboxGame.ActorTraits.Ambitious.id) ? 0.13f : 0.5f))
            // 当寿元不足时，必定会选择突破元婴
            || (!a.hasTrait(ActorTraits.Immortal.id) && a.data.getAge() / a.stats[S.lifespan] > 0.9f))
        {
            var xian_base = e.GetComponent<XianBase>();
            if (jindan.stage < 9)
            {
                // 未满九转金丹可能会死
            }

            if (!a.hasTrait(WorldboxGame.ActorTraits.ScarOfDivinity.id))
            {
                PersistentLogger.Get("JindanStats.log").Log($"{jindan.stage}, {xian_base.GetStrength()}, {jindan.strength}");
            }
            var yuanying = Libraries.Manager.YuanyingLibrary.GetRandomYuanying(jindan.Type);

            if (!string.IsNullOrEmpty(jindan.Type.wrapped_skill_id))
                ae.LearnSkill(jindan.Type.wrapped_skill_id);
            e.AddComponent(new Yuanying
            (
                yuanying.id,
                jindan.strength
            ));
            e.RemoveComponent<Jindan>();
            return true;
        }
        if (jindan.stage < 10000)
        {
            if (!allow_first(intelligence, jindan.stage))
            {
                component.wakan = 0;
                ae.EnhanceSkillRandomly(SkillEnhanceSources.SmallUpgradeFailed);
                return false;
            }
            
            if (!string.IsNullOrEmpty(jindan.Type.wrapped_skill_id))
            {
                ae.EnhanceSkill(jindan.Type.wrapped_skill_id, SkillEnhanceSources.SmallUpgradeSuccess);
            }
            else
            {
                ae.EnhanceSkillRandomly(SkillEnhanceSources.SmallUpgradeSuccess);
            }
            jindan.stage++;
            jindan.strength *= (1f + 0.2f * Randy.randomFloat(intelligence / (10 + intelligence), 1));
            component.wakan *= 0.8f;
            
            return false;
        }
        component.wakan *= 0.6f;
        
        return false;
        bool allow_first(float p,int stage)
        {
            var sample = RdUtils.NextNormal_0_6();
            return Mathf.Abs(sample) * (stage+1) < p;
        }
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
    [Hotfixable]
    internal static void TakeWakanAndCultivate(ActorExtend actor_extend, ref Xian xian)
    {
        var max_wakan = actor_extend.Base.stats[BaseStatses.MaxWakan.id];
        if (xian.wakan >= max_wakan) return;
        Vector2Int tile_pos = actor_extend.Base.current_tile.pos;
        var total = WakanMap.I.map[tile_pos.x, tile_pos.y];
        var to_take = Mathf.Log10(total + 1);

        to_take = Mathf.Min(max_wakan - xian.wakan, total, to_take * actor_extend.GetElementRoot().GetStrength());
        xian.wakan += to_take;
        WakanMap.I.map[tile_pos.x, tile_pos.y] -= to_take;
    }
    internal static void OutWakanAndCultivate(ActorExtend actor_extend, ref Xian xian)
    {
        var max_wakan = actor_extend.Base.stats[BaseStatses.MaxWakan.id];
        if (xian.wakan >= max_wakan) return;
        Vector2Int tile_pos = actor_extend.Base.current_tile.pos;
        var total = WakanMap.I.map[tile_pos.x, tile_pos.y];
        var to_take = Mathf.Log10(total + 1);

        to_take = Mathf.Min(max_wakan - xian.wakan, total, to_take * actor_extend.GetElementRoot().GetStrength());
        xian.wakan += to_take;
        var dirty_wakan_to_take = Mathf.Min(DirtyWakanMap.I.map[tile_pos.x, tile_pos.y],
            to_take * ContentSetting.DirtyWakanToWakanRatio);
        WakanMap.I.map[tile_pos.x, tile_pos.y] += dirty_wakan_to_take;
        DirtyWakanMap.I.map[tile_pos.x, tile_pos.y] -= dirty_wakan_to_take;
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
            var jindan = Libraries.Manager.JindanLibrary.GetJindan(ae, ref xian_base);
            e.AddComponent(new Jindan
            (
                jindan.id,
                strength
            ));
            if (ae.Base.asset == Actors.Plant)
            {
                ae.Base.setName(PlantNameGenerator.Instance.GenerateName([er.Type.GetName(), jindan.GetName()]));
            }
            ae.AddSkillModifier<ScaleModifier, float>(CommonWeaponSkills.StartWeaponSkill.id, new ScaleModifier(Randy.randomFloat(1, 4)));
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