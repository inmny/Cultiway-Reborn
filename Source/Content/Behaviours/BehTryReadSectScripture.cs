using ai.behaviours;
using Cultiway.Content.Extensions;
using Cultiway.Debug;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

public class BehTryReadSectScripture : BehaviourActionActor
{
    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        if (!SectScriptureStudyRules.TryPickStudyBook(pObject, out Book book))
        {
            SectVerifyLog.Log("StudyScriptureTask", $"actor={SectVerifyLog.Actor(pObject)} result=false");
            return BehResult.Stop;
        }

        book.readIt();
        pObject.beh_book_target = book;
        SectVerifyLog.Log("StudyScriptureTask", $"actor={SectVerifyLog.Actor(pObject)} book={SectVerifyLog.Book(book)} result=true");
        return BehResult.Continue;
    }
}
