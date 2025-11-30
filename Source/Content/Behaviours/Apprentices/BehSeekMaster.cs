using System.Linq;
using ai.behaviours;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using strings;

namespace Cultiway.Content.Behaviours.Apprentices;

/// <summary>
/// AI行为：寻找师傅
/// </summary>
public class BehSeekMaster : BehaviourActionActor
{
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
        
        // 寻找合适的师傅
        var potentialMaster = FindPotentialMaster(pObject);
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
    private Actor FindPotentialMaster(Actor apprentice)
    {
        var apprenticeAe = apprentice.GetExtend();
        var apprenticeXian = apprenticeAe.GetCultisys<Xian>();
        var allActors = World.world.units.units_only_alive;
        
        // 过滤条件：
        // 1. 境界高于自己
        // 2. 有修仙状态
        // 3. 可以收徒
        // 4. 在附近（距离不超过一定范围）
        var candidates = allActors
            .Where(a =>
            {
                var ae = a.GetExtend();
                if (!ae.HasCultisys<Xian>()) return false;
                if (!ae.CanRecruit()) return false;
                
                // 检查境界
                ref var masterXian = ref ae.GetCultisys<Xian>();
                if (masterXian.CurrLevel <= apprenticeXian.CurrLevel) return false;
                
                // 检查距离（不超过100格）
                var distance = Toolbox.DistTile(apprentice.current_tile, a.current_tile);
                if (distance > 100) return false;
                
                return true;
            })
            .OrderBy(a => Toolbox.DistTile(apprentice.current_tile, a.current_tile))
            .ToList();
        
        if (candidates.Count == 0) return null;
        
        // 选择最近的候选者
        return candidates.First();
    }
    
    /// <summary>
    /// 尝试拜师
    /// </summary>
    private bool TryBeApprentice(ActorExtend apprentice, ActorExtend master)
    {
        // 请求拜师（由师傅决定是否接受）
        // 这里简化处理，直接尝试收徒
        return master.TryRecruit(apprentice, MasterApprenticeType.Nominal);
    }
}

