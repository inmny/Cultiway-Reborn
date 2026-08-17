using System;
using System.Collections.Generic;
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

    [NonSerialized] public IControlledTaskCommandConfigurator Configurator;

    [NonSerialized] public Func<Actor, ControlledTaskAvailability> EvaluateActor;
    [NonSerialized] public Func<Actor, WorldTile, ControlledTaskAvailability> ValidateWorldTile;
    [NonSerialized] public Action<Actor, WorldTile> ApplyWorldTileContext;

    public IReadOnlyList<ControlledTaskParameterDefinition> Parameters =>
        Configurator?.Parameters ?? Array.Empty<ControlledTaskParameterDefinition>();

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

    public ControlledTaskAvailability ValidateInvocation(Actor actor, ControlledTaskInvocation invocation)
    {
        ControlledTaskAvailability targetAvailability = ValidateTarget(actor, invocation.Target.ResolveTile());
        if (!targetAvailability.Enabled) return targetAvailability;

        IReadOnlyList<ControlledTaskParameterDefinition> parameters = Parameters;
        for (int i = 0; i < parameters.Count; i++)
        {
            ControlledTaskParameterDefinition definition = parameters[i];
            int selected = invocation.GetSelections(definition.Key).Count;
            if (selected < definition.MinSelected || selected > definition.MaxSelected)
                return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.ParameterSelectionInvalid");
        }

        return Configurator?.Validate(actor, invocation) ?? ControlledTaskAvailability.Available;
    }

    public IReadOnlyList<ControlledTaskOption> GetOptions(
        Actor actor,
        string parameterKey,
        ControlledTaskInvocation invocation)
    {
        return Configurator?.GetOptions(actor, parameterKey, invocation) ??
               Array.Empty<ControlledTaskOption>();
    }

    public string GetInvocationSummary(Actor actor, ControlledTaskInvocation invocation)
    {
        return Configurator is IControlledTaskInvocationSummaryProvider provider
            ? provider.GetInvocationSummary(actor, invocation) ?? string.Empty
            : string.Empty;
    }

    internal IControlledTaskExecutionContext PrepareInvocation(Actor actor, ControlledTaskInvocation invocation)
    {
        return Configurator?.Prepare(actor, invocation);
    }

    internal void ApplyTargetContext(Actor actor, WorldTile tile)
    {
        if (TargetMode == ControlledTaskTargetMode.WorldTile) ApplyWorldTileContext?.Invoke(actor, tile);
    }
}
