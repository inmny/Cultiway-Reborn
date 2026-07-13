using ai.behaviours;
using Cultiway.Content.Extensions;
using Cultiway.Content.Sects;
using Cultiway.Debug;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 为宗门杂务选择一个可到达的宗门驻地地块。
/// </summary>
public class BehFindSectChoreTile : BehFindSectResidenceTile
{
    public BehFindSectChoreTile() : base("SectChoreTarget")
    {
    }

    /// <summary>
    /// 权限通过后复用宗门驻地选点逻辑。
    /// </summary>
    [Hotfixable]
    public override BehResult execute(Actor pActor)
    {
        if (!SectAffairExecutionPolicy.CanExecute(pActor, SectAffairs.Chore))
        {
            SectVerifyLog.Log("SectChoreTarget", $"actor={SectVerifyLog.Actor(pActor)} result=false reason=no_permission");
            return BehResult.Stop;
        }

        return base.execute(pActor);
    }
}
