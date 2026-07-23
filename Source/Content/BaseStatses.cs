using System.Text;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Patch;
using NeoModLoader.General.Game.extensions;
using strings;

namespace Cultiway.Content;

// 虽然没有这么拼的，但是方便
public class BaseStatses : ExtendLibrary<BaseStatAsset, BaseStatses>
{
    [AssetId(nameof(MaxWakan))]public static BaseStatAsset MaxWakan { get; private set; }
    [AssetId(nameof(WakanRegen))] public static BaseStatAsset WakanRegen { get; private set; }
    [AssetId(nameof(MaxSpirit))] public static BaseStatAsset MaxSpirit { get; private set; }
    /// <summary>单位每月自然恢复的 mana。</summary>
    [AssetId(nameof(ManaRegen))] public static BaseStatAsset ManaRegen { get; private set; }
    /// <summary>单位每月自然恢复的精神力。</summary>
    [AssetId(nameof(SpiritRegen))] public static BaseStatAsset SpiritRegen { get; private set; }
    /// <summary>骑士的斗气上限（突破资源）。</summary>
    [AssetId(nameof(MaxVigor))] public static BaseStatAsset MaxVigor { get; private set; }
    /// <summary>骑士的闪避几率（自定义 stat；WorldBox 无原生闪避数值）。</summary>
    [AssetId(nameof(KnightEvasion))] public static BaseStatAsset KnightEvasion { get; private set; }
    /// <summary>单位抵消外力与攻击击退效果的内部抗性。</summary>
    [AssetId(S.knockback_reduction)] public static BaseStatAsset KnockbackReduction { get; private set; }
    private static StringBuilder all_stats_ids = new();
    internal static string AllStatsIds => all_stats_ids.ToString();
    protected override bool AutoRegisterAssets() => true;
    protected override void OnInit()
    {
        KnockbackReduction.hidden = true;
        KnockbackReduction.translation_key = KnockbackReduction.id;

        PatchWindowCreatureInfo.RegisterInfoDisplay((ae, sb) =>
        {
            if (!ae.HasCultisys<Xian>()) return;
            var wakan = ae.GetCultisys<Xian>().wakan;
            sb.AppendLine($"灵气: {wakan} / {ae.Base.stats[MaxWakan.id]}");
        });
        
        AssetManager.base_stats_library.ForEach<BaseStatAsset, BaseStatsLibrary>(asset =>
        {
            if (all_stats_ids.Length > 0)
            {
                all_stats_ids.Append(',');
            }

            all_stats_ids.Append('”');
            all_stats_ids.Append(asset.id);
            all_stats_ids.Append('”');
        });
    }
}
