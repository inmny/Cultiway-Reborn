using Cultiway.Content.AIGC;

namespace Cultiway.Content.Events;

public struct CultibookGeneratedEvent
{
    public long WorldSessionId;
    public long ActorId;
    public long OrderId;
    public string RequestId;
    public CultibookDraftDto Draft;
    public bool UsedFallback;
    public string GeneratorError;
    public float ResponseSeconds;
}
