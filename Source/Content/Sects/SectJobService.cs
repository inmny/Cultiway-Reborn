using System.Collections.Generic;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Debug;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Sects;

/// <summary>
/// 宗门岗位服务，负责刷新名额、分配岗位和释放占用。
/// </summary>
public static class SectJobService
{
    /// <summary>
    /// 根据宗门当前状态刷新岗位名额和占用。
    /// </summary>
    [Hotfixable]
    public static void Refresh(Sect sect)
    {
        if (sect == null || sect.isRekt()) return;

        sect.jobs.ClearJobs();
        List<SectJobAsset> jobs = ModClass.L.SectJobLibrary.list;
        for (int i = 0; i < jobs.Count; i++)
        {
            SectJobAsset job = jobs[i];
            if (job == null || !job.commonJob) continue;

            int count = job.countJobs?.Invoke(sect) ?? 0;
            sect.jobs.AddToJob(job, count);
        }

        RefreshOccupied(sect);
    }

    /// <summary>
    /// 判断指定成员当前是否能领取任意宗门岗位。
    /// </summary>
    [Hotfixable]
    public static bool HasAssignableJob(Actor actor)
    {
        if (!CanUseSectJobSystem(actor, out Sect sect)) return false;
        if (!sect.jobs.HasAnyTask()) return false;

        return FindAssignableJob(actor, sect) != null;
    }

    /// <summary>
    /// 给成员领取一个当前可用的宗门岗位，并切换到岗位对应的 ActorJob。
    /// </summary>
    [Hotfixable]
    public static bool TryAssign(Actor actor, out SectJobAsset assignedJob)
    {
        assignedJob = null;
        if (!CanUseSectJobSystem(actor, out Sect sect)) return false;

        assignedJob = FindAssignableJob(actor, sect);
        if (assignedJob == null) return false;

        sect.jobs.TakeJob(assignedJob);
        actor.SetSectJob(sect, assignedJob);
        actor.ai.setJob(assignedJob.actorJobId);
        SectVerifyLog.Log("SectJobAssign", $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(actor)} job={assignedJob.id} occupied={sect.jobs.CountOccupied(assignedJob)}/{sect.jobs.CountCurrentJobs(assignedJob)}");
        return true;
    }

    /// <summary>
    /// 释放成员当前占用的宗门岗位。
    /// </summary>
    [Hotfixable]
    public static void Release(Actor actor)
    {
        if (actor == null || actor.isRekt()) return;

        SectJobAsset job = actor.GetSectJob();
        if (job == null) return;

        Sect sect = GetActorJobSect(actor);
        sect?.jobs.FreeJob(job);
        actor.ClearSectJob();
    }

    private static bool CanUseSectJobSystem(Actor actor, out Sect sect)
    {
        sect = null;
        if (actor == null || actor.isRekt()) return false;
        if (!actor.isSapient()) return false;
        if (!actor.canWork()) return false;

        sect = actor.GetExtend().sect;
        return sect != null && !sect.isRekt();
    }

    private static SectJobAsset FindAssignableJob(Actor actor, Sect sect)
    {
        SectJobAsset selected = null;
        foreach (KeyValuePair<SectJobAsset, int> pair in sect.jobs.jobs)
        {
            SectJobAsset job = pair.Key;
            if (!CanAssignJob(actor, sect, job)) continue;
            if (selected == null || job.priority > selected.priority) selected = job;
        }

        return selected;
    }

    private static bool CanAssignJob(Actor actor, Sect sect, SectJobAsset job)
    {
        if (job == null || string.IsNullOrEmpty(job.actorJobId)) return false;
        if (!sect.jobs.HasJob(job)) return false;
        if (job.requiredPermission != null && !actor.HasSectPermission(job.requiredPermission)) return false;
        return job.shouldBeAssigned == null || job.shouldBeAssigned(actor, sect);
    }

    private static void RefreshOccupied(Sect sect)
    {
        sect.jobs.ClearOccupied();
        List<Actor> members = sect.GetLivingMembers();
        for (int i = 0; i < members.Count; i++)
        {
            Actor actor = members[i];
            SectJobAsset job = actor.GetSectJob();
            if (job == null) continue;

            bool stillUsingJob = actor.GetSectJobSectId() == sect.getID()
                                 && actor.ai?.job?.id == job.actorJobId;
            if (!stillUsingJob)
            {
                actor.ClearSectJob();
                continue;
            }

            sect.jobs.TakeJob(job);
        }
    }

    private static Sect GetActorJobSect(Actor actor)
    {
        long sectId = actor.GetSectJobSectId();
        Sect sect = sectId >= 0 ? WorldboxGame.I?.Sects?.get(sectId) : null;
        return sect != null && !sect.isRekt() ? sect : actor.GetExtend().sect;
    }
}
