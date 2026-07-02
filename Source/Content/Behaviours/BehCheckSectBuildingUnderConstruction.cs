using ai.behaviours;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 检查行为目标仍然是当前宗门的在建建筑。
/// </summary>
public class BehCheckSectBuildingUnderConstruction : BehaviourActionActor
{
    public override void setupErrorChecks()
    {
        base.setupErrorChecks();
        null_check_building_target = true;
    }

    /// <summary>
    /// 若工地已经完成、销毁或不属于当前宗门，则停止任务。
    /// </summary>
    [Hotfixable]
    public override BehResult execute(Actor pActor)
    {
        Sect sect = pActor.GetExtend().sect;
        return SectConstructionRules.IsCurrentConstruction(sect, pActor.beh_building_target)
            ? BehResult.Continue
            : BehResult.Stop;
    }
}
