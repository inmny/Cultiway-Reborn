using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// 宗门岗位资产集合。
/// </summary>
[Dependency(typeof(SectPermissions), typeof(ActorJobs))]
public class SectJobs : ExtendLibrary<SectJobAsset, SectJobs>
{
    /// <summary>
    /// 宗门建筑修建岗位。
    /// </summary>
    public static SectJobAsset Builder { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.Sect.Job";

    protected override void OnInit()
    {
        Builder.nameKey = Builder.id;
        Builder.descriptionKey = $"{Builder.id}.Info";
        Builder.pathIcon = "ui/Icons/citizen_jobs/iconCitizenJobBuilder";
        Builder.actorJobId = ActorJobs.SectConstruction.id;
        Builder.requiredPermission = SectPermissions.BuildBuilding;
        Builder.priority = 9f;
        Builder.countJobs = CountBuilderJobs;
        Builder.shouldBeAssigned = SectConstructionRules.CanUseBuilderJob;
    }

    private static int CountBuilderJobs(Sect sect)
    {
        if (sect == null || sect.isRekt() || !sect.HasBuildingToBuild()) return 0;

        int memberBasedSlots = Mathf.Max(1, sect.GetLivingMembers().Count / SectConst.SectConstructionMembersPerBuilder);
        return Mathf.Min(SectConst.SectConstructionMaxBuilders, memberBasedSlots);
    }
}
