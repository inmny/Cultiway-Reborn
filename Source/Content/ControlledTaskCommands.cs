using System;
using ai.behaviours;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.AIGC;
using Cultiway.Content.Behaviours;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Crafting;
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
    public static ControlledTaskCommandAsset CultivationProgression { get; private set; }
    public static ControlledTaskCommandAsset StudyMagicWeb { get; private set; }
    public static ControlledTaskCommandAsset StudyMagicScroll { get; private set; }
    public static ControlledTaskCommandAsset ImproveMagicSpell { get; private set; }
    public static ControlledTaskCommandAsset CreateCultibook { get; private set; }
    public static ControlledTaskCommandAsset ImproveCultibook { get; private set; }
    public static ControlledTaskCommandAsset FindNewElixir { get; private set; }
    public static ControlledTaskCommandAsset CraftMagicScroll { get; private set; }
    public static ControlledTaskCommandAsset CraftElixir { get; private set; }
    public static ControlledTaskCommandAsset CraftArtifact { get; private set; }
    public static ControlledTaskCommandAsset CraftTalisman { get; private set; }
    public static ControlledTaskCommandAsset WriteCultibook { get; private set; }
    public static ControlledTaskCommandAsset WriteElixirbook { get; private set; }
    public static ControlledTaskCommandAsset WriteSkillbook { get; private set; }
    public static ControlledTaskCommandAsset FoundSect { get; private set; }
    public static ControlledTaskCommandAsset StudySectScripture { get; private set; }
    public static ControlledTaskCommandAsset DoSectChore { get; private set; }
    public static ControlledTaskCommandAsset OrganizeSectScripture { get; private set; }

    private static readonly CultibookControlledTaskConfigurator CultibookCreator =
        new(CultibookRequestKind.Create);
    private static readonly CultibookControlledTaskConfigurator CultibookImprover =
        new(CultibookRequestKind.Improve);
    private static readonly ElixirDiscoveryCommandConfigurator ElixirDiscoverer = new();
    private static readonly ElixirCraftCommandConfigurator ElixirCrafter = new();
    private static readonly ArtifactCraftCommandConfigurator ArtifactCrafter = new();
    private static readonly ScriptureCommandConfigurator CultibookWriter =
        new(ControlledScriptureKind.Cultibook);
    private static readonly ScriptureCommandConfigurator ElixirbookWriter =
        new(ControlledScriptureKind.ElixirRecipe);
    private static readonly ScriptureCommandConfigurator SkillbookWriter =
        new(ControlledScriptureKind.Skill);

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.ControlledTaskCommand";

    protected override void OnInit()
    {
        Set(MoveToTile, ActorTasks.ControlledMoveToTile,
            "ui/icons/iconArrowDestination", ControlledTaskCategory.Movement, 0,
            ControlledTaskTargetMode.WorldTile, EvaluateMovement, ValidateMovementTarget,
            (actor, tile) => actor.beh_tile_target = tile);
        Set(CultivationProgression, ActorTasks.CultivationProgression,
            "cultiway/icons/iconCultivation", ControlledTaskCategory.Cultivation, 0,
            evaluate: EvaluateCultivationProgression,
            requiresConfirmation: true);
        Set(XianCultivate, ActorTasks.DailyXianCultivate,
            "cultiway/icons/iconCultivation", ControlledTaskCategory.Cultivation, 10,
            evaluate: actor => EvaluateXianCultivation(actor, ActorJobs.XianCultivator.id));
        Set(PlantCultivate, ActorTasks.DailyPlantXianCultivate,
            "cultiway/icons/iconCultivation", ControlledTaskCategory.Cultivation, 20,
            evaluate: actor => EvaluateXianCultivation(actor, ActorJobs.PlantXianCultivator.id));
        Set(EnvironmentalCultivate, ActorTasks.DailyEnvironmentalCultivate,
            "cultiway/icons/iconCultivation", ControlledTaskCategory.Cultivation, 30,
            evaluate: EvaluateEnvironmentalCultivation);
        Set(MagicMeditate, ActorTasks.DailyMagicMeditate,
            "cultiway/icons/iconMagic", ControlledTaskCategory.Cultivation, 40,
            evaluate: EvaluateMagicMeditation);
        Set(KnightTrain, ActorTasks.DailyKnightTrain,
            "cultiway/icons/iconCultivation", ControlledTaskCategory.Cultivation, 50,
            evaluate: EvaluateKnightTraining);
        Set(StudyMagicWeb, ActorTasks.StudyMagicWeb,
            "cultiway/icons/iconMagic", ControlledTaskCategory.Research, 10,
            evaluate: EvaluateMagicWebStudy,
            requiresConfirmation: true);
        Set(StudyMagicScroll, ActorTasks.StudyMagicScroll,
            "cultiway/icons/iconCraftMagicScroll", ControlledTaskCategory.Research, 20,
            evaluate: EvaluateMagicScrollStudy,
            requiresConfirmation: true);
        Set(ImproveMagicSpell, ActorTasks.ImproveMagicSpell,
            "cultiway/icons/iconMagic", ControlledTaskCategory.Research, 30,
            evaluate: EvaluateMagicSpellImprovement,
            requiresConfirmation: true);
        Set(CreateCultibook, ActorTasks.CreateCultibook,
            "cultiway/icons/iconCultivation", ControlledTaskCategory.Research, 40,
            evaluate: CultibookRequestService.EvaluateCreate,
            requiresConfirmation: true,
            configurator: CultibookCreator);
        Set(ImproveCultibook, ActorTasks.ImproveCultibook,
            "cultiway/icons/iconCultivation", ControlledTaskCategory.Research, 50,
            evaluate: CultibookRequestService.EvaluateImprove,
            requiresConfirmation: true,
            configurator: CultibookImprover);
        Set(FindNewElixir, ActorTasks.FindNewElixir,
            "cultiway/icons/iconElixirCauldron", ControlledTaskCategory.Research, 60,
            evaluate: EvaluateElixirDiscovery,
            requiresConfirmation: true,
            configurator: ElixirDiscoverer);
        Set(CraftMagicScroll, ActorTasks.CraftMagicScroll,
            "cultiway/icons/iconCraftMagicScroll", ControlledTaskCategory.Crafting, 0,
            evaluate: actor => AvailableWhen(actor, BehCraftMagicScroll.CanCraft(actor?.GetExtend()),
                "Cultiway.ControlledTask.Reason.CannotCraftMagicScroll"),
            requiresConfirmation: true);
        Set(CraftElixir, ActorTasks.CraftElixir,
            "cultiway/icons/iconElixirCauldron", ControlledTaskCategory.Crafting, 10,
            evaluate: EvaluateElixirCrafting,
            requiresConfirmation: true,
            configurator: ElixirCrafter);
        Set(CraftArtifact, ActorTasks.CraftArtifact,
            "ui/icons/iconArtifact", ControlledTaskCategory.Crafting, 20,
            evaluate: EvaluateArtifactCrafting,
            requiresConfirmation: true,
            configurator: ArtifactCrafter);
        Set(CraftTalisman, ActorTasks.CraftTalisman,
            "ui/icons/iconArtifact", ControlledTaskCategory.Crafting, 30,
            evaluate: EvaluateTalismanCrafting,
            requiresConfirmation: true);
        Set(WriteCultibook, ActorTasks.WriteCultibook,
            "cultiway/icons/iconWriting", ControlledTaskCategory.Crafting, 40,
            evaluate: actor => EvaluateConfigurator(actor, CultibookWriter,
                "Cultiway.ControlledTask.Reason.CannotWriteCultibook"),
            requiresConfirmation: true,
            configurator: CultibookWriter);
        Set(WriteElixirbook, ActorTasks.WriteElixirbook,
            "cultiway/icons/iconWriting", ControlledTaskCategory.Crafting, 50,
            evaluate: actor => EvaluateConfigurator(actor, ElixirbookWriter,
                "Cultiway.ControlledTask.Reason.CannotWriteElixirbook"),
            requiresConfirmation: true,
            configurator: ElixirbookWriter);
        Set(WriteSkillbook, ActorTasks.WriteSkillbook,
            "cultiway/icons/iconWriting", ControlledTaskCategory.Crafting, 60,
            evaluate: actor => EvaluateConfigurator(actor, SkillbookWriter,
                "Cultiway.ControlledTask.Reason.CannotWriteSkillbook"),
            requiresConfirmation: true,
            configurator: SkillbookWriter);
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
        bool requiresConfirmation = false,
        IControlledTaskCommandConfigurator configurator = null)
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
        asset.Configurator = configurator;
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

    private static ControlledTaskAvailability EvaluateCultivationProgression(Actor actor)
    {
        ControlledTaskAvailability common = EvaluateCommon(actor);
        if (!common.Enabled) return common;
        return ProgressionService.CanScheduleAny(actor.GetExtend())
            ? ControlledTaskAvailability.Available
            : ControlledTaskAvailability.Unavailable(
                "Cultiway.ControlledTask.Reason.CannotCultivationProgression");
    }

    private static ControlledTaskAvailability EvaluateMagicWebStudy(Actor actor)
    {
        ControlledTaskAvailability common = EvaluateCommon(actor);
        if (!common.Enabled) return common;
        if (!actor.GetExtend().HasCultisys<Magic>())
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.RequiresMagic");
        if (actor.city == null || actor.city.isRekt())
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.RequiresCity");
        return AvailableWhen(actor, MagicWebStudyPlanner.CanStudyNow(actor.GetExtend()),
            "Cultiway.ControlledTask.Reason.CannotStudyMagicWeb");
    }

    private static ControlledTaskAvailability EvaluateMagicScrollStudy(Actor actor)
    {
        ControlledTaskAvailability common = EvaluateCommon(actor);
        if (!common.Enabled) return common;
        if (!actor.GetExtend().HasCultisys<Magic>())
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.RequiresMagic");
        return AvailableWhen(actor, MagicScrollStudyService.CanStudyNow(actor.GetExtend()),
            "Cultiway.ControlledTask.Reason.CannotStudyMagicScroll");
    }

    private static ControlledTaskAvailability EvaluateMagicSpellImprovement(Actor actor)
    {
        ControlledTaskAvailability common = EvaluateCommon(actor);
        if (!common.Enabled) return common;
        if (!actor.GetExtend().HasCultisys<Magic>())
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.RequiresMagic");
        return AvailableWhen(actor, MagicSpellProgressionService.CanImproveNow(actor.GetExtend()),
            "Cultiway.ControlledTask.Reason.CannotImproveMagicSpell");
    }

    private static ControlledTaskAvailability EvaluateElixirDiscovery(Actor actor)
    {
        ControlledTaskAvailability common = EvaluateCommon(actor);
        if (!common.Enabled) return common;
        if (!actor.hasHouse() || !actor.hasCity())
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.RequiresHome");
        if (!actor.GetExtend().TryGetComponent(out Xian xian) || xian.CurrLevel < 2)
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.RequiresJindan");
        if (CraftSessionService.HasActiveCraft(actor))
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.CraftingAlreadyActive");
        return ElixirDiscoverer.GetOptions(actor, ElixirDiscoveryCommandConfigurator.MaterialsParameter,
                   ControlledTaskInvocation.Empty).Count > 0
            ? ControlledTaskAvailability.Available
            : ControlledTaskAvailability.Unavailable(
                "Cultiway.ControlledTask.Reason.CannotDiscoverElixir");
    }

    private static ControlledTaskAvailability EvaluateElixirCrafting(Actor actor)
    {
        ControlledTaskAvailability common = EvaluateCommon(actor);
        if (!common.Enabled) return common;
        if (!actor.hasHouse() || !actor.hasCity())
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.RequiresHome");
        if (!actor.GetExtend().TryGetComponent(out Xian xian) || xian.CurrLevel < 2)
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.RequiresJindan");
        if (CraftSessionService.HasActiveCraft(actor))
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.CraftingAlreadyActive");
        return ElixirCrafter.GetOptions(actor, ElixirCraftCommandConfigurator.RecipeParameter,
                   ControlledTaskInvocation.Empty).Count > 0
            ? ControlledTaskAvailability.Available
            : ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.CannotCraftElixir");
    }

    private static ControlledTaskAvailability EvaluateArtifactCrafting(Actor actor)
    {
        ControlledTaskAvailability common = EvaluateCommon(actor);
        if (!common.Enabled) return common;
        if (!actor.hasHouse() || !actor.hasCity())
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.RequiresHome");
        if (!actor.GetExtend().TryGetComponent(out Xian xian) || xian.CurrLevel < XianLevels.Yuanying)
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.RequiresYuanying");
        if (CraftSessionService.HasActiveCraft(actor))
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.CraftingAlreadyActive");
        return ArtifactCrafter.GetOptions(actor, ArtifactCraftCommandConfigurator.MaterialsParameter,
                   ControlledTaskInvocation.Empty).Count > 0
            ? ControlledTaskAvailability.Available
            : ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.CannotCraftArtifact");
    }

    private static ControlledTaskAvailability EvaluateTalismanCrafting(Actor actor)
    {
        ControlledTaskAvailability common = EvaluateCommon(actor);
        if (!common.Enabled) return common;
        if (!actor.GetExtend().HasCultisys<Xian>())
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.RequiresXian");
        return AvailableWhen(actor, BehCraftTalisman.CanCraft(actor.GetExtend()),
            "Cultiway.ControlledTask.Reason.CannotCraftTalisman");
    }

    private static ControlledTaskAvailability EvaluateConfigurator(
        Actor actor,
        IControlledTaskCommandConfigurator configurator,
        string reasonLocaleKey)
    {
        ControlledTaskAvailability common = EvaluateCommon(actor);
        if (!common.Enabled) return common;
        if (!actor.hasHouse() || !actor.hasCity() || !actor.hasLanguage())
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.RequiresHome");
        ControlledTaskParameterDefinition source = configurator.Parameters[0];
        return configurator.GetOptions(actor, source.Key, ControlledTaskInvocation.Empty).Count > 0
            ? ControlledTaskAvailability.Available
            : ControlledTaskAvailability.Unavailable(reasonLocaleKey);
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
