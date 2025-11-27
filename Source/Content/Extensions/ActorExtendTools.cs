using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using strings;
using UnityEngine;

namespace Cultiway.Content.Extensions;

public static class ActorExtendTools
{
    /// <summary>
    ///     使用丹药
    /// </summary>
    /// <remarks>一旦使用成功，丹药实体将被删除</remarks>
    /// <returns>是否使用成功 </returns>
    [Hotfixable]
    public static bool TryConsumeElixir(this ActorExtend ae, Entity elixir_entity)
    {
        ref Elixir elixir = ref elixir_entity.GetComponent<Elixir>();
        ElixirAsset elixir_asset = Libraries.Manager.ElixirLibrary.get(elixir.elixir_id);
        try
        {
            if (elixir_asset.consumable_check_action?.Invoke(ae, elixir_entity, ref elixir) ?? true)
                elixir_asset.effect_action?.Invoke(ae, elixir_entity, ref elixir);
            else
                return false;
        }
        catch (Exception e)
        {
            ModClass.LogError($"[{elixir_entity.Id}] ElixirAsset({elixir.elixir_id}).consumed_action error: {e}");
            return false;
        }

        ModClass.LogInfo($"[{ae}] consumes {elixir_entity.Id}({elixir.elixir_id})");
        elixir_entity.DeleteEntity();
        return true;
    }
    public static void EnhanceSkillRandomly(this ActorExtend ae, string source)
    {
        if (ae.all_skills.Count > 0)
        {
            var skill_container_entity = ae.all_skills.GetRandom();

            var builder = new SkillContainerBuilder(skill_container_entity);
            var existing_ids = CollectModifierIds(skill_container_entity);
            var candidate_assets = new List<SkillModifierAsset>();
            var weight_accum = new List<int>();
            var conflict_tags = CollectConflictTags(existing_ids);
            var total = 0;
            foreach (SkillModifierAsset asset in ModClass.I.SkillV3.ModifierLib.list)
            {
                if (asset == null) continue;
                if (asset.id == PlaceholderModifier.PlaceholderAssetId) continue;
                if (asset.IsDisabled) continue;
                var weight = asset.Rarity.Weight();
                if (weight <= 0) continue;
                var alreadyHas = existing_ids.Contains(asset.id);
                if (!alreadyHas && asset.ConflictTags.Any(conflict_tags.Contains)) continue;

                total += weight;
                candidate_assets.Add(asset);
                weight_accum.Add(total);
            }

            if (candidate_assets.Count == 0) return;

            var chosen_index = RdUtils.RandomIndexWithAccumWeight(weight_accum.ToArray());
            var chosen_asset = candidate_assets[chosen_index];

            if (chosen_asset.OnAddOrUpgrade?.Invoke(builder) ?? false)
            {
                //ModClass.LogInfo($"[{ae}] enhanced {skill_container_entity.Id}({skill_container_entity.GetComponent<SkillContainer>().Asset})");
                builder.Build();
            }
        }
    }
    public static bool RestoreWakan(this ActorExtend ae, float value)
    {
        if (value <= 0) return false;
        if (!ae.HasCultisys<Xian>()) return false;
        ref Xian xian = ref ae.GetCultisys<Xian>();
        xian.wakan = Mathf.Min(xian.wakan + value,
            Mathf.Max(xian.wakan, ae.Base.stats[BaseStatses.MaxWakan.id] * XianSetting.WakanRestoreLimit));
        return true;
    }

    public static bool HasCultibook(this ActorExtend ae)
    {
        return ae.HasMaster<CultibookAsset>();
    }
/*
    public static CultibookMasterRelation GetCultibookMasterRelation(this ActorExtend ae)
    {
        return ae.E.GetRelations<CultibookMasterRelation>().First();
    }

    public static void SetCultibookMasterRelation(this ActorExtend ae, Entity cultibook, float master_value)
    {
        if (ae.E.GetRelations<CultibookMasterRelation>().Any())
        {
            ae.E.GetRelation<CultibookMasterRelation, Entity>(cultibook).MasterValue = master_value;
        }
        else
        {
            ae.E.AddRelation(new CultibookMasterRelation()
            {
                Cultibook = cultibook,
                MasterValue = master_value
            });
        }
    }
*/
    public static ref Yuanying GetYuanying(this ActorExtend ae)
    {
        return ref ae.GetComponent<Yuanying>();
    }
    public static ref Jindan GetJindan(this ActorExtend ae)
    {
        return ref ae.GetComponent<Jindan>();
    }
    public static ref XianBase GetXianBase(this ActorExtend ae)
    {
        return ref ae.GetComponent<XianBase>();
    }


    /// <summary>
    /// 获取主修功法Asset
    /// </summary>
    public static CultibookAsset GetMainCultibook(this ActorExtend ae)
    {
        if (!ae.TryGetComponent(out ActorCultibookState state)) return null;
        if (!state.HasMainCultibook) return null;
        return state.MainCultibook;
    }

    /// <summary>
    /// 设置主修功法
    /// </summary>
    public static void SetMainCultibook(this ActorExtend ae, CultibookAsset cultibook)
    {
        if (cultibook == null) return;

        ActorCultibookState state;
        if (!ae.HasComponent<ActorCultibookState>())
        {
            state = new ActorCultibookState();
            ae.AddComponent(state);
        }

        ref var stateRef = ref ae.GetComponent<ActorCultibookState>();
        stateRef.MainCultibookId = cultibook.id;
        stateRef.MainMastery = 0;
        stateRef.AccumulatedTime = 0;
        stateRef.InitSkillProgress();
        
        // 管理持续修炼标记
        UpdateContinuousCultivateTag(ae, cultibook);
    }

    /// <summary>
    /// 获取主修掌握程度
    /// </summary>
    public static float GetMainCultibookMastery(this ActorExtend ae)
    {
        if (!ae.TryGetComponent(out ActorCultibookState state)) return 0;
        return state.MainMastery;
    }

    /// <summary>
    /// 增加主修掌握程度
    /// </summary>
    public static void AddMainCultibookMastery(this ActorExtend ae, float amount)
    {
        if (!ae.TryGetComponent(out ActorCultibookState state)) return;
        ref var stateRef = ref ae.GetComponent<ActorCultibookState>();
        stateRef.MainMastery = Mathf.Clamp(stateRef.MainMastery + amount, 0, 100);
    }

    /// <summary>
    /// 转修功法（将新功法设为主修）
    /// </summary>
    public static bool TrySwitchMainCultibook(this ActorExtend ae, CultibookAsset newCultibook)
    {
        if (newCultibook == null) return false;

        ActorCultibookState state;
        if (!ae.TryGetComponent(out state))
        {
            // 如果没有主修功法，直接设置
            ae.SetMainCultibook(newCultibook);
            return true;
        }

        ref var stateRef = ref ae.GetComponent<ActorCultibookState>();
        var oldCultibook = stateRef.MainCultibook;

        // 计算转修成功率
        float successRate = CalculateSwitchSuccessRate(ae, newCultibook);

        if (!Randy.randomChance(successRate))
        {
            // 转修失败，损失灵力
            // TODO: 额外的惩罚
            if (ae.HasCultisys<Xian>())
            {
                ref var xian = ref ae.GetCultisys<Xian>();
                xian.wakan *= 0.5f;
            }
            return false;
        }

        // 转修成功
        if (oldCultibook != null)
        {
            // 将原主修变为了解（保留部分掌握程度）
            var oldMastery = stateRef.MainMastery;
            ae.Master(oldCultibook, oldMastery * 0.5f);
        }

        // 设置新主修功法
        stateRef.MainCultibookId = newCultibook.id;
        // 如果有了解程度，部分转化为主修掌握程度
        var knownMastery = ae.GetMaster(newCultibook);
        stateRef.MainMastery = knownMastery > 0 ? knownMastery * 0.8f : 0;
        stateRef.AccumulatedTime = 0;
        stateRef.InitSkillProgress();
        
        // 管理持续修炼标记
        UpdateContinuousCultivateTag(ae, newCultibook);

        return true;
    }

    /// <summary>
    /// 更新持续修炼标记（根据主修功法的修炼方式类型）
    /// </summary>
    private static void UpdateContinuousCultivateTag(ActorExtend ae, CultibookAsset cultibook)
    {
        if (cultibook == null)
        {
            ae.E.RemoveTag<ContinuousCultivateTag>();
            return;
        }
        
        var method = cultibook.GetCultivateMethod();
        if (method != null && method.TriggerType == CultivateTriggerType.Continuous)
        {
            ae.E.AddTag<ContinuousCultivateTag>();
        }
        else
        {
            ae.E.RemoveTag<ContinuousCultivateTag>();
        }
    }

    /// <summary>
    /// 计算转修成功率
    /// </summary>
    private static float CalculateSwitchSuccessRate(ActorExtend ae, CultibookAsset newCultibook)
    {
        float baseRate = 0.7f;
        
        // 智力加成
        float intelligence = ae.GetStat(S.intelligence);
        float intelligenceBonus = Mathf.Min(intelligence / 100f * 0.2f, 0.2f);
        
        // 灵根契合度加成
        float affinity = 0;
        if (ae.HasElementRoot())
        {
            affinity = newCultibook.ElementReq.GetAffinity(ae.GetElementRoot());
        }
        float affinityBonus = affinity * 0.1f;
        
        // 境界惩罚（金丹后转修更难）
        float levelPenalty = 0;
        if (ae.HasCultisys<Xian>())
        {
            ref var xian = ref ae.GetCultisys<Xian>();
            if (xian.CurrLevel > 3)
            {
                levelPenalty = (xian.CurrLevel - 3) * 0.05f;
            }
        }

        return Mathf.Clamp01(baseRate + intelligenceBonus + affinityBonus - levelPenalty);
    }

    /// <summary>
    /// 尝试领悟法术（从主修功法的法术池中）
    /// </summary>
    public static bool TryComprehendSkill(this ActorExtend ae)
    {
        var mainCultibook = ae.GetMainCultibook();
        if (mainCultibook == null) return false;
        if (mainCultibook.SkillPool == null || mainCultibook.SkillPool.Count == 0) return false;

        if (!ae.TryGetComponent(out ActorCultibookState state)) return false;
        ref var stateRef = ref ae.GetComponent<ActorCultibookState>();
        stateRef.InitSkillProgress();

        foreach (var entry in mainCultibook.SkillPool)
        {
            // 检查掌握程度阈值
            if (stateRef.MainMastery < entry.MasteryThreshold) continue;

            // 检查境界要求
            if (ae.HasCultisys<Xian>())
            {
                ref var xian = ref ae.GetCultisys<Xian>();
                if (xian.CurrLevel < entry.LevelRequirement) continue;
            }

            // 检查是否已学习
            if (ae.HasSkill(entry.SkillEntityAssetId)) continue;

            // 计算领悟概率
            float masteryFactor = stateRef.MainMastery / 100f;
            float intelligence = ae.GetStat(S.intelligence);
            float intelligenceFactor = intelligence / 50f;
            float chance = entry.BaseChance * masteryFactor * intelligenceFactor;

            // 检查领悟进度
            if (stateRef.SkillProgress != null && stateRef.SkillProgress.TryGetValue(entry.SkillEntityAssetId, out var progress))
            {
                // 如果已经有进度，增加成功率
                chance += progress * 0.1f;
            }

            if (Randy.randomChance(chance))
            {
                // 领悟成功，学习技能
                ae.LearnSkillFromCultibook(entry.SkillEntityAssetId);
                return true;
            }
            else
            {
                // 领悟失败，增加进度
                if (stateRef.SkillProgress == null) stateRef.SkillProgress = new Dictionary<string, float>();
                if (!stateRef.SkillProgress.ContainsKey(entry.SkillEntityAssetId))
                {
                    stateRef.SkillProgress[entry.SkillEntityAssetId] = 0;
                }
                stateRef.SkillProgress[entry.SkillEntityAssetId] += entry.BaseChance * 0.1f;
            }
        }

        return false;
    }

    /// <summary>
    /// 从功法中学习技能
    /// </summary>
    private static void LearnSkillFromCultibook(this ActorExtend ae, string skillEntityAssetId)
    {
        var skillEntityAsset = ModClass.I.SkillV3.SkillLib.get(skillEntityAssetId);
        if (skillEntityAsset == null)
        {
            ModClass.LogError($"无法找到技能资源: {skillEntityAssetId}");
            return;
        }

        // 创建技能容器
        var skillContainer = ModClass.I.W.CreateEntity(new SkillContainer
        {
            SkillEntityAssetID = skillEntityAssetId
        });

        // 学习技能
        ae.LearnSkillV3(skillContainer, false);

        ModClass.LogInfo($"[{ae}] 从功法领悟了技能: {skillEntityAssetId}");
    }

    /// <summary>
    /// 检查是否已学习某个技能
    /// </summary>
    private static bool HasSkill(this ActorExtend ae, string skillEntityAssetId)
    {
        if (ae.all_skills == null || ae.all_skills.Count == 0) return false;
        
        foreach (var skillEntity in ae.all_skills)
        {
            if (!skillEntity.HasComponent<SkillContainer>()) continue;
            ref var skillContainer = ref skillEntity.GetComponent<SkillContainer>();
            if (skillContainer.SkillEntityAssetID == skillEntityAssetId)
            {
                return true;
            }
        }
        
        return false;
    }

    private static HashSet<string> CollectModifierIds(Entity skill_container_entity)
    {
        var ids = new HashSet<string>();
        if (skill_container_entity.IsNull) return ids;

        foreach (var component_type in skill_container_entity.GetComponentTypes())
        {
            if (!typeof(IModifier).IsAssignableFrom(component_type)) continue;
            var modifier = (IModifier)skill_container_entity.GetComponent(component_type);
            ids.Add(modifier.ModifierAsset.id);
            if (modifier is PlaceholderModifier placeholder && placeholder.ModifierAssetIds != null)
            {
                ids.UnionWith(placeholder.ModifierAssetIds);
            }
        }

        return ids;
    }

    private static HashSet<string> CollectConflictTags(HashSet<string> modifierIds)
    {
        var tags = new HashSet<string>();
        foreach (var id in modifierIds)
        {
            var asset = ModClass.I.SkillV3.ModifierLib.get(id);
            if (asset == null) continue;
            tags.UnionWith(asset.ConflictTags);
        }

        return tags;
    }

    /// <summary>
    /// 评估功法的综合价值（用于转修决策）
    /// </summary>
    /// <returns>评分值，越高越好</returns>
    public static float EvaluateCultibookValue(this ActorExtend ae, CultibookAsset cultibook)
    {
        if (cultibook == null) return 0f;
        
        float score = 0f;
        
        // 1. 品阶评分（0-36分）
        // 人级(Stage 0, Level 0-8) = 0-8分
        // 地级(Stage 1, Level 0-8) = 9-17分
        // 天级(Stage 2, Level 0-8) = 18-26分
        // 仙级(Stage 3, Level 0-8) = 27-35分
        int stageLevel = cultibook.Level.Stage * 9 + cultibook.Level.Level;
        score += stageLevel;
        
        // 2. 灵根契合度评分（0-10分）
        float affinity = 1f;
        if (ae.HasElementRoot())
        {
            affinity = cultibook.ElementReq.GetAffinity(ae.GetElementRoot());
        }
        score += affinity * 10f;
        
        return score;
    }

    /// <summary>
    /// 获取所有可转修的功法（包括了解的功法和身上的功法书）
    /// </summary>
    public static List<CultibookAsset> GetAvailableCultibooksForSwitch(this ActorExtend ae)
    {
        var result = new List<CultibookAsset>();
        var mainCultibook = ae.GetMainCultibook();
        
        // 1. 添加了解的功法（排除当前主修）
        var knownCultibooks = ae.GetAllMaster<CultibookAsset>();
        foreach (var (cultibook, mastery) in knownCultibooks)
        {
            if (cultibook == null) continue;
            if (mainCultibook != null && cultibook.id == mainCultibook.id) continue;
            
            // 检查境界要求
            if (ae.HasCultisys<Xian>())
            {
                ref var xian = ref ae.GetCultisys<Xian>();
                if (cultibook.MinLevel > xian.CurrLevel || cultibook.MaxLevel < xian.CurrLevel) continue;
            }
            
            // 检查灵根要求
            if (ae.HasElementRoot())
            {
                if (!cultibook.ElementReq.Check(ae.GetElementRoot())) continue;
            }
            
            result.Add(cultibook);
        }
        
        return result;
    }

    /// <summary>
    /// 选择最佳转修功法（评估所有可用功法，选择评分最高的）
    /// </summary>
    public static CultibookAsset SelectBestCultibookToSwitch(this ActorExtend ae)
    {
        var availableCultibooks = ae.GetAvailableCultibooksForSwitch();
        if (availableCultibooks.Count == 0) return null;
        
        var mainCultibook = ae.GetMainCultibook();
        var currentScore = mainCultibook != null ? ae.EvaluateCultibookValue(mainCultibook) : 0f;
        
        CultibookAsset bestCultibook = null;
        float bestScore = currentScore;
        
        // 需要显著提升（至少15%）才值得转修
        float minImprovement = currentScore * 1.15f;
        
        foreach (var cultibook in availableCultibooks)
        {
            float score = ae.EvaluateCultibookValue(cultibook);
            
            // 如果新功法评分明显更高，考虑转修
            if (score > bestScore && score >= minImprovement)
            {
                bestScore = score;
                bestCultibook = cultibook;
            }
        }
        
        return bestCultibook;
    }

    /// <summary>
    /// 尝试改进功法（创建新功法并转修）
    /// </summary>
    public static bool TryImproveCultibook(this ActorExtend ae)
    {
        var mainCultibook = ae.GetMainCultibook();
        if (mainCultibook == null) return false;
        
        float mastery = ae.GetMainCultibookMastery();
        if (mastery < 100f) return false;
        
        var actor = ae.Base;
        if (actor.city == null) return false;
        var city = actor.city;
        if (city.getBuildingWithBookSlot() == null) return false;
        
        // 计算成功率（基于智慧）
        float intelligence = ae.GetStat(S.intelligence);
        // 基础成功率50%，智慧每10点增加1%成功率，最高90%
        float successRate = Mathf.Clamp(0.5f + intelligence / 10f * 0.01f, 0.5f, 0.9f);
        
        if (!Randy.randomChance(successRate))
        {
            return false;
        }
        
        var improvedCultibook = BookManagerTools.CreateImprovedCultibook(mainCultibook, ae);
        if (improvedCultibook == null) return false;
        
        var newBook = World.world.books.GenerateNewBook(actor, BookTypes.Cultibook);
        if (newBook == null) return false;
        
        var be = newBook.GetExtend();
        be.AddComponent(new Cultibook(improvedCultibook.id));
        be.AddComponent(improvedCultibook.Level);
        be.Master(improvedCultibook, 100);
        newBook.data.name = improvedCultibook.Name;
        
        ae.Master(improvedCultibook, 100);
        
        // 无代价转修到改进版功法
        // 将原主修变为了解
        if (ae.HasComponent<ActorCultibookState>())
        {
            ref var stateRef = ref ae.GetComponent<ActorCultibookState>();
            var oldMastery = stateRef.MainMastery;
            ae.Master(mainCultibook, oldMastery * 0.5f);
            
            // 直接设置新主修功法（无代价）
            stateRef.MainCultibookId = improvedCultibook.id;
            stateRef.MainMastery = 100f;
            stateRef.AccumulatedTime = 0;
            stateRef.InitSkillProgress();
            
            UpdateContinuousCultivateTag(ae, improvedCultibook);
        }
        else
        {
            ae.SetMainCultibook(improvedCultibook);
            ae.AddMainCultibookMastery(80f);
        }
        
        return true;
    }
}
