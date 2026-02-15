using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.AIGC;
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
        ModClass.LogWarning($"{creator.name} learned skillbook {raw_skillbook.name}({raw_skillbook.id})");
        return raw_skillbook;
    }
    public static Book CreateCultibookFromDraft(this BookManager manager, Actor creator, CultibookAsset draft_asset)
    {
        var ae = creator.GetExtend();
        var rawCultibook = manager.GenerateNewBook(creator, BookTypes.Cultibook);
        if (rawCultibook == null)
        {
            return null;
        }
        if (string.IsNullOrEmpty(draft_asset.Name))
        {
            draft_asset.Name = rawCultibook.name;
        }
        if (string.IsNullOrEmpty(draft_asset.Description))
        {
            draft_asset.Description = BuildDefaultDescription(draft_asset.Name, ae);
        }

        var be = rawCultibook.GetExtend();
        var cultibook = _cultibookLibrary.AddDynamic(draft_asset);

        be.AddComponent(new Cultibook(cultibook.id));
        be.AddComponent(cultibook.Level);
        be.Master(cultibook, 100);
        ae.Master(cultibook, 100);
        rawCultibook.data.name = draft_asset.Name;
        return rawCultibook;
    }
    public static Book CreateNewCultibook(this BookManager manager, Actor creator)
    {
        var ae = creator.GetExtend();
        var rawCultibook = manager.GenerateNewBook(creator, BookTypes.Cultibook);
        if (rawCultibook == null)
        {
            return null;
        }

        var be = rawCultibook.GetExtend();

        var stats = GenerateStatsFromActor(creator);
        var elementReq = GenerateElementRequirement(ae);
        GenerateLevelRange(ae, out var minLevel, out var maxLevel);
        var cultivateMethodId = DetermineCultivateMethodId(ae);
        var skillPool = GenerateSkillPool(ae, minLevel);

        ItemLevel itemLevel =
            CalculateCultibookLevel(stats, skillPool, minLevel, maxLevel, cultivateMethodId, ae);

        var cultibook = _cultibookLibrary.AddDynamic(new CultibookAsset
        {
            id = Guid.NewGuid().ToString(),
            FinalStats = stats,
            Level = itemLevel,
            Name = rawCultibook.name,
            Description = BuildDefaultDescription(rawCultibook.name, ae),
            ElementReq = elementReq,
            ElementAffinityThreshold = 0.3f,
            MinLevel = minLevel,
            MaxLevel = maxLevel,
            CultivateMethodId = cultivateMethodId,
            SkillPool = skillPool,
            ConflictTags = Array.Empty<string>(),
            SynergyTags = Array.Empty<string>()
        });

        be.AddComponent(new Cultibook(cultibook.id));
        be.AddComponent(cultibook.Level);
        be.Master(cultibook, 100);
        ae.Master(cultibook, 100);
        return rawCultibook;
    }

    private static BaseStats GenerateStatsFromActor(Actor creator)
    {
        var stats = new BaseStats();

        void SoftmaxStats(IList<string> statIds)
        {
            (string id, float value)[] tempStats = new (string, float)[statIds.Count];
            var expSum = 0f;
            var maxVal = 0f;
            for (int i = 0; i < tempStats.Length; i++)
            {
                var statId = statIds[i];
                var statValue = creator.stats[statId];
                maxVal = Mathf.Max(maxVal, statValue);
                tempStats[i] = (statId, statValue);
            }

            for (int i = 0; i < tempStats.Length; i++)
            {
                var statValue = tempStats[i].value;
                var expValue = Mathf.Exp(statValue - maxVal);
                tempStats[i].value = expValue;
                expSum += expValue;
            }

            for (int i = 0; i < tempStats.Length; i++)
            {
                tempStats[i].value /= expSum;
            }

            Array.Sort(tempStats, (a, b) => b.value.CompareTo(a.value));
            var accum = 0f;
            var stopIndex = tempStats.Length - 1;
            for (int i = 0; i < tempStats.Length; i++)
            {
                accum += tempStats[i].value;
                if (accum > 0.9f)
                {
                    stopIndex = i;
                    break;
                }
            }

            for (int i = 0; i <= stopIndex; i++)
            {
                var statId = tempStats[i].id;
                var statValue = tempStats[i].value / accum;
                stats[WorldboxGame.BaseStats.StatsToModStats[statId]] = statValue;
            }
        }

        SoftmaxStats(WorldboxGame.BaseStats.ArmorStats);
        SoftmaxStats(WorldboxGame.BaseStats.MasterStats);

        return stats;
    }

    private static ElementRequirement GenerateElementRequirement(ActorExtend ae)
    {
        ElementRequirement elementReq = new();
        if (!ae.HasElementRoot()) return elementReq;

        var elementRoot = ae.GetElementRoot();
        elementReq.MinIron = Mathf.Max(0f, elementRoot.Iron * 0.7f);
        elementReq.MinWood = Mathf.Max(0f, elementRoot.Wood * 0.7f);
        elementReq.MinWater = Mathf.Max(0f, elementRoot.Water * 0.7f);
        elementReq.MinFire = Mathf.Max(0f, elementRoot.Fire * 0.7f);
        elementReq.MinEarth = Mathf.Max(0f, elementRoot.Earth * 0.7f);
        elementReq.MinNeg = Mathf.Max(0f, elementRoot.Neg * 0.7f);
        elementReq.MinPos = Mathf.Max(0f, elementRoot.Pos * 0.7f);
        elementReq.MinEntropy = Mathf.Max(0f, elementRoot.Entropy * 0.7f);
        return elementReq;
    }

    private static void GenerateLevelRange(ActorExtend ae, out int minLevel, out int maxLevel)
    {
        minLevel = 0;
        maxLevel = 20;
        if (!ae.HasCultisys<Xian>()) return;

        ref var xian = ref ae.GetCultisys<Xian>();
        minLevel = Mathf.Max(0, xian.CurrLevel - 2);
        maxLevel = Mathf.Min(20, xian.CurrLevel + 5);
    }

    private static string DetermineCultivateMethodId(ActorExtend ae)
    {
        var mainCultibook = ae.GetMainCultibook();
        if (mainCultibook != null && !string.IsNullOrEmpty(mainCultibook.CultivateMethodId))
        {
            return mainCultibook.CultivateMethodId;
        }

        var library = Libraries.Manager.CultivateMethodLibrary;
        var candidateMethods = new List<CultivateMethodAsset>();
        var accumWeights = new List<float>();
        float totalWeight = 0f;

        foreach (var method in library.list)
        {
            if (method == null) continue;
            if (method.CanCultivate != null && !method.CanCultivate(ae)) continue;

            float efficiency = 1f;
            if (method.GetEfficiency != null)
            {
                efficiency = method.GetEfficiency(ae);
            }
            if (efficiency <= 0f) continue;

            candidateMethods.Add(method);
            totalWeight += efficiency;
            accumWeights.Add(totalWeight);
        }

        if (candidateMethods.Count == 0)
        {
            return CultivateMethods.Standard.id;
        }

        var selectedIndex = RdUtils.RandomIndexWithAccumWeight(accumWeights);
        return candidateMethods[selectedIndex].id;
    }

    private static List<SkillPoolEntry> GenerateSkillPool(ActorExtend ae, int minLevel)
    {
        var result = new List<SkillPoolEntry>();
        if (ae.all_skills == null || ae.all_skills.Count == 0) return result;

        var skillsToAdd = Mathf.Min(3, ae.all_skills.Count);
        var skillList = new List<Entity>(ae.all_skills);
        skillList.Shuffle();

        int addedCount = 0;
        for (int i = 0; i < skillList.Count && addedCount < skillsToAdd; i++)
        {
            var skillEntity = skillList[i];
            if (!skillEntity.HasComponent<SkillContainer>()) continue;
            
            skillEntity = skillEntity.Store.CloneEntity(skillEntity);
            result.Add(new SkillPoolEntry
            {
                SkillContainer = skillEntity,
                BaseChance = 0.05f + addedCount * 0.02f,
                MasteryThreshold = 20f + addedCount * 20f,
                LevelRequirement = minLevel + addedCount * 2
            });
            skillEntity.AddTag<TagOccupied>();
            addedCount++;
        }

        return result;
    }

    internal static string BuildDefaultDescription(string bookName, ActorExtend ae)
    {
        if (!ae.HasElementRoot()) return bookName;

        var elementType = ae.GetElementRoot().Type;
        if (elementType == null) return bookName;
        return $"{elementType.GetName()}系功法，适合{bookName}修炼。";
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
                if (entry.SkillContainer.IsNull)
                {
                    ModClass.LogWarning($"[CultibookGenerator] SkillContainer is null for skill in cultibook {originalCultibook.id}");
                }
                improvedSkillPool.Add(new SkillPoolEntry()
                {
                    SkillContainer = entry.SkillContainer.Store.CloneEntity(entry.SkillContainer),
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
                    
                    bool alreadyExists = improvedSkillPool.Any(e => e.SkillContainer == skillEntity);
                    if (alreadyExists) continue;
                    
                    improvedSkillPool.Add(new SkillPoolEntry()
                    {
                        SkillContainer = skillEntity.Store.CloneEntity(skillEntity),
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
                    
                    improvedSkillPool.Add(new SkillPoolEntry()
                    {
                        SkillContainer = skillEntity.Store.CloneEntity(skillEntity),
                        BaseChance = 0.05f + addedCount * 0.02f,
                        MasteryThreshold = 30f + addedCount * 20f,
                        LevelRequirement = improvedMinLevel + addedCount
                    });
                    addedCount++;
                }
            }
        }
        foreach (var entry in improvedSkillPool)
        {
            if (!entry.SkillContainer.Tags.Has<TagOccupied>())
            {
                entry.SkillContainer.AddTag<TagOccupied>();
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
