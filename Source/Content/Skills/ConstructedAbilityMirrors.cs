using System.Collections.Generic;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>只用本轮确认后的部位姿态和本次所选人物绘制镜像。</summary>
    /// <param name="running">已受理的施放。</param><param name="now">共同确认时点。</param>
    private void AppendMirrorImages(RunningCast running, double now)
    {
        foreach (int node in running.Cast.Plan.MirrorNodes)
        {
            Actor actor = running.Owner(node).IsLive && running.Cast.IsNodePresent(node)
                ? CorrespondenceReceiver(running, node) : null;
            if (actor == null)
            {
                if (running.MirrorImages?.TryGetValue(node, out MirrorReflection expired) == true)
                {
                    expired.Dispose();
                    running.MirrorImages.Remove(node);
                }
                continue;
            }

            running.MirrorImages ??= new Dictionary<int, MirrorReflection>();
            if (!running.MirrorImages.TryGetValue(node, out MirrorReflection mirror))
            {
                mirror = new MirrorReflection(actor);
                running.MirrorImages.Add(node, mirror);
            }
            Matrix4x4 plane = running.Cast.NodeTransform(node);
            CompiledForm form = running.Cast.Plan.Nodes[node];
            BodySurfacePose sharedPose = BodySurfaceAt(actor.id, now);
            if (!mirror.Update(plane, form.Correspondence.MirrorImage, now, sharedPose)) continue;
            mirror.Append(form.Resource, plane, terrain, visuals);
        }
    }

    /// <summary>清理只属于本次施放的网格，地图读取和正常结束共用。</summary>
    /// <param name="running">结束的施放。</param>
    private static void ReleaseMirrorImages(RunningCast running)
    {
        if (running.MirrorImages == null) return;
        foreach (MirrorReflection image in running.MirrorImages.Values) image.Dispose();
        running.MirrorImages = null;
    }
}
