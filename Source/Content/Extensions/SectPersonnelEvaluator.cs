using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Core;
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

    public static SectRank EvaluateInitialRankForRecruit(Sect sect, Actor actor)
    {
        if (sect == null || actor == null || actor.isRekt()) return SectRank.OuterDisciple;
        if (CanAppointElder(sect, actor)) return SectRank.Elder;

        int score = GetRealmScore(actor);
        if (score >= SectConst.PersonnelDirectDiscipleMinScore) return SectRank.DirectDisciple;
        if (score >= SectConst.PersonnelInnerDiscipleMinScore) return SectRank.InnerDisciple;
        return SectRank.OuterDisciple;
    }

    public static SectRank EvaluatePromotionTarget(Sect sect, Actor actor)
    {
        if (sect == null || actor == null || actor.isRekt()) return SectRank.None;
        if (actor.GetExtend().sect != sect) return SectRank.None;

        SectRank current = actor.GetSectRank();
        if (current is SectRank.Leader or SectRank.Successor) return current;
        if (CanAppointElder(sect, actor)) return SectRank.Elder;

        int total = EvaluateScore(sect, actor).Total;
        if (total >= SectConst.PersonnelDirectDiscipleMinScore) return SectRank.DirectDisciple;
        if (total >= SectConst.PersonnelInnerDiscipleMinScore) return SectRank.InnerDisciple;
        return SectRank.OuterDisciple;
    }

    public static bool CanRecruitExternalMember(Actor recruiter)
    {
        if (!CanManageSectPersonnel(recruiter)) return false;

        return FindExternalRecruitCandidate(recruiter) != null;
    }

    public static bool CanManageSectPersonnel(Actor actor)
    {
        if (actor == null || actor.isRekt()) return false;
        Sect sect = actor.GetExtend().sect;
        if (sect == null || sect.isRekt()) return false;

        return CanRecruitForSect(sect, actor);
    }

    public static Actor FindExternalRecruitCandidate(Actor recruiter)
    {
        if (recruiter == null || recruiter.isRekt()) return null;
        Sect sect = recruiter.GetExtend().sect;
        if (sect == null || sect.isRekt()) return null;
        if (!CanRecruitForSect(sect, recruiter)) return null;

        Actor best = null;
        SectRank bestRank = SectRank.None;
        int bestScore = -1;
        float bestDistance = float.MaxValue;
        List<Actor> actors = World.world.units.units_only_alive;
        for (int i = 0; i < actors.Count; i++)
        {
            Actor candidate = actors[i];
            if (!CanRecruitExternalMember(sect, recruiter, candidate)) continue;

            SectRank rank = EvaluateInitialRankForRecruit(sect, candidate);
            int score = GetRealmScore(candidate);
            float distance = Toolbox.DistTile(recruiter.current_tile, candidate.current_tile);

            if (best == null
                || rank > bestRank
                || rank == bestRank && score > bestScore
                || rank == bestRank && score == bestScore && distance < bestDistance)
            {
                best = candidate;
                bestRank = rank;
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

        SectRank rank = EvaluateInitialRankForRecruit(sect, candidate);
        return sect.JoinSect(candidate, rank);
    }

    public static int CompareSuccessionCandidates(Sect sect, Actor left, Actor right)
    {
        int rankCompare = right.GetSectRank().CompareTo(left.GetSectRank());
        if (rankCompare != 0) return rankCompare;

        int scoreCompare = EvaluateScore(sect, right).Total.CompareTo(EvaluateScore(sect, left).Total);
        if (scoreCompare != 0) return scoreCompare;

        int masteryCompare = GetMainCultibookMastery(right).CompareTo(GetMainCultibookMastery(left));
        if (masteryCompare != 0) return masteryCompare;

        return left.data.id.CompareTo(right.data.id);
    }

    private static bool CanRecruitForSect(Sect sect, Actor recruiter)
    {
        if (recruiter == null || recruiter.isRekt()) return false;
        if (recruiter.GetExtend().sect != sect) return false;

        SectRank rank = recruiter.GetSectRank();
        return rank is SectRank.Leader or SectRank.Elder;
    }

    private static bool CanAppointElder(Sect sect, Actor actor)
    {
        if (GetCultivationLevel(actor) < SectConst.PersonnelElderMinCultivationLevel) return false;
        if (GetRealmScore(actor) < SectConst.PersonnelElderMinScore) return false;

        SectRank current = actor.GetSectRank();
        if (current == SectRank.Elder) return true;

        return CountElders(sect) < GetMaxElderCount(sect);
    }

    private static int GetMaxElderCount(Sect sect)
    {
        int memberCount = sect.GetLivingMembers().Count;
        return SectConst.PersonnelBaseElderSlots
               + memberCount / SectConst.PersonnelMembersPerExtraElderSlot;
    }

    private static int CountElders(Sect sect)
    {
        int result = 0;
        List<Actor> members = sect.GetLivingMembers();
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i].GetSectRank() == SectRank.Elder)
            {
                result++;
            }
        }

        return result;
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
