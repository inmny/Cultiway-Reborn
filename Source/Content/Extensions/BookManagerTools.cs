using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
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
            // 从CultivateMethodLibrary中筛选合适的修炼方式，使用效率作为权重随机选择
            var library = Libraries.Manager.CultivateMethodLibrary;
            var candidateMethods = new List<CultivateMethodAsset>();
            var accumWeights = new List<float>();
            float totalWeight = 0f;
            
            foreach (var method in library.list)
            {
                // 检查创建者是否可以使用此修炼方式
                if (method.CanCultivate != null && !method.CanCultivate(ae)) continue;
                
                // 计算此修炼方式的效率作为权重
                float efficiency = 1.0f;
                if (method.GetEfficiency != null)
                {
                    efficiency = method.GetEfficiency(ae);
                }
                
                // 如果效率为0或负数，跳过
                if (efficiency <= 0f) continue;
                
                candidateMethods.Add(method);
                totalWeight += efficiency;
                accumWeights.Add(totalWeight);
            }
            
            // 如果找到合适的，使用加权随机选择；否则使用标准方式
            if (candidateMethods.Count > 0)
            {
                var selectedIndex = RdUtils.RandomIndexWithAccumWeight(accumWeights);
                cultivateMethodId = candidateMethods[selectedIndex].id;
            }
            else
            {
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
            skillList.Shuffle();
            
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
        
        // 根据功法信息判断品阶
        ItemLevel itemLevel = CalculateCultibookLevel(stats, skillPool, minLevel, maxLevel, cultivateMethodId, ae);
        
        var cultibook = _cultibookLibrary.AddDynamic(new CultibookAsset()
        {
            id = Guid.NewGuid().ToString(),
            FinalStats = stats,
            Level = itemLevel,
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
    
    /// <summary>
    /// 根据功法信息计算品阶
    /// </summary>
    private static ItemLevel CalculateCultibookLevel(
        BaseStats finalStats, 
        List<SkillPoolEntry> skillPool, 
        int minLevel, 
        int maxLevel, 
        string cultivateMethodId,
        ActorExtend creator)
    {
        // 计算属性总值（所有属性值的绝对值之和）
        float statsValue = 0f;
        if (finalStats != null && finalStats._stats_list is IList<BaseStatsContainer> statsList)
        {
            foreach (var container in statsList)
            {
                statsValue += Mathf.Abs(container.value);
            }
        }
        
        // 法术池数量
        int skillCount = skillPool?.Count ?? 0;
        
        // 创建者境界
        int creatorLevel = 0;
        if (creator.HasCultisys<Xian>())
        {
            creatorLevel = creator.GetCultisys<Xian>().CurrLevel;
        }
        
        // 修炼方式特殊性（特殊修炼方式提升品阶）
        // 判断是否为标准修炼方式（Cultiway.Standard 或 Standard）
        bool isSpecialMethod = !string.IsNullOrEmpty(cultivateMethodId) && 
                               cultivateMethodId != CultivateMethods.Standard.id;
        
        // 境界范围（范围越广，品阶可能越高）
        int levelRange = maxLevel - minLevel;
        
        // 综合评分系统
        float score = 0f;
        
        // 1. 属性强度评分（0-40分）
        // 属性总值每1.0分对应约10分，最高40分
        score += Mathf.Min(statsValue * 10f, 40f);
        
        // 2. 法术池数量评分（0-20分）
        // 每个法术5分，最高20分
        score += Mathf.Min(skillCount * 5f, 20f);
        
        // 3. 创建者境界评分（0-20分）
        // 境界越高，能创造的功法品阶越高
        // 筑基期(1) = 0分，金丹期(2) = 5分，元婴期(3) = 10分，更高境界 = 20分
        if (creatorLevel >= 6) score += 20f;
        else if (creatorLevel >= 3) score += 10f + (creatorLevel - 3) * 3.33f;
        else if (creatorLevel >= 2) score += 5f;
        
        // 4. 特殊修炼方式奖励（0-10分）
        if (isSpecialMethod) score += 10f;
        
        // 5. 境界范围奖励（0-10分）
        // 范围越广，说明功法适用性越高
        if (levelRange >= 15) score += 10f;
        else if (levelRange >= 10) score += 5f;
        else if (levelRange >= 5) score += 2f;
        
        // 根据总评分确定品阶
        // 人级: 0-30分
        // 地级: 30-60分
        // 天级: 60-85分
        // 仙级: 85分以上
        
        int stage = 0;
        int level = 0;
        
        if (score >= 85f)
        {
            // 仙级 (Stage 3)
            stage = 3;
            // 在85-100分之间，level 0-4；100分以上，level 5-8
            level = score >= 100f ? Mathf.Clamp(Mathf.RoundToInt((score - 100f) / 15f * 3f) + 5, 5, 8) 
                                   : Mathf.Clamp(Mathf.RoundToInt((score - 85f) / 15f * 4f), 0, 4);
        }
        else if (score >= 60f)
        {
            // 天级 (Stage 2)
            stage = 2;
            // 在60-85分之间，level 0-8
            level = Mathf.Clamp(Mathf.RoundToInt((score - 60f) / 25f * 8f), 0, 8);
        }
        else if (score >= 30f)
        {
            // 地级 (Stage 1)
            stage = 1;
            // 在30-60分之间，level 0-8
            level = Mathf.Clamp(Mathf.RoundToInt((score - 30f) / 30f * 8f), 0, 8);
        }
        else
        {
            // 人级 (Stage 0)
            stage = 0;
            // 在0-30分之间，level 0-8
            level = Mathf.Clamp(Mathf.RoundToInt(score / 30f * 8f), 0, 8);
        }
        
        return new ItemLevel
        {
            Stage = stage,
            Level = level
        };
    }
    
    /// <summary>
    /// 基于原功法创建改进版功法（随机改善修炼要求、属性加成、法术池）
    /// </summary>
    public static CultibookAsset CreateImprovedCultibook(CultibookAsset originalCultibook, ActorExtend creator)
    {
        if (originalCultibook == null) return null;
        
        // 复制并改善属性加成（提升5-20%）
        BaseStats improvedStats = new BaseStats();
        if (originalCultibook.FinalStats != null && originalCultibook.FinalStats._stats_list is IList<BaseStatsContainer> originalStatsList)
        {
            float improvementRate = Randy.randomFloat(1.05f, 1.20f);
            foreach (var container in originalStatsList)
            {
                improvedStats[container.id] = container.value * improvementRate;
            }
        }
        
        // 改善灵根需求（随机变化，但确保创建者能够满足）
        ElementRequirement improvedElementReq = originalCultibook.ElementReq;
        ElementRoot creatorElementRoot = default;
        if (creator.HasElementRoot())
        {
            creatorElementRoot = creator.GetElementRoot();
            
            improvedElementReq.MinIron = Mathf.Min(originalCultibook.ElementReq.MinIron * Randy.randomFloat(0.7f, 1.3f), creatorElementRoot.Iron);
            improvedElementReq.MinWood = Mathf.Min(originalCultibook.ElementReq.MinWood * Randy.randomFloat(0.7f, 1.3f), creatorElementRoot.Wood);
            improvedElementReq.MinWater = Mathf.Min(originalCultibook.ElementReq.MinWater * Randy.randomFloat(0.7f, 1.3f), creatorElementRoot.Water);
            improvedElementReq.MinFire = Mathf.Min(originalCultibook.ElementReq.MinFire * Randy.randomFloat(0.7f, 1.3f), creatorElementRoot.Fire);
            improvedElementReq.MinEarth = Mathf.Min(originalCultibook.ElementReq.MinEarth * Randy.randomFloat(0.7f, 1.3f), creatorElementRoot.Earth);
            improvedElementReq.MinNeg = Mathf.Min(originalCultibook.ElementReq.MinNeg * Randy.randomFloat(0.7f, 1.3f), creatorElementRoot.Neg);
            improvedElementReq.MinPos = Mathf.Min(originalCultibook.ElementReq.MinPos * Randy.randomFloat(0.7f, 1.3f), creatorElementRoot.Pos);
            improvedElementReq.MinEntropy = Mathf.Min(originalCultibook.ElementReq.MinEntropy * Randy.randomFloat(0.7f, 1.3f), creatorElementRoot.Entropy);
        }
        
        // 改善境界限制（随机变化，但确保创建者能够满足）
        int creatorLevel = 0;
        if (creator.HasCultisys<Xian>())
        {
            creatorLevel = creator.GetCultisys<Xian>().CurrLevel;
        }
        
        // 随机变化 MinLevel（可以增加或减少，但不能超过创建者境界）
        int minLevelChange = Randy.randomInt(-2, 3);
        int improvedMinLevel = Mathf.Max(0, Mathf.Min(originalCultibook.MinLevel + minLevelChange, creatorLevel));
        
        // 随机变化 MaxLevel（可以增加或减少，但不能低于创建者境界）
        int maxLevelChange = Randy.randomInt(-1, 4);
        int improvedMaxLevel = originalCultibook.MaxLevel + maxLevelChange;
        improvedMaxLevel = Mathf.Max(improvedMaxLevel, creatorLevel); 
        improvedMaxLevel = Mathf.Max(improvedMaxLevel, improvedMinLevel);
        improvedMaxLevel = Mathf.Min(improvedMaxLevel, 20);
        
        // 改善法术池
        List<SkillPoolEntry> improvedSkillPool = new List<SkillPoolEntry>();
        if (originalCultibook.SkillPool != null && originalCultibook.SkillPool.Count > 0)
        {
            // 复制原法术池并提升概率
            foreach (var entry in originalCultibook.SkillPool)
            {
                improvedSkillPool.Add(new SkillPoolEntry()
                {
                    SkillEntityAssetId = entry.SkillEntityAssetId,
                    BaseChance = entry.BaseChance * Randy.randomFloat(1.1f, 1.5f), // 提升10-50%
                    MasteryThreshold = entry.MasteryThreshold * Randy.randomFloat(0.8f, 0.95f), // 降低5-20%
                    LevelRequirement = Mathf.Max(0, entry.LevelRequirement - (Randy.randomChance(0.3f) ? 1 : 0))
                });
            }
            
            // 有概率增加新法术（从创建者的技能中选择）
            if (creator.all_skills != null && creator.all_skills.Count > 0 && Randy.randomChance(0.3f))
            {
                var skillList = new List<Entity>(creator.all_skills);
                skillList.Shuffle();
                
                foreach (var skillEntity in skillList)
                {
                    if (!skillEntity.HasComponent<SkillContainer>()) continue;
                    var skillContainer = skillEntity.GetComponent<SkillContainer>();
                    string skillId = skillContainer.SkillEntityAssetID;
                    if (string.IsNullOrEmpty(skillId)) continue;
                    
                    bool alreadyExists = improvedSkillPool.Any(e => e.SkillEntityAssetId == skillId);
                    if (alreadyExists) continue;
                    
                    improvedSkillPool.Add(new SkillPoolEntry()
                    {
                        SkillEntityAssetId = skillId,
                        BaseChance = Randy.randomFloat(0.03f, 0.08f),
                        MasteryThreshold = Randy.randomFloat(30f, 60f),
                        LevelRequirement = improvedMinLevel
                    });
                    break;
                }
            }
        }
        else
        {
            // 如果原功法没有法术池，尝试添加1-2个法术
            if (creator.all_skills != null && creator.all_skills.Count > 0)
            {
                var skillsToAdd = Mathf.Min(Randy.randomInt(1, 3), creator.all_skills.Count);
                var skillList = new List<Entity>(creator.all_skills);
                skillList.Shuffle();
                
                int addedCount = 0;
                foreach (var skillEntity in skillList)
                {
                    if (addedCount >= skillsToAdd) break;
                    if (!skillEntity.HasComponent<SkillContainer>()) continue;
                    var skillContainer = skillEntity.GetComponent<SkillContainer>();
                    if (string.IsNullOrEmpty(skillContainer.SkillEntityAssetID)) continue;
                    
                    improvedSkillPool.Add(new SkillPoolEntry()
                    {
                        SkillEntityAssetId = skillContainer.SkillEntityAssetID,
                        BaseChance = 0.05f + addedCount * 0.02f,
                        MasteryThreshold = 30f + addedCount * 20f,
                        LevelRequirement = improvedMinLevel + addedCount
                    });
                    addedCount++;
                }
            }
        }
        
        // 保持相同的修炼方式
        string improvedCultivateMethodId = originalCultibook.CultivateMethodId;
        
        // 重新评级
        ItemLevel improvedLevel = CalculateCultibookLevel(improvedStats, improvedSkillPool, improvedMinLevel, improvedMaxLevel, improvedCultivateMethodId, creator);
        
        // 创建改进版功法Asset
        var improvedCultibook = _cultibookLibrary.AddDynamic(new CultibookAsset()
        {
            id = Guid.NewGuid().ToString(),
            FinalStats = improvedStats,
            Level = improvedLevel,
            Name = originalCultibook.Name,
            Description = originalCultibook.Description + "（改进版）",
            ElementReq = improvedElementReq,
            ElementAffinityThreshold = originalCultibook.ElementAffinityThreshold,
            MinLevel = improvedMinLevel,
            MaxLevel = improvedMaxLevel,
            CultivateMethodId = improvedCultivateMethodId,
            SkillPool = improvedSkillPool,
            ConflictTags = originalCultibook.ConflictTags?.ToArray() ?? Array.Empty<string>(),
            SynergyTags = originalCultibook.SynergyTags?.ToArray() ?? Array.Empty<string>()
        });
        
        return improvedCultibook;
    }
}