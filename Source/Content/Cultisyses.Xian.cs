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
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Patch;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
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

    /// <summary>授予结婴前必须逐项结算到的最低金丹淬炼层数。</summary>
    private const int YuanyingRequiredJindanStage = 9;

    private void InitXian()
    {
        var progression = CreateXianProgressionProfile();
        Xian = (CultisysAsset<Xian>)Add(new CultisysAsset<Xian>(nameof(Xian), 20, new Xian(), progression,
            detailed_levels:
            [
                null, [Hotfixable](ae) =>
                {
                    var res = 0f;
                    if (ae.HasComponent<XianBase>())
                    {
                        res += 0.01f;
                        var xian_base = ae.GetComponent<XianBase>();
                        if (xian_base.jing > 0) res += 0.01f;
                        if (xian_base.qi > 0) res += 0.01f;
                        if (xian_base.shen > 0) res += 0.01f;
                        if (xian_base.fire > 0) res += 0.01f;
                        if (xian_base.wood > 0) res += 0.01f;
                        if (xian_base.earth > 0) res += 0.01f;
                        if (xian_base.iron > 0) res += 0.01f;
                        if (xian_base.water > 0) res += 0.01f;
                    }
                    return res;
                },
                [Hotfixable](ae) =>
                {
                    var res = 0f;
                    if (ae.HasComponent<Jindan>())
                    {
                        res += 0.01f;
                        var jindan = ae.GetComponent<Jindan>();
                        res += 0.9f * (1 - 1f / (jindan.stage + 1));
                    }
                    return res;
                }, null, null, null, null, null,
                null, null,
                null, null, null, null, null, null, null, null,
                null, null,
            ]));
        ProgressionService.Register(Xian);
        Xian.DisplayDetailProvider = AppendXianDisplayDetails;
        SetupXianDisplayStyle();
        LoadStatsForXian();


        RegisterAcquisitionRule(Xian.id, TryAcquireXian);
        ActorExtend.RegisterCachedStatsBuilder([Hotfixable](ae, stats) =>
        {
            if (!ae.TryGetComponent(out Xian xian)) return;

            var curr_level = xian.CurrLevel;

            stats.mergeStats(Xian.LevelAccumBaseStats[curr_level]);
            if (ae.TryGetComponent(out XianBase xian_base))
            {

            }
            if (ae.TryGetComponent(out Jindan jindan))
            {
                var jindan_asset = jindan.Type;
                stats.MergeStats(jindan_asset.Stats, jindan.strength);
                if (!string.IsNullOrEmpty(jindan_asset.wrapped_skill_id))
                {
                    //ae.tmp_all_skills.Add(jindan_asset.wrapped_skill_id);
                    return;
                }
            }

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
                sb.AppendLine($"金丹: {jindan.Type.GetName()}");
            }

            if (a.HasComponent<Yuanying>())
            {
                ref Yuanying yuanying = ref a.GetComponent<Yuanying>();
                sb.AppendLine($"元婴: {yuanying.Type.GetName()}");
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

        if (actor.TryGetComponent(out XianBase xianBase))
        {
            int completed = CountFoundationParts(ref xianBase);
            lines.Add(new CultisysDisplayLine(
                "Cultiway.CultisysTooltip.Xian.Foundation",
                string.Format("Cultiway.CultisysTooltip.Format.Foundation".Localize(), completed,
                    xianBase.GetStrength())));
        }
        if (actor.TryGetComponent(out Jindan jindan))
        {
            string name = jindan.Type?.GetName() ?? jindan.jindan_type;
            lines.Add(new CultisysDisplayLine(
                "Cultiway.CultisysTooltip.Xian.Jindan",
                string.Format("Cultiway.CultisysTooltip.Format.Jindan".Localize(), name, jindan.stage,
                    jindan.strength)));
        }
        if (actor.TryGetComponent(out Yuanying yuanying))
        {
            string name = yuanying.Type?.GetName() ?? yuanying.yuanying_type;
            lines.Add(new CultisysDisplayLine(
                "Cultiway.CultisysTooltip.Xian.Yuanying",
                string.Format("Cultiway.CultisysTooltip.Format.Yuanying".Localize(), name,
                    yuanying.strength)));
        }
    }

    private static int CountFoundationParts(ref XianBase xianBase)
    {
        int count = 0;
        if (xianBase.jing != 0f) count++;
        if (xianBase.qi != 0f) count++;
        if (xianBase.shen != 0f) count++;
        if (xianBase.iron != 0f) count++;
        if (xianBase.wood != 0f) count++;
        if (xianBase.water != 0f) count++;
        if (xianBase.fire != 0f) count++;
        if (xianBase.earth != 0f) count++;
        return count;
    }

    /// <summary>按仙道的种族、灵根和设置约束，为尚未修仙的角色接入仙道。</summary>
    private static bool TryAcquireXian(ActorExtend ae)
    {
        if (ae.HasCultisys<Xian>() || !ae.HasElementRoot()) return false;
        if (ae.HasCultisys<Knight>()) return false; // 骑士与修仙互斥
        if (!GetAvailableCultisysIds(ae).Contains(nameof(Xian))) return false;
        ref var elementRoot = ref ae.GetElementRoot();
        if (!ContentSetting.AllXian && elementRoot.Type == ModClass.L.ElementRootLibrary.Common) return false;

        ae.NewCultisys(Xian);
        ModClass.I.WorldRecord.CheckAndLogFirstLevelup(Xian.id, ae, ref ae.GetCultisys<Xian>());
        if (ae.Base.asset == Actors.Plant)
        {
            PlantNameGenerator.Instance.NewNameGenerateRequest(GetPlantNameParams(ae,
                Xian.GetLevelName(ae.GetCultisys<Xian>().CurrLevel), elementRoot.Type.GetName()), ae.Base);
        }
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

        var enterFoundation = new ProgressionTransitionAsset<Xian>(
            "xian.enter_foundation", ProgressionKind.Major, XianLevels.QiRefinement, XianLevels.XianBase)
        {
            IsApproaching = IsXianApproachingBreakthrough,
            ResolveNatural = ResolveSuccess,
            ResolveGrant = ResolveSuccess
        };
        enterFoundation.Requirements.Add(RequireFullWakan);
        var qiRefinementRealm = new RealmProgressionAsset<Xian>(XianLevels.QiRefinement);
        qiRefinementRealm.Transitions.Add(enterFoundation);
        qiRefinementRealm.SynchronizationEffects.Add(NormalizeQiRefinementRealm);
        profile.AddRealm(qiRefinementRealm);

        var buildFoundation = new ProgressionTransitionAsset<Xian>(
            "xian.build_foundation", ProgressionKind.Minor, XianLevels.XianBase, XianLevels.XianBase)
        {
            IsApproaching = IsXianApproachingBreakthrough,
            ResolveNatural = ResolveFoundationStep,
            ResolveGrant = ResolveGrantedFoundationStep
        };
        buildFoundation.Requirements.Add(RequireFullWakan);
        buildFoundation.Requirements.Add(RequireElementRoot);
        buildFoundation.AttemptCosts.Add(EnsureXianBase);
        buildFoundation.Transformations.Add(ApplyFoundationStep);
        buildFoundation.FailureEffects.Add(ApplySmallBreakthroughFailure);

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

    /// <summary>要求角色持有金丹组件；缺失时拒绝淬炼或结婴。</summary>
    private static ProgressionGateResult RequireJindan(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                        ref Xian component)
    {
        return actor.HasComponent<Jindan>()
            ? ProgressionGateResult.Satisfied
            : ProgressionGateResult.Blocked("xian.jindan_missing");
    }

    /// <summary>用于没有随机失败和额外载荷的固定成功过渡。</summary>
    private static ProgressionResolution ResolveSuccess(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                         ref Xian component)
    {
        return ProgressionResolution.Success();
    }

    /// <summary>筑基未完成时选择逐项筑基，全部筑基项完成后选择结丹。</summary>
    private static ProgressionTransitionAsset<Xian> SelectFoundationTransition(ActorExtend actor,
        CultisysAsset<Xian> cultisys, ref Xian component)
    {
        var realm = cultisys.Progression.GetRealm(XianLevels.XianBase);
        return IsFoundationComplete(actor)
            ? realm.GetMajorTransition()
            : realm.GetMinorTransition();
    }

    /// <summary>无副作用地选择金丹境界的展示候选，不执行普通结婴概率抽取。</summary>
    private static ProgressionTransitionAsset<Xian> SelectJindanTransitionForQuery(ActorExtend actor,
        CultisysAsset<Xian> cultisys, ref Xian component)
    {
        var realm = cultisys.Progression.GetRealm(XianLevels.Jindan);
        if (!actor.TryGetComponent(out Jindan jindan)) return realm.GetMinorTransition();
        return jindan.stage >= YuanyingRequiredJindanStage || MustAttemptYuanyingForLifespan(actor.Base)
            ? realm.GetMajorTransition()
            : realm.GetMinorTransition();
    }

    /// <summary>直接授予大境界时先把金丹逐次淬炼到九转，再允许提交结婴过渡。</summary>
    private static ProgressionTransitionAsset<Xian> SelectJindanTransitionForGrant(ActorExtend actor,
        CultisysAsset<Xian> cultisys, ref Xian component)
    {
        var realm = cultisys.Progression.GetRealm(XianLevels.Jindan);
        return actor.TryGetComponent(out Jindan jindan) && jindan.stage >= YuanyingRequiredJindanStage
            ? realm.GetMajorTransition()
            : realm.GetMinorTransition();
    }

    /// <summary>自然尝试时按金丹淬炼层数、性格概率和寿命压力决定淬炼或结婴。</summary>
    private static ProgressionTransitionAsset<Xian> SelectJindanTransitionForAttempt(ActorExtend actor,
        CultisysAsset<Xian> cultisys, ref Xian component)
    {
        var realm = cultisys.Progression.GetRealm(XianLevels.Jindan);
        if (!actor.TryGetComponent(out Jindan jindan)) return realm.GetMinorTransition();

        var shouldFormYuanying = jindan.stage >= YuanyingRequiredJindanStage
                                 && Randy.randomChance(actor.Base.hasTrait(WorldboxGame.ActorTraits.Ambitious.id)
                                     ? 0.13f
                                     : 0.5f);
        return shouldFormYuanying || MustAttemptYuanyingForLifespan(actor.Base)
            ? realm.GetMajorTransition()
            : realm.GetMinorTransition();
    }

    /// <summary>非长生角色寿命消耗超过九成时强制尝试结婴。</summary>
    private static bool MustAttemptYuanyingForLifespan(Actor actor)
    {
        if (actor.hasTrait(ActorTraits.Immortal.id)) return false;
        var lifespan = actor.stats[S.lifespan];
        return lifespan > 0f && actor.data.getAge() / lifespan > 0.9f;
    }

    /// <summary>检查三花与五气是否都已经写入非零筑基强度。</summary>
    private static bool IsFoundationComplete(ActorExtend actor)
    {
        if (!actor.TryGetComponent(out XianBase xianBase)) return false;
        return xianBase.jing != 0f
               && xianBase.qi != 0f
               && xianBase.shen != 0f
               && xianBase.fire != 0f
               && xianBase.wood != 0f
               && xianBase.earth != 0f
               && xianBase.iron != 0f
               && xianBase.water != 0f;
    }

    /// <summary>自然筑基尝试前确保角色持有用于保存三花五气的 XianBase 组件。</summary>
    private static void EnsureXianBase(ActorExtend actor, CultisysAsset<Xian> cultisys, ref Xian component,
                                       object payload)
    {
        if (!actor.HasComponent<XianBase>()) actor.AddComponent(new XianBase());
    }

    /// <summary>按固定顺序选择下一筑基项，并依据智力或对应灵根强度判定自然筑基。</summary>
    private static ProgressionResolution ResolveFoundationStep(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                                ref Xian component)
    {
        var xianBase = actor.GetComponent<XianBase>();
        var part = GetNextFoundationPart(ref xianBase);
        if (part == FoundationPart.None) return ProgressionResolution.NoProgress();

        var aptitude = GetFoundationAptitude(actor, part);
        var isThreeHua = part is FoundationPart.Jing or FoundationPart.Qi or FoundationPart.Shen;
        var allowed = isThreeHua
            ? Mathf.Abs(RdUtils.NextNormal_0_6()) < aptitude
            : Mathf.Abs(RdUtils.NextStdNormal()) < aptitude;
        if (!allowed) return ProgressionResolution.Failure();

        var value = Mathf.Abs(RdUtils.NextStdNormal() * aptitude);
        return ProgressionResolution.Success(new FoundationStepPayload(part, value));
    }

    /// <summary>直接授予下一筑基项，强度取对应资质绝对值且至少为 0.01。</summary>
    private static ProgressionResolution ResolveGrantedFoundationStep(ActorExtend actor,
        CultisysAsset<Xian> cultisys, ref Xian component)
    {
        var xianBase = actor.TryGetComponent(out XianBase existing) ? existing : default;
        var part = GetNextFoundationPart(ref xianBase);
        if (part == FoundationPart.None) return ProgressionResolution.NoProgress();
        return ProgressionResolution.Success(new FoundationStepPayload(part,
            Mathf.Max(Mathf.Abs(GetFoundationAptitude(actor, part)), 0.01f)));
    }

    /// <summary>按精、气、神、火、木、土、金、水顺序取得第一个尚未完成的筑基项。</summary>
    private static FoundationPart GetNextFoundationPart(ref XianBase xianBase)
    {
        if (xianBase.jing == 0f) return FoundationPart.Jing;
        if (xianBase.qi == 0f) return FoundationPart.Qi;
        if (xianBase.shen == 0f) return FoundationPart.Shen;
        if (xianBase.fire == 0f) return FoundationPart.Fire;
        if (xianBase.wood == 0f) return FoundationPart.Wood;
        if (xianBase.earth == 0f) return FoundationPart.Earth;
        if (xianBase.iron == 0f) return FoundationPart.Iron;
        if (xianBase.water == 0f) return FoundationPart.Water;
        return FoundationPart.None;
    }

    /// <summary>三花使用智力，五气使用灵根对应元素强度，计算指定筑基项的资质。</summary>
    private static float GetFoundationAptitude(ActorExtend actor, FoundationPart part)
    {
        if (part is FoundationPart.Jing or FoundationPart.Qi or FoundationPart.Shen)
            return actor.GetStat(S.intelligence);
        if (!actor.HasElementRoot()) return 0f;

        ref var root = ref actor.GetElementRoot();
        return part switch
        {
            FoundationPart.Fire => root.Fire,
            FoundationPart.Wood => root.Wood,
            FoundationPart.Earth => root.Earth,
            FoundationPart.Iron => root.Iron,
            FoundationPart.Water => root.Water,
            _ => 0f
        };
    }

    /// <summary>把成功判定载荷中的强度写入对应 XianBase 筑基字段。</summary>
    private static void ApplyFoundationStep(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                            ref Xian component, object payload)
    {
        var step = (FoundationStepPayload)payload;
        ref var xianBase = ref actor.GetOrAddComponent<XianBase>();
        switch (step.Part)
        {
            case FoundationPart.Jing: xianBase.jing = step.Value; break;
            case FoundationPart.Qi: xianBase.qi = step.Value; break;
            case FoundationPart.Shen: xianBase.shen = step.Value; break;
            case FoundationPart.Fire: xianBase.fire = step.Value; break;
            case FoundationPart.Wood: xianBase.wood = step.Value; break;
            case FoundationPart.Earth: xianBase.earth = step.Value; break;
            case FoundationPart.Iron: xianBase.iron = step.Value; break;
            case FoundationPart.Water: xianBase.water = step.Value; break;
        }
    }

    /// <summary>根据三花五气总强度判定自然结丹，并生成匹配的金丹类型与强度。</summary>
    private static ProgressionResolution ResolveJindan(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                        ref Xian component)
    {
        var xianBase = actor.GetComponent<XianBase>();
        var strength = xianBase.GetFiveQiStrength() * xianBase.GetThreeHuaStrength();
        if (RdUtils.NextNormal_0_6() > strength) return ProgressionResolution.Failure();

        var localBase = xianBase;
        var jindan = Libraries.Manager.JindanLibrary.GetJindan(actor, ref localBase);
        return ProgressionResolution.Success(new JindanPayload(localBase, jindan, strength));
    }

    /// <summary>直接结丹时先补齐筑基；有灵根则匹配金丹，无灵根则使用普通金丹。</summary>
    private static ProgressionResolution ResolveGrantedJindan(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                               ref Xian component)
    {
        var xianBase = CompleteFoundationForGrant(actor);
        var localBase = xianBase;
        var jindan = actor.HasElementRoot()
            ? Libraries.Manager.JindanLibrary.GetJindan(actor, ref localBase)
            : Jindans.Common;
        var strength = localBase.GetFiveQiStrength() * localBase.GetThreeHuaStrength();
        return ProgressionResolution.Success(new JindanPayload(localBase, jindan, strength));
    }

    /// <summary>以角色当前资质补齐所有为零的三花五气字段，并保证每项至少为 0.01。</summary>
    private static XianBase CompleteFoundationForGrant(ActorExtend actor)
    {
        var xianBase = actor.TryGetComponent(out XianBase existing) ? existing : default;
        var intelligence = Mathf.Max(Mathf.Abs(actor.GetStat(S.intelligence)), 0.01f);
        if (xianBase.jing == 0f) xianBase.jing = intelligence;
        if (xianBase.qi == 0f) xianBase.qi = intelligence;
        if (xianBase.shen == 0f) xianBase.shen = intelligence;
        if (xianBase.fire == 0f) xianBase.fire = Mathf.Max(GetFoundationAptitude(actor, FoundationPart.Fire), 0.01f);
        if (xianBase.wood == 0f) xianBase.wood = Mathf.Max(GetFoundationAptitude(actor, FoundationPart.Wood), 0.01f);
        if (xianBase.earth == 0f) xianBase.earth = Mathf.Max(GetFoundationAptitude(actor, FoundationPart.Earth), 0.01f);
        if (xianBase.iron == 0f) xianBase.iron = Mathf.Max(GetFoundationAptitude(actor, FoundationPart.Iron), 0.01f);
        if (xianBase.water == 0f) xianBase.water = Mathf.Max(GetFoundationAptitude(actor, FoundationPart.Water), 0.01f);
        return xianBase;
    }

    /// <summary>提交结丹所需的筑基数据，并创建或替换角色金丹组件。</summary>
    private static void ApplyJindanTransformation(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                   ref Xian component, object payload)
    {
        var data = (JindanPayload)payload;
        ref var xianBase = ref actor.GetOrAddComponent<XianBase>();
        xianBase = data.XianBase;
        ref var jindan = ref actor.GetOrAddComponent<Jindan>();
        jindan = new Jindan(data.Asset.id, data.Strength);
    }

    /// <summary>结丹后处理植物命名，并从金丹定义中抽取和学习一项自带法术。</summary>
    private static void ApplyJindanReward(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                          ref Xian component, object payload)
    {
        var data = (JindanPayload)payload;
        if (actor.Base.asset == Actors.Plant)
        {
            var rootName = actor.HasElementRoot() ? actor.GetElementRoot().Type.GetName() : null;
            PlantNameGenerator.Instance.NewNameGenerateRequest(
                GetPlantNameParams(actor, cultisys.GetLevelName(XianLevels.Jindan), rootName, data.Asset.GetName()),
                actor.Base);
        }

        if (data.Asset.skills.Count <= 0) return;
        var skill = data.Asset.skills[RdUtils.RandomIndexWithAccumWeight(data.Asset.skill_acc_weight)];
        if (GeneralSettings.EnableSkillSystems)
            actor.LearnSkillV3(new SkillContainerBuilder(skill).Build());
    }

    /// <summary>按智力和当前淬炼层数判定自然淬炼，并生成成功后的强度倍率。</summary>
    private static ProgressionResolution ResolveJindanRefinement(ActorExtend actor,
        CultisysAsset<Xian> cultisys, ref Xian component)
    {
        var jindan = actor.GetComponent<Jindan>();
        if (jindan.stage >= 10000) return ProgressionResolution.NoProgress();

        var intelligence = actor.GetStat(S.intelligence);
        if (Mathf.Abs(RdUtils.NextNormal_0_6()) * (jindan.stage + 1) >= intelligence)
            return ProgressionResolution.Failure();
        return ProgressionResolution.Success(new JindanRefinementPayload(
            1f + 0.2f * Randy.randomFloat(intelligence / (10f + intelligence), 1f)));
    }

    /// <summary>直接授予一次固定 1.2 倍的金丹淬炼；达到一万层后返回无进展。</summary>
    private static ProgressionResolution ResolveGrantedJindanRefinement(ActorExtend actor,
        CultisysAsset<Xian> cultisys, ref Xian component)
    {
        if (!actor.TryGetComponent(out Jindan jindan))
            return ProgressionResolution.Failure(reason: "xian.jindan_missing");
        return jindan.stage < 10000
            ? ProgressionResolution.Success(new JindanRefinementPayload(1.2f))
            : ProgressionResolution.NoProgress(reason: "xian.jindan_refinement_capped");
    }

    /// <summary>自然淬炼成功后保留当前灵气的八成。</summary>
    private static void ApplyJindanRefinementCost(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                  ref Xian component, object payload)
    {
        component.wakan *= 0.8f;
    }

    /// <summary>金丹淬炼层数加一，并应用判定载荷中的强度倍率。</summary>
    private static void ApplyJindanRefinement(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                              ref Xian component, object payload)
    {
        var data = (JindanRefinementPayload)payload;
        ref var jindan = ref actor.GetComponent<Jindan>();
        jindan.stage++;
        jindan.strength *= data.StrengthMultiplier;
    }

    /// <summary>没有绑定包裹法术的金丹在淬炼成功后随机强化角色法术。</summary>
    private static void ApplyJindanRefinementReward(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                    ref Xian component, object payload)
    {
        if (string.IsNullOrEmpty(actor.GetComponent<Jindan>().Type.wrapped_skill_id))
            actor.EnhanceSkillRandomly(SkillEnhanceSources.SmallUpgradeSuccess);
    }

    /// <summary>金丹淬炼达到上限却仍尝试时保留当前灵气的六成。</summary>
    private static void ApplyJindanRefinementCapCost(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                     ref Xian component, object payload)
    {
        component.wakan *= 0.6f;
    }

    /// <summary>根据金丹类型抽取元婴，并保存结婴前的筑基与金丹数据供后续结算。</summary>
    private static ProgressionResolution ResolveYuanying(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                          ref Xian component)
    {
        var jindan = actor.GetComponent<Jindan>();
        var xianBase = actor.TryGetComponent(out XianBase existing) ? existing : default;
        var yuanying = Libraries.Manager.YuanyingLibrary.GetRandomYuanying(jindan.Type);
        return ProgressionResolution.Success(new YuanyingPayload(yuanying, jindan.stage,
            xianBase.GetStrength(), jindan.strength));
    }

    /// <summary>直接结婴仍要求已有金丹；满足时复用正常的元婴类型抽取。</summary>
    private static ProgressionResolution ResolveGrantedYuanying(ActorExtend actor,
        CultisysAsset<Xian> cultisys, ref Xian component)
    {
        return actor.HasComponent<Jindan>()
            ? ResolveYuanying(actor, cultisys, ref component)
            : ProgressionResolution.Failure(reason: "xian.jindan_missing");
    }

    /// <summary>创建或替换元婴组件，并在成功提交前移除已经消耗的金丹组件。</summary>
    private static void ApplyYuanyingTransformation(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                     ref Xian component, object payload)
    {
        var data = (YuanyingPayload)payload;
        ref var yuanying = ref actor.GetOrAddComponent<Yuanying>();
        yuanying = new Yuanying(data.Asset.id, data.JindanStrength);
        actor.E.RemoveComponent<Jindan>();
    }

    /// <summary>结婴后记录统计、处理植物命名，并学习元婴定义携带的随机法术。</summary>
    private static void ApplyYuanyingReward(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                            ref Xian component, object payload)
    {
        var data = (YuanyingPayload)payload;
        if (!actor.Base.hasTrait(WorldboxGame.ActorTraits.ScarOfDivinity.id))
        {
            PersistentLogger.Get("JindanStats.log").Log(
                $"{data.JindanStage}, {data.FoundationStrength}, {data.JindanStrength}");
        }

        if (actor.Base.asset == Actors.Plant)
        {
            var rootName = actor.HasElementRoot() ? actor.GetElementRoot().Type.GetName() : null;
            PlantNameGenerator.Instance.NewNameGenerateRequest(
                GetPlantNameParams(actor, cultisys.GetLevelName(XianLevels.Yuanying), rootName,
                    data.Asset.GetName()), actor.Base);
        }

        if (data.Asset.skills.Count <= 0) return;
        var skill = data.Asset.skills[RdUtils.RandomIndexWithAccumWeight(data.Asset.skill_acc_weight)];
        if (GeneralSettings.EnableSkillSystems)
            actor.LearnSkillV3(new SkillContainerBuilder(skill).Build());
    }

    /// <summary>小境界失败时清空灵气，并按小突破失败来源随机改进法术。</summary>
    private static void ApplySmallBreakthroughFailure(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                      ref Xian component, object payload)
    {
        component.wakan = 0f;
        actor.EnhanceSkillRandomly(SkillEnhanceSources.SmallUpgradeFailed);
    }

    /// <summary>大境界失败时清空灵气，并按大突破失败来源随机改进法术。</summary>
    private static void ApplyLargeBreakthroughFailure(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                      ref Xian component, object payload)
    {
        component.wakan = 0f;
        actor.EnhanceSkillRandomly(SkillEnhanceSources.LargeUpgradeFailed);
    }

    /// <summary>传承仙道体系时同步复制筑基、金丹和元婴三个专属组件。</summary>
    private static void TransferXianExtraState(ActorExtend source, ActorExtend target,
                                               ref Xian sourceComponent, ref Xian targetComponent)
    {
        TransferComponent<XianBase>(source, target);
        TransferComponent<Jindan>(source, target);
        TransferComponent<Yuanying>(source, target);
    }

    /// <summary>修复金丹境界结构：补齐筑基、补建普通金丹，并移除越级残留的元婴。</summary>
    private static void NormalizeJindanRealm(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                              ref Xian component, object payload)
    {
        var xianBaseValue = CompleteFoundationForGrant(actor);
        ref var xianBase = ref actor.GetOrAddComponent<XianBase>();
        xianBase = xianBaseValue;

        if (!actor.HasComponent<Jindan>())
        {
            var strength = xianBaseValue.GetFiveQiStrength() * xianBaseValue.GetThreeHuaStrength();
            actor.AddComponent(new Jindan(Jindans.Common.id, strength));
        }
        if (actor.HasComponent<Yuanying>()) actor.E.RemoveComponent<Yuanying>();
    }

    /// <summary>修复练气境界结构：移除不应存在的筑基、金丹和元婴组件。</summary>
    private static void NormalizeQiRefinementRealm(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                     ref Xian component, object payload)
    {
        if (actor.HasComponent<XianBase>()) actor.E.RemoveComponent<XianBase>();
        if (actor.HasComponent<Jindan>()) actor.E.RemoveComponent<Jindan>();
        if (actor.HasComponent<Yuanying>()) actor.E.RemoveComponent<Yuanying>();
    }

    /// <summary>修复筑基境界结构：保留筑基进度，并移除不应存在的金丹和元婴。</summary>
    private static void NormalizeFoundationRealm(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                  ref Xian component, object payload)
    {
        if (actor.HasComponent<Jindan>()) actor.E.RemoveComponent<Jindan>();
        if (actor.HasComponent<Yuanying>()) actor.E.RemoveComponent<Yuanying>();
    }

    /// <summary>修复元婴境界结构：补齐筑基和元婴，并移除已经消耗或越级残留的金丹。</summary>
    private static void NormalizeYuanyingRealm(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                ref Xian component, object payload)
    {
        var xianBaseValue = CompleteFoundationForGrant(actor);
        ref var xianBase = ref actor.GetOrAddComponent<XianBase>();
        xianBase = xianBaseValue;

        if (!actor.HasComponent<Yuanying>())
        {
            var strength = xianBaseValue.GetFiveQiStrength() * xianBaseValue.GetThreeHuaStrength();
            if (actor.TryGetComponent(out Jindan jindan)) strength = jindan.strength;
            actor.AddComponent(new Yuanying(Yuanyings.Common.id, strength));
        }
        if (actor.HasComponent<Jindan>()) actor.E.RemoveComponent<Jindan>();
    }

    /// <summary>使目标角色的指定附加组件与来源完全一致；来源没有时同步移除目标组件。</summary>
    private static void TransferComponent<TComponent>(ActorExtend source, ActorExtend target)
        where TComponent : struct, IComponent
    {
        if (source.TryGetComponent(out TComponent component))
        {
            ref var targetComponent = ref target.GetOrAddComponent<TComponent>();
            targetComponent = component;
        }
        else if (target.HasComponent<TComponent>())
        {
            target.E.RemoveComponent<TComponent>();
        }
    }

    /// <summary>筑基过程中按顺序填充的三花五气项目。</summary>
    private enum FoundationPart
    {
        /// <summary>所有筑基项目都已完成，没有下一项。</summary>
        None,

        /// <summary>精之花，资质取角色智力。</summary>
        Jing,

        /// <summary>气之花，资质取角色智力。</summary>
        Qi,

        /// <summary>神之花，资质取角色智力。</summary>
        Shen,

        /// <summary>火气，资质取火灵根强度。</summary>
        Fire,

        /// <summary>木气，资质取木灵根强度。</summary>
        Wood,

        /// <summary>土气，资质取土灵根强度。</summary>
        Earth,

        /// <summary>金气，资质取组件中 iron 对应的金灵根强度。</summary>
        Iron,

        /// <summary>水气，资质取水灵根强度。</summary>
        Water
    }

    /// <summary>一次筑基成功后传给结构变换的不可变数据。</summary>
    private sealed class FoundationStepPayload
    {
        public FoundationStepPayload(FoundationPart part, float value)
        {
            Part = part;
            Value = value;
        }

        /// <summary>本次完成的三花或五气项目。</summary>
        public FoundationPart Part { get; }

        /// <summary>写入对应 XianBase 字段的筑基强度。</summary>
        public float Value { get; }
    }

    /// <summary>结丹判定后传给结构变换和奖励阶段的不可变数据。</summary>
    private sealed class JindanPayload
    {
        public JindanPayload(XianBase xianBase, JindanAsset asset, float strength)
        {
            XianBase = xianBase;
            Asset = asset;
            Strength = strength;
        }

        /// <summary>金丹匹配过程中可能完成或调整后的筑基数据。</summary>
        public XianBase XianBase { get; }

        /// <summary>根据筑基、灵根和金丹规则选出的金丹类型。</summary>
        public JindanAsset Asset { get; }

        /// <summary>由三花强度与五气强度相乘得到的初始金丹强度。</summary>
        public float Strength { get; }
    }

    /// <summary>一次金丹淬炼成功后传给结构变换的不可变数据。</summary>
    private sealed class JindanRefinementPayload
    {
        public JindanRefinementPayload(float strengthMultiplier)
        {
            StrengthMultiplier = strengthMultiplier;
        }

        /// <summary>本次淬炼乘到当前金丹强度上的倍率。</summary>
        public float StrengthMultiplier { get; }
    }

    /// <summary>结婴判定后传给结构变换、统计和奖励阶段的不可变数据。</summary>
    private sealed class YuanyingPayload
    {
        public YuanyingPayload(YuanyingAsset asset, int jindanStage, float foundationStrength,
                               float jindanStrength)
        {
            Asset = asset;
            JindanStage = jindanStage;
            FoundationStrength = foundationStrength;
            JindanStrength = jindanStrength;
        }

        /// <summary>根据原金丹类型抽取出的元婴类型。</summary>
        public YuanyingAsset Asset { get; }

        /// <summary>结婴前金丹已经完成的淬炼层数，仅用于统计和后续奖励。</summary>
        public int JindanStage { get; }

        /// <summary>结婴前筑基结构的综合强度，仅用于统计。</summary>
        public float FoundationStrength { get; }

        /// <summary>结婴前最终金丹强度，同时作为新元婴的初始强度。</summary>
        public float JindanStrength { get; }
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

    [Hotfixable]
    internal static void TakeWakanAndCultivate(ActorExtend actor_extend, ref Xian xian)
    {
        var max_wakan = actor_extend.Base.stats[BaseStatses.MaxWakan.id];
        if (xian.wakan >= max_wakan) return;
        Vector2Int tile_pos = actor_extend.Base.current_tile.pos;
        var total = WakanMap.I.map[tile_pos.x, tile_pos.y];
        var to_take = Mathf.Log10(total + 1);

        var cultivate_method = actor_extend.GetMainCultibook()?.GetCultivateMethod() ?? CultivateMethods.Standard;

        to_take = Mathf.Min(max_wakan - xian.wakan, total, to_take * cultivate_method.GetEfficiency?.Invoke(actor_extend) ?? 1f);
        xian.wakan += to_take;
        WakanMap.I.map[tile_pos.x, tile_pos.y] -= to_take;
    }
    internal static void OutWakanAndCultivate(ActorExtend actor_extend, ref Xian xian)
    {
        var max_wakan = actor_extend.Base.stats[BaseStatses.MaxWakan.id];
        if (xian.wakan >= max_wakan) return;
        Vector2Int tile_pos = actor_extend.Base.current_tile.pos;
        var total = WakanMap.I.map[tile_pos.x, tile_pos.y];
        var to_take = Mathf.Log10(total + 1);

        var cultivate_method = actor_extend.GetMainCultibook()?.GetCultivateMethod() ?? CultivateMethods.Standard;
        to_take = Mathf.Min(max_wakan - xian.wakan, total, to_take * cultivate_method.GetEfficiency?.Invoke(actor_extend) ?? 1f);
        xian.wakan += to_take;
        var dirty_wakan_to_take = Mathf.Min(DirtyWakanMap.I.map[tile_pos.x, tile_pos.y],
            to_take * ContentSetting.DirtyWakanToWakanRatio);
        WakanMap.I.map[tile_pos.x, tile_pos.y] += dirty_wakan_to_take;
        DirtyWakanMap.I.map[tile_pos.x, tile_pos.y] -= dirty_wakan_to_take;
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
