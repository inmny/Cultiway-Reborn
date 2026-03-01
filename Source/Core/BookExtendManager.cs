using System.Collections.Concurrent;
using Cultiway.Abstract;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using NeoModLoader.services;
using NeoModLoader.utils;

namespace Cultiway.Core;

public class BookExtendManager : ExtendComponentManager<BookExtend>
{
    public readonly EntityStore World;
    private readonly ConcurrentDictionary<BookData, BookExtend> _book_to_extend = new();
    private readonly ConcurrentDictionary<BookData, string> _book_to_create_stacktrace = new();
    internal BookExtendManager(EntityStore world)
    {
        World = world;
    }

    public BookExtend Get(Book book)
    {
        var bookData = book.data;
        var bookId = bookData.id;

        // 所有对EntityStore的访问都需要在锁的保护下进行
        lock (EntityStoreLock.GlobalLock)
        {
            if (_book_to_extend.TryGetValue(bookData, out var val))
            {
                ref var binder = ref val.E.GetComponent<BookBinder>();
                if (binder.ID == bookId)
                {
                    return val;
                }

                ModClass.LogWarning($"BookBinder错位。Book {bookId} ({val.E}) Binder: {binder.ID}, Binder book: {binder._book?.data.id}");
                LogService.LogStackTraceAsWarning();

                LogService.LogWarning($"错位的BookBinder创建于：\n{(_book_to_create_stacktrace.TryGetValue(bookData, out var stacktrace) ? stacktrace : "未知")}");
                return val;
            }

            // 创建新的BookExtend
            var newExtend = new BookExtend(World.CreateEntity(new BookBinder(bookId)));
            //ModClass.LogInfo($"Creating BookExtend for Book {bookId} ({newExtend.E})");
            //_book_to_create_stacktrace[bookData] = OtherUtils.GetStackTrace(1);
            _book_to_extend[bookData] = newExtend;
            return newExtend;
        }
    }

    public bool Has(Book book)
    {
        return _book_to_extend.TryGetValue(book.data, out var val);
    }

    public void Remove(Book book)
    {
        _book_to_extend.TryRemove(book.data, out _);
        //_book_to_create_stacktrace.TryRemove(book.data, out _);
    }

    public void Clear()
    {
        lock (EntityStoreLock.GlobalLock)
        {
            _book_to_extend.Clear();
            //_book_to_create_stacktrace.Clear();
        }
    }
}