using System;
using System.Collections.Generic;
using Cultiway.Content.CreatureCompositions.Combat;
using Cultiway.Content.CreatureCompositions.Components;
using Cultiway.Content.CreatureCompositions.Services;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.Systems.Logic;

/// <summary>低频推进器官的生命过程效果，例如脱离战斗后的再生。</summary>
public sealed class CreatureOrganUpkeepSystem : QuerySystem<ActorBinder, CreaturePhenotype>
{
    private const int ActorsPerBatch = 16;
    private const float Interval = 2f;

    private readonly List<Actor> eligible = new();
    private int nextActor;
    private float timer;

    /// <summary>只统计未回收的活动实体。</summary>
    public CreatureOrganUpkeepSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagUncompleted, TagRecycle>());
    }

    /// <summary>攒够间隔后每批只处理有限数量的当前身体，避免随世界人口增长。</summary>
    protected override void OnUpdate()
    {
        timer -= Tick.deltaTime;
        if (timer > 0f) return;
        timer = Interval;

        eligible.Clear();
        Query.ForEachEntity((ref ActorBinder binder, ref CreaturePhenotype phenotype, Entity entity) =>
        {
            Actor actor = binder.Actor;
            if (actor == null || actor.isRekt() || !actor.isAlive() || !phenotype.IsValid ||
                !CreaturePhenotypeCompiler.TryGetCompiled(phenotype.CompiledIndex, phenotype.Signature, out var body)) return;
            foreach (var effect in body.PassiveEffects)
            {
                if (!CreatureOrganEffectFamilies.TryGet(effect.EffectFamilyId, out var family) ||
                    (family.Events & CreatureOrganEventMask.Upkeep) == 0) continue;
                eligible.Add(actor);
                break;
            }
        });

        if (eligible.Count == 0)
        {
            nextActor = 0;
            return;
        }

        nextActor %= eligible.Count;
        int count = Math.Min(ActorsPerBatch, eligible.Count);
        for (int i = 0; i < count; i++)
        {
            CreatureOrganEffectDispatcher.DispatchUpkeep(eligible[nextActor].GetExtend());
            nextActor = (nextActor + 1) % eligible.Count;
        }
    }
}
