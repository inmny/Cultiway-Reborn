using System.Runtime.CompilerServices;
using Cultiway.Abstract;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core;

public class BookExtendManager : ExtendComponentManager<BookExtend>
{
    public readonly EntityStore                     World;
    private ConditionalWeakTable<BookData, BookExtend> _book_to_extend = new();
    private object _lock = new();

    internal BookExtendManager(EntityStore world)
    {
        World = world;
    }
    [MethodImpl(MethodImplOptions.Synchronized)]
    public BookExtend Get(Book book)
    {
        lock (_lock)
        {
             if (_book_to_extend.TryGetValue(book.data, out var val))
             {
                 // 检查BookBinder.ID是否匹配，防止BookData被重用导致的问题
                 ref var binder = ref val.E.GetComponent<BookBinder>();
                 if (binder.ID != book.data.id)
                 {
                     ModClass.LogWarning($"BookExtend for Book {book.data.id} ({val.E}) has mismatched ID {binder.ID}, expected {book.data.id}.");
                 }
             }
             else
             {
                 val = new BookExtend(World.CreateEntity(new BookBinder(book.data.id)));
                 ModClass.LogInfo($"Creating BookExtend for Book {book.data.id} ({val.E.GetComponent<BookBinder>().ID}) ({val.E})");
                 _book_to_extend.Add(book.data, val);
             }
             if (val.Base == null)
             {
                 ModClass.LogInfo($"BookExtend for Book {book.data.id} ({val.E.GetComponent<BookBinder>().ID}) ({val.E}) has null Base, this should not happen.");
             }
             return val;
        }
    }
    public bool Has(Book book)
    {
        return _book_to_extend.TryGetValue(book.data, out var val);
    }
    public void Remove(Book book)
    {
        if (_book_to_extend.TryGetValue(book.data, out var val))
        {
            _book_to_extend.Remove(book.data);
        }
    }
    public void Clear()
    {
        _book_to_extend = new ConditionalWeakTable<BookData, BookExtend>();
    }
}