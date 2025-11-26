using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Extensions;

public static class BookManagerTools
{
    private static CultibookLibrary _cultibookLibrary = Libraries.Manager.CultibookLibrary;

    public static Book CreateNewSkillbook(this BookManager manager, Actor creator, Entity skill_container)
    {
        var ae = creator.GetExtend();
        var raw_skillbook = manager.GenerateNewBook(creator, BookTypes.Skillbook);
        if (raw_skillbook == null) return null;
        var be = raw_skillbook.GetExtend();

        skill_container = skill_container.Store.CloneEntity(skill_container);
        var skillbook = new Skillbook()
        {
            SkillContainer = skill_container
        };
        be.AddComponent(skillbook);
        be.E.AddRelation(new SkillMasterRelation()
        {
            SkillContainer = skill_container
        });
        return raw_skillbook;
    }
    public static Book CreateNewCultibook(this BookManager manager, Actor creator)
    {
        var ae = creator.GetExtend();
        var raw_cultibook = manager.GenerateNewBook(creator, BookTypes.Cultibook);
        if (raw_cultibook == null)
        {
            return null;
        }
        var be = raw_cultibook.GetExtend();
        var stats = new BaseStats();

        void SoftmaxStats(IList<string> stat_ids)
        {
            (string, float)[] armor_stats = new (string, float)[stat_ids.Count];
            var exp_sum = 0f;
            var max_val = 0f;
            for (int i = 0; i < armor_stats.Length; i++)
            {
                var stat_id = stat_ids[i];
                var stat_value = creator.stats[stat_id];
                max_val = Mathf.Max(max_val, stat_value);
                armor_stats[i] = (stat_id, stat_value);
            }

            for (int i = 0; i < armor_stats.Length; i++)
            {
                var stat_value = armor_stats[i].Item2;
                var exp_value = Mathf.Exp(stat_value - max_val);
                armor_stats[i].Item2 = exp_value;
                exp_sum += exp_value;
            }

            for (int i = 0; i < armor_stats.Length; i++)
            {
                armor_stats[i].Item2 /= exp_sum;
            }
            Array.Sort(armor_stats, (a, b) => b.Item2.CompareTo(a.Item2));
            var accum_prob = 0f;
            var stop_idx = 0;
            for (int i = 0; i < armor_stats.Length; i++)
            {
                var prob = armor_stats[i].Item2;
                accum_prob += prob;
                if (accum_prob > 0.9f)
                {
                    stop_idx = i;
                    break;
                }
            }

            for (int i = 0; i <= stop_idx; i++)
            {
                var stat_id = armor_stats[i].Item1;
                var stat_value = armor_stats[i].Item2 / accum_prob;
                stats[WorldboxGame.BaseStats.StatsToModStats[stat_id]] = stat_value;
            }

        }
        SoftmaxStats(WorldboxGame.BaseStats.ArmorStats);
        SoftmaxStats(WorldboxGame.BaseStats.MasterStats);
        
        // 生成灵根需求（基于创建者的灵根）
        ElementRequirement elementReq = new();
        if (ae.HasElementRoot())
        {
            var elementRoot = ae.GetElementRoot();
            // 设置最低需求为创建者灵根的70%，确保创建者自己能够修炼
            elementReq.MinIron = Mathf.Max(0f, elementRoot.Iron * 0.7f);
            elementReq.MinWood = Mathf.Max(0f, elementRoot.Wood * 0.7f);
            elementReq.MinWater = Mathf.Max(0f, elementRoot.Water * 0.7f);
            elementReq.MinFire = Mathf.Max(0f, elementRoot.Fire * 0.7f);
            elementReq.MinEarth = Mathf.Max(0f, elementRoot.Earth * 0.7f);
            elementReq.MinNeg = Mathf.Max(0f, elementRoot.Neg * 0.7f);
            elementReq.MinPos = Mathf.Max(0f, elementRoot.Pos * 0.7f);
            elementReq.MinEntropy = Mathf.Max(0f, elementRoot.Entropy * 0.7f);
        }
        
        // 生成境界限制（基于创建者的当前境界）
        int minLevel = 0;
        int maxLevel = 20;
        if (ae.HasCultisys<Xian>())
        {
            var xian = ae.GetCultisys<Xian>();
            // 最低境界为创建者当前境界-2（向下兼容），最高境界为当前境界+5
            minLevel = Mathf.Max(0, xian.CurrLevel - 2);
            maxLevel = Mathf.Min(20, xian.CurrLevel + 5);
        }
        
        // 获取修炼方式ID（如果有主修功法，使用其修炼方式；否则从库中随机选择一个合适的）
        string cultivateMethodId;
        var mainCultibook = ae.GetMainCultibook();
        if (mainCultibook != null && !string.IsNullOrEmpty(mainCultibook.CultivateMethodId))
        {
            cultivateMethodId = mainCultibook.CultivateMethodId;
        }
        else
        {
            // 从CultivateMethodLibrary中筛选合适的修炼方式，选择效率最高的
            CultivateMethodAsset bestMethod = null;
            float bestEfficiency = float.MinValue;
            var library = Libraries.Manager.CultivateMethodLibrary;
            
            foreach (var method in library.list)
            {
                // 优先选择主动修炼方式（Active），更适合作为功法修炼方式
                if (method.TriggerType != CultivateTriggerType.Active) continue;
                
                // 检查创建者是否可以使用此修炼方式
                if (method.CanCultivate != null && !method.CanCultivate(ae)) continue;
                
                // 计算此修炼方式的效率
                float efficiency = 1.0f;
                if (method.GetEfficiency != null)
                {
                    efficiency = method.GetEfficiency(ae);
                }
                
                // 选择效率最高的
                if (efficiency > bestEfficiency)
                {
                    bestEfficiency = efficiency;
                    bestMethod = method;
                }
            }
            
            // 如果找到合适的，使用效率最高的；否则使用标准方式
            if (bestMethod != null)
            {
                cultivateMethodId = bestMethod.id;
            }
            else
            {
                // 如果没有找到合适的Active方式，使用标准方式
                cultivateMethodId = CultivateMethods.Standard.id;
            }
        }
        
        // 生成法术池（从创建者已学会的技能中选择一部分）
        List<SkillPoolEntry> skillPool = new();
        if (ae.all_skills != null && ae.all_skills.Count > 0)
        {
            // 随机选择0-3个技能加入法术池
            var skillsToAdd = Mathf.Min(3, ae.all_skills.Count);
            var skillList = new List<Entity>(ae.all_skills);
            // 随机打乱
            for (int i = 0; i < skillList.Count; i++)
            {
                var randomIndex = UnityEngine.Random.Range(i, skillList.Count);
                (skillList[i], skillList[randomIndex]) = (skillList[randomIndex], skillList[i]);
            }
            
            int addedCount = 0;
            for (int i = 0; i < skillList.Count && addedCount < skillsToAdd; i++)
            {
                var skillEntity = skillList[i];
                if (!skillEntity.HasComponent<SkillContainer>()) continue;
                
                ref var skillContainer = ref skillEntity.GetComponent<SkillContainer>();
                if (string.IsNullOrEmpty(skillContainer.SkillEntityAssetID)) continue;
                
                skillPool.Add(new SkillPoolEntry()
                {
                    SkillEntityAssetId = skillContainer.SkillEntityAssetID,
                    BaseChance = 0.05f + addedCount * 0.02f, // 第一个技能5%基础概率，后续递增
                    MasteryThreshold = 20f + addedCount * 20f, // 第一个需要20%掌握，后续递增
                    LevelRequirement = minLevel + addedCount * 2 // 根据境界要求递增
                });
                addedCount++;
            }
        }
        
        // 生成描述
        string description = raw_cultibook.name;
        if (ae.HasElementRoot())
        {
            var elementType = ae.GetElementRoot().Type;
            if (elementType != null)
            {
                description = $"{elementType.GetName()}系功法，适合{raw_cultibook.name}修炼。";
            }
        }
        
        var cultibook = _cultibookLibrary.AddDynamic(new CultibookAsset()
        {
            id = Guid.NewGuid().ToString(),
            FinalStats = stats,
            Level = new ItemLevel(),
            Name = raw_cultibook.name,
            Description = description,
            ElementReq = elementReq,
            ElementAffinityThreshold = 0.3f, // 默认契合阈值
            MinLevel = minLevel,
            MaxLevel = maxLevel,
            CultivateMethodId = cultivateMethodId,
            SkillPool = skillPool,
            ConflictTags = Array.Empty<string>(), // 暂时不生成冲突标签
            SynergyTags = Array.Empty<string>() // 暂时不生成协同标签
        });
        be.AddComponent(new Cultibook(cultibook.id));
        be.AddComponent(cultibook.Level);
        be.Master(cultibook, 100);
        ae.Master(cultibook, 100);
        return raw_cultibook;
    }
}