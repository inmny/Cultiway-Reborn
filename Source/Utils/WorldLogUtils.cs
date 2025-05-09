using Cultiway.Abstract;
using Cultiway.Content;
using Cultiway.Core;
using Cultiway.Utils.Extension;

namespace Cultiway.Utils;

public static class WorldLogUtils
{
    public static void LogCultisysLevelup<T>(ActorExtend ae, ref T component) where T : ICultisysComponent
    {
        var msg_key = component.Asset.LevelupMsgKeys[component.CurrLevel];
        if (!LMTools.Has(msg_key)) return;

        var world_log = new WorldLogMessage(WorldLogs.LogCultisysLevelup, ae.Base.getName(), component.Asset.LevelupMsgKeys[component.CurrLevel])
        {
            unit = ae.Base,
            location = ae.Base.current_position
        };
        if (ae.Base.kingdom?.kingdomColor != null)
        {
            world_log.color_special1 = ae.Base.kingdom.kingdomColor.getColorText();
        }
        world_log.add();
    }
}