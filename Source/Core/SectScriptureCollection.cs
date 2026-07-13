using System.Collections.Generic;
using Cultiway.Content;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Debug;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Core;

/// <summary>
/// 管理宗门藏经阁中的藏书索引、去重和分类统计。
/// </summary>
public sealed class SectScriptureCollection
{
    private readonly Sect _sect;

    internal SectScriptureCollection(Sect sect)
    {
        _sect = sect;
    }

    public IReadOnlyList<long> BookIds => _sect.data.ScriptureBookIDs;
    public int Count => _sect.data.ScriptureBookIDs.Count;

    internal void Reset()
    {
        _sect.data.ScriptureBookIDs = new List<long>();
        RefreshStats();
    }

    public bool Add(Book book)
    {
        if (book == null || book.isRekt()) return false;
        if (Contains(book))
        {
            SectVerifyLog.Log("ScriptureAddSkip", $"sect={SectVerifyLog.Sect(_sect)} book={SectVerifyLog.Book(book)} reason=duplicate");
            return false;
        }

        _sect.data.ScriptureBookIDs.Add(book.id);
        RefreshStats();
        SectVerifyLog.Log("ScriptureAdd", $"sect={SectVerifyLog.Sect(_sect)} book={SectVerifyLog.Book(book)} cultibooks={_sect.data.CultibookCount} elixirs={_sect.data.ElixirRecipeCount} skills={_sect.data.SkillbookCount}");
        return true;
    }

    private bool Contains(Book book)
    {
        if (book == null || book.isRekt()) return false;

        BookExtend bookExtend = book.GetExtend();
        if (book.getAsset() == BookTypes.Cultibook && bookExtend.HasComponent<Cultibook>())
        {
            return Contains(bookExtend.GetComponent<Cultibook>().Asset);
        }

        if (book.getAsset() == BookTypes.Elixirbook && bookExtend.HasComponent<Elixirbook>())
        {
            return Contains(bookExtend.GetComponent<Elixirbook>().Asset);
        }

        if (book.getAsset() == BookTypes.Skillbook && bookExtend.HasComponent<Skillbook>())
        {
            return Contains(bookExtend.GetComponent<Skillbook>().SkillContainer);
        }

        return _sect.data.ScriptureBookIDs.Contains(book.id);
    }

    public bool Contains(CultibookAsset cultibook)
    {
        if (cultibook == null) return false;

        for (int i = 0; i < _sect.data.ScriptureBookIDs.Count; i++)
        {
            Book book = World.world.books.get(_sect.data.ScriptureBookIDs[i]);
            if (book == null || book.isRekt() || book.getAsset() != BookTypes.Cultibook) continue;

            BookExtend bookExtend = book.GetExtend();
            if (!bookExtend.HasComponent<Cultibook>()) continue;
            CultibookAsset existing = bookExtend.GetComponent<Cultibook>().Asset;
            if (existing != null && existing.id == cultibook.id) return true;
        }

        return false;
    }

    public bool Contains(ElixirAsset elixir)
    {
        if (elixir == null) return false;

        for (int i = 0; i < _sect.data.ScriptureBookIDs.Count; i++)
        {
            Book book = World.world.books.get(_sect.data.ScriptureBookIDs[i]);
            if (book == null || book.isRekt() || book.getAsset() != BookTypes.Elixirbook) continue;

            BookExtend bookExtend = book.GetExtend();
            if (!bookExtend.HasComponent<Elixirbook>()) continue;
            ElixirAsset existing = bookExtend.GetComponent<Elixirbook>().Asset;
            if (existing != null && existing.id == elixir.id) return true;
        }

        return false;
    }

    public bool Contains(Entity skillContainer)
    {
        if (skillContainer.IsNull) return false;

        for (int i = 0; i < _sect.data.ScriptureBookIDs.Count; i++)
        {
            Book book = World.world.books.get(_sect.data.ScriptureBookIDs[i]);
            if (book == null || book.isRekt() || book.getAsset() != BookTypes.Skillbook) continue;

            BookExtend bookExtend = book.GetExtend();
            if (!bookExtend.HasComponent<Skillbook>()) continue;
            if (SkillContainerUtils.IsSimilar(bookExtend.GetComponent<Skillbook>().SkillContainer, skillContainer)) return true;
        }

        return false;
    }

    public List<Book> GetBooks(BookTypeAsset bookType)
    {
        List<Book> result = new();
        for (int i = 0; i < _sect.data.ScriptureBookIDs.Count; i++)
        {
            Book book = World.world.books.get(_sect.data.ScriptureBookIDs[i]);
            if (book == null || book.isRekt() || book.getAsset() != bookType) continue;
            result.Add(book);
        }

        result.Sort(CompareBooks);
        return result;
    }

    private void RefreshStats()
    {
        int cultibooks = 0;
        int elixirRecipes = 0;
        int skillbooks = 0;
        for (int i = 0; i < _sect.data.ScriptureBookIDs.Count; i++)
        {
            Book book = World.world.books.get(_sect.data.ScriptureBookIDs[i]);
            if (book == null || book.isRekt()) continue;

            if (book.getAsset() == BookTypes.Cultibook)
            {
                cultibooks++;
            }
            else if (book.getAsset() == BookTypes.Elixirbook)
            {
                elixirRecipes++;
            }
            else if (book.getAsset() == BookTypes.Skillbook)
            {
                skillbooks++;
            }
        }

        _sect.data.CultibookCount = cultibooks;
        _sect.data.ElixirRecipeCount = elixirRecipes;
        _sect.data.SkillbookCount = skillbooks;
    }

    private static int CompareBooks(Book left, Book right)
    {
        int typeCompare = string.Compare(left.data.book_type, right.data.book_type, System.StringComparison.Ordinal);
        if (typeCompare != 0) return typeCompare;

        int nameCompare = string.Compare(left.data.name, right.data.name, System.StringComparison.CurrentCulture);
        return nameCompare != 0 ? nameCompare : left.id.CompareTo(right.id);
    }
}
