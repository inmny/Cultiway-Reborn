using ai.behaviours;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours.Apprentices;

/// <summary>
/// AI行为：寻找师傅
/// </summary>
public class BehSeekMaster : BehaviourActionActor
{
    private const int MaxSearchDistanceSquared = 100 * 100;

    private static ArchetypeQuery<Xian, ActorBinder> candidateQuery;

    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        
        // 检查是否需要师傅
        if (ae.HasMaster())
        {
            return BehResult.Stop;
        }
        
        // 检查是否有修仙状态
        if (!ae.HasCultisys<Xian>())
        {
            return BehResult.Stop;
        }
        
        ref Xian apprenticeXian = ref ae.GetCultisys<Xian>();
        Actor potentialMaster = GetCachedPotentialMaster(
            pObject,
            ae,
            apprenticeXian.CurrLevel);
        if (potentialMaster == null)
        {
            potentialMaster = FindPotentialMaster(
                pObject,
                ae,
                apprenticeXian.CurrLevel);
            pObject.beh_actor_target = potentialMaster;
        }

        if (potentialMaster == null)
        {
            return BehResult.Stop;
        }
        
        // 前往师傅
        if (!pObject.isInAttackRange(potentialMaster))
        {
            pObject.goTo(potentialMaster.current_tile);
            return BehResult.RepeatStep;
        }
        
        // 尝试拜师
        var masterAe = potentialMaster.GetExtend();
        bool success = TryBeApprentice(ae, masterAe);
        
        if (success)
        {
            return BehResult.Continue;
        }
        
        return BehResult.Stop;
    }
    
    /// <summary>
    /// 寻找潜在的师傅
    /// </summary>
    private static Actor FindPotentialMaster(
        Actor apprentice,
        ActorExtend apprenticeAe,
        int apprenticeLevel)
    {
        WorldTile apprenticeTile = apprentice.current_tile;
        Actor closestMaster = null;
        int closestDistanceSquared = MaxSearchDistanceSquared + 1;

        GetCandidateQuery().ForEachEntity(
            (ref Xian masterXian, ref ActorBinder binder, Entity _) =>
            {
                if (masterXian.CurrLevel <= apprenticeLevel)
                {
                    return;
                }

                Actor master = binder.Actor;
                if (master == null ||
                    master == apprentice ||
                    master.isRekt())
                {
                    return;
                }

                int distanceSquared = Toolbox.SquaredDistTile(
                    apprenticeTile,
                    master.current_tile);
                if (distanceSquared >= closestDistanceSquared)
                {
                    return;
                }

                ActorExtend masterAe = binder.AE;
                if (!masterAe.CanRecruit(apprenticeAe))
                {
                    return;
                }

                closestMaster = master;
                closestDistanceSquared = distanceSquared;
            });

        return closestMaster;
    }

    private static Actor GetCachedPotentialMaster(
        Actor apprentice,
        ActorExtend apprenticeAe,
        int apprenticeLevel)
    {
        Actor master = apprentice.beh_actor_target?.a;
        if (master == null ||
            master == apprentice ||
            master.isRekt())
        {
            apprentice.beh_actor_target = null;
            return null;
        }

        int distanceSquared = Toolbox.SquaredDistTile(
            apprentice.current_tile,
            master.current_tile);
        if (distanceSquared > MaxSearchDistanceSquared)
        {
            apprentice.beh_actor_target = null;
            return null;
        }

        ActorExtend masterAe = master.GetExtend();
        if (!masterAe.HasCultisys<Xian>() ||
            masterAe.GetCultisys<Xian>().CurrLevel <= apprenticeLevel ||
            !masterAe.CanRecruit(apprenticeAe))
        {
            apprentice.beh_actor_target = null;
            return null;
        }

        return master;
    }

    private static ArchetypeQuery<Xian, ActorBinder> GetCandidateQuery()
    {
        if (candidateQuery != null)
        {
            return candidateQuery;
        }

        var filter = new QueryFilter();
        filter.WithoutAnyTags(Tags.Get<TagRecycle>());
        candidateQuery = ModClass.I.W.Query<Xian, ActorBinder>(filter);
        candidateQuery.FreezeFilter();
        return candidateQuery;
    }
    
    /// <summary>
    /// 尝试拜师
    /// </summary>
    private bool TryBeApprentice(ActorExtend apprentice, ActorExtend master)
    {
        // 请求拜师（由师傅决定是否接受）
        // 这里简化处理，直接尝试收徒
        return master.TryRecruit(apprentice);
    }
}

