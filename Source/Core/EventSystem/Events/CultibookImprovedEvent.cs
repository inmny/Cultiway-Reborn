using Cultiway.Content.AIGC;
using Cultiway.Content.Libraries;

namespace Cultiway.Core.EventSystem.Events;

public struct CultibookImprovedEvent
{
    public long ActorId;
    public string RequestId;
    public CultibookAsset OriginalCultibook;
    public CultibookAsset ImprovedDraft;
    public float ResponseSeconds;
}

