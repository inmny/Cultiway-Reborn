using Cultiway.Content.Combat;
using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>真实神魂与召唤物出现、回归时的短暂空间表现，不承担生成或销毁规则。</summary>
internal static class ActorManifestationVisual
{
    /// <summary>围绕实际身体聚拢或散开少量灵流。</summary>
    /// <param name="actor">已经出现或即将退场的真实生物。</param><param name="arriving">是否正在出现。</param>
    internal static void Show(Actor actor, bool arriving)
    {
        Vector3 point = SpatialActorPose.Foot(actor);
        float height = SpatialActorPose.Height(actor);
        Color color = new(.55f, .68f, 1f, .5f);
        SpatialVisualEffects.Ring(point + Vector3.forward * .04f, height * .45f, color, .3f, arriving);
        for (int i = 0; i < 4; i++)
        {
            float angle = i * Mathf.PI * .5f;
            Vector3 side = new(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            SpatialVisualEffects.Particle(point + side * height * .25f, side * (arriving ? -.3f : .3f) + Vector3.forward * .4f,
                color, height * .06f, .3f);
        }
    }
}
