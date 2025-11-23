using Cultiway.Abstract;
using Cultiway.Core.Libraries;
using strings;
using UnityEngine;

namespace Cultiway.Content;

public class StatusEffects : ExtendLibrary<StatusEffectAsset, StatusEffects>
{
    public static StatusEffectAsset Enlighten { get; private set; }
    public static StatusEffectAsset Slow { get; private set; }
    protected override void OnInit()
    {
        Enlighten = StatusEffectAsset.StartBuild(nameof(Enlighten))
            .SetDuration(60)
            .EnableParticle(new Color(1f, 0.85f, 0.35f), 1, 0.1f)
            .Build();
        Slow = StatusEffectAsset.StartBuild(nameof(Slow))
            .SetDuration(3f)
            .SetStats(new BaseStats
            {
                [S.multiplier_speed] = -1f
            })
            .EnableParticle(new Color(0.4f, 0.6f, 1f), 1, 0.1f)
            .Build();
    }
}
