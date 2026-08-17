using System;
using ai.behaviours;

namespace Cultiway.Core.ControlledTasks;

/// <summary>可由玩家显式下达的角色任务资产；资格查询和目标上下文均由资产本身声明。</summary>
public sealed class ControlledTaskCommandAsset : Asset
{
    public BehaviourTaskActor Task;
    public string NameLocaleKey;
    public string DescriptionLocaleKey;
    public string IconPath = "ui/icons/iconShowTasks";
    public ControlledTaskCategory Category;
    public int Order;
    public ControlledTaskTargetMode TargetMode;
    public bool RequiresConfirmation;

    [NonSerialized] public Func<Actor, ControlledTaskAvailability> EvaluateActor;
    [NonSerialized] public Func<Actor, WorldTile, ControlledTaskAvailability> ValidateWorldTile;
    [NonSerialized] public Action<Actor, WorldTile> ApplyWorldTileContext;

    public ControlledTaskAvailability Evaluate(Actor actor)
    {
        return EvaluateActor?.Invoke(actor) ?? ControlledTaskAvailability.Available;
    }

    public ControlledTaskAvailability ValidateTarget(Actor actor, WorldTile tile)
    {
        if (TargetMode == ControlledTaskTargetMode.None)
            return tile == null
                ? ControlledTaskAvailability.Available
                : ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.TargetUnexpected");
        if (tile == null)
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.TargetMissing");
        return ValidateWorldTile?.Invoke(actor, tile) ?? ControlledTaskAvailability.Available;
    }

    internal void ApplyTargetContext(Actor actor, WorldTile tile)
    {
        if (TargetMode == ControlledTaskTargetMode.WorldTile) ApplyWorldTileContext?.Invoke(actor, tile);
    }
}
