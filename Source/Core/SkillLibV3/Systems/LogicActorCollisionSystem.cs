using System.Collections.Generic;
using Cultiway.Content.Skills;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Components.TrajParams;
using Cultiway.Core.SkillLibV3.Impacts;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV3.Systems;

public class LogicActorCollisionSystem : QuerySystem<SkillContext, SkillEntity, ColliderSphere, ColliderConfig, Position>
{
    /// <summary>本轮待确认的命中，退出实体扫描后才调用原版。</summary>
    private readonly List<PendingHit> _pendingHits = new();
    /// <summary>兼容旧技能入口，实际范围由共用扫掠检查。</summary>
    private readonly WorldObjectContactSweep sweep = new();
    private readonly List<BaseSimObject> candidates = new();
    /// <summary>本轮已经被命中回调终止的技能。</summary>
    private readonly HashSet<int> _stoppedSkillIds = new();

    /// <summary>忽略没有运行或禁止碰撞的实例。</summary>
    public LogicActorCollisionSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle, TagSkillAnimationNoCollision>());
    }

    /// <summary>确认旧技能的实际范围，离开查询后执行回调。</summary>
    protected override void OnUpdate()
    {
        _pendingHits.Clear();
        sweep.Begin();
        Query.ForEachEntity((ref SkillContext _, ref SkillEntity __, ref ColliderSphere ___,
            ref ColliderConfig ____, ref Position pos, Entity entity) =>
        {
            if (entity.TryGetComponent(out SkillExecution execution) && execution.end_requested) return;
            if (entity.TryGetComponent(out MotionParams motion) && motion.HasHeightGate && pos.z > motion.HeightGateMax) return;
            sweep.Collect(entity, candidates);
            foreach (BaseSimObject target in candidates)
                _pendingHits.Add(new PendingHit(entity, target));
        });

        ResolvePendingHits();
    }

    /// <summary>
    /// 在 ECS 查询结束后执行命中回调，避免其触发的原版逻辑在查询锁内修改实体结构。
    /// </summary>
    private void ResolvePendingHits()
    {
        _stoppedSkillIds.Clear();
        for (int i = 0; i < _pendingHits.Count; i++)
        {
            PendingHit pending = _pendingHits[i];
            Entity entity = pending.SkillEntity;
            if (entity.IsNull || _stoppedSkillIds.Contains(entity.Id)) continue;
            if (entity.TryGetComponent(out SkillExecution execution) && execution.end_requested) continue;

            BaseSimObject target = pending.Target;
            if (target == null || target.isRekt()) continue;

            if (entity.HasComponent<SkillHitMemory>())
            {
                ref SkillHitMemory hitMemory = ref entity.GetComponent<SkillHitMemory>();
                long targetKey = GetTargetKey(target);
                SkillImpactProfileAsset profile = entity.GetComponent<SkillEntity>().Asset.ImpactProfile;
                if (profile != null && profile.RepeatHitInterval > 0f)
                {
                    float elapsed = entity.GetComponent<AliveTimer>().value;
                    if (hitMemory.NextHitTimes.TryGetValue(targetKey, out float nextHit) && elapsed < nextHit) continue;
                    hitMemory.NextHitTimes[targetKey] = elapsed + profile.RepeatHitInterval;
                }
                else if (!hitMemory.TargetIds.Add(targetKey))
                {
                    continue;
                }
            }

            ref SkillContext context = ref entity.GetComponent<SkillContext>();
            ref SkillEntity skillEntity = ref entity.GetComponent<SkillEntity>();
            bool continueAfterHit =
                skillEntity.Asset.OnObjCollision(ref context, skillEntity.SkillContainer, entity, target);
            if (continueAfterHit)
            {
                MarkExplicitTargetPassed(entity, ref context, target);
            }
            else
            {
                _stoppedSkillIds.Add(entity.Id);
            }
        }
    }

    private static void MarkExplicitTargetPassed(Entity entity, ref SkillContext context, BaseSimObject target)
    {
        if (context.TargetObj != target || !entity.HasComponent<TrajectoryRuntimeState>()) return;

        ref TrajectoryRuntimeState state = ref entity.GetComponent<TrajectoryRuntimeState>();
        state.TargetPhase = TrajectoryTargetPhase.PassedTarget;
        state.TargetExitDirection = entity.GetComponent<Rotation>().value;
    }

    private static long GetTargetKey(BaseSimObject target)
    {
        return unchecked((target.getID() << 1) | (target.isActor() ? 0L : 1L));
    }

    /// <summary>在实体遍历完成后再提交的实际命中。</summary>
    /// <param name="skillEntity">已受理技能。</param>
    /// <param name="target">相交目标。</param>
    private readonly struct PendingHit(Entity skillEntity, BaseSimObject target)
    {
        /// <summary>命中来源技能。</summary>
        public Entity SkillEntity { get; } = skillEntity;
        /// <summary>相交的实际目标。</summary>
        public BaseSimObject Target { get; } = target;
    }
}
