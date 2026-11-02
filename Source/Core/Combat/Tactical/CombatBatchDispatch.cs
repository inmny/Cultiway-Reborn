using System.Collections.Generic;

namespace Cultiway.Core.Combat.Tactical;

/// <summary>在游戏已有批量任务中接入战斗，避免短人物方法的调用绕过接管；每批只绑定一次。</summary>
internal static class CombatBatchDispatch
{
    /// <summary>只记录当前世界中已经处理的批次。</summary>
    private static readonly HashSet<BatchActors> bound = new();

    /// <summary>沿用已有任务、容器和调度间隔，不另外遍历全世界人物。</summary>
    /// <param name="batches">游戏当前的活动批次。</param>
    internal static void Bind(List<BatchActors> batches)
    {
        foreach (BatchActors batch in batches)
        {
            if (!bound.Add(batch)) continue;
            foreach (Job<Actor> job in batch.jobs_post)
            {
                if (job.id == "b2_checkCurrentEnemyTarget")
                    job.job_updater = () => CheckTargets(batch, job.container);
                else if (job.id == "b3_findEnemyTarget")
                    job.job_updater = () => FindTargets(batch, job.container);
            }
        }
    }

    /// <summary>保持原版容器刷新和暂停条件，再逐项调用共同执行判断。</summary>
    /// <param name="batch">当前人物批次。</param><param name="container">原任务使用的容器。</param>
    private static void CheckTargets(BatchActors batch, ObjectContainer<Actor> container)
    {
        if (!batch.check(container) || World.world.isPaused()) return;
        Actor[] actors = batch._array;
        int count = batch._count;
        for (int i = 0; i < count; i++)
            if (!CombatWorldService.TryUpdateEnemyTarget(actors[i], batch._elapsed))
                actors[i].b2_checkCurrentEnemyTarget(batch._elapsed);
    }

    /// <summary>普通批量路径直接准备共同计划；不接管的人物继续调用原入口。</summary>
    /// <param name="batch">当前人物批次。</param><param name="container">原任务使用的容器。</param>
    private static void FindTargets(BatchActors batch, ObjectContainer<Actor> container)
    {
        if (!batch.check(container) || World.world.isPaused()) return;
        Actor[] actors = batch._array;
        int count = batch._count;
        for (int i = 0; i < count; i++)
            if (!CombatWorldService.TryUpdateEnemySearch(actors[i]))
                actors[i].b3_findEnemyTarget(batch._elapsed);
    }

    /// <summary>结束世界时释放批次引用，新世界按自己的任务重新绑定。</summary>
    internal static void Clear() => bound.Clear();
}