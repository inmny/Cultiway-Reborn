using System.Linq;
using ai.behaviours;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours.Masters;

/// <summary>
/// AI行为：向弟子传授知识
/// </summary>
public class BehTeachApprentice : BehaviourActionActor
{
    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        
        // 检查是否有弟子
        var apprentices = ae.GetApprentices();
        if (apprentices.Count == 0)
        {
            return BehResult.Stop;
        }
        
        // 检查传授意愿
        if (ae.TryGetComponent(out MasterApprenticeState state))
        {
            if (state.TeachWillingness < 30 && !Randy.randomChance(state.TeachWillingness / 100f))
            {
                return BehResult.Stop;
            }
        }
        
        // 选择需要传授的弟子
        var apprentice = GetApprenticeNeedTeaching(ae, apprentices);
        if (apprentice == null)
        {
            return BehResult.Stop;
        }
        
        // 移动到弟子附近
        if (!pObject.isInAttackRange(apprentice.Base))
        {
            pObject.goTo(apprentice.Base.current_tile);
            return BehResult.RepeatStep;
        }
        
        // 选择传授内容
        var content = SelectTeachContent(ae, apprentice);
        if (content == null)
        {
            return BehResult.Stop;
        }
        
        // 执行传授
        bool success = ExecuteTeaching(ae, apprentice, content);
        
        if (success)
        {
            // 传授成功，增加亲密度
            apprentice.AddIntimacy(2f);
            return BehResult.Continue;
        }
        
        return BehResult.Stop;
    }
    
    /// <summary>
    /// 获取需要传授的弟子
    /// </summary>
    private ActorExtend GetApprenticeNeedTeaching(ActorExtend master, System.Collections.Generic.List<ActorExtend> apprentices)
    {
        // 优先选择亲密度较低的弟子
        return apprentices
            .OrderBy(a => a.GetIntimacy())
            .FirstOrDefault();
    }
    
    /// <summary>
    /// 选择传授内容
    /// </summary>
    private object SelectTeachContent(ActorExtend master, ActorExtend apprentice)
    {
        // 优先传授主修功法
        var mainCultibook = master.GetMainCultibook();
        if (mainCultibook != null)
        {
            var apprenticeMastery = apprentice.GetMaster(mainCultibook);
            var masterMastery = master.GetMainCultibookMastery();
            
            // 如果弟子还没有学或者掌握度低于师傅，可以传授
            if (apprenticeMastery <= 0 || apprenticeMastery < masterMastery * 0.8f)
            {
                return mainCultibook;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 执行传授
    /// </summary>
    private bool ExecuteTeaching(ActorExtend master, ActorExtend apprentice, object content)
    {
        if (content is CultibookAsset cultibook)
        {
            return master.TeachCultibook(apprentice, cultibook);
        }
        
        return false;
    }
}

