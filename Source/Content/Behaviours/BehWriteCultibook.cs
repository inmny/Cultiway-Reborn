using System.Linq;
using ai.behaviours;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours;

public class BehWriteCultibook : BehCityActor
{
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        var cultibook_to_share = ae.GetAllMaster<CultibookAsset>().ToList().GetRandom();
        if (!pObject.hasCity() || !pObject.city.hasBookSlots()) return BehResult.Continue;

        var city = pObject.getCity();
        foreach (var book_id in city.getBooks())
        {
            var book = World.world.books.get(book_id);
            var be = book.GetExtend();
            if (be.HasComponent<Cultibook>() && be.GetComponent<Cultibook>().Asset?.id == cultibook_to_share.Item1.id)
            {
                return BehResult.Continue;
            }
        }

        var new_book = World.world.books.WriteCultibookBook(pObject, cultibook_to_share.Item1, cultibook_to_share.Item2);
        World.world.books.TryStoreBookInCity(pObject, new_book);
        return BehResult.Continue;
    }
}
