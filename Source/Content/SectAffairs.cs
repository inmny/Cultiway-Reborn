using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Extensions;
using Cultiway.Content.Sects;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using NeoModLoader.api.attributes;

namespace Cultiway.Content;

/// <summary>
/// 宗门事务资产集合，集中配置宗门日常事务的职位、权限、任务和奖励。
/// </summary>
[Dependency(typeof(SectPermissions), typeof(SectRoles), typeof(ActorTasks))]
public class SectAffairs : ExtendLibrary<SectAffairAsset, SectAffairs>
{
    /// <summary>
    /// 普通弟子处理宗门杂务。
    /// </summary>
    public static SectAffairAsset Chore { get; private set; }

    /// <summary>
    /// 执事整理宗门藏经阁。
    /// </summary>
    public static SectAffairAsset OrganizeScripture { get; private set; }

    /// <summary>
    /// 长老或掌门为宗门弟子讲法。
    /// </summary>
    public static SectAffairAsset LectureCultibook { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.Sect.Affair";

    protected override void OnInit()
    {
        Setup(
            Chore,
            SectPermissions.DoSectChore,
            SectRoles.NoOffice,
            false,
            ActorTasks.DoSectChore.id,
            SectConst.ContributionSectChore,
            1f,
            "ui/icons/iconBuildings");

        Setup(
            OrganizeScripture,
            SectPermissions.OrganizeScripture,
            SectRoles.Deacon,
            false,
            ActorTasks.OrganizeSectScripture.id,
            SectConst.ContributionOrganizeScripture,
            1f,
            "ui/icons/iconBooks");

        Setup(
            LectureCultibook,
            SectPermissions.TeachSectCultibook,
            SectRoles.Elder,
            true,
            ActorTasks.LectureSectCultibook.id,
            SectConst.ContributionSectLecture,
            1f,
            "cultiway/icons/iconCultivation");

        OrganizeScripture.bypassRequirements = (actor, sect) =>
            SectTraitRules.CanDiscipleOrganizeScripture(sect, actor);
        OrganizeScripture.canExecute = CanOrganizeScripture;
        LectureCultibook.canExecute = SectLectureService.CanLectureCultibook;
    }

    private static void Setup(
        SectAffairAsset asset,
        SectPermissionAsset permission,
        SectRoleAsset officeRole,
        bool requireOfficeAtLeast,
        string taskId,
        int contributionReward,
        float weight,
        string iconPath)
    {
        asset.nameKey = asset.id;
        asset.descriptionKey = $"{asset.id}.Info";
        asset.requiredPermission = permission;
        asset.requiredOfficeRole = officeRole;
        asset.requireOfficeAtLeast = requireOfficeAtLeast;
        asset.taskId = taskId;
        asset.contributionReward = contributionReward;
        asset.weight = weight;
        asset.iconPath = iconPath;
    }

    private static bool CanOrganizeScripture(Actor _, Sect sect)
    {
        return sect.Scriptures.Count > 0;
    }
}
