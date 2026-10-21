using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Core.Components;
using Cultiway.Core.Visuals;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>魔法护持读取真实人物和法力，常态克制，实际消耗法力减伤时短暂响应。</summary>
internal static class MagicShieldVfx
{
    /// <summary>按真实身体建立低亮防护弧，法力耗尽后立即消失。</summary>
    /// <param name="query">当前世界的魔法修炼者。</param><param name="output">本帧共用快照。</param>
    internal static void Append(ArchetypeQuery<ActorBinder, Magic> query, List<SpatialVisualMeshInstance> output)
    {
        float now = (float)World.world.getCurWorldTime();
        query.ForEachComponents((ref ActorBinder binder, ref Magic _) =>
        {
            Actor actor = binder.Actor;
            if (actor == null || !actor.isAlive() || actor.getMana() < 1f) return;
            Vector3 foot = SpatialActorPose.Foot(actor);
            float height = SpatialActorPose.Height(actor);
            if (!SpatialVisualEffects.IsRelevant(foot, height)) return;
            Color color = new(.4f, .65f, 1f, .11f);
            SpatialVisualDraw.Mesh(output, SpatialVisualGeometry.Arc, foot + Vector3.forward * height * .5f,
                Quaternion.Euler(70f, 0f, now * 13f), Vector3.one * height * .55f, color, true, foot);
            output[output.Count - 1] = output[output.Count - 1].WithBodyAnchor(actor);
        });
    }

    /// <summary>调用者已确认发生法力支付和减伤，仅在身体周围产生短纹。</summary>
    /// <param name="actor">实际承受攻击的身体。</param>
    internal static void OnAbsorb(Actor actor)
    {
        Vector3 foot = SpatialActorPose.Foot(actor);
        float height = SpatialActorPose.Height(actor);
        SpatialVisualEffects.Ring(foot + Vector3.forward * height * .5f, height * .55f, new Color(.6f, .8f, 1f, .7f), .18f);
    }
}
