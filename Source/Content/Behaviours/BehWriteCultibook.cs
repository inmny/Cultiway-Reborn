using System.Linq;
using ai.behaviours;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours;

public class BehWriteCultibook : BehCityActor
{
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        var cultibook_to_share = ae.GetAllMaster<CultibookAsset>().ToList().GetRandom();
        var city = pObject.getCity();
        foreach (var book_id in city.getBooks())
        {
            var book = World.world.books.get(book_id);
            var be = book.GetExtend();
            if (be.HasComponent<Cultibook>() && be.GetComponent<Cultibook>().Asset == cultibook_to_share.Item1)
            {
                return BehResult.Continue;
            }
        }

        var new_book = World.world.books.GenerateNewBook(pObject, BookTypes.Cultibook);
        if (new_book != null)
        {
            var new_be = new_book.GetExtend();
            new_be.AddComponent(new Cultibook(cultibook_to_share.Item1.id));
            new_be.AddComponent(cultibook_to_share.Item1.Level);
            new_be.Master(cultibook_to_share.Item1, cultibook_to_share.Item2);
            new_book.data.name = cultibook_to_share.Item1.Name;
        }

        return BehResult.Continue;
    }
}