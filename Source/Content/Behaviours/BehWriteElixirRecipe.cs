using ai.behaviours;
using Cultiway.Content.Extensions;
using Cultiway.Content.Sects;

namespace Cultiway.Content.Behaviours;

public class BehWriteElixirRecipe : BehCityActor
{
    public override BehResult execute(Actor pObject)
    {
        if (!SectScriptureContributionPlanner.TryPickElixirRecipeTarget(pObject, out var target, out var elixir, out float mastery))
        {
            return BehResult.Continue;
        }

        var new_book = World.world.books.WriteElixirRecipeBook(pObject, elixir, mastery);
        target.StoreBook(pObject, new_book);
        return BehResult.Continue;
    }
}
