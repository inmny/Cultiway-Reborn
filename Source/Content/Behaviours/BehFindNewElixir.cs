using System;
using System.Collections.Generic;
using System.Linq;
using ai.behaviours;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Crafting;
using Cultiway.Core.ControlledTasks;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Behaviours;

public sealed class BehFindNewElixir : BehCityActor
{
    [Hotfixable]
    public override BehResult execute(Actor actor)
    {
        if (ControlledTaskOrderService.TryGetActiveOrderId(actor.getID(), out _))
        {
            if (!ControlledTaskOrderService.TryTakeExecutionContext(actor.getID(),
                    out ControlledElixirDiscoveryContext context))
            {
                ControlledTaskOrderService.ReportExecutionFailure(actor,
                    "Cultiway.ControlledTask.Reason.ExecutionContextMissing");
                return BehResult.Continue;
            }
            if (ElixirDiscoveryService.TryDiscover(actor, context.Materials,
                    out _, out string reasonLocaleKey))
                ControlledTaskOrderService.MarkExecutionCommitted(actor);
            else
                ControlledTaskOrderService.ReportExecutionFailure(actor, reasonLocaleKey);
            return BehResult.Continue;
        }

        if (CraftSessionService.HasActiveCraft(actor)) return BehResult.Stop;
        IHasInventory inventory = actor.GetExtend();
        using var pool = new ListPool<Entity>(inventory.GetItems()
            .Where(ElixirDiscoveryCommandConfigurator.IsValidMaterial));
        if (!pool.Any()) return BehResult.Stop;

        int availableCount = ((IList<Entity>)pool).Count;
        int ingredientCount = Math.Min(
            Randy.randomInt(1, Mathf.FloorToInt(Mathf.Log(availableCount)) + 2),
            availableCount);
        Entity[] materials = pool.SampleOut(ingredientCount).ToArray();
        if (!ElixirDiscoveryService.TryDiscover(actor, materials, out _, out _))
            return BehResult.Stop;
        actor.timer_action = Randy.randomFloat(TimeScales.SecPerMonth, TimeScales.SecPerYear);
        return BehResult.Continue;
    }
}
