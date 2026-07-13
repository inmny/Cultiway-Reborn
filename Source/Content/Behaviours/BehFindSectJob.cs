using ai.behaviours;
using Cultiway.Content.Extensions;
using Cultiway.Content.Sects;
using Cultiway.Core.Libraries;
using Cultiway.Debug;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 给成员分配一个当前可用的宗门岗位。
/// </summary>
public class BehFindSectJob : BehaviourActionActor
{
    /// <summary>
    /// 尝试领取宗门岗位；成功后会切换到岗位对应的 ActorJob。
    /// </summary>
    [Hotfixable]
    public override BehResult execute(Actor pActor)
    {
        if (SectJobService.TryAssign(pActor, out SectJobAsset job))
        {
            SectVerifyLog.Log("SectFindJob", $"actor={SectVerifyLog.Actor(pActor)} job={job.id} result=true");
            return BehResult.Continue;
        }

        SectVerifyLog.Log("SectFindJob", $"actor={SectVerifyLog.Actor(pActor)} result=false");
        return BehResult.Stop;
    }
}
