using System.Collections.Generic;
using System.Linq;
using Cultiway.Const;
using Cultiway.Content;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
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

        if (master.sect != null && apprentice.sect == null)
        {
            master.sect.JoinSect(apprentice.Base, GetSectJoinProfileForRelation(type));
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

        if (master.sect != null && apprentice.sect == master.sect)
        {
            ApplySectRolesForRelation(master.sect, apprentice.Base, relation.RelationType);
            master.sect.AddContribution(master.Base, SectConst.ContributionTeachCultibook);
        }
    }

    public static SectJoinProfile GetSectJoinProfileForRelation(MasterApprenticeType type)
    {
        return new SectJoinProfile(
            GetSectGradeForRelation(type),
            null,
            type == MasterApprenticeType.Successor ? SectRoles.Successor : null);
    }

    /// <summary>
    /// 判断授予宗门角色前是否满足师徒关系要求；只做可行性检查，不写入关系状态。
    /// </summary>
    public static bool CanMeetSectRoleMasterRequirement(this Actor actor, Sect sect, SectRoleAsset role)
    {
        if (role == null) return false;
        if (role.slot != SectRoleSlot.Grade && role != SectRoles.Successor) return true;
        if (role.defaultForSlot || role == SectRoles.OuterDisciple) return true;
        if (IsInnerDiscipleGrade(role)) return HasQualifiedInnerMasterRelation(actor, sect) || CanFindInnerDiscipleMaster(actor, sect, out _);

        MasterApprenticeType requiredType = GetRequiredRelationTypeForSectRole(role);
        return HasSectMasterRelationAtLeast(actor, sect, requiredType);
    }

    /// <summary>
    /// 确保授予宗门角色前满足师徒关系要求；内门门阶会在可行时建立或升级正式师徒关系。
    /// </summary>
    public static bool EnsureSectRoleMasterRequirement(this Actor actor, Sect sect, SectRoleAsset role)
    {
        if (role == null) return false;
        if (role.slot != SectRoleSlot.Grade && role != SectRoles.Successor) return true;
        if (role.defaultForSlot || role == SectRoles.OuterDisciple) return true;
        if (IsInnerDiscipleGrade(role)) return EnsureInnerDiscipleMaster(actor, sect);

        MasterApprenticeType requiredType = GetRequiredRelationTypeForSectRole(role);
        return HasSectMasterRelationAtLeast(actor, sect, requiredType);
    }

    private static void ApplySectRolesForRelation(Sect sect, Actor apprentice, MasterApprenticeType type)
    {
        sect.PromoteMember(apprentice, GetSectGradeForRelation(type));
        if (type == MasterApprenticeType.Successor)
        {
            sect.PromoteMember(apprentice, SectRoles.Successor);
        }
    }

    private static SectRoleAsset GetSectGradeForRelation(MasterApprenticeType type)
    {
        return type switch
        {
            MasterApprenticeType.Nominal => SectRoles.OuterDisciple,
            MasterApprenticeType.Formal => SectRoles.InnerDisciple,
            MasterApprenticeType.Direct => SectRoles.DirectDisciple,
            MasterApprenticeType.Successor => SectRoles.DirectDisciple,
            _ => SectRoles.OuterDisciple
        };
    }

    /// <summary>
    /// 将宗门门阶或特殊身份映射为最低需要的师徒关系层级。
    /// </summary>
    private static MasterApprenticeType GetRequiredRelationTypeForSectRole(SectRoleAsset role)
    {
        if (role == SectRoles.Successor) return MasterApprenticeType.Successor;
        if (role == SectRoles.DirectDisciple) return MasterApprenticeType.Direct;
        if (role.slot == SectRoleSlot.Grade && role.order >= SectRoles.DirectDisciple.order) return MasterApprenticeType.Direct;
        return MasterApprenticeType.Formal;
    }

    /// <summary>
    /// 判断角色是否属于外门和亲传之间、需要现场寻找执事以上师父的内门门阶。
    /// </summary>
    private static bool IsInnerDiscipleGrade(SectRoleAsset role)
    {
        return role.slot == SectRoleSlot.Grade
               && role.order > SectRoles.OuterDisciple.order
               && role.order < SectRoles.DirectDisciple.order;
    }

    /// <summary>
    /// 判断角色现有师父是否已经满足内门门阶要求。
    /// </summary>
    private static bool HasQualifiedInnerMasterRelation(Actor actor, Sect sect)
    {
        if (!HasSectMasterRelationAtLeast(actor, sect, MasterApprenticeType.Formal)) return false;

        Actor master = actor.GetExtend().GetMaster();
        return IsSectDeaconOrAbove(master, actor, sect);
    }

    /// <summary>
    /// 查找可让角色晋升内门的师父；已有师父时只检查当前师父，不重新换师父。
    /// </summary>
    private static bool CanFindInnerDiscipleMaster(Actor actor, Sect sect, out Actor master)
    {
        master = null;
        if (actor == null || actor.isRekt()) return false;
        if (sect == null || sect.isRekt()) return false;

        ActorExtend ae = actor.GetExtend();
        if (ae.HasMaster())
        {
            Actor currentMaster = ae.GetMaster();
            if (CanCurrentMasterPromoteToInnerDisciple(currentMaster, actor, sect))
            {
                master = currentMaster;
                return true;
            }

            return false;
        }

        List<Actor> members = sect.GetLivingMembers();
        members.Sort((left, right) => right.GetSectRole(SectRoleSlot.Office).order.CompareTo(left.GetSectRole(SectRoleSlot.Office).order));
        for (int i = 0; i < members.Count; i++)
        {
            Actor candidate = members[i];
            if (!CanServeAsNewInnerDiscipleMaster(candidate, actor, sect)) continue;

            master = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 为内门晋升落实正式师徒关系，必要时让宗门内执事以上成员收徒。
    /// </summary>
    private static bool EnsureInnerDiscipleMaster(Actor actor, Sect sect)
    {
        if (HasQualifiedInnerMasterRelation(actor, sect))
        {
            ref MasterApprenticeRelation relation = ref actor.GetExtend().GetMasterRelation();
            UpgradeMasterRelation(ref relation, MasterApprenticeType.Formal);
            return true;
        }

        if (!CanFindInnerDiscipleMaster(actor, sect, out Actor master)) return false;

        ActorExtend actorExtend = actor.GetExtend();
        if (actorExtend.HasMaster())
        {
            ref MasterApprenticeRelation relation = ref actorExtend.GetMasterRelation();
            UpgradeMasterRelation(ref relation, MasterApprenticeType.Formal);
            return true;
        }

        ActorExtend masterExtend = master.GetExtend();
        actorExtend.E.AddRelation(new MasterApprenticeRelation
        {
            Master = masterExtend.E,
            RelationType = MasterApprenticeType.Formal,
            Intimacy = GetMinIntimacyForRelation(MasterApprenticeType.Formal),
            ApprenticeTime = (float)World.world.getCurWorldTime(),
            TransferredCultibookCount = 0,
            TransferredSkillCount = 0,
            IsSuccessor = false
        });

        ref MasterApprenticeState state = ref masterExtend.GetOrAddComponent<MasterApprenticeState>();
        state.ApprenticeCount++;
        EnsureMasterStateDefaults(masterExtend, ref state);
        ModClass.LogInfo($"[{master.getName()}] 收内门弟子: {actor.getName()}");
        return true;
    }

    /// <summary>
    /// 判断已有师父是否能支持徒弟晋升内门。
    /// </summary>
    private static bool CanCurrentMasterPromoteToInnerDisciple(Actor master, Actor apprentice, Sect sect)
    {
        return IsSectDeaconOrAbove(master, apprentice, sect)
               && IsWillingToAcceptInnerDisciple(master.GetExtend());
    }

    /// <summary>
    /// 判断候选成员是否能作为新师父接纳内门弟子。
    /// </summary>
    private static bool CanServeAsNewInnerDiscipleMaster(Actor master, Actor apprentice, Sect sect)
    {
        return IsSectDeaconOrAbove(master, apprentice, sect)
               && IsWillingToAcceptInnerDisciple(master.GetExtend())
               && CanAcceptNewInnerDisciple(master.GetExtend());
    }

    /// <summary>
    /// 判断候选师父是否属于同宗门执事及以上成员，且不是徒弟本人。
    /// </summary>
    private static bool IsSectDeaconOrAbove(Actor master, Actor apprentice, Sect sect)
    {
        if (master == null || master.isRekt()) return false;
        if (apprentice == null || apprentice.isRekt()) return false;
        if (master == apprentice) return false;
        if (master.GetExtend().sect != sect) return false;
        return master.GetSectRole(SectRoleSlot.Office).order >= SectRoles.Deacon.order;
    }

    /// <summary>
    /// 判断候选师父是否具备新增一名内门弟子的修为、功法和名额条件。
    /// </summary>
    private static bool CanAcceptNewInnerDisciple(ActorExtend master)
    {
        if (!master.HasCultisys<Xian>()) return false;
        ref Xian xian = ref master.GetCultisys<Xian>();
        if (xian.level < XianLevels.Jindan) return false;

        CultibookAsset mainCultibook = master.GetMainCultibook();
        if (mainCultibook == null) return false;
        if (master.GetMainCultibookMastery() < 40) return false;

        int maxCount = master.GetMaxApprenticeCount();
        return maxCount > 0 && master.GetApprentices().Count < maxCount;
    }

    /// <summary>
    /// 判断候选师父当前是否愿意收内门弟子。
    /// </summary>
    private static bool IsWillingToAcceptInnerDisciple(ActorExtend master)
    {
        if (!master.TryGetComponent(out MasterApprenticeState state)) return true;
        return state.RecruitWillingness >= SectConst.PersonnelInnerDiscipleMasterMinRecruitWillingness;
    }

    /// <summary>
    /// 为首次参与收徒的师父补齐师徒状态默认值。
    /// </summary>
    private static void EnsureMasterStateDefaults(ActorExtend master, ref MasterApprenticeState state)
    {
        if (state.MaxApprenticeCount == 0)
        {
            state.MaxApprenticeCount = master.GetMaxApprenticeCount();
        }
        if (state.TeachWillingness == 0)
        {
            state.TeachWillingness = 50f;
        }
        if (state.RecruitWillingness == 0)
        {
            state.RecruitWillingness = 50f;
        }
        if (state.Style == 0)
        {
            state.Style = MasterStyle.Gentle;
        }
    }

    /// <summary>
    /// 将师徒关系提升到目标层级，并补足该层级需要的最低亲密度。
    /// </summary>
    private static void UpgradeMasterRelation(ref MasterApprenticeRelation relation, MasterApprenticeType targetType)
    {
        if (GetRelationRank(relation.RelationType) < GetRelationRank(targetType))
        {
            relation.RelationType = targetType;
        }

        relation.Intimacy = Mathf.Max(relation.Intimacy, GetMinIntimacyForRelation(targetType));
    }

    /// <summary>
    /// 获取指定师徒关系层级需要保底的亲密度。
    /// </summary>
    private static float GetMinIntimacyForRelation(MasterApprenticeType type)
    {
        return type switch
        {
            MasterApprenticeType.Formal => 30f,
            MasterApprenticeType.Direct => 60f,
            MasterApprenticeType.Successor => 90f,
            _ => 0f
        };
    }

    /// <summary>
    /// 判断角色是否拥有同宗门师父，且师徒关系层级不低于指定要求。
    /// </summary>
    private static bool HasSectMasterRelationAtLeast(Actor actor, Sect sect, MasterApprenticeType requiredType)
    {
        if (actor == null || actor.isRekt()) return false;
        if (sect == null || sect.isRekt()) return false;

        ActorExtend ae = actor.GetExtend();
        if (!ae.HasMaster()) return false;

        Actor master = ae.GetMaster();
        if (master == null || master.isRekt()) return false;
        if (master.GetExtend().sect != sect) return false;

        return GetRelationRank(ae.GetRelationType()) >= GetRelationRank(requiredType);
    }

    /// <summary>
    /// 将师徒关系层级转换为可比较的排序值。
    /// </summary>
    private static int GetRelationRank(MasterApprenticeType type)
    {
        return type switch
        {
            MasterApprenticeType.Nominal => 0,
            MasterApprenticeType.Formal => 1,
            MasterApprenticeType.Direct => 2,
            MasterApprenticeType.Successor => 3,
            _ => 0
        };
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

