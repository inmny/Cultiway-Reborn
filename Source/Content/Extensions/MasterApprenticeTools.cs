using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using strings;
using UnityEngine;

namespace Cultiway.Content.Extensions;

/// <summary>
/// 师徒系统扩展方法（依赖Content）
/// </summary>
public static class MasterApprenticeTools
{
    // ======== 师傅相关方法 ========
    
    /// <summary>
    /// 检查是否可以收徒
    /// </summary>
    [Hotfixable]
    public static bool CanRecruit(this ActorExtend ae)
    {
        // 检查境界
        if (!ae.HasCultisys<Xian>()) return false;
        ref var xian = ref ae.GetCultisys<Xian>();
        if (xian.level < XianLevels.Jindan) return false;  // 至少金丹期
        
        // 检查功法掌握
        var mainCultibook = ae.GetMainCultibook();
        if (mainCultibook == null) return false;
        if (ae.GetMainCultibookMastery() < 40) return false;
        
        // 检查弟子数量
        if (!ae.TryGetComponent(out MasterApprenticeState state)) return true;
        return state.ApprenticeCount < state.MaxApprenticeCount;
    }
    
    /// <summary>
    /// 获取最大弟子数量
    /// </summary>
    public static int GetMaxApprenticeCount(this ActorExtend ae)
    {
        if (!ae.HasCultisys<Xian>()) return 0;
        ref var xian = ref ae.GetCultisys<Xian>();
        
        return xian.level switch
        {
            >= XianLevels.Yuanying => 10,
            >= XianLevels.Jindan => 5,
            >= XianLevels.XianBase => 2,
            _ => 0
        };
    }
    
    /// <summary>
    /// 收取弟子
    /// </summary>
    [Hotfixable]
    public static bool TryRecruit(this ActorExtend master, ActorExtend apprentice, 
        MasterApprenticeType type = MasterApprenticeType.Nominal)
    {
        if (!master.CanRecruit()) return false;
        if (apprentice.HasMaster()) return false;
        
        // 计算成功率
        float successRate = CalculateRecruitSuccessRate(master, apprentice);
        if (!Randy.randomChance(successRate)) return false;
        
        // 建立关系
        apprentice.E.AddRelation(new MasterApprenticeRelation
        {
            Master = master.E,
            RelationType = type,
            Intimacy = 0,
            ApprenticeTime = (float)World.world.getCurWorldTime(),
            TransferredCultibookCount = 0,
            TransferredSkillCount = 0,
            IsSuccessor = false
        });
        
        // 更新师傅状态
        ref var state = ref master.GetOrAddComponent<MasterApprenticeState>();
        state.ApprenticeCount++;
        if (state.MaxApprenticeCount == 0)
        {
            state.MaxApprenticeCount = master.GetMaxApprenticeCount();
        }
        if (state.RecruitWillingness == 0)
        {
            state.RecruitWillingness = 50f; // 默认收徒意愿
        }
        if (state.TeachWillingness == 0)
        {
            state.TeachWillingness = 50f; // 默认传授意愿
        }
        if (state.Style == 0) // 默认值为0，需要初始化
        {
            state.Style = MasterStyle.Gentle; // 默认温和型
        }
        
        // TODO: 触发事件
        // MasterApprenticeEvents.OnRecruit(master, apprentice);
        
        ModClass.LogInfo($"[{master.Base.getName()}] 收徒成功: {apprentice.Base.getName()}");
        
        return true;
    }
    
    // ======== 弟子相关方法 ========
    
    /// <summary>
    /// 尝试出师
    /// </summary>
    [Hotfixable]
    public static bool TryGraduate(this ActorExtend ae)
    {
        if (!ae.HasMaster()) return false;
        
        var master = ae.GetMaster();
        if (master == null) return false;
        
        ref var relation = ref ae.GetMasterRelation();
        
        // 检查出师条件
        // 1. 境界达到师傅境界
        // 2. 或功法掌握达到100%
        bool canGraduate = false;
        
        if (ae.HasCultisys<Xian>() && master.GetExtend().HasCultisys<Xian>())
        {
            ref var apprenticeXian = ref ae.GetCultisys<Xian>();
            ref var masterXian = ref master.GetExtend().GetCultisys<Xian>();
            canGraduate = apprenticeXian.level >= masterXian.level;
        }
        
        if (!canGraduate)
        {
            canGraduate = ae.GetMainCultibookMastery() >= 100;
        }
        
        if (!canGraduate) return false;
        
        // 执行出师
        // TODO: MasterApprenticeEvents.OnGraduate(master, ae);
        
        // 移除师徒关系
        ae.E.RemoveRelation<MasterApprenticeRelation>(master.GetExtend().E);
        
        // 更新师傅状态
        ref var state = ref master.GetExtend().GetOrAddComponent<MasterApprenticeState>();
        state.ApprenticeCount--;
        
        ModClass.LogInfo($"[{ae.Base.getName()}] 出师成功");
        
        return true;
    }
    
    // ======== 传承相关方法 ========
    
    /// <summary>
    /// 传授功法
    /// </summary>
    [Hotfixable]
    public static bool TeachCultibook(this ActorExtend master, ActorExtend apprentice, 
        CultibookAsset cultibook)
    {
        if (master == null || apprentice == null || cultibook == null) return false;
        if (!apprentice.HasMaster() || apprentice.GetMaster() != master.Base) return false;
        
        // 检查师傅是否掌握该功法
        var masterMastery = master.GetMaster(cultibook);
        if (masterMastery <= 0)
        {
            // 如果师傅主修的就是这个功法，使用主修掌握度
            var mainCultibook = master.GetMainCultibook();
            if (mainCultibook == cultibook)
            {
                masterMastery = master.GetMainCultibookMastery();
            }
            else
            {
                return false;
            }
        }
        
        // 计算传承效率
        float efficiency = CalculateTeachEfficiency(master, apprentice);
        
        // 计算弟子获得的掌握度
        float gainedMastery = Mathf.Min(masterMastery * efficiency, 80f);
        
        // 更新弟子功法状态
        var currentMastery = apprentice.GetMaster(cultibook);
        if (currentMastery <= 0)
        {
            // 新学功法
            if (apprentice.GetMainCultibook() == null)
            {
                apprentice.SetMainCultibook(cultibook);
                apprentice.AddMainCultibookMastery(gainedMastery);
            }
            apprentice.Master(cultibook, gainedMastery);
        }
        else
        {
            // 已有功法，增加掌握度（取较大值）
            apprentice.Master(cultibook, Mathf.Max(currentMastery, gainedMastery));
        }
        
        // 更新师徒关系
        UpdateRelationAfterTeaching(master, apprentice);
        
        ModClass.LogInfo($"[{master.Base.getName()}] 传授功法 {cultibook.Name} 给 [{apprentice.Base.getName()}]");
        
        return true;
    }
    
    // ======== 辅助方法 ========
    
    private static float CalculateRecruitSuccessRate(ActorExtend master, ActorExtend apprentice)
    {
        float baseRate = 0.7f;
        
        // 灵根契合度
        float affinityBonus = 0;
        var mainCultibook = master.GetMainCultibook();
        if (mainCultibook != null && apprentice.HasElementRoot() && master.HasElementRoot())
        {
            var apprenticeRoot = apprentice.GetElementRoot();
            var masterRoot = master.GetElementRoot();
            affinityBonus = mainCultibook.ElementReq.GetAffinity(apprenticeRoot) * 0.2f;
        }
        
        // 关系系数（暂时简单处理）
        float relationBonus = 0; // TODO: 获取双方关系值
        
        // 智力系数
        float intelligenceBonus = apprentice.GetStat(S.intelligence) / 50f * 0.1f;
        
        return Mathf.Clamp01(baseRate + affinityBonus + relationBonus + intelligenceBonus);
    }
    
    private static float CalculateTeachEfficiency(ActorExtend master, ActorExtend apprentice)
    {
        float baseEfficiency = 0.3f;
        
        // 关系系数
        ref var relation = ref apprentice.GetMasterRelation();
        float relationMultiplier = relation.RelationType switch
        {
            MasterApprenticeType.Nominal => 0.5f,
            MasterApprenticeType.Formal => 0.8f,
            MasterApprenticeType.Direct => 1.0f,
            MasterApprenticeType.Successor => 1.2f,
            _ => 0.5f
        };
        
        // 师傅掌握度系数
        var mainCultibook = master.GetMainCultibook();
        float masterMasteryMultiplier = 1.0f;
        if (mainCultibook != null)
        {
            masterMasteryMultiplier = master.GetMainCultibookMastery() / 100f;
        }
        
        // 智力系数
        float intelligenceMultiplier = 
            (master.GetStat(S.intelligence) + apprentice.GetStat(S.intelligence)) / 100f;
        
        return baseEfficiency * relationMultiplier * masterMasteryMultiplier * intelligenceMultiplier;
    }
    
    private static void UpdateRelationAfterTeaching(ActorExtend master, ActorExtend apprentice)
    {
        ref var relation = ref apprentice.GetMasterRelation();
        relation.TransferredCultibookCount++;
        relation.Intimacy = Mathf.Min(relation.Intimacy + 2, 100);
        UpdateRelationType(ref relation);
    }
    
    private static void UpdateRelationType(ref MasterApprenticeRelation relation)
    {
        if (relation.IsSuccessor && relation.Intimacy >= 90)
        {
            relation.RelationType = MasterApprenticeType.Successor;
        }
        else if (relation.Intimacy >= 60)
        {
            relation.RelationType = MasterApprenticeType.Direct;
        }
        else if (relation.Intimacy >= 30)
        {
            relation.RelationType = MasterApprenticeType.Formal;
        }
        else
        {
            relation.RelationType = MasterApprenticeType.Nominal;
        }
    }
}

