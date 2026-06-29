using ai.behaviours;
using Cultiway.Content.Extensions;

namespace Cultiway.Content.Behaviours;

public class BehWriteCultibook : BehCityActor
{
    public override BehResult execute(Actor pObject)
    {
        if (!pObject.TryPickCultibookTarget(out var target, out var cultibook, out float mastery))
        {
            return BehResult.Continue;
        }

        var new_book = World.world.books.WriteCultibookBook(pObject, cultibook, mastery);
        target.StoreBook(pObject, new_book);
        return BehResult.Continue;
    }
}
