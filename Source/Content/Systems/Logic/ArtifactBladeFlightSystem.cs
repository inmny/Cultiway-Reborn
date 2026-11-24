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
internal sealed class ArtifactBladeFlightSystem : QuerySystem<ArtifactSpatialAttackMotion, SkillContext>
{
    /// <summary>退出实体查询后推进，收势和屏障结算可以安全结束会话。</summary>
    private readonly List<Entity> flights = new();

    /// <summary>同一轮先收集真实接触，结束几何采集后再结算伤害和反击。</summary>
    private readonly WorldObjectContactSweep sweep = new();
    private readonly List<BaseSimObject> targets = new();
    private readonly List<(Entity Flight, BaseSimObject Target)> contacts = new();

    /// <summary>只推进实际开始且尚未进入回收的飞行。</summary>
    internal ArtifactBladeFlightSystem() =>
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());

    /// <summary>每轮只推进一次，在普通碰撞之前写入已经确认的原件位置。</summary>
    protected override void OnUpdate()
    {
        flights.Clear();
        Query.ForEachEntity((ref ArtifactSpatialAttackMotion _, ref SkillContext __, Entity flight) =>
            flights.Add(flight));

        contacts.Clear();
        sweep.Begin();
        foreach (Entity flight in flights)
        {
            if (!ArtifactSpatialAttackExecution.Advance(flight, Tick.deltaTime, out Entity blade)) continue;
            if (ConstructedAbilityService.InterceptFlyingBody(flight)) continue;
            blade.GetComponent<Position>() = flight.GetComponent<Position>();
            blade.GetComponent<Rotation>() = flight.GetComponent<Rotation>();
            sweep.Collect(flight, targets);
            foreach (BaseSimObject target in targets) contacts.Add((flight, target));
        }

        foreach (var contact in contacts)
            ArtifactSpatialAttackExecution.ConfirmContact(contact.Flight, contact.Target);
    }
}
