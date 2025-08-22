using System.Runtime.CompilerServices;
using Cultiway.Abstract;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core;

public class BookExtendManager : ExtendComponentManager<BookExtend>
{
    public readonly EntityStore                     World;
    private ConditionalWeakTable<BookData, BookExtend> _book_to_extend = new();

    internal BookExtendManager(EntityStore world)
    {
        World = world;
    }
    public BookExtend Get(Book book)
    {
        if (_book_to_extend.TryGetValue(book.data, out var val)) return val;
        val = new BookExtend(World.CreateEntity(new BookBinder(book.data.id)));
        _book_to_extend.Add(book.data, val);
        return val;
    }
}