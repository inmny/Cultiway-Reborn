using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Debug;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Sects;

public static class SectScriptureService
{
    public static bool TryStoreContribution(Sect sect, Actor contributor, Book book)
    {
        int contribution = SectTraitRules.GetWriteScriptureContributionReward(sect);
        return TryStore(sect, book, contributor, contribution);
    }

    private static bool TryStore(Sect sect, Book book, Actor contributor, int contribution)
    {
        if (sect == null || sect.isRekt()) return false;
        if (book == null || book.isRekt()) return false;
        if (contributor != null && !SectScripturePolicy.CanContribute(contributor, sect)) return false;
        if (!sect.Scriptures.Add(book)) return false;

        if (contributor != null && contribution > 0)
        {
            sect.AddContribution(contributor, contribution);
        }

        WorldLogUtils.LogSectScriptureContributed(sect, contributor, book);
        return true;
    }

    public static void CreateDoctrineBook(Sect sect, Actor founder, CultibookAsset cultibook, float mastery)
    {
        if (founder == null || founder.isRekt() || founder.language == null) return;

        Book book = World.world.books.NewBook(founder, BookTypes.Cultibook);
        if (book == null) return;

        BookExtend bookExtend = book.GetExtend();
        bookExtend.AddComponent(new Cultibook(cultibook.id));
        bookExtend.AddComponent(cultibook.Level);
        bookExtend.Master(cultibook, Mathf.Max(1f, mastery));
        book.data.name = cultibook.Name;
        sect.Scriptures.Add(book);
        SectVerifyLog.Log("DoctrineBook", $"sect={SectVerifyLog.Sect(sect)} founder={SectVerifyLog.Actor(founder)} book={SectVerifyLog.Book(book)} cultibook={cultibook.id} mastery={mastery:F1}");
    }
}
