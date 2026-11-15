using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV3.Systems;

/// <summary>旧执行体完成运动后先查询新屏障，再进入所有落点和命中作用。</summary>
internal sealed class LogicSkillBarrierSystem : QuerySystem<SkillContext, Position, Trajectory>
{
    /// <summary>查询解锁后再回收，避免在遍历中改变实体所属集合。</summary>
    private readonly List<Entity> pending = new();

    /// <summary>只查询仍在实际活动的技能。</summary>
    internal LogicSkillBarrierSystem() => Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle, TagSkillAnimationNoCollision>());

    /// <summary>容量在主线程真实接触时消费，不写入新构造的临时预测。</summary>
    protected override void OnUpdate()
    {
        if (Manager.Barriers == null) return;
        pending.Clear();
        Query.ForEachEntity((ref SkillContext context, ref Position position, ref Trajectory trajectory, Entity entity) => pending.Add(entity));
        foreach (Entity entity in pending) Manager.Barriers.Intercept(entity);
    }
}
