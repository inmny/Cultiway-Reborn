using Friflo.Engine.ECS;
using Friflo.Json.Fliox;

namespace Cultiway.Core.Components;

public struct BookBinder(long id) : IComponent
{
    public long ID = id;
    [Ignore]
    public Book Book
    {
        get
        {
            if (_book != null && _book.id == ID) return _book;

            _book = World.world.books.get(ID);

            return _book;
        }
    }

    [Ignore]
    public   BookExtend BE => _be;
    [Ignore]
    internal BookExtend _be;
    [Ignore]
    private  Book       _book;
}