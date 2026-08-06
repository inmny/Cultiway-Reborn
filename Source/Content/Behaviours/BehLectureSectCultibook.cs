using System.Collections.Generic;
using ai.behaviours;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Content.Sects;
using Cultiway.Core;
using Cultiway.Core.Coordination;
using Cultiway.Core.Libraries;
using Cultiway.Debug;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 长老或掌门发起一次需要真实到场的宗门讲法协调行动。
/// </summary>
public class BehLectureSectCultibook : BehaviourActionActor
{
    /// <summary>
    /// 选定讲法内容与候选听众，并让当前角色占用讲师席位。
    /// </summary>
    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        SectAffairAsset affair = SectAffairs.LectureCultibook;
        if (!SectAffairExecutionPolicy.CanExecute(pObject, affair))
        {
            SectVerifyLog.Log("SectLectureTask", $"actor={SectVerifyLog.Actor(pObject)} result=false");
            return BehResult.Stop;
        }

        Sect sect = pObject.GetExtend().sect;
        if (!SectLectureService.TryPickLecture(pObject, sect, out CultibookAsset cultibook, out List<Actor> audience))
        {
            SectVerifyLog.Log("SectLectureTask", $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(pObject)} result=false reason=no_target");
            return BehResult.Stop;
        }

        if (pObject.beh_tile_target == null)
        {
            SectVerifyLog.Log("SectLectureTask", $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(pObject)} result=false reason=no_target_tile");
            return BehResult.Stop;
        }

        var group = new CoordinationGroupKey(SectCoordinationGroupProvider.ProviderId, sect.id);
        var session = new SectLectureSession(
            sect,
            pObject,
            cultibook,
            audience,
            pObject.beh_tile_target);
        bool started = CoordinatedActivityService.TryStart(
            CoordinationActivities.SectLecture,
            group,
            session,
            [new CoordinationInitialParticipant(pObject, SectLectureSession.LecturerRoleId)],
            out long activityId);
        SectVerifyLog.Log(
            "SectLectureTask",
            $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(pObject)} cultibook={cultibook.id} candidates={audience.Count} activity={activityId} result={started}");
        return started ? BehResult.Continue : BehResult.Stop;
    }
}
