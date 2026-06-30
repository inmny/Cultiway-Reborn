using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Extensions;

/// <summary>
/// 宗门权限规则，统一判断成员能否执行某类宗门行为。
/// </summary>
public static class SectPermissionRules
{
    /// <summary>
    /// 判断成员是否拥有任意藏经阁研读权限。
    /// </summary>
    public static bool HasAnySectScriptureReadPermission(this Actor actor)
    {
        return actor.HasSectPermission(SectPermissions.ReadBasicScripture)
               || actor.HasSectPermission(SectPermissions.ReadCoreScripture)
               || actor.HasSectPermission(SectPermissions.ReadHighScripture);
    }

    /// <summary>
    /// 判断成员能否研读指定宗门藏书。
    /// </summary>
    public static bool CanReadSectScriptureBook(this Actor actor, Book book)
    {
        if (actor == null || actor.isRekt()) return false;
        if (book == null || book.isRekt()) return false;

        Sect sect = actor.GetExtend().sect;
        if (sect == null || sect.isRekt()) return false;
        if (!ContainsScriptureBook(sect, book)) return false;

        return actor.HasSectPermission(GetReadPermission(book));
    }

    /// <summary>
    /// 判断成员能否向指定宗门贡献典籍。
    /// </summary>
    public static bool CanContributeSectScripture(this Actor actor, Sect sect)
    {
        return IsMemberOfSect(actor, sect)
               && actor.HasSectPermission(SectPermissions.WriteScripture);
    }

    /// <summary>
    /// 判断成员能否把书放入指定宗门藏经阁。
    /// </summary>
    public static bool CanStoreSectScripture(this Actor actor, Sect sect)
    {
        return IsMemberOfSect(actor, sect)
               && (actor.HasSectPermission(SectPermissions.WriteScripture)
                   || actor.HasSectPermission(SectPermissions.ManageScripture));
    }

    /// <summary>
    /// 判断成员能否管理指定宗门藏经阁。
    /// </summary>
    public static bool CanManageSectScripture(this Actor actor, Sect sect)
    {
        return IsMemberOfSect(actor, sect)
               && actor.HasSectPermission(SectPermissions.ManageScripture);
    }

    /// <summary>
    /// 判断成员能否执行指定宗门的人事评定。
    /// </summary>
    public static bool CanEvaluateSectPersonnel(this Actor actor, Sect sect)
    {
        return IsMemberOfSect(actor, sect)
               && actor.HasSectPermission(SectPermissions.EvaluatePersonnel);
    }

    /// <summary>
    /// 判断成员能否为指定宗门招揽门人。
    /// </summary>
    public static bool CanRecruitSectMember(this Actor actor, Sect sect)
    {
        return IsMemberOfSect(actor, sect)
               && actor.HasSectPermission(SectPermissions.RecruitMember);
    }

    /// <summary>
    /// 判断成员能否将自己的徒弟带入指定宗门。
    /// </summary>
    public static bool CanBringApprenticeToSect(this Actor actor, Sect sect)
    {
        return IsMemberOfSect(actor, sect)
               && actor.HasSectPermission(SectPermissions.BringApprenticeToSect);
    }

    /// <summary>
    /// 判断成员能否任免指定宗门成员的角色。
    /// </summary>
    public static bool CanPromoteSectMember(this Actor actor, Sect sect, Actor target, SectRoleAsset role)
    {
        if (!IsMemberOfSect(actor, sect)) return false;
        if (target == null || target.isRekt()) return false;
        if (target.GetExtend().sect != sect) return false;
        if (role == null) return false;

        return role == SectRoles.Leader
            ? actor.HasSectPermission(SectPermissions.ManageSect)
            : actor.HasSectPermission(SectPermissions.PromoteMember);
    }

    /// <summary>
    /// 判断成员能否管理指定宗门。
    /// </summary>
    public static bool CanManageSect(this Actor actor, Sect sect)
    {
        return IsMemberOfSect(actor, sect)
               && actor.HasSectPermission(SectPermissions.ManageSect);
    }

    /// <summary>
    /// 获取指定藏书需要的研读权限。
    /// </summary>
    public static SectPermissionAsset GetReadPermission(Book book)
    {
        int stage = GetBookStage(book);
        if (stage >= SectConst.ScriptureHighPermissionMinStage) return SectPermissions.ReadHighScripture;
        if (stage >= SectConst.ScriptureCorePermissionMinStage) return SectPermissions.ReadCoreScripture;
        return SectPermissions.ReadBasicScripture;
    }

    private static bool IsMemberOfSect(Actor actor, Sect sect)
    {
        return actor != null
               && !actor.isRekt()
               && sect != null
               && !sect.isRekt()
               && actor.GetExtend().sect == sect;
    }

    private static bool ContainsScriptureBook(Sect sect, Book book)
    {
        IReadOnlyList<long> bookIds = sect.GetScriptureBookIds();
        for (int i = 0; i < bookIds.Count; i++)
        {
            if (bookIds[i] == book.id) return true;
        }

        return false;
    }

    private static int GetBookStage(Book book)
    {
        return GetBookLevel(book).Stage;
    }

    private static ItemLevel GetBookLevel(Book book)
    {
        BookExtend bookExtend = book.GetExtend();
        if (bookExtend.HasComponent<ItemLevel>())
        {
            return bookExtend.GetComponent<ItemLevel>();
        }

        if (book.getAsset() == BookTypes.Cultibook && bookExtend.HasComponent<Cultibook>())
        {
            CultibookAsset cultibook = bookExtend.GetComponent<Cultibook>().Asset;
            if (cultibook != null) return cultibook.Level;
        }

        if (book.getAsset() == BookTypes.Elixirbook && bookExtend.HasComponent<Elixirbook>())
        {
            ElixirAsset elixir = bookExtend.GetComponent<Elixirbook>().Asset;
            if (elixir != null) return elixir.base_level;
        }

        return default;
    }
}
