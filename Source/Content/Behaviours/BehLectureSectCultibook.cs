using System.Collections.Generic;
using ai.behaviours;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Debug;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 长老或掌门讲法，将自身掌握的功法传给同宗成员。
/// </summary>
public class BehLectureSectCultibook : BehaviourActionActor
{
    /// <summary>
    /// 完成一次宗门讲法，使缺少该功法的听众获得了解度，并给讲法者发放贡献。
    /// </summary>
    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        SectAffairAsset affair = SectAffairs.LectureCultibook;
        if (!SectAffairRules.CanDoSectAffair(pObject, affair))
        {
            SectVerifyLog.Log("SectLectureTask", $"actor={SectVerifyLog.Actor(pObject)} result=false");
            return BehResult.Stop;
        }

        Sect sect = pObject.GetExtend().sect;
        if (!SectLectureRules.TryPickLecture(pObject, sect, out CultibookAsset cultibook, out List<Actor> audience))
        {
            SectVerifyLog.Log("SectLectureTask", $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(pObject)} result=false reason=no_target");
            return BehResult.Stop;
        }

        int taughtCount = SectLectureRules.ApplyLecture(pObject, sect, cultibook, audience);
        if (taughtCount <= 0)
        {
            SectVerifyLog.Log("SectLectureTask", $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(pObject)} cultibook={cultibook.id} result=false reason=no_effect");
            return BehResult.Stop;
        }

        bool result = sect.AddContribution(pObject, affair.contributionReward);
        SectVerifyLog.Log("SectLectureTask", $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(pObject)} cultibook={cultibook.id} audience={taughtCount} contribution={affair.contributionReward} result={result}");
        return result ? BehResult.Continue : BehResult.Stop;
    }
}
