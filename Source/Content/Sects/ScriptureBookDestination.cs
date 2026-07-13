using Cultiway.Core;
using Cultiway.Debug;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Sects;

public readonly struct ScriptureBookDestination
{
    private readonly City _city;
    private readonly Sect _sect;

    private ScriptureBookDestination(City city, Sect sect)
    {
        _city = city;
        _sect = sect;
    }

    public static ScriptureBookDestination ForCity(City city)
    {
        return new ScriptureBookDestination(city, null);
    }

    public static ScriptureBookDestination ForSect(Sect sect)
    {
        return new ScriptureBookDestination(null, sect);
    }

    public bool StoreBook(Actor contributor, Book book)
    {
        if (_sect != null)
        {
            bool result = SectScriptureService.TryStoreContribution(_sect, contributor, book);
            SectVerifyLog.Log("StoreScriptureBook", $"target=sect sect={SectVerifyLog.Sect(_sect)} contributor={SectVerifyLog.Actor(contributor)} book={SectVerifyLog.Book(book)} result={result}");
            return result;
        }

        if (_city != null)
        {
            bool result = World.world.books.TryStoreBookInCity(_city, contributor, book);
            SectVerifyLog.Log("StoreScriptureBook", $"target=city city={_city.name}#{_city.data.id} contributor={SectVerifyLog.Actor(contributor)} book={SectVerifyLog.Book(book)} result={result}");
            return result;
        }

        return false;
    }
}
