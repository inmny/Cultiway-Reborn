using Cultiway.Core.Logging;

namespace Cultiway.Content.Railways;

/// <summary>铁路只记录任务和状态变化，不记录连续移动。</summary>
internal static class RailwayLog
{
    /// <summary>铁路关键事件。</summary>
    private static readonly CultiLogEventDef change = new(3000, "Train.Change", CultiLogCategory.Train,
        CultiLogLevel.Info, "{message}");

    /// <summary>写入可按车辆和车站查找的事件。</summary>
    /// <param name="action">已本地化的动作键后缀。</param>
    /// <param name="actor">车辆；线路事件可不指定。</param>
    /// <param name="station">相关车站。</param>
    /// <param name="detail">装卸或损失的实际数量说明。</param>
    internal static void Record(string action, Actor actor = null, long station = -1, string detail = null)
    {
        if (!CultiLog.IsEnabled(change)) return;
        string message = LocalizedTextManager.getText("Cultiway.Railways.Log." + action);
        if (detail != null) message = string.Format(message, detail);
        CultiLog.Emit(change, CultiLog.Args().Str("message", message).Build(),
            actorId: actor?.id ?? -1, targetId: station);
    }
}
