using Cultiway.Core;

namespace Cultiway.Utils.Extension;

public static class BookTools
{
    private static readonly BookExtendManager BookExtendManager = ModClass.I.BookExtendManager;

    public static BookExtend GetExtend(this Book book)
    {
        return BookExtendManager.Get(book);
    }
}