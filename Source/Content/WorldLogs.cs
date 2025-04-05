using Cultiway.Abstract;
using NeoModLoader.General;

namespace Cultiway.Content;

public class WorldLogs : ExtendLibrary<WorldLogAsset, WorldLogs>
{
    public static WorldLogAsset LogCultisysLevelup { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets();
        LogCultisysLevelup.locale_id = "Cultiway";
        LogCultisysLevelup.path_icon = "cultiway/icons/iconCultivation";
        LogCultisysLevelup.color = Toolbox.color_log_warning;
        LogCultisysLevelup.text_replacer = (WorldLogMessage message, ref string text) =>
        {
            var key = message.special2;
            text = LM.Get(key);
            AssetManager.world_log_library.updateText(ref text, message, "$actor$", 1);
        };
    }
}