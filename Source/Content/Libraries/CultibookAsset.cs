using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Libraries;

public class CultibookAsset : Asset, IDeleteWhenUnknown
{
    /// <summary>
    /// 当掌握程度达到100%时的属性加成
    /// </summary>
    public BaseStats FinalStats;

    public string Name;
    public ItemLevel Level;

    /// <summary>
    /// 本地化描述
    /// </summary>
    public string Description;

    /// <summary>
    /// 灵根需求
    /// </summary>
    public ElementRequirement ElementReq;

    /// <summary>
    /// 灵根契合阈值
    /// </summary>
    public float ElementAffinityThreshold = 0.3f;

    /// <summary>
    /// 最低/最高适配境界
    /// </summary>
    public int MinLevel = 0;
    public int MaxLevel = 20;

    /// <summary>
    /// 修炼方式 Asset ID
    /// </summary>
    public string CultivateMethodId = "Cultiway.Standard";

    /// <summary>
    /// 可领悟法术
    /// </summary>
    public List<SkillPoolEntry> SkillPool = new();

    /// <summary>
    /// 冲突/协同标签
    /// </summary>
    public string[] ConflictTags = Array.Empty<string>();
    public string[] SynergyTags = Array.Empty<string>();

    public int Current { get; set; } = 0;
    
    /// <summary>
    /// 获取对应的修炼方式Asset
    /// </summary>
    public CultivateMethodAsset GetCultivateMethod()
    {
        if (string.IsNullOrEmpty(CultivateMethodId))
        {
            CultivateMethodId = "Cultiway.Standard";
        }
        return Manager.CultivateMethodLibrary.get(CultivateMethodId);
    }

    public void OnDelete()
    {
        ModClass.LogInfo($"Cultibook {id} is deleted");
        foreach (var entry in SkillPool)
        {
            entry.SkillContainer.RemoveTag<TagOccupied>();
        }
    }
}

/// <summary>
/// 灵根需求结构体
/// </summary>
public struct ElementRequirement
{
    public float MinIron;
    public float MinWood;
    public float MinWater;
    public float MinFire;
    public float MinEarth;
    public float MinNeg;
    public float MinPos;
    public float MinEntropy;

    /// <summary>
    /// 检查灵根是否满足最低需求
    /// </summary>
    public bool Check(ElementRoot root)
    {
        if (MinIron > 0 && root.Iron < MinIron) return false;
        if (MinWood > 0 && root.Wood < MinWood) return false;
        if (MinWater > 0 && root.Water < MinWater) return false;
        if (MinFire > 0 && root.Fire < MinFire) return false;
        if (MinEarth > 0 && root.Earth < MinEarth) return false;
        if (MinNeg > 0 && root.Neg < MinNeg) return false;
        if (MinPos > 0 && root.Pos < MinPos) return false;
        if (MinEntropy > 0 && root.Entropy < MinEntropy) return false;
        return true;
    }

    /// <summary>
    /// 返回与灵根的契合度（0-1）
    /// </summary>
    public float GetAffinity(ElementRoot root)
    {
        float totalRequirement = MinIron + MinWood + MinWater + MinFire + MinEarth + MinNeg + MinPos + MinEntropy;
        if (totalRequirement <= 0) return 1f;

        float matched = Match(root.Iron, MinIron)
                        + Match(root.Wood, MinWood)
                        + Match(root.Water, MinWater)
                        + Match(root.Fire, MinFire)
                        + Match(root.Earth, MinEarth)
                        + Match(root.Neg, MinNeg)
                        + Match(root.Pos, MinPos)
                        + Match(root.Entropy, MinEntropy);

        float ratio = matched / totalRequirement;
        if (ratio < 0f) ratio = 0f;
        if (ratio > 1f) ratio = 1f;
        return ratio;

        static float Match(float value, float requirement)
        {
            if (requirement <= 0) return 0f;
            return Math.Min(value, requirement);
        }
    }
}

/// <summary>
/// 法术池配置
/// </summary>
public class SkillPoolEntry
{
    /// <summary>
    /// 技能容器实体（直接引用成品技能）
    /// </summary>
    public Entity SkillContainer;
    public float BaseChance;
    public float MasteryThreshold;
    public int LevelRequirement;
}