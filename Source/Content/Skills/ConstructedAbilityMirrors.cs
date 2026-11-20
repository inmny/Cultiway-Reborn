using System.Collections.Generic;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>已经离开实际镜面的碎片，只保留很短一段显示。</summary>
    private static readonly List<MirrorCrack> fallingMirrors = new();

    /// <summary>只用本轮确认后的部位姿态和本次所选人物绘制镜像。</summary>
    /// <param name="running">已受理的施放。</param><param name="now">共同确认时点。</param>
    private void AppendMirrorImages(RunningCast running, double now)
    {
        if (running.MirrorCracks != null)
            foreach (MirrorCrack crack in running.MirrorCracks.Values)
                if (!crack.BrokenAt.HasValue)
                    crack.Glass.Append(crack.Resource, crack.LastPlane, crack.Rule.Centre, crack.Origin, crack.Ends,
                        crack.Travelled, 0f, terrain, visuals);
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

            Matrix4x4 plane = running.Cast.NodeTransform(node);
            CompiledForm form = running.Cast.Plan.Nodes[node];
            MirrorReflection mirror = CurrentMirror(running, node, actor, now);
            if (mirror != null) mirror.Append(form.Resource, plane, terrain, visuals);
        }
    }

    /// <summary>已经形成的真实裂片保留当前位置，停止后续传播。</summary>
    private static void BreakMirror(MirrorCrack crack, double clock)
    {
        if (crack.BrokenAt.HasValue) return;
        crack.Stopped = true;
        crack.BrokenAt = clock;
        fallingMirrors.Add(crack);
    }

    /// <summary>逐轮发布仍在下落的裂片，到期销毁该次网格。</summary>
    private void AppendFallingMirrors(double now)
    {
        for (int i = fallingMirrors.Count - 1; i >= 0; i--)
        {
            MirrorCrack crack = fallingMirrors[i];
            float age = (float)(now - crack.BrokenAt.Value);
            if (age >= .55f)
            {
                crack.Glass.Dispose();
                fallingMirrors.RemoveAt(i);
                continue;
            }
            crack.Glass.Append(crack.Resource, crack.LastPlane, crack.Rule.Centre, crack.Origin,
                crack.Ends, crack.Travelled, age, terrain, visuals);
        }
    }

    /// <summary>换世界时清理已经离开施放控制的短期玻璃。</summary>
    private static void ClearFallingMirrors()
    {
        foreach (MirrorCrack crack in fallingMirrors) crack.Glass.Dispose();
        fallingMirrors.Clear();
    }

    /// <summary>显示和实际击镜取用同一姿态及投影，网格只在发布画面时上传。</summary>
    /// <param name="running">镜框施放。</param><param name="node">实际镜面部位。</param>
    /// <param name="actor">已经核对的原身。</param><param name="time">共同确认时刻。</param>
    private static MirrorReflection CurrentMirror(RunningCast running, int node, Actor actor, double time)
    {
        running.MirrorImages ??= new Dictionary<int, MirrorReflection>();
        if (!running.MirrorImages.TryGetValue(node, out MirrorReflection mirror))
        {
            mirror = new MirrorReflection(actor);
            running.MirrorImages.Add(node, mirror);
        }
        var rule = running.Cast.Plan.Nodes[node].Correspondence.MirrorImage;
        return mirror.Update(running.Cast.NodeTransform(node), rule, time, BodySurfaceAt(actor.id, time)) ? mirror : null;
    }

    /// <summary>清理只属于本次施放的网格，地图读取和正常结束共用。</summary>
    /// <param name="running">结束的施放。</param>
    private static void ReleaseMirrorImages(RunningCast running, bool worldChanged = false)
    {
        if (running.MirrorImages != null)
            foreach (MirrorReflection image in running.MirrorImages.Values) image.Dispose();
        running.MirrorImages = null;
        if (running.MirrorCracks != null)
            foreach (MirrorCrack crack in running.MirrorCracks.Values)
            {
                if (worldChanged) crack.Glass.Dispose();
                else BreakMirror(crack, running.LastWorldTime);
            }
    }
}
