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

public class BehPrepareCreateCultibook : BehCityActor
{
    public override BehResult execute(Actor pObject)
    {
        pObject.data.set(ContentActorDataKeys.WaitingForCultibookCreation_int, -1);
        return BehResult.Continue;
    }
}
