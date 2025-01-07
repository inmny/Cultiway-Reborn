using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Patch;

namespace Cultiway.Content;

// 虽然没有这么拼的，但是方便
public class BaseStatses : ExtendLibrary<BaseStatAsset, BaseStatses>
{
    [AssetId(nameof(MaxWakan))]public static BaseStatAsset MaxWakan { get; private set; }
    [AssetId(nameof(WakanRegen))] public static BaseStatAsset WakanRegen { get; private set; }

    protected override void OnInit()
    {
        RegisterAssets("Cultiway.BaseStats");
        PatchWindowCreatureInfo.RegisterInfoDisplay((ae, sb) =>
        {
            if (!ae.HasCultisys<Xian>()) return;
            var wakan = ae.GetCultisys<Xian>().wakan;
            sb.AppendLine($"灵气: {wakan} / {ae.Base.stats[MaxWakan.id]}");
        });
    }
}