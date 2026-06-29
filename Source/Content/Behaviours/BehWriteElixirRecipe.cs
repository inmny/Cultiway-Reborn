using System.Linq;
using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours;

public class BehWriteElixirRecipe : BehCityActor
{
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        var elixirbook_to_share = ae.GetAllMaster<ElixirAsset>().ToList().GetRandom();
        if (!pObject.hasCity() || !pObject.city.hasBookSlots()) return BehResult.Continue;

        var city = pObject.getCity();
        foreach (var book_id in city.getBooks())
        {
            var book = World.world.books.get(book_id);
            var be = book.GetExtend();
            if (be.HasComponent<Elixirbook>() && be.GetComponent<Elixirbook>().Asset?.id == elixirbook_to_share.Item1.id)
            {
                return BehResult.Continue;
            }
        }

        var new_book = World.world.books.WriteElixirRecipeBook(pObject, elixirbook_to_share.Item1, elixirbook_to_share.Item2);
        World.world.books.TryStoreBookInCity(pObject, new_book);
        return BehResult.Continue;
    }
}
