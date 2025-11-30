using ai.behaviours;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours.Apprentices;

/// <summary>
/// AI行为：跟随师傅修炼
/// </summary>
public class BehFollowMaster : BehaviourActionActor
{
    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        var master = ae.GetMaster();
        
        if (master.isRekt())
        {
            return BehResult.Stop;
        }
        var master_ae = master.GetExtend();
        
        // 检查师傅是否在修炼
        if (!IsMasterCultivating(master_ae))
        {
            return BehResult.Stop;
        }
        
        // 跟随师傅
        var distance = Toolbox.DistTile(pObject.current_tile, master.current_tile);
        if (distance > 3)
        {
            pObject.goTo(master.current_tile);
            return BehResult.RepeatStep;
        }
        
        // 跟随修炼（效率提升）
        FollowCultivate(ae, master_ae);
        
        // 增加亲密度（每天+0.1，这里简化处理）
        if (Randy.randomChance(0.01f)) // 约1%概率增加亲密度（模拟每天）
        {
            ae.AddIntimacy(0.1f);
        }
        
        return BehResult.Continue;
    }
    
    /// <summary>
    /// 检查师傅是否在修炼
    /// </summary>
    private bool IsMasterCultivating(ActorExtend master)
    {
        // 检查师傅是否在进行修炼相关的行为
        // 这里简化处理，检查师傅是否有修仙状态
        if (!master.HasCultisys<Xian>()) return false;
        
        // 可以扩展为检查师傅的当前任务类型
        return true;
    }
    
    /// <summary>
    /// 跟随修炼
    /// </summary>
    private void FollowCultivate(ActorExtend apprentice, ActorExtend master)
    {
        if (!apprentice.HasCultisys<Xian>()) return;
        
        ref var xian = ref apprentice.GetCultisys<Xian>();
        
        // 跟随师傅修炼，效率提升30%
        // 这里简化处理，直接调用修炼方法
        // TODO: 调用实际的修炼方法，并应用效率加成
        apprentice.Base.data.get(ContentActorDataKeys.CultivateTime_float, out var time, -1f);
        if (time <= 0)
        {
            // 跟随修炼效果
            // 实际应该在修炼系统中实现
        }
    }
}

