using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Core.CollectiveProjects;

namespace Cultiway.Content.CollectiveProjects;

/// <summary>注册首批城市功能法术工程；通用内核不依赖城市或魔法类型。</summary>
[Dependency(typeof(SkillEntities), typeof(ActorJobs), typeof(Decisions))]
internal sealed class MagicUtilityProjectContent : ICanInit
{
    /// <summary>注册城市发起者、法术执行器、工程定义与两档规划节奏。</summary>
    public void Init()
    {
        CollectiveProjectService.Initialize();
        CollectiveProjectService.RegisterOwnerAdapter(new CityCollectiveProjectOwnerAdapter());
        CollectiveProjectService.RegisterExecutor(new MagicUtilityProjectExecutor());

        RegisterDefinition(CityMagicUtilityProjectIds.EmergencyClean, 100f, false);
        RegisterDefinition(CityMagicUtilityProjectIds.RoutineClean, 20f, false);
        RegisterDefinition(CityMagicUtilityProjectIds.CropFertilization, 40f, false);
        RegisterDefinition(CityMagicUtilityProjectIds.NatureGrowth, 30f, true);
        RegisterDefinition(CityMagicUtilityProjectIds.HousingTerrain, 50f, true);
        RegisterDefinition(CityMagicUtilityProjectIds.FarmTerrain, 50f, true);

        CollectiveProjectService.RegisterPlanner(new CityEmergencyMagicProjectPlanner());
        CollectiveProjectService.RegisterPlanner(new CityRoutineMagicProjectPlanner());
    }

    /// <summary>创建共享校验/验收规则，并按需挂接组织级永久改造额度。</summary>
    private static void RegisterDefinition(string id, float priority, bool rateLimited)
    {
        CollectiveProjectService.RegisterDefinition(new CollectiveProjectDefinitionAsset
        {
            id = id,
            ExecutorId = CityMagicUtilityProjectIds.Executor,
            WorkerSlots = 1,
            DefaultPriority = priority,
            Validate = CityMagicUtilityProjectRules.Validate,
            Verify = CityMagicUtilityProjectRules.Verify,
            RatePolicy = rateLimited
                ? new CollectiveProjectRatePolicy
                {
                    BudgetGroup = CityMagicUtilityProjectIds.PermanentWorldChangeBudget,
                    MaxCompletions = 1,
                    WindowSeconds = TimeScales.SecPerYear,
                }
                : null,
        });
    }
}
