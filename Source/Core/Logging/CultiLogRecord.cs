namespace Cultiway.Core.Logging;

public struct CultiLogRecord
{
    public long Sequence;
    public long RealTicks;
    public float WorldTime;
    public int EventId;
    public string EventName;
    public string Template;
    public CultiLogCategory Category;
    public CultiLogLevel Level;
    public long ActorId;
    public long TargetId;
    public long EntityId;
    public int X;
    public int Y;
    public CultiLogArg[] Args;

    public string RenderMessage()
    {
        return CultiLogFormatter.Format(Template, Args);
    }
}
