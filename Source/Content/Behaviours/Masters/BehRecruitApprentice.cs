using System.Linq;
using ai.behaviours;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using strings;

namespace Cultiway.Content.Behaviours.Masters;

/// <summary>
/// AI行为：寻找并收取弟子
/// </summary>
public class BehRecruitApprentice : BehaviourActionActor
{
    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        
        // 检查是否可以收徒
        if (!ae.CanRecruit())
        {
            return BehResult.Stop;
        }
        
        // 检查收徒意愿
        if (ae.TryGetComponent(out MasterApprenticeState state))
        {
            if (state.RecruitWillingness < 30 && !Randy.randomChance(state.RecruitWillingness / 100f))
            {
                return BehResult.Stop;
            }
        }
        
        // 寻找合适的弟子候选
        var candidate = FindApprenticeCandidate(pObject);
        if (candidate == null)
        {
            return BehResult.Stop;
        }
        
        // 移动到候选者附近
        if (!pObject.isInAttackRange(candidate))
        {
            pObject.goTo(candidate.current_tile);
            return BehResult.RepeatStep;
        }
        
        // 尝试收徒
        var candidateAe = candidate.GetExtend();
        if (ae.TryRecruit(candidateAe, MasterApprenticeType.Nominal))
        {
            return BehResult.Continue;
        }
        
        return BehResult.Stop;
    }
    
    /// <summary>
    /// 寻找合适的弟子候选
    /// </summary>
    private Actor FindApprenticeCandidate(Actor master)
    {
        var masterAe = master.GetExtend();
        var allActors = World.world.units.units_only_alive;
        
        // 过滤条件：
        // 1. 没有师傅
        // 2. 境界低于师傅
        // 3. 有修仙状态
        // 4. 在附近（距离不超过一定范围）
        var candidates = allActors
            .Where(a => 
            {
                var ae = a.GetExtend();
                if (ae.HasMaster()) return false;
                if (!ae.HasCultisys<Xian>()) return false;
                
                // 检查境界
                ref var candidateXian = ref ae.GetCultisys<Xian>();
                ref var masterXian = ref masterAe.GetCultisys<Xian>();
                if (candidateXian.level >= masterXian.level) return false;
                
                // 检查距离（不超过50格）
                var distance = Toolbox.DistTile(master.current_tile, a.current_tile);
                if (distance > 50) return false;
                
                return true;
            })
            .OrderBy(a => Toolbox.DistTile(master.current_tile, a.current_tile))
            .ToList();
        
        if (candidates.Count == 0) return null;
        
        // 选择最近的候选者
        return candidates.First();
    }
}

