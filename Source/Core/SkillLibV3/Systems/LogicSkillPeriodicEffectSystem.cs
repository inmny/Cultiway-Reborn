using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Effects;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV3.Systems;

/// <summary>在查询解锁后结算持久技能的周期对象和地块效果。</summary>
public sealed class LogicSkillPeriodicEffectSystem : QuerySystem<SkillPeriodicEffectState, AliveTimer>
{
    private readonly List<PendingResolution> pending = new();

    public LogicSkillPeriodicEffectSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());
    }

    protected override void OnUpdate()
    {
        pending.Clear();
        Query.ForEachEntity((ref SkillPeriodicEffectState state, ref AliveTimer timer, Entity entity) =>
        {
            if (entity.TryGetComponent(out SkillAnimationLifecycleState lifecycle) &&
                lifecycle.Phase != SkillAnimationPhase.Runtime) return;
            if (timer.value < state.NextTick) return;
            float interval = state.Interval > 0f ? state.Interval : 1f;
            do state.NextTick += interval;
            while (state.NextTick <= timer.value);
            pending.Add(new PendingResolution(entity, state.LastResolvedTime, timer.value));
            state.LastResolvedTime = timer.value;
        });
        for (int i = 0; i < pending.Count; i++)
        {
            PendingResolution resolution = pending[i];
            if (!resolution.Entity.IsNull)
                SkillEffectResolver.ResolvePeriodic(
                    resolution.Entity,
                    resolution.PreviousTime,
                    resolution.CurrentTime);
        }
    }

    /// <summary>保存查询解锁后一次周期结算所需的实体与时间边界。</summary>
    private readonly struct PendingResolution
    {
        public readonly Entity Entity;
        public readonly float PreviousTime;
        public readonly float CurrentTime;

        public PendingResolution(Entity entity, float previousTime, float currentTime)
        {
            Entity = entity;
            PreviousTime = previousTime;
            CurrentTime = currentTime;
        }
    }
}
