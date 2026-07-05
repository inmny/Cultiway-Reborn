using Cultiway.Abstract;
using NeoModLoader.General;

namespace Cultiway.Content;
[Dependency(typeof(HistoryGroups))]
public class WorldLogs : ExtendLibrary<WorldLogAsset, WorldLogs>
{
    public static WorldLogAsset LogCultisysLevelup { get; private set; }
    public static WorldLogAsset LogSectFounded { get; private set; }
    public static WorldLogAsset LogSectJoined { get; private set; }
    public static WorldLogAsset LogSectPromoted { get; private set; }
    public static WorldLogAsset LogSectSuccession { get; private set; }
    public static WorldLogAsset LogSectScriptureContributed { get; private set; }
    public static WorldLogAsset LogSectLecture { get; private set; }
    public static WorldLogAsset LogDemonAscension { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override void OnInit()
    {
        LogCultisysLevelup.locale_id = "Cultiway";
        LogCultisysLevelup.path_icon = "cultiway/icons/iconCultivation";
        LogCultisysLevelup.color = Toolbox.color_log_warning;
        LogCultisysLevelup.group = HistoryGroups.Cultivations.id;
        LogCultisysLevelup.text_replacer = (WorldLogMessage message, ref string text) =>
        {
            text = message.special1;
            AssetManager.world_log_library.updateText(ref text, message, "$actor$", 2);
            AssetManager.world_log_library.updateText(ref text, message, "$realm$", 3);
        };

        SetupSectLog(LogSectFounded, "Cultiway.WorldLog.SectFounded", "cultiway/icons/iconSect", Toolbox.color_log_good);
        SetupSectLog(LogSectJoined, "Cultiway.WorldLog.SectJoined", "cultiway/icons/iconMasterApprentice", Toolbox.color_log_good);
        SetupSectLog(LogSectPromoted, "Cultiway.WorldLog.SectPromoted", "ui/icons/iconInterestingPeople", Toolbox.color_log_good);
        SetupSectLog(LogSectSuccession, "Cultiway.WorldLog.SectSuccession", "ui/Icons/iconKings", Toolbox.color_log_warning);
        SetupSectLog(LogSectScriptureContributed, "Cultiway.WorldLog.SectScriptureContributed", "ui/icons/iconBooks", Toolbox.color_log_good);
        SetupSectLog(LogSectLecture, "Cultiway.WorldLog.SectLecture", "cultiway/icons/iconCultivation", Toolbox.color_log_good);

        LogDemonAscension.locale_id = "Cultiway.WorldLog.DemonAscension";
        LogDemonAscension.path_icon = "cultiway/icons/iconCultivation";
        LogDemonAscension.color = Toolbox.color_log_warning;
        LogDemonAscension.group = HistoryGroups.Cultivations.id;
        LogDemonAscension.text_replacer = (WorldLogMessage message, ref string text) =>
        {
            AssetManager.world_log_library.updateText(ref text, message, "$actor$", 1);
            AssetManager.world_log_library.updateText(ref text, message, "$daemon$", 2);
        };
    }

    private static void SetupSectLog(WorldLogAsset asset, string localeId, string iconPath, UnityEngine.Color color)
    {
        asset.locale_id = localeId;
        asset.path_icon = iconPath;
        asset.color = color;
        asset.group = HistoryGroups.Sects.id;
        asset.text_replacer = (WorldLogMessage message, ref string text) =>
        {
            AssetManager.world_log_library.updateText(ref text, message, "$sect$", 1);
            AssetManager.world_log_library.updateText(ref text, message, "$actor$", 2);
            AssetManager.world_log_library.updateText(ref text, message, "$value$", 3);
        };
    }
}
