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
    public static WorldLogAsset LogSectLeaderDead { get; private set; }
    public static WorldLogAsset LogSectLeaderKilled { get; private set; }
    public static WorldLogAsset LogSectScriptureContributed { get; private set; }
    public static WorldLogAsset LogSectLecture { get; private set; }
    public static WorldLogAsset LogDemonAscension { get; private set; }
    public static WorldLogAsset LogBalefulWindTribulationStarted { get; private set; }
    public static WorldLogAsset LogBalefulWindTribulationSurvived { get; private set; }
    public static WorldLogAsset LogBalefulWindTribulationFailed { get; private set; }
    public static WorldLogAsset LogYuanyingEscape { get; private set; }
    public static WorldLogAsset LogYuanyingPossessionSuccess { get; private set; }
    public static WorldLogAsset LogYuanyingPossessionFailure { get; private set; }

    // ===== 妖兽玩法日志 =====
    public static WorldLogAsset LogYaoAwakened { get; private set; }
    public static WorldLogAsset LogYaoQuenchedBlood { get; private set; }
    public static WorldLogAsset LogYaoOrganDigested { get; private set; }
    public static WorldLogAsset LogYaoOrganRejected { get; private set; }
    public static WorldLogAsset LogYaoCoreCondensed { get; private set; }
    public static WorldLogAsset LogYaoCoreCracked { get; private set; }
    public static WorldLogAsset LogYaoTribulationStarted { get; private set; }
    public static WorldLogAsset LogYaoTribulationSucceeded { get; private set; }
    public static WorldLogAsset LogYaoTribulationRetreated { get; private set; }
    public static WorldLogAsset LogYaoHumanTransformation { get; private set; }
    public static WorldLogAsset LogYaoAtavism { get; private set; }
    public static WorldLogAsset LogYaoSolidified { get; private set; }
    public static WorldLogAsset LogYaoNirvanaStarted { get; private set; }
    public static WorldLogAsset LogYaoNirvanaReborn { get; private set; }
    public static WorldLogAsset LogYaoTailLife { get; private set; }
    public static WorldLogAsset LogYaoBirthResolved { get; private set; }

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
        SetupSectLog(LogSectLeaderDead, "Cultiway.WorldLog.SectLeaderDead", "ui/icons/iconDead", Toolbox.color_log_warning);
        SetupSectLog(LogSectLeaderKilled, "Cultiway.WorldLog.SectLeaderKilled", "ui/icons/actor_traits/iconKingslayer", Toolbox.color_log_warning);
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

        SetupCultivationActorLog(LogBalefulWindTribulationStarted,
            "Cultiway.WorldLog.BalefulWindTribulationStarted", Toolbox.color_log_warning);
        SetupCultivationActorLog(LogBalefulWindTribulationSurvived,
            "Cultiway.WorldLog.BalefulWindTribulationSurvived", Toolbox.color_log_good);
        SetupCultivationActorLog(LogBalefulWindTribulationFailed,
            "Cultiway.WorldLog.BalefulWindTribulationFailed", Toolbox.color_log_warning);

        SetupPossessionLog(LogYuanyingEscape, "Cultiway.WorldLog.YuanyingEscape", Toolbox.color_log_warning);
        SetupPossessionLog(LogYuanyingPossessionSuccess, "Cultiway.WorldLog.YuanyingPossessionSuccess",
            Toolbox.color_log_good);
        SetupPossessionLog(LogYuanyingPossessionFailure, "Cultiway.WorldLog.YuanyingPossessionFailure",
            Toolbox.color_log_warning);

        // 妖兽日志：主体 + 结果的固定两段式文本。
        SetupYaoLog(LogYaoAwakened, "Cultiway.WorldLog.YaoAwakened", Toolbox.color_log_good, "cultiway/icons/iconCultivation");
        SetupYaoLog(LogYaoQuenchedBlood, "Cultiway.WorldLog.YaoQuenchedBlood", Toolbox.color_log_good, "cultiway/icons/iconCultivation");
        SetupYaoLog(LogYaoOrganDigested, "Cultiway.WorldLog.YaoOrganDigested", Toolbox.color_log_good, "cultiway/icons/iconCultivation");
        SetupYaoLog(LogYaoOrganRejected, "Cultiway.WorldLog.YaoOrganRejected", Toolbox.color_log_warning, "cultiway/icons/iconCultivation");
        SetupYaoLog(LogYaoCoreCondensed, "Cultiway.WorldLog.YaoCoreCondensed", Toolbox.color_log_good, "cultiway/icons/achievements/nascent_soul_formed");
        SetupYaoLog(LogYaoCoreCracked, "Cultiway.WorldLog.YaoCoreCracked", Toolbox.color_log_warning, "cultiway/icons/achievements/nascent_soul_formed");
        SetupYaoLog(LogYaoTribulationStarted, "Cultiway.WorldLog.YaoTribulationStarted", Toolbox.color_log_warning, "cultiway/icons/element_root/entropy");
        SetupYaoLog(LogYaoTribulationSucceeded, "Cultiway.WorldLog.YaoTribulationSucceeded", Toolbox.color_log_good, "cultiway/icons/element_root/entropy");
        SetupYaoLog(LogYaoTribulationRetreated, "Cultiway.WorldLog.YaoTribulationRetreated", Toolbox.color_log_warning, "cultiway/icons/element_root/entropy");
        SetupYaoLog(LogYaoHumanTransformation, "Cultiway.WorldLog.YaoHumanTransformation", Toolbox.color_log_good, "cultiway/icons/iconCultivation");
        SetupYaoLog(LogYaoAtavism, "Cultiway.WorldLog.YaoAtavism", Toolbox.color_log_good, "cultiway/icons/iconCultivation");
        SetupYaoLog(LogYaoSolidified, "Cultiway.WorldLog.YaoSolidified", Toolbox.color_log_good, "cultiway/icons/iconCultivation");
        SetupYaoLog(LogYaoNirvanaStarted, "Cultiway.WorldLog.YaoNirvanaStarted", Toolbox.color_log_warning, "cultiway/icons/iconCultivation");
        SetupYaoLog(LogYaoNirvanaReborn, "Cultiway.WorldLog.YaoNirvanaReborn", Toolbox.color_log_good, "cultiway/icons/iconCultivation");
        SetupYaoLog(LogYaoTailLife, "Cultiway.WorldLog.YaoTailLife", Toolbox.color_log_good, "cultiway/icons/iconCultivation");
        SetupYaoLog(LogYaoBirthResolved, "Cultiway.WorldLog.YaoBirthResolved", Toolbox.color_log_good, "cultiway/icons/iconCultivation");
    }

    private static void SetupYaoLog(WorldLogAsset asset, string localeId, UnityEngine.Color color, string iconPath)
    {
        asset.locale_id = localeId;
        asset.path_icon = iconPath;
        asset.color = color;
        asset.group = HistoryGroups.Cultivations.id;
        asset.text_replacer = (WorldLogMessage message, ref string text) =>
        {
            AssetManager.world_log_library.updateText(ref text, message, "$actor$", 1);
            AssetManager.world_log_library.updateText(ref text, message, "$value$", 2);
        };
    }

    private static void SetupCultivationActorLog(WorldLogAsset asset, string localeId, UnityEngine.Color color)
    {
        asset.locale_id = localeId;
        asset.path_icon = "cultiway/icons/element_root/entropy";
        asset.color = color;
        asset.group = HistoryGroups.Cultivations.id;
        asset.text_replacer = (WorldLogMessage message, ref string text) =>
            AssetManager.world_log_library.updateText(ref text, message, "$actor$", 1);
    }

    private static void SetupPossessionLog(WorldLogAsset asset, string localeId, UnityEngine.Color color)
    {
        asset.locale_id = localeId;
        asset.path_icon = "cultiway/icons/achievements/nascent_soul_formed";
        asset.color = color;
        asset.group = HistoryGroups.Cultivations.id;
        asset.text_replacer = (WorldLogMessage message, ref string text) =>
        {
            AssetManager.world_log_library.updateText(ref text, message, "$actor$", 1);
            AssetManager.world_log_library.updateText(ref text, message, "$host$", 2);
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
