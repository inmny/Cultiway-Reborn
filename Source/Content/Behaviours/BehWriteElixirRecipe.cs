using ai.behaviours;
using Cultiway.Content.Extensions;

namespace Cultiway.Content.Behaviours;

public class BehWriteElixirRecipe : BehCityActor
{
    public override BehResult execute(Actor pObject)
    {
        if (!pObject.TryPickElixirRecipeTarget(out var target, out var elixir, out float mastery))
        {
            return BehResult.Continue;
        }

        var new_book = World.world.books.WriteElixirRecipeBook(pObject, elixir, mastery);
        target.StoreBook(pObject, new_book);
        return BehResult.Continue;
    }
}
