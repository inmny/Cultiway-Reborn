using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.Extensions;
using Cultiway.Content.Sects;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Debug;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 执行宗门杂务，为普通弟子提供少量宗门贡献。
/// </summary>
public class BehDoSectChore : BehaviourActionActor
{
    /// <summary>
    /// 处理一次宗门杂务并给当前宗门增加单位贡献。
    /// </summary>
    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        SectAffairAsset affair = SectAffairs.Chore;
        if (!SectAffairExecutionPolicy.CanExecute(pObject, affair))
        {
            SectVerifyLog.Log("SectChoreTask", $"actor={SectVerifyLog.Actor(pObject)} result=false");
            return BehResult.Stop;
        }

        Sect sect = pObject.GetExtend().sect;
        int contribution = SectTraitRules.GetAffairContributionReward(sect, affair);
        bool result = sect.AddContribution(pObject, contribution);
        SectVerifyLog.Log("SectChoreTask", $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(pObject)} contribution={contribution} result={result}");
        return result ? BehResult.Continue : BehResult.Stop;
    }
}
