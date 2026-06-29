using ai.behaviours;
using Cultiway.Content.Extensions;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

public class BehTryReadSectScripture : BehaviourActionActor
{
    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        if (!SectScriptureStudyRules.TryPickStudyBook(pObject, out Book book))
        {
            return BehResult.Stop;
        }

        book.readIt();
        pObject.beh_book_target = book;
        return BehResult.Continue;
    }
}
