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
using Cultiway.Debug;
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
    /// 检查指定修士能否收取目标弟子；在宗门内收徒时额外校验师父职司是否能支撑对应入宗身份。
    /// </summary>
    public static bool CanRecruit(this ActorExtend master, ActorExtend apprentice, MasterApprenticeTypeAsset type = null)
    {
        type ??= MasterApprenticeTypes.Nominal;
        if (!master.CanRecruit()) return false;
        if (apprentice == null || apprentice.Base == null || apprentice.Base.isRekt()) return false;
        if (apprentice.HasMaster()) return false;

        return CanRecruitForSectContext(master, apprentice, type);
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
    public static bool TryRecruit(this ActorExtend master, ActorExtend apprentice, MasterApprenticeTypeAsset type = null)
    {
        type ??= MasterApprenticeTypes.Nominal;
        if (!master.CanRecruit(apprentice, type))
        {
            SectVerifyLog.Log("RecruitApprenticeBlocked", $"master={SectVerifyLog.Actor(master.Base)} apprentice={SectVerifyLog.Actor(apprentice?.Base)} relation={SectVerifyLog.Relation(type)} reason=not_qualified masterSect={SectVerifyLog.Sect(master.sect)} apprenticeSect={SectVerifyLog.Sect(apprentice?.sect)}");
            return false;
        }
        
        // 计算成功率
        float successRate = CalculateRecruitSuccessRate(master, apprentice);
        if (!Randy.randomChance(successRate)) return false;
        
        // 建立关系
        apprentice.E.AddRelation(new MasterApprenticeRelation
        {
            Master = master.E,
            RelationTypeId = type.id,
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
        
        SectVerifyLog.Log("RecruitApprentice", $"master={SectVerifyLog.Actor(master.Base)} apprentice={SectVerifyLog.Actor(apprentice.Base)} relation={SectVerifyLog.Relation(type)} masterSect={SectVerifyLog.Sect(master.sect)} apprenticeSect={SectVerifyLog.Sect(apprentice.sect)}");
        
        return true;
    }

    private static bool CanRecruitForSectContext(ActorExtend master, ActorExtend apprentice, MasterApprenticeTypeAsset type)
    {
        Sect sect = master.sect;
        if (sect == null) return true;
        if (apprentice.sect != null && apprentice.sect != sect) return false;

        SectJoinProfile profile = GetSectJoinProfileForRelation(type);
        return CanServeAsRoleMaster(master.Base, apprentice.Base, sect, profile.Grade)
               && CanServeAsRoleMaster(master.Base, apprentice.Base, sect, profile.Title);
    }

    private static bool CanServeAsRoleMaster(Actor master, Actor apprentice, Sect sect, SectRoleAsset role)
    {
        if (role == null) return true;
        return IsQualifiedSectMaster(master, apprentice, sect, GetSectRoleOrNull(role.requiredMasterOfficeRoleId));
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
        
        SectVerifyLog.Log("GraduateApprentice", $"apprentice={SectVerifyLog.Actor(ae.Base)} master={SectVerifyLog.Actor(master)} relation={SectVerifyLog.Relation(ModClass.L.MasterApprenticeTypeLibrary.GetOrDefault(relation.RelationTypeId))}");
        
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
        
        SectVerifyLog.Log("TeachCultibook", $"master={SectVerifyLog.Actor(master.Base)} apprentice={SectVerifyLog.Actor(apprentice.Base)} cultibook={cultibook.id} gained={gainedMastery:F1} relation={SectVerifyLog.Relation(ModClass.L.MasterApprenticeTypeLibrary.GetOrDefault(apprentice.GetMasterRelation().RelationTypeId))}");
        
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
        float relationMultiplier = ModClass.L.MasterApprenticeTypeLibrary
            .GetOrDefault(relation.RelationTypeId)
            .teachEfficiencyMultiplier;
        
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
        MasterApprenticeTypeAsset oldType = ModClass.L.MasterApprenticeTypeLibrary.GetOrDefault(relation.RelationTypeId);
        relation.TransferredCultibookCount++;
        relation.Intimacy = Mathf.Min(relation.Intimacy + 2, 100);
        UpdateRelationType(ref relation);
        MasterApprenticeTypeAsset newType = ModClass.L.MasterApprenticeTypeLibrary.GetOrDefault(relation.RelationTypeId);
        SectVerifyLog.Log("RelationAfterTeach", $"master={SectVerifyLog.Actor(master.Base)} apprentice={SectVerifyLog.Actor(apprentice.Base)} intimacy={relation.Intimacy:F1} relation={SectVerifyLog.Relation(oldType)}->{SectVerifyLog.Relation(newType)} transferredCultibooks={relation.TransferredCultibookCount}");

        if (master.sect != null && apprentice.sect == master.sect)
        {
            ApplySectRolesForRelation(master.sect, apprentice.Base, ModClass.L.MasterApprenticeTypeLibrary.GetOrDefault(relation.RelationTypeId));
            master.sect.AddContribution(master.Base, SectConst.ContributionTeachCultibook);
        }
    }

    public static SectJoinProfile GetSectJoinProfileForRelation(MasterApprenticeTypeAsset type)
    {
        type ??= MasterApprenticeTypes.Nominal;
        return new SectJoinProfile(
            GetSectGradeForRelation(type),
            null,
            GetSectTitleForRelation(type));
    }

    /// <summary>
    /// 判断授予宗门角色前是否满足师徒关系要求；只做可行性检查，不写入关系状态。
    /// </summary>
    public static bool CanMeetSectRoleMasterRequirement(this Actor actor, Sect sect, SectRoleAsset role)
    {
        if (role == null) return false;
        MasterApprenticeTypeAsset requiredType = GetRequiredRelationTypeForSectRole(role);
        if (requiredType == null) return true;

        if (role.canAutoAssignMasterForRequirement)
        {
            return HasQualifiedRoleMasterRelation(actor, sect, role, requiredType)
                   || CanFindRoleMaster(actor, sect, role, out _);
        }

        return HasSectMasterRelationAtLeast(actor, sect, requiredType);
    }

    /// <summary>
    /// 确保授予宗门角色前满足师徒关系要求；内门门阶会在可行时建立或升级正式师徒关系。
    /// </summary>
    public static bool EnsureSectRoleMasterRequirement(this Actor actor, Sect sect, SectRoleAsset role)
    {
        if (role == null) return false;
        MasterApprenticeTypeAsset requiredType = GetRequiredRelationTypeForSectRole(role);
        if (requiredType == null) return true;

        if (role.canAutoAssignMasterForRequirement)
        {
            return EnsureRoleMasterRequirement(actor, sect, role, requiredType);
        }

        return HasSectMasterRelationAtLeast(actor, sect, requiredType);
    }

    private static void ApplySectRolesForRelation(Sect sect, Actor apprentice, MasterApprenticeTypeAsset type)
    {
        SectRoleAsset grade = GetSectGradeForRelation(type);
        if (grade != null)
        {
            sect.PromoteMember(apprentice, grade);
        }

        SectRoleAsset title = GetSectTitleForRelation(type);
        if (title != null)
        {
            sect.PromoteMember(apprentice, title);
        }
    }

    private static SectRoleAsset GetSectGradeForRelation(MasterApprenticeTypeAsset type)
    {
        return GetSectRoleOrNull(type?.sectGradeRoleId);
    }

    private static SectRoleAsset GetSectTitleForRelation(MasterApprenticeTypeAsset type)
    {
        return GetSectRoleOrNull(type?.sectTitleRoleId);
    }

    /// <summary>
    /// 获取宗门角色配置的最低师徒关系层级。
    /// </summary>
    private static MasterApprenticeTypeAsset GetRequiredRelationTypeForSectRole(SectRoleAsset role)
    {
        if (string.IsNullOrEmpty(role.requiredMasterRelationTypeId)) return null;
        return ModClass.L.MasterApprenticeTypeLibrary.GetOrDefault(role.requiredMasterRelationTypeId);
    }

    /// <summary>
    /// 判断角色现有师父是否已经满足指定宗门角色的师徒要求。
    /// </summary>
    private static bool HasQualifiedRoleMasterRelation(Actor actor, Sect sect, SectRoleAsset role, MasterApprenticeTypeAsset requiredType)
    {
        if (!HasSectMasterRelationAtLeast(actor, sect, requiredType)) return false;

        Actor master = actor.GetExtend().GetMaster();
        return IsQualifiedSectMaster(master, actor, sect, GetSectRoleOrNull(role.requiredMasterOfficeRoleId));
    }

    /// <summary>
    /// 查找可满足宗门角色要求的师父；已有师父时只检查当前师父，不重新换师父。
    /// </summary>
    private static bool CanFindRoleMaster(Actor actor, Sect sect, SectRoleAsset role, out Actor master)
    {
        master = null;
        if (actor == null || actor.isRekt()) return false;
        if (sect == null || sect.isRekt()) return false;

        ActorExtend ae = actor.GetExtend();
        if (ae.HasMaster())
        {
            Actor currentMaster = ae.GetMaster();
            if (CanCurrentMasterSupportRole(currentMaster, actor, sect, role))
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
            if (!CanServeAsNewRoleMaster(candidate, actor, sect, role)) continue;

            master = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 为宗门角色授予落实所需师徒关系，必要时让宗门内符合要求的成员收徒。
    /// </summary>
    private static bool EnsureRoleMasterRequirement(Actor actor, Sect sect, SectRoleAsset role, MasterApprenticeTypeAsset requiredType)
    {
        if (HasQualifiedRoleMasterRelation(actor, sect, role, requiredType))
        {
            ref MasterApprenticeRelation relation = ref actor.GetExtend().GetMasterRelation();
            UpgradeMasterRelation(ref relation, requiredType);
            SectVerifyLog.Log("RoleMasterSatisfied", $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(actor)} role={SectVerifyLog.Role(role)} required={SectVerifyLog.Relation(requiredType)} master={SectVerifyLog.Actor(actor.GetExtend().GetMaster())}");
            return true;
        }

        if (!CanFindRoleMaster(actor, sect, role, out Actor master))
        {
            SectVerifyLog.Log("RoleMasterMissing", $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(actor)} role={SectVerifyLog.Role(role)} required={SectVerifyLog.Relation(requiredType)}");
            return false;
        }

        ActorExtend actorExtend = actor.GetExtend();
        if (actorExtend.HasMaster())
        {
            ref MasterApprenticeRelation relation = ref actorExtend.GetMasterRelation();
            UpgradeMasterRelation(ref relation, requiredType);
            SectVerifyLog.Log("RoleMasterUpgrade", $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(actor)} role={SectVerifyLog.Role(role)} required={SectVerifyLog.Relation(requiredType)} master={SectVerifyLog.Actor(master)}");
            return true;
        }

        ActorExtend masterExtend = master.GetExtend();
        actorExtend.E.AddRelation(new MasterApprenticeRelation
        {
            Master = masterExtend.E,
            RelationTypeId = requiredType.id,
            Intimacy = requiredType.minIntimacy,
            ApprenticeTime = (float)World.world.getCurWorldTime(),
            TransferredCultibookCount = 0,
            TransferredSkillCount = 0,
            IsSuccessor = false
        });

        ref MasterApprenticeState state = ref masterExtend.GetOrAddComponent<MasterApprenticeState>();
        state.ApprenticeCount++;
        EnsureMasterStateDefaults(masterExtend, ref state);
        SectVerifyLog.Log("AutoAssignMaster", $"sect={SectVerifyLog.Sect(sect)} master={SectVerifyLog.Actor(master)} apprentice={SectVerifyLog.Actor(actor)} role={SectVerifyLog.Role(role)} relation={SectVerifyLog.Relation(requiredType)} apprenticeCount={state.ApprenticeCount}/{state.MaxApprenticeCount}");
        return true;
    }

    /// <summary>
    /// 判断已有师父是否能支持徒弟获得指定宗门角色。
    /// </summary>
    private static bool CanCurrentMasterSupportRole(Actor master, Actor apprentice, Sect sect, SectRoleAsset role)
    {
        return IsQualifiedSectMaster(master, apprentice, sect, GetSectRoleOrNull(role.requiredMasterOfficeRoleId))
               && IsWillingToAcceptInnerDisciple(master.GetExtend());
    }

    /// <summary>
    /// 判断候选成员是否能作为新师父接纳指定宗门角色需要的弟子。
    /// </summary>
    private static bool CanServeAsNewRoleMaster(Actor master, Actor apprentice, Sect sect, SectRoleAsset role)
    {
        return IsQualifiedSectMaster(master, apprentice, sect, GetSectRoleOrNull(role.requiredMasterOfficeRoleId))
               && IsWillingToAcceptInnerDisciple(master.GetExtend())
               && CanAcceptNewInnerDisciple(master.GetExtend());
    }

    /// <summary>
    /// 判断候选师父是否属于同宗门成员、不是徒弟本人，并满足最低职司要求。
    /// </summary>
    private static bool IsQualifiedSectMaster(Actor master, Actor apprentice, Sect sect, SectRoleAsset requiredOffice)
    {
        if (master == null || master.isRekt()) return false;
        if (apprentice == null || apprentice.isRekt()) return false;
        if (master == apprentice) return false;
        if (master.GetExtend().sect != sect) return false;
        return requiredOffice == null || master.GetSectRole(SectRoleSlot.Office).order >= requiredOffice.order;
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
    private static void UpgradeMasterRelation(ref MasterApprenticeRelation relation, MasterApprenticeTypeAsset targetType)
    {
        MasterApprenticeTypeAsset currentType = ModClass.L.MasterApprenticeTypeLibrary.GetOrDefault(relation.RelationTypeId);
        if (currentType.rank < targetType.rank)
        {
            relation.RelationTypeId = targetType.id;
            SectVerifyLog.Log("RelationUpgrade", $"relation={SectVerifyLog.Relation(currentType)}->{SectVerifyLog.Relation(targetType)} minIntimacy={targetType.minIntimacy:F1}");
        }

        relation.Intimacy = Mathf.Max(relation.Intimacy, targetType.minIntimacy);
    }

    /// <summary>
    /// 判断角色是否拥有同宗门师父，且师徒关系层级不低于指定要求。
    /// </summary>
    private static bool HasSectMasterRelationAtLeast(Actor actor, Sect sect, MasterApprenticeTypeAsset requiredType)
    {
        if (requiredType == null) return true;
        if (actor == null || actor.isRekt()) return false;
        if (sect == null || sect.isRekt()) return false;

        ActorExtend ae = actor.GetExtend();
        if (!ae.HasMaster()) return false;

        Actor master = ae.GetMaster();
        if (master == null || master.isRekt()) return false;
        if (master.GetExtend().sect != sect) return false;

        return ae.GetRelationType().rank >= requiredType.rank;
    }

    /// <summary>
    /// 从宗门角色库中获取角色 id 对应的角色资产。
    /// </summary>
    private static SectRoleAsset GetSectRoleOrNull(string roleId)
    {
        if (string.IsNullOrEmpty(roleId)) return null;
        return ModClass.L.SectRoleLibrary.has(roleId) ? ModClass.L.SectRoleLibrary.get(roleId) : null;
    }
    
    private static void UpdateRelationType(ref MasterApprenticeRelation relation)
    {
        relation.RelationTypeId = ModClass.L.MasterApprenticeTypeLibrary
            .GetByIntimacy(relation.Intimacy, relation.IsSuccessor)
            .id;
    }
}

