using Cultiway.Abstract;
using Cultiway.Content.CultisysComponents;
using Cultiway.Patch;

namespace Cultiway.Content;

// 虽然没有这么拼的，但是方便
public class BaseStatses : ExtendLibrary<BaseStatAsset, BaseStatses>
{
    public static BaseStatAsset MaxWakan { get; private set; }

    protected override void OnInit()
    {
        MaxWakan = Add(new BaseStatAsset()
        {
            id = nameof(MaxWakan)
        });
        PatchWindowCreatureInfo.RegisterInfoDisplay((ae, sb) =>
        {
            if (!ae.HasCultisys<Xian>()) return;
            var wakan = ae.GetCultisys<Xian>().wakan;
            sb.AppendLine($"灵气: {wakan} / {ae.Base.stats[MaxWakan.id]}");
        });
    }
}