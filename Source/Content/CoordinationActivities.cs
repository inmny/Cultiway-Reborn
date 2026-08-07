using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Sects;
using Cultiway.Core.Coordination;

namespace Cultiway.Content;

/// <summary>注册当前内容层使用的协调行动定义。</summary>
[Dependency(typeof(ActorTasks))]
public sealed class CoordinationActivities : ExtendLibrary<CoordinatedActivityDefinitionAsset, CoordinationActivities>
{
    /// <summary>讲师与自愿听众真实到场后执行的宗门讲法。</summary>
    public static CoordinatedActivityDefinitionAsset SectLecture { get; private set; }

    /// <summary>非巡逻鼠群留守巢穴的常态行动。</summary>
    public static CoordinatedActivityDefinitionAsset SkavenGuard { get; private set; }

    /// <summary>一至三个鼠群在巢穴附近进行的巡逻行动。</summary>
    public static CoordinatedActivityDefinitionAsset SkavenPatrol { get; private set; }

    /// <summary>鼠群响应近期威胁并向目标集结的动员行动。</summary>
    public static CoordinatedActivityDefinitionAsset SkavenDefend { get; private set; }

    /// <summary>一个大魔领导一支混沌战帮的常态行动。</summary>
    public static CoordinatedActivityDefinitionAsset ChaosWarband { get; private set; }

    /// <inheritdoc />
    protected override bool AutoRegisterAssets() => true;

    /// <inheritdoc />
    protected override string Prefix() => "Cultiway.Coordination";

    /// <inheritdoc />
    protected override void OnInit()
    {
        CoordinatedActivityService.Initialize();
        CollectiveProjectActivityBridge.Initialize();
        CoordinatedActivityService.RegisterGroupProvider(new SectCoordinationGroupProvider());

        ConfigureSectLecture();
        ConfigureSkavenActivity(SkavenGuard, priority: 100);
        ConfigureSkavenActivity(SkavenPatrol, priority: 120);
        ConfigureSkavenActivity(SkavenDefend, priority: 300);
        ConfigureChaosWarband();

        CoordinatedActivityService.RegisterDefinition(SectLecture);
        CoordinatedActivityService.RegisterDefinition(SkavenGuard);
        CoordinatedActivityService.RegisterDefinition(SkavenPatrol);
        CoordinatedActivityService.RegisterDefinition(SkavenDefend);
        CoordinatedActivityService.RegisterDefinition(ChaosWarband);
    }

    /// <summary>配置讲法的讲师与自愿听众席位。</summary>
    private static void ConfigureSectLecture()
    {
        SectLecture.Priority = 180;
        SectLecture.Preemptible = true;
        SectLecture.RecruitmentTimeoutSeconds = TimeScales.SecPerMonth * 4f;
        SectLecture.AssemblyTimeoutSeconds = TimeScales.SecPerMonth * 4f;
        SectLecture.RunningTimeoutSeconds = TimeScales.SecPerMonth * 3f;
        SectLecture.HeartbeatSeconds = 0.25f;
        SectLecture.MinimumReadyCount = 2;
        SectLecture.MinimumReadyRatio = 0.5f;
        SectLecture.RunningReadinessPolicy = CoordinationRunningReadinessPolicy.Reassemble;
        SectLecture.Roles =
        [
            new CoordinationRoleDefinition
            {
                Id = SectLectureSession.LecturerRoleId,
                MinimumCount = 1,
                MaximumCount = 1,
                MinimumReadyCount = 1,
                ParticipationMode = CoordinationParticipationMode.Forced,
                ParticipantLifetime = CoordinationParticipantLifetime.ExecutionBound,
                ExecutionTaskIds = [ActorTasks.LectureSectCultibook.id]
            },
            new CoordinationRoleDefinition
            {
                Id = SectLectureSession.AudienceRoleId,
                MinimumCount = 1,
                MaximumCount = 64,
                MinimumReadyCount = 1,
                ParticipationMode = CoordinationParticipationMode.Voluntary,
                ParticipantLifetime = CoordinationParticipantLifetime.ExecutionBound,
                ExecutionTaskIds = [ActorTasks.CoordinatedActivity.id]
            }
        ];
    }

    /// <summary>配置鼠群行动共用的队长与成员席位。</summary>
    private static void ConfigureSkavenActivity(
        CoordinatedActivityDefinitionAsset definition,
        int priority)
    {
        definition.Priority = priority;
        definition.Preemptible = true;
        definition.RecruitmentTimeoutSeconds = 2f;
        definition.AssemblyTimeoutSeconds = 6f;
        definition.RunningTimeoutSeconds = 0f;
        definition.HeartbeatSeconds = 0.25f;
        definition.MinimumReadyCount = 1;
        definition.MinimumReadyRatio = 0.25f;
        definition.RunningReadinessPolicy = CoordinationRunningReadinessPolicy.Ignore;
        definition.Roles =
        [
            new CoordinationRoleDefinition
            {
                Id = SkavenPackService.LeaderRoleId,
                MinimumCount = 1,
                MaximumCount = 1,
                MinimumReadyCount = 1,
                ParticipationMode = CoordinationParticipationMode.Forced,
                AllowLateJoin = true,
                ParticipantLifetime = CoordinationParticipantLifetime.ActivityBound,
                ExecutionTaskIds =
                [
                    ActorTasks.CoordinateSkavenPack.id,
                    ActorTasks.CoordinatedActivity.id
                ]
            },
            new CoordinationRoleDefinition
            {
                Id = SkavenPackService.MemberRoleId,
                MinimumCount = 0,
                MaximumCount = SkavenEvolution.GroupSize - 1,
                MinimumReadyCount = 0,
                ParticipationMode = CoordinationParticipationMode.Forced,
                AllowLateJoin = true,
                ParticipantLifetime = CoordinationParticipantLifetime.ActivityBound,
                ExecutionTaskIds =
                [
                    ActorTasks.CoordinateSkavenPack.id,
                    ActorTasks.CoordinatedActivity.id
                ]
            },
            new CoordinationRoleDefinition
            {
                Id = SkavenPackService.SlaveRoleId,
                MinimumCount = 0,
                MaximumCount = 1,
                MinimumReadyCount = 0,
                ParticipationMode = CoordinationParticipationMode.Forced,
                AllowLateJoin = true,
                ParticipantLifetime = CoordinationParticipantLifetime.ActivityBound,
                ExecutionTaskIds =
                [
                    ActorTasks.CoordinateSkavenPack.id,
                    ActorTasks.CoordinatedActivity.id
                ]
            }
        ];
    }

    /// <summary>配置大魔领队和同神成员组成的战帮。</summary>
    private static void ConfigureChaosWarband()
    {
        ChaosWarband.Priority = 110;
        ChaosWarband.Preemptible = true;
        ChaosWarband.RecruitmentTimeoutSeconds = 2f;
        ChaosWarband.AssemblyTimeoutSeconds = 6f;
        ChaosWarband.RunningTimeoutSeconds = 0f;
        ChaosWarband.HeartbeatSeconds = 0.25f;
        ChaosWarband.MinimumReadyCount = 1;
        ChaosWarband.MinimumReadyRatio = 0.25f;
        ChaosWarband.RunningReadinessPolicy = CoordinationRunningReadinessPolicy.Ignore;
        ChaosWarband.Roles =
        [
            new CoordinationRoleDefinition
            {
                Id = ChaosWarbandService.LeaderRoleId,
                MinimumCount = 1,
                MaximumCount = 1,
                MinimumReadyCount = 1,
                ParticipationMode = CoordinationParticipationMode.Forced,
                AllowLateJoin = true,
                ParticipantLifetime = CoordinationParticipantLifetime.ActivityBound,
                ExecutionTaskIds = [ActorTasks.CoordinateChaosWarband.id, ActorTasks.CoordinatedActivity.id]
            },
            new CoordinationRoleDefinition
            {
                Id = ChaosWarbandService.MemberRoleId,
                MinimumCount = 0,
                MaximumCount = 64,
                MinimumReadyCount = 0,
                ParticipationMode = CoordinationParticipationMode.Forced,
                AllowLateJoin = true,
                ParticipantLifetime = CoordinationParticipantLifetime.ActivityBound,
                ExecutionTaskIds = [ActorTasks.CoordinateChaosWarband.id, ActorTasks.CoordinatedActivity.id]
            }
        ];
    }
}
