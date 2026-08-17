using System;

namespace Cultiway.Core.ControlledTasks;

public enum ControlledTaskTargetMode
{
    None,
    WorldTile
}

public enum ControlledTaskCategory
{
    Movement,
    Cultivation,
    Crafting,
    Research,
    Sect,
    Affairs
}

public enum ControlledTaskOrderState
{
    Running,
    Completed,
    Failed,
    Interrupted,
    Cancelled,
    ActorLost
}

public readonly struct ControlledTaskAvailability
{
    public static readonly ControlledTaskAvailability Available = new(true, string.Empty);

    public bool Enabled { get; }
    public string ReasonLocaleKey { get; }

    private ControlledTaskAvailability(bool enabled, string reasonLocaleKey)
    {
        Enabled = enabled;
        ReasonLocaleKey = reasonLocaleKey ?? string.Empty;
    }

    public static ControlledTaskAvailability Unavailable(string reasonLocaleKey)
    {
        if (string.IsNullOrEmpty(reasonLocaleKey))
            throw new ArgumentException("Disabled controlled task availability requires a reason locale key.",
                nameof(reasonLocaleKey));
        return new ControlledTaskAvailability(false, reasonLocaleKey);
    }
}

public readonly struct ControlledTaskTarget
{
    public static readonly ControlledTaskTarget None = new(ControlledTaskTargetMode.None, -1);

    public ControlledTaskTargetMode Mode { get; }
    public int TileId { get; }

    private ControlledTaskTarget(ControlledTaskTargetMode mode, int tileId)
    {
        Mode = mode;
        TileId = tileId;
    }

    public static ControlledTaskTarget ForTile(WorldTile tile)
    {
        if (tile == null) throw new ArgumentNullException(nameof(tile));
        return new ControlledTaskTarget(ControlledTaskTargetMode.WorldTile, tile.tile_id);
    }

    public WorldTile ResolveTile()
    {
        if (Mode != ControlledTaskTargetMode.WorldTile || World.world?.tiles_list == null || TileId < 0 ||
            TileId >= World.world.tiles_list.Length)
            return null;
        return World.world.tiles_list[TileId];
    }
}

public readonly struct ControlledTaskStartResult
{
    public bool Success { get; }
    public long OrderId { get; }
    public string ReasonLocaleKey { get; }

    private ControlledTaskStartResult(bool success, long orderId, string reasonLocaleKey)
    {
        Success = success;
        OrderId = orderId;
        ReasonLocaleKey = reasonLocaleKey ?? string.Empty;
    }

    internal static ControlledTaskStartResult Started(long orderId)
    {
        return new ControlledTaskStartResult(true, orderId, string.Empty);
    }

    internal static ControlledTaskStartResult Rejected(string reasonLocaleKey)
    {
        return new ControlledTaskStartResult(false, 0, reasonLocaleKey);
    }
}

public readonly struct ControlledTaskOrderView
{
    public long OrderId { get; }
    public long ActorId { get; }
    public string ActorName { get; }
    public string CommandId { get; }
    public string CommandNameLocaleKey { get; }
    public string IconPath { get; }
    public ControlledTaskOrderState State { get; }
    public string ReasonLocaleKey { get; }
    public float StartedAt { get; }
    public bool CanLocate { get; }
    public bool CanCancel { get; }

    internal ControlledTaskOrderView(long orderId, long actorId, string actorName, string commandId,
        string commandNameLocaleKey, string iconPath, ControlledTaskOrderState state, string reasonLocaleKey,
        float startedAt, bool canLocate, bool canCancel)
    {
        OrderId = orderId;
        ActorId = actorId;
        ActorName = actorName;
        CommandId = commandId;
        CommandNameLocaleKey = commandNameLocaleKey;
        IconPath = iconPath;
        State = state;
        ReasonLocaleKey = reasonLocaleKey ?? string.Empty;
        StartedAt = startedAt;
        CanLocate = canLocate;
        CanCancel = canCancel;
    }
}
