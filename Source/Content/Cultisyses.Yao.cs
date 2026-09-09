using System;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.YaoBeasts;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Core.Progression;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>妖修进阶过渡的稳定标识。</summary>
public static class YaoTransitions
{
    /// <summary>启灵进入炼血。</summary>
    public const string EnterBloodRefining = "yao.enter_blood_refining";

    /// <summary>炼血淬血小层次。</summary>
    public const string QuenchBlood = "yao.quench_blood";

    /// <summary>炼血进入妖躯。</summary>
    public const string EnterBodyTempering = "yao.enter_body_tempering";

    /// <summary>妖躯淬炼小层次。</summary>
    public const string TemperBody = "yao.temper_body";

    /// <summary>妖躯进入妖丹（凝丹天劫）。</summary>
    public const string CondenseCore = "yao.condense_core";

    /// <summary>妖丹凝纯小层次。</summary>
    public const string RefineCore = "yao.refine_core";

    /// <summary>妖丹进入化形（化形劫）。</summary>
    public const string TransformHuman = "yao.transform_human";
}

/// <summary>妖修体系资产与进阶规则。</summary>
public partial class Cultisyses
{
    /// <summary>妖修体系资产：启灵、炼血、妖躯、妖丹、化形五个主境界。</summary>
    public static CultisysAsset<Yao> Yao { get; private set; }

    private void InitYao()
    {
        Yao = (CultisysAsset<Yao>)Add(new CultisysAsset<Yao>(
            nameof(Yao), YaoSetting.RealmCount, new Yao(), CreateYaoProgressionProfile(),
            power_levels: [1f, 2f, 3f, 4.5f, 6f]));
        ProgressionService.Register(Yao);
        Yao.ConfigureOnAcquired(InitializeYaoState);
        Yao.DisplayDetailProvider = AppendYaoDisplayDetails;

        // 妖兽境界属性直接在代码中声明，避免再维护一份只读表格。
        Yao.LevelBaseStats[0][BaseStatses.MaxYaoPower.id] = 40f;
        Yao.LevelBaseStats[0][BaseStatses.YaoPowerRegen.id] = 6f;
        Yao.LevelBaseStats[1][BaseStatses.MaxYaoPower.id] = 70f;
        Yao.LevelBaseStats[1][BaseStatses.YaoPowerRegen.id] = 9f;
        Yao.LevelBaseStats[2][BaseStatses.MaxYaoPower.id] = 110f;
        Yao.LevelBaseStats[2][BaseStatses.YaoPowerRegen.id] = 13f;
        Yao.LevelBaseStats[3][BaseStatses.MaxYaoPower.id] = 170f;
        Yao.LevelBaseStats[3][BaseStatses.YaoPowerRegen.id] = 18f;
        Yao.LevelBaseStats[4][BaseStatses.MaxYaoPower.id] = 240f;
        Yao.LevelBaseStats[4][BaseStatses.YaoPowerRegen.id] = 24f;
        Yao.UpdateAccumStats();

        // 妖修境界等级提供妖力上限与恢复；妖丹方向按容量系数继续放大上限。
        ActorExtend.RegisterCachedStatsBuilder([Hotfixable](ae, stats) =>
        {
            if (!ae.TryGetComponent(out Yao yao)) return;
            stats.mergeStats(Yao.LevelAccumBaseStats[yao.CurrLevel]);
            if (ae.E.TryGetComponent(out YaoCore core) &&
                YaoCorePatterns.Get(core.CorePatternId) is { } pattern)
            {
                stats[BaseStatses.MaxYaoPower.id] *= pattern.PowerCapacityMultiplier;
            }
        });

        // 妖力变化后刷新属性缓存，让依赖妖力比例的判定即时生效。
        YaoResourceService.MutationCommitted += (actor, _) => actor.MarkCultiwayStatsDirty(false);
    }

    /// <summary>启灵时写入妖修主数据的初始值；不恢复生命也不重置年龄。</summary>
    private static void InitializeYaoState(ActorExtend actor, CultisysAsset<Yao> cultisys, ref Yao yao)
    {
        yao.OriginalSpeciesId = actor.Base.asset.id;
        yao.AwakenedAt = (float)World.world.getCurWorldTime();
        yao.Seed = actor.Base.data.id.GetHashCode() & 0x7fffffff;
        yao.MinorLevel = 0;
        yao.yao_power = YaoSetting.InitialYaoPower;
        yao.BodyStability = YaoSetting.InitialBodyStability;
        yao.OrganCapacityBonus = 0;
        yao.MutationTolerance = 0.3f;
        yao.RecoveryCost = 1f;
        yao.PhoenixRevivalUses = 0;
        yao.NineTailLifeUses = 0;
        yao.CorePreparationStartedAt = -1f;
        yao.CorePreparationPatternId = null;
    }

    /// <summary>向通用修炼体系详情追加妖力、稳定度与身体承载。</summary>
    private static void AppendYaoDisplayDetails(ActorExtend actor, System.Collections.Generic.ICollection<CultisysDisplayLine> lines)
    {
        ref Yao yao = ref actor.GetCultisys<Yao>();
        lines.Add(CultisysDisplayLine.CreateProgress(
            "Cultiway.CultisysTooltip.Resource.YaoPower",
            yao.yao_power,
            actor.Base.stats[BaseStatses.MaxYaoPower.id],
            "cultiway/icons/iconWakan",
            "#4CAF50"));
        if (yao.CurrLevel >= 1)
        {
            lines.Add(CultisysDisplayLine.CreateProgress(
                "Cultiway.CultisysTooltip.Yao.BodyStability",
                yao.BodyStability,
                YaoSetting.MaximumBodyStability,
                null,
                "#8D6E63"));
            lines.Add(new CultisysDisplayLine(
                "Cultiway.CultisysTooltip.Yao.Capacity",
                $"{yao.OrganCapacityBonus}"));
        }

        if (actor.E.TryGetComponent(out YaoCore core))
        {
            lines.Add(new CultisysDisplayLine(
                "Cultiway.CultisysTooltip.Yao.Core",
                string.Format("Cultiway.CultisysTooltip.Format.YaoCore".Localize(),
                    YaoCoreService.GetPatternName(core.CorePatternId),
                    core.Quality,
                    core.Cracks)));
        }
    }

    /// <summary>声明妖修从启灵到化形的完整进阶图。</summary>
    private static CultisysProgressionProfile<Yao> CreateYaoProgressionProfile()
    {
        var profile = new CultisysProgressionProfile<Yao>();

        // ===== 启灵 → 炼血：妖力充盈且身体稳定即可突破 =====
        var enterBlood = new ProgressionTransitionAsset<Yao>(
            YaoTransitions.EnterBloodRefining, ProgressionKind.Major, 0, 1)
        {
            IsApproaching = IsYaoApproaching,
            ResolveNatural = ResolveYaoSuccess,
            ResolveGrant = ResolveYaoSuccess,
        };
        enterBlood.Requirements.Add(RequireYaoPowerNearlyFull);
        enterBlood.Requirements.Add(RequireBodyStabilityAbove(40f));
        enterBlood.SuccessCosts.Add(ConsumeYaoPowerSixty);
        var awakeningRealm = new RealmProgressionAsset<Yao>(0);
        awakeningRealm.Transitions.Add(enterBlood);
        awakeningRealm.SelectForQuery = SelectSingleTransition;
        awakeningRealm.SelectForNaturalAttempt = SelectSingleTransition;
        awakeningRealm.SelectForMajorGrant = SelectSingleTransition;
        profile.AddRealm(awakeningRealm);

        // ===== 炼血：三次淬血小层次，每次提升一个器官表达 =====
        var quenchBlood = new ProgressionTransitionAsset<Yao>(
            YaoTransitions.QuenchBlood, ProgressionKind.Minor, 1, 1)
        {
            IsApproaching = IsYaoApproaching,
            ResolveNatural = ResolveQuenchBlood,
            ResolveGrant = ResolveQuenchBlood,
        };
        quenchBlood.Requirements.Add(RequireYaoPowerNearlyFull);
        quenchBlood.Requirements.Add(RequireBodyStabilityAbove(YaoSetting.BodyStabilityLowThreshold));
        quenchBlood.Requirements.Add(RequireNoPendingDigestion);
        quenchBlood.SuccessCosts.Add(ConsumeYaoPowerHalf);
        quenchBlood.Transformations.Add(ApplyQuenchBlood);
        quenchBlood.FailureEffects.Add(ApplyYaoBreakthroughFailure);

        var enterBody = new ProgressionTransitionAsset<Yao>(
            YaoTransitions.EnterBodyTempering, ProgressionKind.Major, 1, 2)
        {
            IsApproaching = IsYaoApproaching,
            ResolveNatural = RequireQuenchComplete,
            ResolveGrant = RequireQuenchComplete,
        };
        enterBody.Requirements.Add(RequireYaoPowerNearlyFull);
        enterBody.Requirements.Add(RequireBodyStabilityAbove(50f));
        enterBody.SuccessCosts.Add(ConsumeYaoPowerSixty);
        var bloodRealm = new RealmProgressionAsset<Yao>(1);
        bloodRealm.Transitions.Add(quenchBlood);
        bloodRealm.Transitions.Add(enterBody);
        bloodRealm.SelectForQuery = SelectBloodRealmTransition;
        bloodRealm.SelectForNaturalAttempt = SelectBloodRealmTransition;
        bloodRealm.SelectForMajorGrant = SelectBloodRealmTransition;
        profile.AddRealm(bloodRealm);

        // ===== 妖躯：淬炼小层次提高稳定度与容量；凝丹以天劫为挑战 =====
        var temperBody = new ProgressionTransitionAsset<Yao>(
            YaoTransitions.TemperBody, ProgressionKind.Minor, 2, 2)
        {
            IsApproaching = IsYaoApproaching,
            ResolveNatural = ResolveYaoSuccess,
            ResolveGrant = ResolveYaoSuccess,
        };
        temperBody.Requirements.Add(RequireYaoPowerNearlyFull);
        temperBody.Requirements.Add(RequireBodyStabilityAbove(YaoSetting.BodyStabilityLowThreshold));
        temperBody.SuccessCosts.Add(ConsumeYaoPowerHalf);
        temperBody.Transformations.Add(ApplyTemperBody);

        var condenseCore = new ProgressionTransitionAsset<Yao>(
            YaoTransitions.CondenseCore, ProgressionKind.Major, 2, 3)
        {
            IsApproaching = IsYaoApproaching,
            ResolveNatural = YaoCoreService.ResolveCondensation,
            ResolveGrant = YaoCoreService.ResolveCondensation,
            Preparation = new YaoCorePreparationStage(),
            Challenge = new YaoTribulationStage(),
        };
        condenseCore.Requirements.Add(RequireYaoPowerNearlyFull);
        condenseCore.Requirements.Add(RequireBodyStabilityAbove(60f));
        condenseCore.Transformations.Add(ApplyCoreCondensation);
        var bodyRealm = new RealmProgressionAsset<Yao>(2);
        bodyRealm.Transitions.Add(temperBody);
        bodyRealm.Transitions.Add(condenseCore);
        bodyRealm.SelectForQuery = SelectBodyRealmTransition;
        bodyRealm.SelectForNaturalAttempt = SelectBodyRealmTransition;
        bodyRealm.SelectForMajorGrant = SelectBodyRealmTransition;
        profile.AddRealm(bodyRealm);

        // ===== 妖丹：凝纯小层次强化妖丹；化形劫进入人形 =====
        var refineCore = new ProgressionTransitionAsset<Yao>(
            YaoTransitions.RefineCore, ProgressionKind.Minor, 3, 3)
        {
            IsApproaching = IsYaoApproaching,
            ResolveNatural = ResolveYaoSuccess,
            ResolveGrant = ResolveYaoSuccess,
        };
        refineCore.Requirements.Add(RequireYaoPowerNearlyFull);
        refineCore.Requirements.Add(RequireBodyStabilityAbove(YaoSetting.BodyStabilityLowThreshold));
        refineCore.SuccessCosts.Add(ConsumeYaoPowerHalf);
        refineCore.Transformations.Add(ApplyCoreRefinement);

        var transformHuman = new ProgressionTransitionAsset<Yao>(
            YaoTransitions.TransformHuman, ProgressionKind.Major, 3, 4)
        {
            IsApproaching = IsYaoApproaching,
            ResolveNatural = ResolveYaoSuccess,
            ResolveGrant = ResolveYaoSuccess,
            Challenge = new YaoHumanTransformationStage(),
        };
        transformHuman.Requirements.Add(RequireCoreForTransformation);
        transformHuman.Requirements.Add(RequireBodyStabilityAbove(70f));
        transformHuman.SuccessCosts.Add(ConsumeYaoPowerEighty);
        transformHuman.Transformations.Add(ApplyHumanFormGranted);
        var coreRealm = new RealmProgressionAsset<Yao>(3);
        coreRealm.Transitions.Add(refineCore);
        coreRealm.Transitions.Add(transformHuman);
        coreRealm.SelectForQuery = SelectCoreRealmTransition;
        coreRealm.SelectForNaturalAttempt = SelectCoreRealmTransition;
        coreRealm.SelectForMajorGrant = SelectCoreRealmTransition;
        profile.AddRealm(coreRealm);

        // ===== 化形：人形境没有后续过渡 =====
        var humanRealm = new RealmProgressionAsset<Yao>(4);
        profile.AddRealm(humanRealm);

        return profile;
    }

    // ===== 通用关卡与结算 =====

    /// <summary>妖力接近上限时允许调度进阶工作。</summary>
    private static bool IsYaoApproaching(ActorExtend actor, CultisysAsset<Yao> cultisys, ref Yao yao)
    {
        float maximum = actor.Base.stats[BaseStatses.MaxYaoPower.id];
        return maximum > 0f && yao.yao_power / maximum > 0.8f;
    }

    /// <summary>自然突破要求当前妖力接近上限。</summary>
    private static ProgressionGateResult RequireYaoPowerNearlyFull(
        ActorExtend actor, CultisysAsset<Yao> cultisys, ref Yao yao)
    {
        return yao.yao_power >= actor.Base.stats[BaseStatses.MaxYaoPower.id] - 0.1f
            ? ProgressionGateResult.Satisfied
            : ProgressionGateResult.NotReady("yao.yao_power_not_full");
    }

    /// <summary>生成一项身体稳定度门槛检查。</summary>
    private static ProgressionRequirement<Yao> RequireBodyStabilityAbove(float threshold)
    {
        return delegate(ActorExtend actor, CultisysAsset<Yao> cultisys, ref Yao yao)
        {
            return yao.BodyStability >= threshold
                ? ProgressionGateResult.Satisfied
                : ProgressionGateResult.NotReady("yao.body_stability_low");
        };
    }

    /// <summary>淬血期间要求没有正在消化或等待结算的精华。</summary>
    private static ProgressionGateResult RequireNoPendingDigestion(
        ActorExtend actor, CultisysAsset<Yao> cultisys, ref Yao yao)
    {
        if (!actor.E.TryGetComponent(out YaoDigestion digestion)) return ProgressionGateResult.Satisfied;
        return digestion.CountOccupied() == 0
            ? ProgressionGateResult.Satisfied
            : ProgressionGateResult.NotReady("yao.digestion_pending");
    }

    /// <summary>炼血三层次完成前不允许进入妖躯。</summary>
    private static ProgressionResolution RequireQuenchComplete(
        ActorExtend actor, CultisysAsset<Yao> cultisys, ref Yao yao)
    {
        return yao.MinorLevel >= YaoSetting.QuenchBloodSteps
            ? ProgressionResolution.Success()
            : ProgressionResolution.NoProgress(null, "yao.quench_incomplete");
    }

    /// <summary>没有随机成分的固定成功判定。</summary>
    private static ProgressionResolution ResolveYaoSuccess(ActorExtend actor, CultisysAsset<Yao> cultisys, ref Yao yao)
    {
        return ProgressionResolution.Success();
    }

    /// <summary>淬血判定：稳定度与消化历史满足时按稳定种子决定表达哪个器官。</summary>
    private static ProgressionResolution ResolveQuenchBlood(
        ActorExtend actor, CultisysAsset<Yao> cultisys, ref Yao yao)
    {
        return ProgressionResolution.Success(yao.Seed + yao.MinorLevel);
    }

    /// <summary>按上限六成支付妖力。</summary>
    private static void ConsumeYaoPowerSixty(ActorExtend actor, CultisysAsset<Yao> cultisys, ref Yao yao, object payload)
    {
        YaoResourceService.Spend(actor, ref yao, actor.Base.stats[BaseStatses.MaxYaoPower.id] * 0.6f);
    }

    /// <summary>按上限一半支付妖力。</summary>
    private static void ConsumeYaoPowerHalf(ActorExtend actor, CultisysAsset<Yao> cultisys, ref Yao yao, object payload)
    {
        YaoResourceService.Spend(actor, ref yao, actor.Base.stats[BaseStatses.MaxYaoPower.id] * 0.5f);
    }

    /// <summary>按上限八成支付妖力。</summary>
    private static void ConsumeYaoPowerEighty(ActorExtend actor, CultisysAsset<Yao> cultisys, ref Yao yao, object payload)
    {
        YaoResourceService.Spend(actor, ref yao, actor.Base.stats[BaseStatses.MaxYaoPower.id] * 0.8f);
    }

    /// <summary>淬血成功：提升一个器官的表达等级并记录小层次。</summary>
    private static void ApplyQuenchBlood(ActorExtend actor, CultisysAsset<Yao> cultisys, ref Yao yao, object payload)
    {
        int seed = payload is int value ? value : yao.Seed;
        if (YaoFormPlanService.UpgradeRandomOrgan(actor, ref yao, seed))
        {
            yao.MinorLevel++;
            YaoWorldLog.QuenchedBlood(actor, yao.MinorLevel);
        }
    }

    /// <summary>妖躯淬炼：优先补充结构容量，之后提高身体稳定度。</summary>
    private static void ApplyTemperBody(ActorExtend actor, CultisysAsset<Yao> cultisys, ref Yao yao, object payload)
    {
        if (yao.OrganCapacityBonus < YaoSetting.MaximumOrganCapacityBonus)
        {
            yao.OrganCapacityBonus++;
        }
        else
        {
            yao.BodyStability = Mathf.Min(
                YaoSetting.MaximumBodyStability, yao.BodyStability + 10f);
        }

        yao.MutationTolerance = Mathf.Min(1f, yao.MutationTolerance + 0.05f);
    }

    /// <summary>妖丹凝纯：提高妖丹强度与稳定度。</summary>
    private static void ApplyCoreRefinement(ActorExtend actor, CultisysAsset<Yao> cultisys, ref Yao yao, object payload)
    {
        if (actor.E.TryGetComponent(out YaoCore core))
        {
            core.Strength += 1f;
            core.Stability = Mathf.Min(100f, core.Stability + 5f);
            actor.E.GetComponent<YaoCore>() = core;
        }
    }

    /// <summary>凝丹成功：把准备期确定的妖丹写入核心数据。</summary>
    private static void ApplyCoreCondensation(ActorExtend actor, CultisysAsset<Yao> cultisys, ref Yao yao, object payload)
    {
        YaoCoreService.CommitCondensation(actor, ref yao, payload as YaoCoreCondensationResult);
    }

    /// <summary>化形劫成功：登记人形方案并切换活动形态。</summary>
    private static void ApplyHumanFormGranted(ActorExtend actor, CultisysAsset<Yao> cultisys, ref Yao yao, object payload)
    {
        YaoHumanFormService.CommitHumanForm(actor, ref yao);
    }

    /// <summary>要求妖丹品质与裂痕满足化形门槛。</summary>
    private static ProgressionGateResult RequireCoreForTransformation(
        ActorExtend actor, CultisysAsset<Yao> cultisys, ref Yao yao)
    {
        if (!actor.E.TryGetComponent(out YaoCore core))
            return ProgressionGateResult.Blocked("yao.core_missing");
        if (core.Cracks > 0)
            return ProgressionGateResult.NotReady("yao.core_cracked");
        return core.Quality >= 40f
            ? ProgressionGateResult.Satisfied
            : ProgressionGateResult.NotReady("yao.core_quality_low");
    }

    /// <summary>小境界失败：清空妖力并降低少量稳定度。</summary>
    private static void ApplyYaoBreakthroughFailure(
        ActorExtend actor, CultisysAsset<Yao> cultisys, ref Yao yao, object payload)
    {
        YaoResourceService.Clear(actor, ref yao);
        yao.BodyStability = Mathf.Max(0f, yao.BodyStability - 3f);
    }

    // ===== 过渡选择器 =====

    private static ProgressionTransitionAsset<Yao> SelectSingleTransition(
        ActorExtend actor, CultisysAsset<Yao> cultisys, ref Yao yao)
    {
        RealmProgressionAsset<Yao> realm = cultisys.Progression.GetRealm(yao.CurrLevel);
        return realm?.Transitions.Count > 0 ? realm.Transitions[0] : null;
    }

    private static ProgressionTransitionAsset<Yao> SelectBloodRealmTransition(
        ActorExtend actor, CultisysAsset<Yao> cultisys, ref Yao yao)
    {
        return yao.MinorLevel >= YaoSetting.QuenchBloodSteps
            ? cultisys.Progression.GetRealm(1)?.GetMajorTransition()
            : cultisys.Progression.GetRealm(1)?.GetMinorTransition();
    }

    private static ProgressionTransitionAsset<Yao> SelectBodyRealmTransition(
        ActorExtend actor, CultisysAsset<Yao> cultisys, ref Yao yao)
    {
        // 已在准备凝丹或已有妖丹方向时优先选择凝丹过渡。
        if (!string.IsNullOrEmpty(yao.CorePreparationPatternId) ||
            actor.E.HasComponent<YaoCore>())
            return cultisys.Progression.GetRealm(2)?.GetMajorTransition();
        return yao.OrganCapacityBonus >= YaoSetting.MaximumOrganCapacityBonus &&
               yao.BodyStability >= 80f
            ? cultisys.Progression.GetRealm(2)?.GetMajorTransition()
            : cultisys.Progression.GetRealm(2)?.GetMinorTransition();
    }

    private static ProgressionTransitionAsset<Yao> SelectCoreRealmTransition(
        ActorExtend actor, CultisysAsset<Yao> cultisys, ref Yao yao)
    {
        if (!actor.E.TryGetComponent(out YaoCore core)) return cultisys.Progression.GetRealm(3)?.GetMinorTransition();
        return core.Quality >= 40f && core.Cracks == 0
            ? cultisys.Progression.GetRealm(3)?.GetMajorTransition()
            : cultisys.Progression.GetRealm(3)?.GetMinorTransition();
    }
}
