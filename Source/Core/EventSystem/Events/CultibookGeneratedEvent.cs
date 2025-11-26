using Cultiway.Content.AIGC;
using Cultiway.Content.Libraries;

namespace Cultiway.Core.EventSystem.Events;

public struct CultibookGeneratedEvent
{
    public long ActorId;
    public string RequestId;
    public CultibookAsset Draft;
    public float ResponseSeconds;
}
