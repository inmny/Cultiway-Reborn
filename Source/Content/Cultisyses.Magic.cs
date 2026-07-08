using System.Text;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Patch;

namespace Cultiway.Content;

public partial class Cultisyses
{
    public static CultisysAsset<Magic> Magic { get; private set; }

    private void InitMagic()
    {
        Magic = (CultisysAsset<Magic>)Add(
            new CultisysAsset<Magic>(nameof(Magic), 10, new Magic()));

        ActorExtend.RegisterActionOnNewCreature((ae) =>
        {
            if (!ae.HasElementRoot()) return;
            if (!GetAvailableCultisysIds(ae).Contains(nameof(Magic))) return;
            ae.NewCultisys(Magic);
            ModClass.I.WorldRecord.CheckAndLogFirstLevelup(Magic.id, ae, ref ae.GetCultisys<Magic>());
        });

        PatchWindowCreatureInfo.RegisterInfoDisplay((a, sb) =>
        {
            if (!a.HasCultisys<Magic>()) return;
            ref var magic_info = ref a.GetCultisys<Magic>();
            sb.AppendLine($"{magic_info.Asset.GetName()}: {magic_info.Asset.GetLevelName(magic_info.CurrLevel)}");
        });
    }
}

