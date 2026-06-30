namespace Cultiway.Core.Logging;

public readonly struct CultiLogStats
{
    public readonly bool Enabled;
    public readonly bool DiskEnabled;
    public readonly CultiLogCategory CategoryMask;
    public readonly CultiLogLevel MinLevel;
    public readonly int QueuedCount;
    public readonly int RecentCount;
    public readonly int RecentCapacity;
    public readonly long WrittenCount;
    public readonly long DroppedCount;

    public CultiLogStats(bool enabled, bool diskEnabled, CultiLogCategory categoryMask, CultiLogLevel minLevel,
        int queuedCount, int recentCount, int recentCapacity, long writtenCount, long droppedCount)
    {
        Enabled = enabled;
        DiskEnabled = diskEnabled;
        CategoryMask = categoryMask;
        MinLevel = minLevel;
        QueuedCount = queuedCount;
        RecentCount = recentCount;
        RecentCapacity = recentCapacity;
        WrittenCount = writtenCount;
        DroppedCount = droppedCount;
    }
}
