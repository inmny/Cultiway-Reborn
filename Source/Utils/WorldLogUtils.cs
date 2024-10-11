using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Utils.Extension;

namespace Cultiway.Utils;

public static class WorldLogUtils
{
    public static void LogCultisysLevelup<T>(ActorExtend ae, ref T component) where T : ICultisysComponent
    {
        var msg_key = component.Asset.LevelupMsgKeys[component.CurrLevel];
        if (LMTools.Has(msg_key)) return;

        var world_log = new WorldLogMessage(msg_key, ae.Base.getName())
        {
            unit = ae.Base,
            location = ae.Base.currentPosition
        };
        world_log.add();
    }
}