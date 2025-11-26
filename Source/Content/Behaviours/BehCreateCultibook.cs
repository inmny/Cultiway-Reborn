using System;
using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.AIGC;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Behaviours;

public class BehCreateCultibook : BehCityActor
{
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();

        pObject.data.get(ContentActorDataKeys.WaitingForCultibookCreation_int, out int state, -1);
        if (state == -1)
        {
            var requestId = Guid.NewGuid().ToString();
            var creationDuration = Randy.randomFloat(TimeScales.SecPerYear, TimeScales.SecPerYear * 3);
            pObject.timer_action = creationDuration;
            StayInside(pObject);
            pObject.data.set(ContentActorDataKeys.WaitingForCultibookCreation_int, 1);
            CultibookGenerator.Instance.RequestGeneration(ae, requestId);
            return BehResult.RepeatStep;
        }
        if (state == 1)
        {
            StayInside(pObject);
            return BehResult.RepeatStep;
        }
        return BehResult.Continue;
    }

    private static void StayInside(Actor actor)
    {
        if (actor.beh_building_target != null)
        {
            actor.stayInBuilding(actor.beh_building_target);
        }
        else if (actor.inside_building != null)
        {
            actor.stayInBuilding(actor.inside_building);
        }
    }
}
