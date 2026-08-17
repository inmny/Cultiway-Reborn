using Cultiway.Content.AIGC;

namespace Cultiway.Content.Events;

public struct CultibookImprovedEvent
{
    public long WorldSessionId;
    public long ActorId;
    public long OrderId;
    public string RequestId;
    public string OriginalCultibookId;
    public CultibookDraftDto ImprovedDraft;
    public bool UsedFallback;
    public string GeneratorError;
    public float ResponseSeconds;
}
