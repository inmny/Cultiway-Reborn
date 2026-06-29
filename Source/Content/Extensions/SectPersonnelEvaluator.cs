using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Extensions;

public static class SectPersonnelEvaluator
{
    public static SectPersonnelScore EvaluateScore(Sect sect, Actor actor)
    {
        if (sect == null || actor == null || actor.isRekt()) return new SectPersonnelScore(0, 0, 0);
        if (actor.GetExtend().sect != sect) return new SectPersonnelScore(0, 0, 0);

        return new SectPersonnelScore(
            GetRealmScore(actor),
            actor.GetSectTenureYears() * SectConst.PersonnelTenureScorePerYear,
            actor.GetSectContribution());
    }

    public static SectJoinProfile EvaluateInitialRolesForRecruit(Sect sect, Actor actor)
    {
        return new SectJoinProfile(
            GetBestAssignableRole(sect, actor, SectRoleSlot.Grade, true),
            GetBestAssignableRole(sect, actor, SectRoleSlot.Office, true),
            ModClass.L.SectRoleLibrary.GetDefault(SectRoleSlot.Title));
    }

    public static SectPersonnelEvaluation EvaluatePromotionTarget(Sect sect, Actor actor)
    {
        if (sect == null || actor == null || actor.isRekt()) return default;
        if (actor.GetExtend().sect != sect) return default;

        bool hasSeniorOffice = actor.GetSectRole(SectRoleSlot.Office)?.clearsGrade == true;
        SectRoleAsset targetGrade = hasSeniorOffice
            ? null
            : GetBestAssignableRole(sect, actor, SectRoleSlot.Grade, false);
        if (targetGrade != null && !IsHigherRole(targetGrade, actor.GetSectRole(SectRoleSlot.Grade)))
        {
            targetGrade = null;
        }

        SectRoleAsset targetOffice = null;
        if (!actor.HasSectRole(SectRoles.Leader))
        {
            targetOffice = GetBestAssignableRole(sect, actor, SectRoleSlot.Office, false);
            if (!IsHigherRole(targetOffice, actor.GetSectRole(SectRoleSlot.Office)))
            {
                targetOffice = null;
            }
        }

        return new SectPersonnelEvaluation(targetGrade, targetOffice, null);
    }

    public static bool CanRecruitExternalMember(Actor recruiter)
    {
        if (!CanRecruitForSect(recruiter?.GetExtend().sect, recruiter)) return false;

        return FindExternalRecruitCandidate(recruiter) != null;
    }

    public static bool CanManageSectPersonnel(Actor actor)
    {
        if (actor == null || actor.isRekt()) return false;
        Sect sect = actor.GetExtend().sect;
        if (sect == null || sect.isRekt()) return false;

        return actor.CanEvaluateSectPersonnel(sect);
    }

    public static Actor FindExternalRecruitCandidate(Actor recruiter)
    {
        if (recruiter == null || recruiter.isRekt()) return null;
        Sect sect = recruiter.GetExtend().sect;
        if (sect == null || sect.isRekt()) return null;
        if (!CanRecruitForSect(sect, recruiter)) return null;

        Actor best = null;
        int bestRoleOrder = -1;
        int bestScore = -1;
        float bestDistance = float.MaxValue;
        List<Actor> actors = World.world.units.units_only_alive;
        for (int i = 0; i < actors.Count; i++)
        {
            Actor candidate = actors[i];
            if (!CanRecruitExternalMember(sect, recruiter, candidate)) continue;

            SectJoinProfile profile = EvaluateInitialRolesForRecruit(sect, candidate);
            int roleOrder = GetProfileOrder(profile);
            int score = GetRealmScore(candidate);
            float distance = Toolbox.DistTile(recruiter.current_tile, candidate.current_tile);

            if (best == null
                || roleOrder > bestRoleOrder
                || roleOrder == bestRoleOrder && score > bestScore
                || roleOrder == bestRoleOrder && score == bestScore && distance < bestDistance)
            {
                best = candidate;
                bestRoleOrder = roleOrder;
                bestScore = score;
                bestDistance = distance;
            }
        }

        return best;
    }

    public static bool CanRecruitExternalMember(Sect sect, Actor recruiter, Actor candidate)
    {
        if (sect == null || sect.isRekt()) return false;
        if (!CanRecruitForSect(sect, recruiter)) return false;
        if (candidate == null || candidate.isRekt()) return false;
        if (candidate == recruiter) return false;
        if (!candidate.isSapient()) return false;
        if (candidate.GetExtend().sect != null) return false;
        if (candidate.GetExtend().HasMaster()) return false;
        if (!candidate.GetExtend().HasCultisys<Xian>()) return false;
        if (Toolbox.DistTile(recruiter.current_tile, candidate.current_tile) > SectConst.PersonnelRecruitRange) return false;

        Actor leader = sect.GetLeaderActor();
        if (leader == null) return false;

        int candidateLevel = GetCultivationLevel(candidate);
        int leaderLevel = GetCultivationLevel(leader);
        return candidateLevel <= leaderLevel + SectConst.PersonnelRecruitMaxLevelAboveLeader;
    }

    public static bool TryRecruitExternalMember(Sect sect, Actor recruiter, Actor candidate)
    {
        if (!CanRecruitExternalMember(sect, recruiter, candidate)) return false;

        SectJoinProfile profile = EvaluateInitialRolesForRecruit(sect, candidate);
        return sect.JoinSect(candidate, profile);
    }

    public static int CompareSuccessionCandidates(Sect sect, Actor left, Actor right)
    {
        int successorCompare = right.HasSectRole(SectRoles.Successor).CompareTo(left.HasSectRole(SectRoles.Successor));
        if (successorCompare != 0) return successorCompare;

        int authorityCompare = GetRoleAuthority(right).CompareTo(GetRoleAuthority(left));
        if (authorityCompare != 0) return authorityCompare;

        int scoreCompare = EvaluateScore(sect, right).Total.CompareTo(EvaluateScore(sect, left).Total);
        if (scoreCompare != 0) return scoreCompare;

        int masteryCompare = GetMainCultibookMastery(right).CompareTo(GetMainCultibookMastery(left));
        if (masteryCompare != 0) return masteryCompare;

        int tenureCompare = right.GetSectTenureYears().CompareTo(left.GetSectTenureYears());
        if (tenureCompare != 0) return tenureCompare;

        return left.data.id.CompareTo(right.data.id);
    }

    private static bool CanRecruitForSect(Sect sect, Actor recruiter)
    {
        if (recruiter == null || recruiter.isRekt()) return false;
        if (recruiter.GetExtend().sect != sect) return false;

        return recruiter.CanRecruitSectMember(sect);
    }

    private static SectRoleAsset GetBestAssignableRole(Sect sect, Actor actor, SectRoleSlot slot, bool initial)
    {
        List<SectRoleAsset> roles = ModClass.L.SectRoleLibrary.GetRoles(slot);
        for (int i = 0; i < roles.Count; i++)
        {
            SectRoleAsset role = roles[i];
            if (CanAssignRole(sect, actor, role, initial))
            {
                return role;
            }
        }

        return ModClass.L.SectRoleLibrary.GetDefault(slot);
    }

    private static bool CanAssignRole(Sect sect, Actor actor, SectRoleAsset role, bool initial)
    {
        if (role == null) return false;
        if (role.defaultForSlot) return true;
        if (actor == null || actor.isRekt()) return false;
        if (initial && !role.allowInitialAssign) return false;
        if (!initial && !role.allowAutoAssign) return false;
        if (!actor.CanMeetSectRoleMasterRequirement(sect, role)) return false;

        int score = initial ? GetRealmScore(actor) : EvaluateScore(sect, actor).Total;
        if (score < role.minPersonnelScore) return false;
        if (role.minCultivationLevel >= 0 && GetCultivationLevel(actor) < role.minCultivationLevel) return false;

        return HasAvailableRoleSlot(sect, actor, role);
    }

    private static bool HasAvailableRoleSlot(Sect sect, Actor actor, SectRoleAsset role)
    {
        if (role.baseSlots < 0) return true;
        if (actor.HasSectRole(role)) return true;

        int usedSlots = CountMembersWithRole(sect, role);
        int maxSlots = role.GetMaxCount(sect.GetLivingMembers().Count);
        return usedSlots < maxSlots;
    }

    private static int CountMembersWithRole(Sect sect, SectRoleAsset role)
    {
        int result = 0;
        List<Actor> members = sect.GetLivingMembers();
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i].HasSectRole(role))
            {
                result++;
            }
        }

        return result;
    }

    private static bool IsHigherRole(SectRoleAsset target, SectRoleAsset current)
    {
        if (target == null) return false;
        if (current == null) return true;
        return target.order > current.order;
    }

    private static int GetProfileOrder(SectJoinProfile profile)
    {
        int result = 0;
        UseRoleOrder(profile.Grade, ref result);
        UseRoleOrder(profile.Office, ref result);
        UseRoleOrder(profile.Title, ref result);
        return result;
    }

    private static void UseRoleOrder(SectRoleAsset role, ref int result)
    {
        if (role != null && role.showInPersonnel && role.order > result)
        {
            result = role.order;
        }
    }

    private static int GetRoleAuthority(Actor actor)
    {
        int result = 0;
        UseRoleAuthority(actor.GetSectRole(SectRoleSlot.Grade), ref result);
        UseRoleAuthority(actor.GetSectRole(SectRoleSlot.Office), ref result);
        UseRoleAuthority(actor.GetSectRole(SectRoleSlot.Title), ref result);
        return result;
    }

    private static void UseRoleAuthority(SectRoleAsset role, ref int result)
    {
        if (role != null && role.authority > result)
        {
            result = role.authority;
        }
    }

    private static int GetRealmScore(Actor actor)
    {
        return Mathf.Max(0, GetCultivationLevel(actor)) * SectConst.PersonnelRealmScorePerLevel;
    }

    private static int GetCultivationLevel(Actor actor)
    {
        ActorExtend ae = actor.GetExtend();
        return ae.HasCultisys<Xian>() ? ae.GetCultisys<Xian>().CurrLevel : -1;
    }

    private static float GetMainCultibookMastery(Actor actor)
    {
        return actor.GetExtend().GetMainCultibookMastery();
    }
}
