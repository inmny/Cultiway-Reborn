using Cultiway.Abstract;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core;

public class BookExtend : ExtendComponent<Book>
{
    private Entity e;
    public override Entity E => e;
    public override Book Base => e.HasComponent<BookBinder>() ? e.GetComponent<BookBinder>().Book : null;
    
    public BookExtend(Entity e)
    {
        this.e = e;
        e.GetComponent<BookBinder>()._be = this;
    }
}