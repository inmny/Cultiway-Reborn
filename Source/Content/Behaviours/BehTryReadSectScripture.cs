using ai.behaviours;
using Cultiway.Content.Sects;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

public class BehTryReadSectScripture : BehaviourActionActor
{
    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        return SectScriptureStudyService.TryStudy(pObject) ? BehResult.Continue : BehResult.Stop;
    }
}
