using System;
using ai.behaviours;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Behaviours;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.KnightCombat;
using Cultiway.Content.Libraries;
using Cultiway.Content.Sects;
using Cultiway.Core;
using Cultiway.Core.ControlledTasks;
using Cultiway.Core.Progression;
using Cultiway.Utils.Extension;

namespace Cultiway.Content;

/// <summary>只把资格查询和副作用边界明确的角色任务注册为玩家命令资产。</summary>
[Dependency(typeof(ActorTasks), typeof(ActorJobs), typeof(CultivateMethods), typeof(SectAffairs))]
public sealed class ControlledTaskCommands : ExtendLibrary<ControlledTaskCommandAsset, ControlledTaskCommands>
{
    public static ControlledTaskCommandAsset MoveToTile { get; private set; }
    public static ControlledTaskCommandAsset XianCultivate { get; private set; }
    public static ControlledTaskCommandAsset PlantCultivate { get; private set; }
    public static ControlledTaskCommandAsset EnvironmentalCultivate { get; private set; }
    public static ControlledTaskCommandAsset MagicMeditate { get; private set; }
    public static ControlledTaskCommandAsset KnightTrain { get; private set; }
    public static ControlledTaskCommandAsset CraftMagicScroll { get; private set; }
    public static ControlledTaskCommandAsset FoundSect { get; private set; }
    public static ControlledTaskCommandAsset StudySectScripture { get; private set; }
    public static ControlledTaskCommandAsset DoSectChore { get; private set; }
    public static ControlledTaskCommandAsset OrganizeSectScripture { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.ControlledTaskCommand";

    protected override void OnInit()
    {
        Set(MoveToTile, ActorTasks.ControlledMoveToTile,
            "ui/icons/iconArrowDestination", ControlledTaskCategory.Movement, 0,
            ControlledTaskTargetMode.WorldTile, EvaluateMovement, ValidateMovementTarget,
            (actor, tile) => actor.beh_tile_target = tile);
        Set(XianCultivate, ActorTasks.DailyXianCultivate,
            "cultiway/icons/iconCultivation", ControlledTaskCategory.Cultivation, 0,
            evaluate: actor => EvaluateXianCultivation(actor, ActorJobs.XianCultivator.id));
        Set(PlantCultivate, ActorTasks.DailyPlantXianCultivate,
            "cultiway/icons/iconCultivation", ControlledTaskCategory.Cultivation, 10,
            evaluate: actor => EvaluateXianCultivation(actor, ActorJobs.PlantXianCultivator.id));
        Set(EnvironmentalCultivate, ActorTasks.DailyEnvironmentalCultivate,
            "cultiway/icons/iconCultivation", ControlledTaskCategory.Cultivation, 20,
            evaluate: EvaluateEnvironmentalCultivation);
        Set(MagicMeditate, ActorTasks.DailyMagicMeditate,
            "cultiway/icons/iconMagic", ControlledTaskCategory.Cultivation, 30,
            evaluate: EvaluateMagicMeditation);
        Set(KnightTrain, ActorTasks.DailyKnightTrain,
            "cultiway/icons/iconCultivation", ControlledTaskCategory.Cultivation, 40,
            evaluate: EvaluateKnightTraining);
        Set(CraftMagicScroll, ActorTasks.CraftMagicScroll,
            "cultiway/icons/iconCraftMagicScroll", ControlledTaskCategory.Crafting, 0,
            evaluate: actor => AvailableWhen(actor, BehCraftMagicScroll.CanCraft(actor?.GetExtend()),
                "Cultiway.ControlledTask.Reason.CannotCraftMagicScroll"),
            requiresConfirmation: true);
        Set(FoundSect, ActorTasks.BuildSect,
            "ui/icons/iconKingdom", ControlledTaskCategory.Sect, 0,
            evaluate: actor => AvailableWhen(actor, SectRules.CanFoundSect(actor),
                "Cultiway.ControlledTask.Reason.CannotFoundSect"),
            requiresConfirmation: true);
        Set(StudySectScripture, ActorTasks.StudySectScripture,
            "ui/icons/iconBooks", ControlledTaskCategory.Research, 0,
            evaluate: actor => AvailableWhen(actor, SectScriptureStudyPlanner.CanPlan(actor),
                "Cultiway.ControlledTask.Reason.CannotStudySectScripture"),
            requiresConfirmation: true);
        Set(DoSectChore, ActorTasks.DoSectChore,
            "ui/icons/iconBuildings", ControlledTaskCategory.Affairs, 0,
            evaluate: actor => AvailableWhen(actor,
                SectAffairExecutionPolicy.CanExecute(actor, SectAffairs.Chore),
                "Cultiway.ControlledTask.Reason.CannotDoSectChore"));
        Set(OrganizeSectScripture, ActorTasks.OrganizeSectScripture,
            "ui/icons/iconBooks", ControlledTaskCategory.Affairs, 10,
            evaluate: actor => AvailableWhen(actor,
                SectAffairExecutionPolicy.CanExecute(actor, SectAffairs.OrganizeScripture),
                "Cultiway.ControlledTask.Reason.CannotOrganizeSectScripture"));

        ControlledTaskOrderService.Initialize();
    }

    private static void Set(ControlledTaskCommandAsset asset, BehaviourTaskActor task,
        string iconPath, ControlledTaskCategory category, int order,
        ControlledTaskTargetMode targetMode = ControlledTaskTargetMode.None,
        Func<Actor, ControlledTaskAvailability> evaluate = null,
        Func<Actor, WorldTile, ControlledTaskAvailability> validateTarget = null,
        Action<Actor, WorldTile> applyTarget = null,
        bool requiresConfirmation = false)
    {
        string localeSuffix = asset.id.Substring(asset.id.LastIndexOf('.') + 1);
        asset.Task = task;
        asset.NameLocaleKey = $"Cultiway.ControlledTask.Command.{localeSuffix}.Name";
        asset.DescriptionLocaleKey = $"Cultiway.ControlledTask.Command.{localeSuffix}.Description";
        asset.IconPath = iconPath;
        asset.Category = category;
        asset.Order = order;
        asset.TargetMode = targetMode;
        asset.EvaluateActor = evaluate;
        asset.ValidateWorldTile = validateTarget;
        asset.ApplyWorldTileContext = applyTarget;
        asset.RequiresConfirmation = requiresConfirmation;
    }

    private static ControlledTaskAvailability EvaluateMovement(Actor actor)
    {
        ControlledTaskAvailability common = EvaluateCommon(actor);
        if (!common.Enabled) return common;
        return actor.current_tile == null
            ? ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.ActorCannotMove")
            : ControlledTaskAvailability.Available;
    }

    private static ControlledTaskAvailability ValidateMovementTarget(Actor actor, WorldTile tile)
    {
        ControlledTaskAvailability common = EvaluateMovement(actor);
        if (!common.Enabled) return common;
        if (tile?.Type == null || tile.world_edge)
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.TargetMissing");
        if (tile.Type.block && !actor.asset.ignore_blocks)
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.TargetBlocked");
        if (tile.Type.lava && actor.asset.die_in_lava && !actor.isImmuneToFire())
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.TargetDangerous");
        if (tile.Type.damage_units || (tile.isOnFire() && !actor.isImmuneToFire()))
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.TargetDangerous");
        return ControlledTaskAvailability.Available;
    }

    private static ControlledTaskAvailability EvaluateXianCultivation(Actor actor, string expectedJobId)
    {
        ControlledTaskAvailability common = EvaluateCommon(actor);
        if (!common.Enabled) return common;
        ActorExtend actorExtend = actor.GetExtend();
        if (!actorExtend.HasCultisys<Xian>())
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.RequiresXian");
        if (ProgressionService.CanScheduleAny(actorExtend))
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.ProgressionReady");

        ref Xian xian = ref actorExtend.GetCultisys<Xian>();
        float maximum = actor.stats[BaseStatses.MaxWakan.id];
        if (maximum <= 0f || xian.wakan >= maximum - 0.1f)
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.CultivationResourceFull");

        CultivateMethodAsset method = actorExtend.GetMainCultibook()?.GetCultivateMethod() ?? CultivateMethods.Standard;
        if (method == null || method.Execute == null || !method.Handles(CultivationTriggerKind.ActiveTick) ||
            method.CanCultivate?.Invoke(actorExtend) == false)
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.CultivationMethodUnavailable");
        if (method.GetBehaviourJobId?.Invoke(actorExtend) != expectedJobId)
            return expectedJobId == ActorJobs.XianCultivator.id && !actor.hasHouse()
                ? ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.RequiresHome")
                : ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.CultivationMethodMismatch");
        return ControlledTaskAvailability.Available;
    }

    private static ControlledTaskAvailability EvaluateEnvironmentalCultivation(Actor actor)
    {
        ControlledTaskAvailability availability = EvaluateXianCultivation(actor, ActorJobs.EnvironmentalCultivator.id);
        if (!availability.Enabled) return availability;
        ActorExtend actorExtend = actor.GetExtend();
        CultivateMethodAsset method = actorExtend.GetMainCultibook()?.GetCultivateMethod();
        if (method?.EnvironmentRule == null ||
            CultivationEnvironmentService.ResolveBestNearbyQuality(actorExtend, method.EnvironmentRule) <= 0f)
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.NoCultivationSite");
        return ControlledTaskAvailability.Available;
    }

    private static ControlledTaskAvailability EvaluateMagicMeditation(Actor actor)
    {
        ControlledTaskAvailability common = EvaluateCommon(actor);
        if (!common.Enabled) return common;
        ActorExtend actorExtend = actor.GetExtend();
        if (!actorExtend.HasCultisys<Magic>())
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.RequiresMagic");
        if (ProgressionService.CanScheduleAny(actorExtend))
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.ProgressionReady");
        float maximum = actor.stats[BaseStatses.MaxSpirit.id];
        return maximum <= 0f || actorExtend.GetCultisys<Magic>().spirit >= maximum - 0.1f
            ? ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.CultivationResourceFull")
            : ControlledTaskAvailability.Available;
    }

    private static ControlledTaskAvailability EvaluateKnightTraining(Actor actor)
    {
        ControlledTaskAvailability common = EvaluateCommon(actor);
        if (!common.Enabled) return common;
        ActorExtend actorExtend = actor.GetExtend();
        if (!actorExtend.HasCultisys<Knight>())
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.RequiresKnight");
        if (ProgressionService.CanScheduleAny(actorExtend))
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.ProgressionReady");
        float maximum = actor.stats[BaseStatses.MaxVigor.id];
        if (maximum <= 0f || actorExtend.GetCultisys<Knight>().vigor >= maximum - 0.1f)
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.CultivationResourceFull");
        return KnightTrainingDummyService.TryFind(actor, out _)
            ? ControlledTaskAvailability.Available
            : ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.RequiresTrainingDummy");
    }

    private static ControlledTaskAvailability AvailableWhen(Actor actor, bool condition, string reasonLocaleKey)
    {
        ControlledTaskAvailability common = EvaluateCommon(actor);
        if (!common.Enabled) return common;
        return condition ? ControlledTaskAvailability.Available : ControlledTaskAvailability.Unavailable(reasonLocaleKey);
    }

    private static ControlledTaskAvailability EvaluateCommon(Actor actor)
    {
        if (actor == null || actor.isRekt())
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.ActorLost");
        if (actor.is_unconscious || actor.asset == null || actor.asset.skip_fight_logic)
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.ActorUnavailable");
        return ControlledTaskAvailability.Available;
    }
}
