using Cultiway.Content.Components;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

public partial class Cultisyses
{
    public static CultisysAsset<Magic> Magic { get; private set; }

    private void InitMagic()
    {
        Magic = (CultisysAsset<Magic>)Add(
            new CultisysAsset<Magic>(nameof(Magic), 10, new Magic()));
    }
}
