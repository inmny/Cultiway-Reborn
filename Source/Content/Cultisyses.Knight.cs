using System.Collections.Generic;
using System.IO;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Core.Progression;
using Cultiway.Patch;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content;

public partial class Cultisyses
{
    /// <summary>骑士体系资产及其等级、属性和进阶规则入口。</summary>
    public static CultisysAsset<Knight> Knight { get; private set; }

    private void InitKnight()
    {
        Knight = (CultisysAsset<Knight>)Add(new CultisysAsset<Knight>(nameof(Knight), KnightSetting.LevelNumber,
            new Knight(), CreateKnightProgressionProfile()));
        Knight.IconPath = "cultiway/icons/iconKnight";
        ProgressionService.Register(Knight);
        Knight.DisplayDetailProvider = AppendKnightDisplayDetails;
        LoadStatsForKnight();

        RegisterAcquisitionRule(Knight.id, TryAcquireKnight);

        ActorExtend.RegisterCachedStatsBuilder([Hotfixable](ae, stats) =>
        {
            if (!ae.TryGetComponent(out Knight knight)) return;
            stats.mergeStats(Knight.LevelAccumBaseStats[knight.CurrLevel]);
        });

        PatchWindowCreatureInfo.RegisterInfoDisplay((a, sb) =>
        {
            if (!a.HasCultisys<Knight>()) return;
            ref var knight_info = ref a.GetCultisys<Knight>();
            sb.AppendLine($"骑士 {knight_info.CurrLevel} 级");
            sb.AppendLine($"斗气: {knight_info.vigor} / {a.Base.stats[BaseStatses.MaxVigor.id]}");
        });
    }

    /// <summary>向通用修炼体系详情追加斗气与当前等级的自然突破率。</summary>
    private static void AppendKnightDisplayDetails(ActorExtend actor, ICollection<CultisysDisplayLine> lines)
    {
        ref var knight = ref actor.GetCultisys<Knight>();
        lines.Add(CultisysDisplayLine.CreateProgress(
            "Cultiway.CultisysTooltip.Resource.Vigor",
            knight.vigor,
            actor.Base.stats[BaseStatses.MaxVigor.id],
            Knight.IconPath,
            "#F3961F"));
        if (knight.CurrLevel >= KnightSetting.BreakthroughSuccessChance.Length) return;
        lines.Add(new CultisysDisplayLine(
            "Cultiway.CultisysTooltip.Knight.SuccessChance",
            $"{KnightSetting.BreakthroughSuccessChance[knight.CurrLevel] * 100f:0.#}%"));
    }

    private void LoadStatsForKnight()
    {
        var csv = CSVUtils.ReadCSV(File.ReadAllText(Path.Combine(ModClass.I.GetDeclaration().FolderPath,
            KnightSetting.StatsPath)));
        var keys = csv[0];
        _ = csv[1]; // 中文表头

        for (int i = 0; i < Knight.LevelNumber; i++)
        {
            var line = csv[i + 2];
            var stats = Knight.LevelBaseStats[i];
            stats.clear();
            for (int j = 0; j < keys.Length; j++)
            {
                var key = keys[j];
                if (!AssetManager.base_stats_library.Contains(key)) continue;
                stats[key] = float.Parse(line[j]);
            }
        }

        Knight.UpdateAccumStats();
    }

    /// <summary>骑士体系只有大等级进阶，每级共用斗气门槛与（带几率的）突破结算。</summary>
    private static CultisysProgressionProfile<Knight> CreateKnightProgressionProfile()
    {
        var profile = new CultisysProgressionProfile<Knight>();
        for (var level = 0; level < KnightSetting.LevelNumber - 1; level++)
        {
            var transition = new ProgressionTransitionAsset<Knight>(
                $"knight.level_{level}_to_{level + 1}", ProgressionKind.Major, level, level + 1)
            {
                IsApproaching = IsKnightApproachingBreakthrough,
                ResolveNatural = ResolveKnightBreakthrough,
                ResolveGrant = ResolveKnightBreakthroughGrant,
            };
            transition.Requirements.Add(RequireFullVigor);
            // 成功与失败都清空斗气：成功后进入下一级重新积累，失败则重攒。
            transition.SuccessCosts.Add(ConsumeVigorAfterBreakthrough);
            transition.FailureEffects.Add(ConsumeVigorAfterBreakthrough);
            profile.AddRealm(new RealmProgressionAsset<Knight>(level)
            {
                Transitions = { transition }
            });
        }
        return profile;
    }

    /// <summary>出生准入占位：出生时还不是士兵，始终返回 false；真正觉醒由 KnightAcquisitionSystem 月度掷骰。</summary>
    private static bool TryAcquireKnight(ActorExtend ae)
    {
        return false;
    }

    /// <summary>斗气达到预突破比例时认为接近突破（供调度查询）。</summary>
    private static bool IsKnightApproachingBreakthrough(ActorExtend actor, CultisysAsset<Knight> cultisys,
                                                        ref Knight component)
    {
        var maxVigor = actor.Base.stats[BaseStatses.MaxVigor.id];
        return maxVigor > 0f
               && component.vigor / maxVigor > KnightSetting.CommonPreUpgradeVigorRatio;
    }

    /// <summary>自然突破要求当前斗气接近最大斗气。</summary>
    private static ProgressionGateResult RequireFullVigor(ActorExtend actor, CultisysAsset<Knight> cultisys,
                                                         ref Knight component)
    {
        return component.vigor >= actor.Base.stats[BaseStatses.MaxVigor.id] - 0.1f
            ? ProgressionGateResult.Satisfied
            : ProgressionGateResult.NotReady("knight.vigor_not_full");
    }

    /// <summary>自然突破按等级相关的成功率结算；失败进入 FailureEffects（清空斗气重攒）。</summary>
    private static ProgressionResolution ResolveKnightBreakthrough(ActorExtend actor, CultisysAsset<Knight> cultisys,
                                                                  ref Knight component)
    {
        var chance = KnightSetting.BreakthroughSuccessChance[component.CurrLevel];
        return Randy.randomChance(chance)
            ? ProgressionResolution.Success()
            : ProgressionResolution.Failure(reason: "knight.breakthrough_failed");
    }

    /// <summary>直接授予（管理/作弊入口）固定成功，不走失败几率。</summary>
    private static ProgressionResolution ResolveKnightBreakthroughGrant(ActorExtend actor,
                                                                        CultisysAsset<Knight> cultisys,
                                                                        ref Knight component)
    {
        return ProgressionResolution.Success();
    }

    /// <summary>突破结算后清空积累的斗气。</summary>
    private static void ConsumeVigorAfterBreakthrough(ActorExtend actor, CultisysAsset<Knight> cultisys,
                                                      ref Knight component, object payload)
    {
        component.vigor = 0f;
    }
}
