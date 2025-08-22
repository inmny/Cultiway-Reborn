namespace Cultiway.Core;

public delegate void MetaTypeHistoryActionExtend(WindowHistoryExtend history_extend);
public class MetaTypeAssetExtend
{
    public MetaTypeHistoryActionExtend ExtendWindowHistoryActionUpdate;
    public MetaTypeHistoryActionExtend ExtendWindowHistoryActionRestore;
}