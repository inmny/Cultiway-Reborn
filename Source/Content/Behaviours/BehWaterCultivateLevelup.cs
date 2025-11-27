using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 水中修炼突破境界行为
/// </summary>
public class BehWaterCultivateLevelup : BehaviourActionActor
{
    public override BehResult execute(Actor pObject)
    {
        // 如果不在水中，先移动到水域
        if (!pObject.current_tile.IsWater())
        {
            return BehResult.Stop; // 由寻找水域行为处理
        }

        ActorExtend actor_extend = pObject.GetExtend();
        ref Xian xian = ref actor_extend.GetCultisys<Xian>();

        // 获取修炼收益
        Cultisyses.TakeWakanAndCultivate(actor_extend, ref xian);

        if (Cultisyses.Xian.AllowUpgrade(actor_extend))
        {
            pObject.changeHappiness(HappinessAssets.LevelUp.id);
            Cultisyses.Xian.TryPerformUpgrade(actor_extend);
            return BehResult.Continue;
        }

        return !Cultisyses.Xian.PreCheckUpgrade(actor_extend) ? BehResult.Continue : BehResult.RepeatStep;
    }
}

