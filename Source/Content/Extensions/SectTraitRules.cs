using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Extensions;

/// <summary>
/// 宗门特质规则入口，集中把宗门制度特质转换为各系统使用的倍率和门槛修正。
/// </summary>
public static class SectTraitRules
{
    public static float GetRecruitRange(Actor recruiter)
    {
        Sect sect = recruiter.GetExtend().sect;
        return SectConst.PersonnelRecruitRange * GetMultiplier(sect, WorldboxGame.BaseStats.RecruitRangeModifier);
    }

    public static int GetRecruitMaxLevelAboveLeader(Sect sect)
    {
        return SectConst.PersonnelRecruitMaxLevelAboveLeader + GetIntBonus(sect, WorldboxGame.BaseStats.RecruitMaxLevelBonus);
    }

    public static float GetRecruitRealmScoreMultiplier(Sect sect)
    {
        return GetMultiplier(sect, WorldboxGame.BaseStats.RecruitRealmScoreModifier);
    }

    public static bool RequiresMasterIntroduction(Sect sect)
    {
        return sect.base_stats.hasTag(WorldboxGame.BaseStats.TagRecruitRequiresMasterIntroduction);
    }

    public static int GetRealmPersonnelScore(Sect sect, Actor actor, int baseScore)
    {
        return ScaleInt(baseScore, GetMultiplier(sect, WorldboxGame.BaseStats.PersonnelRealmScoreModifier));
    }

    public static int GetTenurePersonnelScore(Sect sect, Actor actor, int baseScore)
    {
        return ScaleInt(baseScore, GetMultiplier(sect, WorldboxGame.BaseStats.PersonnelTenureScoreModifier));
    }

    public static int GetContributionPersonnelScore(Sect sect, Actor actor, int baseScore)
    {
        return ScaleInt(baseScore, GetMultiplier(sect, WorldboxGame.BaseStats.PersonnelContributionScoreModifier));
    }

    public static int GetPromotionScoreThreshold(Sect sect, SectRoleAsset role)
    {
        float multiplier = GetMultiplier(sect, WorldboxGame.BaseStats.PromotionScoreThresholdModifier);
        if (role == SectRoles.Deacon)
        {
            multiplier *= GetMultiplier(sect, WorldboxGame.BaseStats.DeaconThresholdModifier);
        }
        else if (role == SectRoles.Elder)
        {
            multiplier *= GetMultiplier(sect, WorldboxGame.BaseStats.ElderThresholdModifier);
        }

        return ScaleInt(role.minPersonnelScore, multiplier);
    }

    public static int GetMaxRoleSlots(Sect sect, SectRoleAsset role)
    {
        int baseCount = role.GetMaxCount(sect.GetLivingMembers().Count);
        if (baseCount == int.MaxValue) return baseCount;
        if (baseCount <= 0) return 0;

        float multiplier = 1f;
        if (role == SectRoles.Deacon)
        {
            multiplier = GetMultiplier(sect, WorldboxGame.BaseStats.DeaconSlotModifier);
        }
        else if (role == SectRoles.Elder)
        {
            multiplier = GetMultiplier(sect, WorldboxGame.BaseStats.ElderSlotModifier);
        }

        return Mathf.Max(1, Mathf.RoundToInt(baseCount * multiplier));
    }

    public static float GetMasterWillingnessThreshold(Sect sect)
    {
        return SectConst.PersonnelInnerDiscipleMasterMinRecruitWillingness
               * GetMultiplier(sect, WorldboxGame.BaseStats.MasterWillingnessThresholdModifier);
    }

    public static int GetMasterApprenticeCapacity(Sect sect, ActorExtend master)
    {
        int baseCount = master.GetMaxApprenticeCount();
        float multiplier = GetMultiplier(sect, WorldboxGame.BaseStats.MasterApprenticeCapacityModifier);
        return Mathf.Max(0, Mathf.RoundToInt(baseCount * multiplier));
    }

    public static bool CanDiscipleOrganizeScripture(Sect sect, Actor actor)
    {
        if (actor.GetSectRole(SectRoleSlot.Grade).order < SectRoles.InnerDisciple.order) return false;
        return sect.base_stats.hasTag(WorldboxGame.BaseStats.TagAllowDiscipleOrganizeScripture);
    }

    public static float GetStudyScoreMultiplier(Sect sect, Book book)
    {
        if (book.getAsset() == BookTypes.Cultibook)
        {
            return GetCultibookStudyScoreMultiplier(sect, book);
        }

        if (book.getAsset() == BookTypes.Skillbook)
        {
            return GetMultiplier(sect, WorldboxGame.BaseStats.SkillbookStudyModifier);
        }

        if (book.getAsset() == BookTypes.Elixirbook)
        {
            return GetMultiplier(sect, WorldboxGame.BaseStats.ElixirbookStudyModifier);
        }

        return 1f;
    }

    public static float GetOutOfPermissionReadCostMultiplier(Sect sect)
    {
        return GetMultiplier(sect, WorldboxGame.BaseStats.OutOfPermissionReadCostModifier);
    }

    public static float GetLectureWeightMultiplier(Sect sect, CultibookAsset cultibook)
    {
        if (sect.GetDoctrineCultibook() != cultibook) return 1f;
        return GetMultiplier(sect, WorldboxGame.BaseStats.DoctrineLectureWeightModifier);
    }

    public static int GetLectureMaxAudience(Sect sect)
    {
        return Mathf.Max(1, SectConst.SectLectureMaxAudience + GetIntBonus(sect, WorldboxGame.BaseStats.LectureMaxAudienceBonus));
    }

    public static float GetTeachingGainMultiplier(Sect sect)
    {
        return GetMultiplier(sect, WorldboxGame.BaseStats.TeachingGainModifier);
    }

    public static float GetAffairWeightMultiplier(Sect sect, SectAffairAsset affair)
    {
        if (affair == SectAffairs.Chore)
        {
            return GetMultiplier(sect, WorldboxGame.BaseStats.ChoreAffairWeightModifier);
        }

        if (affair == SectAffairs.OrganizeScripture)
        {
            return GetMultiplier(sect, WorldboxGame.BaseStats.OrganizeScriptureAffairWeightModifier);
        }

        if (affair == SectAffairs.LectureCultibook)
        {
            return GetMultiplier(sect, WorldboxGame.BaseStats.LectureAffairWeightModifier);
        }

        return 1f;
    }

    public static int GetAffairContributionReward(Sect sect, SectAffairAsset affair)
    {
        float multiplier = 1f;
        if (affair == SectAffairs.Chore)
        {
            multiplier = GetMultiplier(sect, WorldboxGame.BaseStats.ChoreContributionModifier);
        }
        else if (affair == SectAffairs.OrganizeScripture)
        {
            multiplier = GetMultiplier(sect, WorldboxGame.BaseStats.OrganizeScriptureContributionModifier);
        }
        else if (affair == SectAffairs.LectureCultibook)
        {
            multiplier = GetMultiplier(sect, WorldboxGame.BaseStats.LectureContributionModifier);
        }

        return ScaleReward(affair.contributionReward, multiplier);
    }

    public static int GetBuildContributionReward(Sect sect)
    {
        return ScaleReward(SectConst.ContributionBuildSectBuilding, GetMultiplier(sect, WorldboxGame.BaseStats.BuildContributionModifier));
    }

    public static int GetWriteScriptureContributionReward(Sect sect)
    {
        return ScaleReward(SectConst.ContributionWriteScriptureBook, GetMultiplier(sect, WorldboxGame.BaseStats.WriteScriptureContributionModifier));
    }

    public static float GetSectStudyJobChance(Sect sect)
    {
        return Mathf.Clamp01(SectConst.SectStudyJobChance * GetMultiplier(sect, WorldboxGame.BaseStats.SectStudyJobChanceModifier));
    }

    public static float GetSectAffairJobChance(Sect sect)
    {
        return Mathf.Clamp01(SectConst.SectAffairJobChance * GetMultiplier(sect, WorldboxGame.BaseStats.SectAffairJobChanceModifier));
    }

    private static float GetCultibookStudyScoreMultiplier(Sect sect, Book book)
    {
        BookExtend bookExtend = book.GetExtend();
        if (!bookExtend.HasComponent<Cultibook>()) return 1f;

        CultibookAsset cultibook = bookExtend.GetComponent<Cultibook>().Asset;
        if (sect.GetDoctrineCultibook() == cultibook)
        {
            return GetMultiplier(sect, WorldboxGame.BaseStats.DoctrineCultibookStudyModifier);
        }

        return GetMultiplier(sect, WorldboxGame.BaseStats.OtherCultibookStudyModifier);
    }

    private static int ScaleInt(int value, float multiplier)
    {
        if (value <= 0) return value;
        return Mathf.Max(0, Mathf.RoundToInt(value * multiplier));
    }

    private static int ScaleReward(int value, float multiplier)
    {
        if (value <= 0) return value;
        return Mathf.Max(1, Mathf.RoundToInt(value * multiplier));
    }

    private static float GetMultiplier(Sect sect, BaseStatAsset stat)
    {
        return Mathf.Max(0f, 1f + sect.base_stats[stat.id]);
    }

    private static int GetIntBonus(Sect sect, BaseStatAsset stat)
    {
        return Mathf.RoundToInt(sect.base_stats[stat.id]);
    }
}
