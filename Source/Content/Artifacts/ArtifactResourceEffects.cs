using System;
using Cultiway.Content.Components;
using Cultiway.Content.Events;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content.Artifacts;/// <summary>法器能力共用的生命与灵气转移原语。</summary>
public static class ArtifactResourceEffects
{
    public static void RestoreHealth(Actor target, float amount)
    {
        if (target == null || target.isRekt() || amount <= 0f) return;
        target.restoreHealth(Mathf.Max(1, Mathf.RoundToInt(amount)));
    }

    public static float RestoreWakan(Actor target, float amount)
    {
        if (target == null || target.isRekt() || amount <= 0f) return 0f;
        ActorExtend extend = target.GetExtend();
        if (!extend.HasCultisys<Xian>()) return 0f;

        ref Xian xian = ref extend.GetCultisys<Xian>();
        float capacity = Mathf.Max(0f, target.stats[BaseStatses.MaxWakan.id]);
        float restored = Mathf.Min(amount, Mathf.Max(0f, capacity - xian.wakan));
        xian.wakan += restored;
        return restored;
    }

    public static float DrainWakan(Actor target, float amount)
    {
        if (target == null || target.isRekt() || amount <= 0f) return 0f;
        ActorExtend extend = target.GetExtend();
        if (!extend.HasCultisys<Xian>()) return 0f;

        ref Xian xian = ref extend.GetCultisys<Xian>();
        float drained = Mathf.Min(amount, Mathf.Max(0f, xian.wakan));
        xian.wakan -= drained;
        return drained;
    }

    public static float TransferWakan(Actor source, Actor target, float amount)
    {
        float drained = DrainWakan(source, amount);
        float restored = RestoreWakan(target, drained);
        if (restored < drained) RestoreWakan(source, drained - restored);
        return restored;
    }
}
