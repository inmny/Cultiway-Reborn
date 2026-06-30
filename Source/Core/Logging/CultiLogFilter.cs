namespace Cultiway.Core.Logging;

public sealed class CultiLogFilter
{
    public CultiLogCategory Categories = CultiLogCategory.All;
    public CultiLogLevel MinLevel = CultiLogLevel.Trace;
    public int EventId;
    public long? ActorId;
    public long? TargetId;
    public string TextContains;

    public bool Matches(CultiLogRecord record)
    {
        if ((record.Category & Categories) == 0) return false;
        if (record.Level < MinLevel) return false;
        if (EventId > 0 && record.EventId != EventId) return false;
        if (ActorId.HasValue && record.ActorId != ActorId.Value && record.TargetId != ActorId.Value) return false;
        if (TargetId.HasValue && record.TargetId != TargetId.Value) return false;

        if (!string.IsNullOrEmpty(TextContains))
        {
            string message = record.RenderMessage();
            if (message == null || !message.Contains(TextContains)) return false;
        }

        return true;
    }
}
