using Cultiway.Abstract;
using Cultiway.Content;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;

namespace Cultiway.Utils;

public static class WorldLogUtils
{
    public static void LogCultisysLevelup<T>(ActorExtend ae, ref T component) where T : ICultisysComponent
    {
        var msg_key = component.Asset.LevelupMsgKeys[component.CurrLevel];
        if (!LMTools.Has(msg_key)) return;

        var world_log = new WorldLogMessage(WorldLogs.LogCultisysLevelup, component.Asset.GetLevelupMessage(component.CurrLevel), ae.Base.getName(), component.Asset.GetLevelName(component.CurrLevel))
        {
            unit = ae.Base,
            location = ae.Base.current_position
        };
        if (ae.Base.kingdom?.getColor() != null)
        {
            world_log.color_special1 = ae.Base.kingdom.getColor().getColorText();
        }
        world_log.add();
    }

    public static void LogDemonAscension(Actor initiator, Actor daemon)
    {
        if (WorldLogs.LogDemonAscension == null || daemon == null) return;

        var worldLog = new WorldLogMessage(
            WorldLogs.LogDemonAscension,
            initiator?.getName(),
            LMTools.GetOrKey(daemon.asset.name_locale))
        {
            unit = daemon,
            location = daemon.current_position
        };

        if (initiator?.kingdom?.getColor() != null)
        {
            worldLog.color_special1 = initiator.kingdom.getColor().getColorText();
        }

        worldLog.add();
    }

    public static void LogSectFounded(Sect sect, Actor founder)
    {
        LogSect(WorldLogs.LogSectFounded, sect, founder, sect?.data.DoctrineCultibookName);
    }

    public static void LogSectJoined(Sect sect, Actor actor)
    {
        if (!CanWriteSeniorSectStory(actor)) return;

        LogSect(WorldLogs.LogSectJoined, sect, actor, actor?.GetSectRoleSummary());
    }

    public static void LogSectPromoted(Sect sect, Actor actor, SectRoleAsset role)
    {
        if (!CanWriteSeniorSectStory(actor)) return;

        LogSect(WorldLogs.LogSectPromoted, sect, actor, role?.GetName());
    }

    public static void LogSectSuccession(Sect sect, Actor leader)
    {
        LogSect(WorldLogs.LogSectSuccession, sect, leader, null);
    }

    public static void LogSectScriptureContributed(Sect sect, Actor contributor, Book book)
    {
        LogSect(WorldLogs.LogSectScriptureContributed, sect, contributor, GetBookName(book));
    }

    public static void LogSectLecture(Sect sect, Actor lecturer, string cultibookName, int audienceCount)
    {
        string value = audienceCount > 0 ? $"{cultibookName} x{audienceCount}" : cultibookName;
        LogSect(WorldLogs.LogSectLecture, sect, lecturer, value);
    }

    private static void LogSect(WorldLogAsset asset, Sect sect, Actor actor, string value)
    {
        if (asset == null) return;
        if (sect == null || sect.isRekt()) return;

        var worldLog = new WorldLogMessage(asset, sect.data.name, actor?.getName(), value)
        {
            unit = actor,
            location = GetLogLocation(sect, actor)
        };

        if (actor?.kingdom?.getColor() != null)
        {
            worldLog.color_special2 = actor.kingdom.getColor().getColorText();
        }

        worldLog.add();
    }

    private static bool CanWriteSeniorSectStory(Actor actor)
    {
        return actor != null
               && !actor.isRekt()
               && actor.HasSectPermission(SectPermissions.TeachSectCultibook);
    }

    private static string GetBookName(Book book)
    {
        if (book == null || book.isRekt()) return null;
        return string.IsNullOrEmpty(book.data.name) ? book.name : book.data.name;
    }

    private static UnityEngine.Vector2 GetLogLocation(Sect sect, Actor actor)
    {
        if (actor != null && !actor.isRekt()) return actor.current_position;

        City city = sect.GetHomeCity();
        if (city != null && !city.isRekt()) return city.last_city_center;

        return UnityEngine.Vector2.zero;
    }
}
