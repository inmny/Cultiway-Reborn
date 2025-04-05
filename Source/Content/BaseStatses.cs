using System.Text;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Patch;
using NeoModLoader.General.Game.extensions;

namespace Cultiway.Content;

// 虽然没有这么拼的，但是方便
public class BaseStatses : ExtendLibrary<BaseStatAsset, BaseStatses>
{
    [AssetId(nameof(MaxWakan))]public static BaseStatAsset MaxWakan { get; private set; }
    [AssetId(nameof(WakanRegen))] public static BaseStatAsset WakanRegen { get; private set; }
    private static StringBuilder all_stats_ids = new();
    internal static string AllStatsIds => all_stats_ids.ToString();

    protected override void OnInit()
    {
        RegisterAssets();
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