using System.Collections.Generic;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Skills;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.Systems.Logic;

/// <summary>推进实际借出的剑形法宝，在人物接触前确认位姿与屏障。</summary>
internal sealed class ArtifactBladeFlightSystem : QuerySystem<SkillExecution, SkillContext>
{
    /// <summary>退出实体查询后推进，收势和屏障结算可以安全结束会话。</summary>
    private readonly List<Entity> flights = new();

    /// <summary>同一轮先收集真实接触，结束几何采集后再结算伤害和反击。</summary>
    private readonly WorldObjectContactSweep sweep = new();
    private readonly List<BaseSimObject> targets = new();
    private readonly List<(Entity Blade, BaseSimObject Target, bool Projection)> contacts = new();

    /// <summary>只推进实际开始且尚未进入回收的飞行。</summary>
    internal ArtifactBladeFlightSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());
        Filter.AnyComponents(ComponentTypes.Get<ArtifactSpatialAttackMotion, ArtifactSwordArrayExecutionState>());
    }

    /// <summary>每轮只推进一次，在普通碰撞之前写入已经确认的原件位置。</summary>
    protected override void OnUpdate()
    {
        flights.Clear();
        Query.ForEachEntity((ref SkillExecution _, ref SkillContext __, Entity flight) =>
            flights.Add(flight));

        contacts.Clear();
        sweep.Begin();
        foreach (Entity flight in flights)
        {
            if (flight.IsNull) continue;
            if (flight.HasComponent<ArtifactSwordArrayExecutionState>())
            {
                if (ArtifactSwordArrayExecution.Advance(flight, Tick.deltaTime))
                    foreach (ArtifactSwordArrayBladeState bladeState in flight.GetComponent<ArtifactSwordArrayExecutionState>().blades)
                        Collect(bladeState.entity, projection: true);
                continue;
            }

            if (!ArtifactSpatialAttackExecution.Advance(flight, Tick.deltaTime, out Entity blade)) continue;
            if (ConstructedAbilityService.InterceptFlyingBody(flight)) continue;
            blade.GetComponent<Position>() = flight.GetComponent<Position>();
            blade.GetComponent<Rotation>() = flight.GetComponent<Rotation>();
            Collect(flight, projection: false);
        }

        foreach (var contact in contacts)
        {
            if (contact.Projection) ArtifactSwordArrayExecution.ConfirmContact(contact.Blade, contact.Target);
            else ArtifactSpatialAttackExecution.ConfirmContact(contact.Blade, contact.Target);
        }
    }

    /// <summary>先记录当轮真正相交的对象，再统一处理相应的命中规则。</summary>
    /// <param name="blade">原件或阵内的剑影。</param>
    /// <param name="projection">是否由阵体生成并控制。</param>
    private void Collect(Entity blade, bool projection)
    {
        if (blade.IsNull || blade.Tags.Has<TagPrefab>() || blade.Tags.Has<TagRecycle>()) return;
        sweep.Collect(blade, targets);
        foreach (BaseSimObject target in targets) contacts.Add((blade, target, projection));
    }
}
