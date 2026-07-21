using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Sects;

public static class SectScripturePolicy
{
    public static bool CanAccess(Actor actor, Book book)
    {
        if (actor == null || actor.isRekt()) return false;
        if (book == null || book.isRekt()) return false;

        Sect sect = actor.GetExtend().sect;
        if (!IsMember(actor, sect)) return false;

        IReadOnlyList<long> bookIds = sect.Scriptures.BookIds;
        for (int i = 0; i < bookIds.Count; i++)
        {
            if (bookIds[i] == book.id) return true;
        }

        return false;
    }

    public static bool HasReadPermission(Actor actor, Book book)
    {
        if (actor == null || actor.isRekt()) return false;
        if (book == null || book.isRekt()) return false;

        SectPermissionAsset permission = GetReadPermission(book);
        if (permission == SectPermissions.ReadHighScripture)
        {
            return actor.HasSectPermission(SectPermissions.ReadHighScripture);
        }

        if (permission == SectPermissions.ReadCoreScripture)
        {
            return actor.HasSectPermission(SectPermissions.ReadCoreScripture)
                   || actor.HasSectPermission(SectPermissions.ReadHighScripture);
        }

        return actor.HasSectPermission(SectPermissions.ReadBasicScripture)
               || actor.HasSectPermission(SectPermissions.ReadCoreScripture)
               || actor.HasSectPermission(SectPermissions.ReadHighScripture);
    }

    public static int GetReadCost(Actor actor, Book book)
    {
        if (!CanAccess(actor, book)) return int.MaxValue;

        Sect sect = actor.GetExtend().sect;
        float multiplier = HasReadPermission(actor, book)
            ? SectConst.ScriptureReadPermissionDiscount
            : SectConst.ScriptureReadOutOfPermissionMultiplier * SectTraitRules.GetOutOfPermissionReadCostMultiplier(sect);
        return Mathf.CeilToInt(GetBaseReadCost(book) * multiplier);
    }

    public static bool CanAffordRead(Actor actor, Book book)
    {
        int cost = GetReadCost(actor, book);
        return cost != int.MaxValue && actor.GetAvailableSectContribution() >= cost;
    }

    public static bool CanContribute(Actor actor, Sect sect)
    {
        return IsMember(actor, sect)
               && actor.HasSectPermission(SectPermissions.WriteScripture);
    }

    public static bool CanAccept(Sect sect, CultibookAsset cultibook)
    {
        return sect != null
               && !sect.isRekt()
               && cultibook != null
               && !sect.Scriptures.Contains(cultibook);
    }

    public static bool CanAccept(Sect sect, ElixirAsset elixir)
    {
        return sect != null
               && !sect.isRekt()
               && elixir != null
               && !sect.Scriptures.Contains(elixir);
    }

    public static bool CanAccept(Sect sect, Entity skillContainer)
    {
        return sect != null
               && !sect.isRekt()
               && !skillContainer.IsNull
               && !sect.Scriptures.Contains(skillContainer);
    }

    private static SectPermissionAsset GetReadPermission(Book book)
    {
        int stage = GetBookLevel(book).Stage;
        if (stage >= SectConst.ScriptureHighPermissionMinStage) return SectPermissions.ReadHighScripture;
        if (stage >= SectConst.ScriptureCorePermissionMinStage) return SectPermissions.ReadCoreScripture;
        return SectPermissions.ReadBasicScripture;
    }

    private static bool IsMember(Actor actor, Sect sect)
    {
        return actor != null
               && !actor.isRekt()
               && sect != null
               && !sect.isRekt()
               && actor.GetExtend().sect == sect;
    }

    private static int GetBaseReadCost(Book book)
    {
        int stage = GetBookLevel(book).Stage;
        if (stage >= SectConst.ScriptureHighPermissionMinStage) return SectConst.ScriptureHighReadCost;
        if (stage >= SectConst.ScriptureCorePermissionMinStage) return SectConst.ScriptureCoreReadCost;
        return SectConst.ScriptureBasicReadCost;
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
            if (elixir != null) return ItemLevel.FromValue(elixir.recipe_context.quality_stage * 9);
        }

        return default;
    }
}
