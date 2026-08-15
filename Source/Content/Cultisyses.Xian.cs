using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cultiway.Const;
using Cultiway.Content.AIGC;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Core.Progression;
using Cultiway.Patch;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using strings;
using UnityEngine;

namespace Cultiway.Content;

public partial class Cultisyses
{
    /// <summary>仙道体系资产及其境界、属性和进阶规则入口。</summary>
    public static CultisysAsset<Xian> Xian { get; private set; }

    /// <summary>没有命理指定体系时默认允许选择的仙道体系标识集合。</summary>
    private static readonly HashSet<string> _default_xian = new(StringComparer.Ordinal) { nameof(Xian) };

    private void InitXian()
    {
        var progression = CreateXianProgressionProfile();
        Xian = (CultisysAsset<Xian>)Add(new CultisysAsset<Xian>(nameof(Xian), 20, new Xian(), progression,
            detailed_levels:
            [
                GetQiRefinementDetailedLevel,
                GetFoundationDetailedLevel,
                GetJindanDetailedLevel,
                null, null, null, null, null,
                null, null,
                null, null, null, null, null, null, null, null,
                null, null,
            ]));
        ProgressionService.Register(Xian);
        Xian.ConfigureOnAcquired(InitializeXianState);
        Xian.DisplayDetailProvider = AppendXianDisplayDetails;
        SetupXianDisplayStyle();
        LoadStatsForXian();


        RegisterAcquisitionRule(Xian.id, TryAcquireXian);
        ActorExtend.RegisterCachedStatsBuilder([Hotfixable](ae, stats) =>
        {
            if (!ae.TryGetComponent(out Xian xian)) return;

            var curr_level = xian.CurrLevel;

            stats.mergeStats(Xian.LevelAccumBaseStats[curr_level]);
            if (CoreFormationEffectResolver.TryGetFormation(ae, out var achievement))
                MergeCoreFormationStats(stats, achievement.Snapshot, achievement.Strength);

            // 仅主修功法提供属性加成
            var mainCultibook = ae.GetMainCultibook();
            if (mainCultibook != null)
            {
                var mastery = ae.GetMainCultibookMastery();
                // 根据掌握程度应用属性加成（0-100%映射到0-1）
                stats.MergeStats(mainCultibook.FinalStats, mastery / 100f);
            }
            
            
            //ae.tmp_all_skills.UnionWith(Xian.Skills[curr_level]);
        });
        PatchWindowCreatureInfo.RegisterInfoDisplay((a, sb) =>
        {
            if (a.HasCultisys<Xian>())
            {
                ref var xian_info = ref a.GetCultisys<Xian>();
                sb.AppendLine($"{xian_info.Asset.GetName()}: {xian_info.Asset.GetLevelName(xian_info.CurrLevel)}");
            }

            if (a.HasComponent<XianBase>())
            {
                ref XianBase xian_base = ref a.GetComponent<XianBase>();
                sb.AppendLine("筑基情况:");
                sb.AppendLine($"\t精: {xian_base.jing}");
                sb.AppendLine($"\t气: {xian_base.qi}");
                sb.AppendLine($"\t神: {xian_base.shen}");
                sb.AppendLine($"\t火: {xian_base.fire}");
                sb.AppendLine($"\t木: {xian_base.wood}");
                sb.AppendLine($"\t土: {xian_base.earth}");
                sb.AppendLine($"\t金: {xian_base.iron}");
                sb.AppendLine($"\t水: {xian_base.water}");
            }

            if (a.HasComponent<Jindan>())
            {
                ref Jindan jindan = ref a.GetComponent<Jindan>();
                sb.AppendLine($"金丹: {jindan.GetName()}");
            }

            if (a.HasComponent<Yuanying>())
            {
                ref Yuanying yuanying = ref a.GetComponent<Yuanying>();
                sb.AppendLine($"元婴: {yuanying.GetName()}");
            }
        });
    }

    /// <summary>向通用修炼体系详情追加仙道资源与阶段性结构。</summary>
    private static void AppendXianDisplayDetails(ActorExtend actor, ICollection<CultisysDisplayLine> lines)
    {
        ref var xian = ref actor.GetCultisys<Xian>();
        if (actor.HasElementRoot())
        {
            ref var root = ref actor.GetElementRoot();
            lines.Add(new CultisysDisplayLine(
                Xian.DisplayStyle.category_label_key,
                string.Format("Cultiway.CultisysTooltip.Format.ElementRoot".Localize(),
                    root.Type.GetName(Xian), root.GetStrength())));
        }
        lines.Add(CultisysDisplayLine.CreateProgress(
            "Cultiway.CultisysTooltip.Resource.Wakan",
            xian.wakan,
            actor.Base.stats[BaseStatses.MaxWakan.id],
            "cultiway/icons/iconWakan",
            "#009EC7"));

        CoreFormationSnapshot qiFormation = actor.GetComponent<QiRefinementState>().formation;
        string qiName = qiFormation.IsFinalized
            ? qiFormation.canonical_name
            : qiFormation.IsValid
                ? "Cultiway.RealmPage.QiRefinement.Forming".Localize()
                : "Cultiway.RealmPage.QiRefinement.Unformed".Localize();
        lines.Add(new CultisysDisplayLine(
            "Cultiway.CultisysTooltip.Xian.QiRefinement",
            string.Format("Cultiway.CultisysTooltip.Format.QiRefinement".Localize(),
                qiName,
                qiFormation.IsFinalized ? qiFormation.quality.GetName() : "--",
                qiFormation.IsValid ? qiFormation.refinement : 0,
                qiFormation.IsValid ? qiFormation.strength : 0f)));

        if (actor.TryGetComponent(out XianBase xianBase))
        {
            int completed = CountFoundationParts(ref xianBase);
            CoreFormationSnapshot foundation = xianBase.formation;
            lines.Add(new CultisysDisplayLine(
                "Cultiway.CultisysTooltip.Xian.Foundation",
                string.Format("Cultiway.CultisysTooltip.Format.Foundation".Localize(),
                    foundation.IsFinalized
                        ? foundation.canonical_name
                        : "Cultiway.RealmPage.Foundation.Forming".Localize(),
                    foundation.IsFinalized ? foundation.quality.GetName() : "--",
                    completed,
                    xianBase.GetStrength())));
        }
        if (actor.HasComponent<Jindan>())
        {
            ref var jindan = ref actor.GetComponent<Jindan>();
            lines.Add(new CultisysDisplayLine(
                "Cultiway.CultisysTooltip.Xian.Jindan",
                string.Format("Cultiway.CultisysTooltip.Format.Jindan".Localize(), jindan.GetName(),
                    jindan.GetQuality().GetName(), jindan.stage, jindan.strength)));
        }
        if (actor.HasComponent<Yuanying>())
        {
            ref var yuanying = ref actor.GetComponent<Yuanying>();
            lines.Add(new CultisysDisplayLine(
                "Cultiway.CultisysTooltip.Xian.Yuanying",
                string.Format("Cultiway.CultisysTooltip.Format.Yuanying".Localize(), yuanying.GetName(),
                    yuanying.GetQuality().GetName(), yuanying.strength)));
        }
    }

    /// <summary>把组合快照中的属性系数按当前金丹或元婴强度写入角色属性。</summary>
    private static void MergeCoreFormationStats(BaseStats target, CoreFormationSnapshot formation, float strength)
    {
        foreach (var stat in formation.stats ?? [])
        {
            if (string.IsNullOrEmpty(stat.stat_id) || stat.value == 0f) continue;
            target[stat.stat_id] += stat.value * strength;
        }
    }

    /// <summary>按仙道的种族、灵根和设置约束，为尚未修仙的角色接入仙道。</summary>
    private static bool TryAcquireXian(ActorExtend ae)
    {
        if (ae.HasCultisys<Xian>() || !ae.HasElementRoot()) return false;
        if (ae.HasCultisys<Knight>()) return false; // 骑士与修仙互斥
        if (!GetAvailableCultisysIds(ae).Contains(nameof(Xian))) return false;
        ElementRoot elementRoot = ae.GetElementRoot();
        if (!ContentSetting.AllXian && elementRoot.Type == ModClass.L.ElementRootLibrary.Common) return false;

        ae.NewCultisys(Xian);
        ModClass.I.WorldRecord.CheckAndLogFirstLevelup(Xian.id, ae, ref ae.GetCultisys<Xian>());
        if (ae.Base.asset == Actors.Plant)
        {
            PlantNameGenerator.Instance.NewNameGenerateRequest(GetPlantNameParams(ae,
                Xian.GetLevelName(ae.GetCultisys<Xian>().CurrLevel), elementRoot.Type.GetName()), ae.Base);
        }
        CultivationAchievementService.OnXianAcquired(ae);
        return true;
    }

    private static void SetupXianDisplayStyle()
    {
        Xian.DisplayStyle = new ElementRootDisplayStyle
        {
            category_label_key   = "Cultiway.ERStyle.Xian.Category",
            components_label_key = "Cultiway.ERStyle.Xian.Components",
            overall_label_key    = "Cultiway.ERStyle.Xian.Overall",
            page_title_key       = "ElementRootPage",
            stage_count          = 4,
            level_per_stage      = 9,
            stage_name_keys      = Enumerable.Range(0, 4)
                .Select(i => $"Cultiway.Stage.{i}").ToArray(),
            level_name_keys      = Enumerable.Range(0, 9)
                .Select(i => $"Cultiway.Level.{i}").ToArray(),
            level_format         = "{stage}阶{level}",
            element_root_name_prefix = "Cultiway.ER",
            element_root_desc_prefix = "Cultiway.ER"
        };
    }

    /// <summary>
    ///     声明仙道前三个已实现境界的进阶图。后续境界尚无规则，因此不会生成空过渡。
    /// </summary>
    private static CultisysProgressionProfile<Xian> CreateXianProgressionProfile()
    {
        var profile = new CultisysProgressionProfile<Xian>
        {
            TransferExtraState = TransferXianExtraState
        };

        var circulateQi = new ProgressionTransitionAsset<Xian>(
            "xian.circulate_qi", ProgressionKind.Minor, XianLevels.QiRefinement, XianLevels.QiRefinement)
        {
            IsApproaching = IsXianApproachingBreakthrough,
            ResolveNatural = ResolveQiCirculation,
            ResolveGrant = ResolveQiCirculation
        };
        circulateQi.Requirements.Add(RequireFullWakan);
        circulateQi.Requirements.Add(RequireElementRoot);
        circulateQi.SuccessCosts.Add(ApplyQiLayerCost);
        circulateQi.Transformations.Add(ApplyQiCirculation);

        var enterFoundation = new ProgressionTransitionAsset<Xian>(
            "xian.enter_foundation", ProgressionKind.Major, XianLevels.QiRefinement, XianLevels.XianBase)
        {
            IsApproaching = IsXianApproachingBreakthrough,
            ResolveNatural = ResolveSuccess,
            ResolveGrant = ResolveSuccess
        };
        enterFoundation.Requirements.Add(RequireFullWakan);
        enterFoundation.Requirements.Add(RequireElementRoot);
        enterFoundation.Requirements.Add(RequireQiFoundationReady);
        enterFoundation.SuccessCosts.Add(ApplyQiFoundationCost);
        enterFoundation.Transformations.Add(CreateFoundationEmbryo);

        var qiRefinementRealm = new RealmProgressionAsset<Xian>(XianLevels.QiRefinement);
        qiRefinementRealm.Transitions.Add(circulateQi);
        qiRefinementRealm.Transitions.Add(enterFoundation);
        qiRefinementRealm.SelectForQuery = SelectQiRefinementTransition;
        qiRefinementRealm.SelectForNaturalAttempt = SelectQiRefinementTransition;
        qiRefinementRealm.SelectForMajorGrant = SelectQiRefinementGrantTransition;
        qiRefinementRealm.SynchronizationEffects.Add(NormalizeQiRefinementRealm);
        profile.AddRealm(qiRefinementRealm);

        var buildFoundation = new ProgressionTransitionAsset<Xian>(
            "xian.build_foundation", ProgressionKind.Minor, XianLevels.XianBase, XianLevels.XianBase)
        {
            IsApproaching = IsXianApproachingBreakthrough,
            ResolveNatural = ResolveFoundationStep,
            ResolveGrant = ResolveFoundationStep
        };
        buildFoundation.Requirements.Add(RequireFullWakan);
        buildFoundation.Requirements.Add(RequireElementRoot);
        buildFoundation.SuccessCosts.Add(ApplyFoundationStepCost);
        buildFoundation.Transformations.Add(ApplyFoundationStep);

        var formJindan = new ProgressionTransitionAsset<Xian>(
            "xian.form_jindan", ProgressionKind.Major, XianLevels.XianBase, XianLevels.Jindan)
        {
            IsApproaching = IsXianApproachingBreakthrough,
            ResolveNatural = ResolveJindan,
            ResolveGrant = ResolveGrantedJindan
        };
        formJindan.Requirements.Add(RequireFullWakan);
        formJindan.Requirements.Add(RequireElementRoot);
        formJindan.Transformations.Add(ApplyJindanTransformation);
        formJindan.Rewards.Add(ApplyJindanReward);
        formJindan.FailureEffects.Add(ApplyLargeBreakthroughFailure);

        var foundationRealm = new RealmProgressionAsset<Xian>(XianLevels.XianBase);
        foundationRealm.Transitions.Add(buildFoundation);
        foundationRealm.Transitions.Add(formJindan);
        foundationRealm.SelectForQuery = SelectFoundationTransition;
        foundationRealm.SelectForNaturalAttempt = SelectFoundationTransition;
        foundationRealm.SelectForMajorGrant = SelectFoundationTransition;
        foundationRealm.SynchronizationEffects.Add(NormalizeFoundationRealm);
        profile.AddRealm(foundationRealm);

        var refineJindan = new ProgressionTransitionAsset<Xian>(
            "xian.refine_jindan", ProgressionKind.Minor, XianLevels.Jindan, XianLevels.Jindan)
        {
            IsApproaching = IsXianApproachingBreakthrough,
            ResolveNatural = ResolveJindanRefinement,
            ResolveGrant = ResolveGrantedJindanRefinement
        };
        refineJindan.Requirements.Add(RequireFullWakan);
        refineJindan.Requirements.Add(RequireJindan);
        refineJindan.SuccessCosts.Add(ApplyJindanRefinementCost);
        refineJindan.Transformations.Add(ApplyJindanRefinement);
        refineJindan.Rewards.Add(ApplyJindanRefinementReward);
        refineJindan.FailureEffects.Add(ApplySmallBreakthroughFailure);
        refineJindan.NoProgressEffects.Add(ApplyJindanRefinementCapCost);

        var formYuanying = new ProgressionTransitionAsset<Xian>(
            "xian.form_yuanying", ProgressionKind.Major, XianLevels.Jindan, XianLevels.Yuanying)
        {
            IsApproaching = IsXianApproachingBreakthrough,
            ResolveNatural = ResolveYuanying,
            ResolveGrant = ResolveGrantedYuanying
        };
        formYuanying.Requirements.Add(RequireFullWakan);
        formYuanying.Requirements.Add(RequireJindan);
        formYuanying.Transformations.Add(ApplyYuanyingTransformation);
        formYuanying.Rewards.Add(ApplyYuanyingReward);

        var jindanRealm = new RealmProgressionAsset<Xian>(XianLevels.Jindan);
        jindanRealm.Transitions.Add(refineJindan);
        jindanRealm.Transitions.Add(formYuanying);
        jindanRealm.SelectForQuery = SelectJindanTransitionForQuery;
        jindanRealm.SelectForNaturalAttempt = SelectJindanTransitionForAttempt;
        jindanRealm.SelectForMajorGrant = SelectJindanTransitionForGrant;
        jindanRealm.SynchronizationEffects.Add(NormalizeJindanRealm);
        profile.AddRealm(jindanRealm);

        var yuanyingRealm = new RealmProgressionAsset<Xian>(XianLevels.Yuanying);
        yuanyingRealm.SynchronizationEffects.Add(NormalizeYuanyingRealm);
        profile.AddRealm(yuanyingRealm);

        return profile;
    }

    /// <summary>灵气达到预突破比例时允许 AI 调度进阶任务。</summary>
    private static bool IsXianApproachingBreakthrough(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                       ref Xian component)
    {
        var maxWakan = actor.Base.stats[BaseStatses.MaxWakan.id];
        return maxWakan > 0f
               && component.wakan / maxWakan > XianSetting.CommonPreUpgradeWakanRatio;
    }

    /// <summary>自然突破要求当前灵气接近角色灵气上限。</summary>
    private static ProgressionGateResult RequireFullWakan(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                           ref Xian component)
    {
        return component.wakan >= actor.Base.stats[BaseStatses.MaxWakan.id] - 0.1f
            ? ProgressionGateResult.Satisfied
            : ProgressionGateResult.NotReady("xian.wakan_not_full");
    }

    /// <summary>要求角色具有灵根；缺失灵根属于无法自然恢复的硬性阻断。</summary>
    private static ProgressionGateResult RequireElementRoot(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                             ref Xian component)
    {
        return actor.HasElementRoot()
            ? ProgressionGateResult.Satisfied
            : ProgressionGateResult.Blocked("xian.element_root_missing");
    }

    /// <summary>用于没有随机失败和额外载荷的固定成功过渡。</summary>
    private static ProgressionResolution ResolveSuccess(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                         ref Xian component)
    {
        return ProgressionResolution.Success();
    }

    /// <summary>小境界失败时清空灵气，并按小突破失败来源随机改进法术。</summary>
    private static void ApplySmallBreakthroughFailure(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                      ref Xian component, object payload)
    {
        WakanResourceService.Clear(actor, ref component);
        actor.EnhanceSkillRandomly(SkillEnhanceSources.SmallUpgradeFailed);
    }

    /// <summary>大境界失败时清空灵气，并按大突破失败来源随机改进法术。</summary>
    private static void ApplyLargeBreakthroughFailure(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                      ref Xian component, object payload)
    {
        WakanResourceService.Clear(actor, ref component);
        actor.EnhanceSkillRandomly(SkillEnhanceSources.LargeUpgradeFailed);
    }

    /// <summary>传承仙道体系时复制修炼实践、个人资源及各境界的完整成果谱系。</summary>
    private static void TransferXianExtraState(ActorExtend source, ActorExtend target,
                                               ref Xian sourceComponent, ref Xian targetComponent)
    {
        target.GetComponent<CultivationPracticeState>() =
            source.GetComponent<CultivationPracticeState>().DeepClone();
        target.GetComponent<CultivationResourceState>() = source.GetComponent<CultivationResourceState>();
        TransferQiRefinementState(source, target);
        TransferFoundation(source, target);
        TransferJindan(source, target);
        TransferYuanying(source, target);
    }

    private void LoadStatsForXian()
    {
        var csv = CSVUtils.ReadCSV(File.ReadAllText(Path.Combine(ModClass.I.GetDeclaration().FolderPath,
            XianSetting.StatsPath)));
        var offset = 0;
        var keys = csv[offset++];
        _ = csv[offset++];
        for (int i = 0; i < Xian.LevelNumber; i++)
        {
            var line = csv[i + offset];
            var stats = Xian.LevelBaseStats[i];
            stats.clear();
            for (int j = 0; j < keys.Length; j++)
            {
                var key = keys[j];
                if (!AssetManager.base_stats_library.Contains(key)) continue;

                stats[key] = float.Parse(line[j]);
            }
        }

        Xian.UpdateAccumStats();
    }

    private static string[] GetPlantNameParams(ActorExtend ae, params string[] cultivationFactors)
    {
        List<string> param = new();
        foreach (var factor in cultivationFactors)
        {
            if (string.IsNullOrEmpty(factor)) continue;
            param.Add(factor);
        }

        var traits = GetPlantTraitNames(ae);
        if (traits.Count > 0)
        {
            StringBuilder sb = new();
            sb.Append(PlantNameGenerator.TraitPrefix);
            for (int i = 0; i < traits.Count; i++)
            {
                sb.Append(traits[i]);
                if (i < traits.Count - 1) sb.Append('、');
            }
            param.Add(sb.ToString());
        }

        return param.ToArray();
    }

    private static List<string> GetPlantTraitNames(ActorExtend ae)
    {
        List<string> traits = new();
        var data = ae.Base.data;
        if (data?.saved_traits == null || data.saved_traits.Count == 0) return traits;

        foreach (var trait_id in data.saved_traits)
        {
            var trait_asset = AssetManager.traits.get(trait_id);
            if (trait_asset == null) continue;
            if (trait_asset.group_id == ActorTraitGroups.System.id) continue;
            var name = trait_asset.getTranslatedName();
            if (string.IsNullOrEmpty(name)) continue;

            traits.Add(name);
        }
        traits.Shuffle();
        if (traits.Count >= 3) traits = traits.GetRange(0, 3);

        traits.Sort((a, b) => string.Compare(a, b, StringComparison.Ordinal));
        return traits;
    }
}
