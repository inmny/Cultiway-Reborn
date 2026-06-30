using System.Collections.Generic;
using System.Linq;
using Cultiway.Const;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Extensions;

/// <summary>
/// 宗门讲法规则，负责挑选讲法功法和能从讲法中获益的同宗成员。
/// </summary>
public static class SectLectureRules
{
    /// <summary>
    /// 判断单位当前是否具备讲法内容和可受益听众。
    /// </summary>
    public static bool CanLectureCultibook(Actor lecturer, Sect sect)
    {
        return TryPickLecture(lecturer, sect, out _, out _);
    }

    /// <summary>
    /// 从讲法者掌握的功法中挑选一个能让同宗成员增长了解度的功法。
    /// </summary>
    public static bool TryPickLecture(
        Actor lecturer,
        Sect sect,
        out CultibookAsset cultibook,
        out List<Actor> audience)
    {
        cultibook = null;
        audience = null;

        if (lecturer == null || lecturer.isRekt()) return false;
        if (sect == null || sect.isRekt() || lecturer.GetExtend().sect != sect) return false;

        ActorExtend lecturerExtend = lecturer.GetExtend();
        var cultibooks = lecturerExtend.GetAllMaster<CultibookAsset>()
            .Where(item => item.Item1 != null && item.Item2 > 0f)
            .ToList();
        CultibookAsset mainCultibook = lecturerExtend.GetMainCultibook();
        if (mainCultibook != null
            && lecturerExtend.GetMainCultibookMastery() > 0f
            && cultibooks.All(item => item.Item1 != mainCultibook))
        {
            cultibooks.Add((mainCultibook, lecturerExtend.GetMainCultibookMastery()));
        }

        if (cultibooks.Count == 0) return false;

        List<LectureCandidate> candidates = new();
        for (int i = 0; i < cultibooks.Count; i++)
        {
            CultibookAsset candidate = cultibooks[i].Item1;
            List<Actor> members = GetAudienceForCultibook(sect, lecturer, candidate);
            if (members.Count == 0) continue;

            candidates.Add(new LectureCandidate(candidate, members, members.Count));
        }

        if (candidates.Count == 0) return false;

        LectureCandidate picked = PickWeighted(candidates);
        cultibook = picked.Cultibook;
        audience = picked.Audience;
        return true;
    }

    /// <summary>
    /// 将一次讲法收益应用到听众身上，返回实际获得提升的人数。
    /// </summary>
    public static int ApplyLecture(Actor lecturer, Sect sect, CultibookAsset cultibook, List<Actor> audience)
    {
        if (cultibook == null || audience == null || audience.Count == 0) return 0;

        int taughtCount = 0;
        for (int i = 0; i < audience.Count && taughtCount < SectConst.SectLectureMaxAudience; i++)
        {
            Actor student = audience[i];
            if (student == null || student.isRekt()) continue;
            if (student.GetExtend().sect != sect) continue;

            ActorExtend studentExtend = student.GetExtend();
            float oldMastery = GetKnownMastery(studentExtend, cultibook);
            if (oldMastery >= SectConst.SectLectureCultibookMasteryCap) continue;

            float gain = oldMastery <= 0f
                ? SectConst.SectLectureNewCultibookGain
                : SectConst.SectLectureKnownCultibookGain;
            float newMastery = Mathf.Min(SectConst.SectLectureCultibookMasteryCap, oldMastery + gain);
            studentExtend.Master(cultibook, newMastery);

            taughtCount++;
        }

        return taughtCount;
    }

    private static List<Actor> GetAudienceForCultibook(Sect sect, Actor lecturer, CultibookAsset cultibook)
    {
        List<Actor> result = new();
        List<Actor> members = sect.GetLivingMembers();
        for (int i = 0; i < members.Count; i++)
        {
            Actor member = members[i];
            if (member == lecturer || member == null || member.isRekt()) continue;
            if (member.HasSectRole(SectRoles.NoGrade)) continue;

            ActorExtend ae = member.GetExtend();
            float mastery = GetKnownMastery(ae, cultibook);

            if (mastery < SectConst.SectLectureCultibookMasteryCap)
            {
                result.Add(member);
            }
        }

        result.Sort((left, right) => GetKnownMastery(left.GetExtend(), cultibook).CompareTo(GetKnownMastery(right.GetExtend(), cultibook)));
        return result;
    }

    private static float GetKnownMastery(ActorExtend ae, CultibookAsset cultibook)
    {
        float mastery = ae.GetMaster(cultibook);
        if (ae.GetMainCultibook() == cultibook)
        {
            mastery = Mathf.Max(mastery, ae.GetMainCultibookMastery());
        }

        return mastery;
    }

    private static LectureCandidate PickWeighted(List<LectureCandidate> candidates)
    {
        float totalWeight = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            totalWeight += candidates[i].Weight;
        }

        float roll = Randy.randomFloat(0f, totalWeight);
        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= candidates[i].Weight;
            if (roll <= 0f) return candidates[i];
        }

        return candidates[^1];
    }

    private readonly struct LectureCandidate
    {
        public LectureCandidate(CultibookAsset cultibook, List<Actor> audience, float weight)
        {
            Cultibook = cultibook;
            Audience = audience;
            Weight = weight;
        }

        public CultibookAsset Cultibook { get; }
        public List<Actor> Audience { get; }
        public float Weight { get; }
    }
}
