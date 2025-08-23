using System.Linq;
using ai.behaviours;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours;

public class BehWriteElixirRecipe : BehCityActor
{
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        var elixirbook_to_share = ae.GetAllMaster<ElixirAsset>().ToList().GetRandom();
        var city = pObject.getCity();
        foreach (var book_id in city.getBooks())
        {
            var book = World.world.books.get(book_id);
            var be = book.GetExtend();
            if (be.HasComponent<Elixirbook>() && be.GetComponent<Elixirbook>().Asset == elixirbook_to_share.Item1)
            {
                return BehResult.Continue;
            }
        }

        var new_book = World.world.books.GenerateNewBook(pObject, BookTypes.Elixirbook);
        if (new_book != null)
        {
            new_book.GetExtend().AddComponent(new Cultibook(elixirbook_to_share.Item1.id));
            new_book.data.name = elixirbook_to_share.Item1.GetName() + "丹方";
        }

        return BehResult.Continue;
    }
}