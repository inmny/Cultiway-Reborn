using System;
using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Coordination;
using Cultiway.Debug;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Sects;

/// <summary>保存一次宗门讲法的功法、候选听众、位置与实际到场结算。</summary>
public sealed class SectLectureSession : ICoordinatedActivitySession
{
    /// <summary>讲师席位标识。</summary>
    public const string LecturerRoleId = "lecturer";

    /// <summary>听众席位标识。</summary>
    public const string AudienceRoleId = "audience";

    private static readonly Vector2Int[] AudienceOffsets =
    [
        new(-1, 0), new(1, 0), new(0, -1), new(0, 1),
        new(-1, -1), new(1, -1), new(-1, 1), new(1, 1),
        new(-2, 0), new(2, 0), new(0, -2), new(0, 2),
        new(-2, -1), new(2, -1), new(-2, 1), new(2, 1)
    ];

    private readonly Sect sect;
    private readonly long lecturerId;
    private readonly CultibookAsset cultibook;
    private readonly HashSet<long> candidateIds;
    private readonly int targetTileId;
    private readonly int maximumAudience;
    private readonly double lectureDuration;
    private double runningStartedAt;
    private double nextTalkAt;
    private bool settled;

    /// <summary>创建一次只允许指定候选成员参加的讲法会话。</summary>
    public SectLectureSession(
        Sect sect,
        Actor lecturer,
        CultibookAsset cultibook,
        IReadOnlyList<Actor> candidates,
        WorldTile target)
    {
        this.sect = sect;
        lecturerId = lecturer.getID();
        this.cultibook = cultibook;
        targetTileId = target.tile_id;
        maximumAudience = SectTraitRules.GetLectureMaxAudience(sect);
        lectureDuration = Randy.randomFloat(TimeScales.SecPerMonth, TimeScales.SecPerMonth * 2f);
        candidateIds = new HashSet<long>();
        for (var i = 0; i < candidates.Count; i++)
        {
            Actor candidate = candidates[i];
            if (!candidate.isRekt()) candidateIds.Add(candidate.getID());
        }
    }

    /// <inheritdoc />
    public void CollectCandidates(
        in CoordinatedActivityView activity,
        CoordinationRoleDefinition role,
        IList<CoordinationCandidate> output)
    {
        if (role.Id != AudienceRoleId) return;
        WorldTile target = ResolveTarget();
        if (target == null) return;
        using var candidates = new ListPool<Actor>();
        foreach (long actorId in candidateIds)
        {
            Actor actor = World.world.units.get(actorId);
            if (!IsEligibleAudience(actor)) continue;
            candidates.Add(actor);
        }
        candidates.Sort((left, right) =>
        {
            float leftScore = ResolveCandidateScore(left, target);
            float rightScore = ResolveCandidateScore(right, target);
            int score = rightScore.CompareTo(leftScore);
            return score != 0 ? score : left.getID().CompareTo(right.getID());
        });
        for (var i = 0; i < candidates.Count && i < maximumAudience; i++)
        {
            Actor candidate = candidates[i];
            output.Add(new CoordinationCandidate(candidate, ResolveCandidateScore(candidate, target)));
        }
    }

    /// <inheritdoc />
    public bool IsParticipantValid(
        in CoordinatedActivityView activity,
        in CoordinationParticipantView participant,
        Actor actor)
    {
        if (sect.isRekt() || cultibook == null || actor.isRekt() || actor.GetExtend().sect != sect)
            return false;
        if (participant.RoleId == LecturerRoleId)
            return actor.getID() == lecturerId &&
                   SectLectureService.GetKnownMastery(actor.GetExtend(), cultibook) > 0f;
        return participant.RoleId == AudienceRoleId && IsEligibleAudience(actor);
    }

    /// <inheritdoc />
    public void OnStageChanged(in CoordinationUpdateContext context)
    {
        RefreshPlacements(context.Controller);
        if (context.Controller.View.Stage != CoordinatedActivityStage.Running) return;
        runningStartedAt = context.Now;
        nextTalkAt = context.Now;
    }

    /// <inheritdoc />
    public CoordinationSessionResult Update(in CoordinationUpdateContext context)
    {
        if (sect.isRekt() || ResolveTarget() == null) return CoordinationSessionResult.Fail;
        RefreshPlacements(context.Controller);
        CoordinatedActivityView activity = context.Controller.View;
        if (activity.Stage != CoordinatedActivityStage.Running) return CoordinationSessionResult.Continue;
        if (!context.Controller.MeetsReadinessRequirements) return CoordinationSessionResult.Continue;
        if (runningStartedAt <= 0d) runningStartedAt = context.Now;
        if (context.Now - runningStartedAt < lectureDuration) return CoordinationSessionResult.Continue;
        return Settle(activity) > 0
            ? CoordinationSessionResult.Complete
            : CoordinationSessionResult.Fail;
    }

    /// <inheritdoc />
    public CoordinationParticipantResult TickParticipant(in CoordinationParticipantContext context)
    {
        if (context.Activity.Stage == CoordinatedActivityStage.Running &&
            context.Participant.RoleId == LecturerRoleId &&
            context.Now >= nextTalkAt)
        {
            WorldTile target = ResolveTarget();
            if (target != null) context.Actor.spawnSlashTalk(target.pos);
            nextTalkAt = context.Now + TimeScales.SecPerMonth * 0.35f;
        }
        return CoordinationParticipantResult.Continue;
    }

    /// <inheritdoc />
    public string ResolvePresentationLocaleKey(
        in CoordinatedActivityView activity,
        in CoordinationParticipantView participant)
    {
        if (participant.RoleId == LecturerRoleId)
        {
            return activity.Stage switch
            {
                CoordinatedActivityStage.Recruiting => "Task.Unit.Cultiway.SectLecture.Recruiting",
                CoordinatedActivityStage.Assembling => "Task.Unit.Cultiway.SectLecture.AssemblingLecturer",
                CoordinatedActivityStage.Running => "Task.Unit.Cultiway.LectureSectCultibook",
                _ => "Task.Unit.Cultiway.LectureSectCultibook"
            };
        }
        return activity.Stage switch
        {
            CoordinatedActivityStage.Recruiting => "Task.Unit.Cultiway.SectLecture.Accepting",
            CoordinatedActivityStage.Assembling => "Task.Unit.Cultiway.SectLecture.AssemblingAudience",
            CoordinatedActivityStage.Running => "Task.Unit.Cultiway.SectLecture.Listening",
            _ => "Task.Unit.Cultiway.SectLecture.Listening"
        };
    }

    /// <inheritdoc />
    public void OnEnded(in CoordinatedActivityResult result)
    {
        if (settled) return;
        SectVerifyLog.Log(
            "SectLectureTask",
            $"sect={SectVerifyLog.Sect(sect)} lecturer={lecturerId} cultibook={cultibook?.id ?? "null"} result=false reason={result.Reason}");
    }

    /// <summary>为讲师设置中心位置，并按稳定顺序为听众分配环形位置。</summary>
    private void RefreshPlacements(ICoordinatedActivityController controller)
    {
        CoordinatedActivityView activity = controller.View;
        var audienceIndex = 0;
        for (var i = 0; i < activity.Participants.Count; i++)
        {
            CoordinationParticipantView participant = activity.Participants[i];
            if (participant.RoleId == LecturerRoleId)
            {
                controller.SetPlacement(
                    participant.ActorId,
                    CoordinationPlacementOrder.AtTile(targetTileId, default, 0.75f));
                continue;
            }
            Vector2Int offset = AudienceOffsets[audienceIndex % AudienceOffsets.Length];
            audienceIndex++;
            controller.SetPlacement(
                participant.ActorId,
                CoordinationPlacementOrder.AtTile(targetTileId, offset, 0.9f));
        }
    }

    /// <summary>只对仍有效且实际到场的听众应用原有讲法收益。</summary>
    private int Settle(in CoordinatedActivityView activity)
    {
        if (settled) return 0;
        Actor lecturer = World.world.units.get(lecturerId);
        if (lecturer.isRekt()) return 0;
        using var audience = new ListPool<Actor>();
        for (var i = 0; i < activity.Participants.Count; i++)
        {
            CoordinationParticipantView participant = activity.Participants[i];
            if (participant.RoleId != AudienceRoleId || !participant.Ready) continue;
            Actor actor = World.world.units.get(participant.ActorId);
            if (IsEligibleAudience(actor)) audience.Add(actor);
        }
        int taughtCount = SectLectureService.ApplyLecture(lecturer, sect, cultibook, audience);
        if (taughtCount <= 0) return 0;

        int contribution = SectTraitRules.GetAffairContributionReward(sect, SectAffairs.LectureCultibook);
        bool contributionApplied = sect.AddContribution(lecturer, contribution);
        SectVerifyLog.Log(
            "SectLectureTask",
            $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(lecturer)} cultibook={cultibook.id} audience={taughtCount} contribution={contribution} result={contributionApplied}");
        if (contributionApplied)
            WorldLogUtils.LogSectLecture(sect, lecturer, cultibook.Name, taughtCount);
        settled = true;
        return taughtCount;
    }

    /// <summary>判断成员当前是否仍能从本次讲法受益。</summary>
    private bool IsEligibleAudience(Actor actor)
    {
        if (actor.isRekt() || actor.getID() == lecturerId || actor.GetExtend().sect != sect) return false;
        if (!candidateIds.Contains(actor.getID()) || actor.HasSectRole(SectRoles.NoGrade)) return false;
        return SectLectureService.GetKnownMastery(actor.GetExtend(), cultibook) <
               SectConst.SectLectureCultibookMasteryCap;
    }

    /// <summary>按学习需求、距离和当前安全状态计算邀请优先级。</summary>
    private float ResolveCandidateScore(Actor actor, WorldTile target)
    {
        float mastery = SectLectureService.GetKnownMastery(actor.GetExtend(), cultibook);
        float need = SectConst.SectLectureCultibookMasteryCap - mastery;
        float distance = Toolbox.DistVec2Float(actor.current_position, target.posV3);
        float safety = actor.has_attack_target || actor.ai.task?.in_combat == true ? -1000f : 0f;
        return need * 10f - distance + safety;
    }

    /// <summary>解析本次讲法的固定驻地目标。</summary>
    private WorldTile ResolveTarget()
    {
        WorldTile[] tiles = World.world?.tiles_list;
        return tiles != null && targetTileId >= 0 && targetTileId < tiles.Length
            ? tiles[targetTileId]
            : null;
    }
}
