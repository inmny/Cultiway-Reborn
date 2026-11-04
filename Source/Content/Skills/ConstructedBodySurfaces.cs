using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>只为实际存在双方关系查询的人物准备表面，不逐帧重建整个世界的网格。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>仍被实际施放使用的目标身体资源。</summary>
    private static readonly Dictionary<long, BodySurfaceBinding> bodySurfaces = new();

    /// <summary>已经完成的一轮表面，显示只读这里，不再运行骨架或接触。</summary>
    private static Dictionary<long, (BodySurfaceBinding Binding, BodySurfacePose Pose)> presentedSurfaces = new();

    /// <summary>正式施放边界准备目标真实表面，缺少实际模型时不能改用全身胶囊。</summary>
    /// <param name="target">实际目标。</param>
    private static bool PrepareBodySurface(Actor target)
    {
        if (bodySurfaces.TryGetValue(target.id, out BodySurfaceBinding current) && current.Actor == target &&
            BodySurfaceInUse(target.id)) return true;
        BodySurfaceBinding binding = BodySurfaceBinding.Create(target);
        if (binding == null) return false;
        bodySurfaces[target.id] = binding;
        return true;
    }

    /// <summary>只有仍占用身体的实际关系动作使用表面，结束的施放不能给下一招带入旧轨迹。</summary>
    /// <param name="target">目标身份。</param>
    private static bool BodySurfaceInUse(long target)
    {
        foreach (RunningCast running in casts)
            if (running.BodyControlled && running.TargetId == target &&
                running.Cast.Plan.Body.Construction.HasControlRelations)
                return true;
        return false;
    }

    /// <summary>人物原版移动完成后采集一次，候选查询共用同一对前后态。</summary>
    /// <param name="time">实际帧末世界时间。</param>
    private void CaptureBodySurfaces(double time)
    {
        var keep = new HashSet<long>();
        foreach (RunningCast running in casts)
        {
            if (!running.BodyControlled || !running.Cast.Plan.Body.Construction.HasControlRelations ||
                running.Target?.data?.id != running.TargetId ||
                !actorIndices.TryGetValue(running.TargetId, out int index) ||
                !keep.Add(running.TargetId)) continue;
            if (!PrepareBodySurface(running.Target)) continue;
            ActorMotion actor = actors[index];
            bodySurfaces[running.TargetId].Capture(actor.To, actor.Height, time);
        }

        var removed = new List<long>();
        foreach (long id in bodySurfaces.Keys)
            if (!keep.Contains(id))
                removed.Add(id);
        foreach (long id in removed) bodySurfaces.Remove(id);
    }

    /// <summary>抓持保持接触时的真实姿态；未取得关系时目标仍按自身移动更新。</summary>
    /// <param name="id">实际人物编号。</param><param name="time">共同时间。</param>
    private static BodySurfacePose BodySurfaceAt(long id, double time)
    {
        foreach (RunningCast running in casts)
            if (running.Connection?.Active == true && running.Connection.TargetId == id)
                return running.Connection.Surface;
        return bodySurfaces.TryGetValue(id, out BodySurfaceBinding binding) ? binding.At(time) : null;
    }

    /// <summary>只发布还在实际使用的表面，不让已经结束的施放继续接管显示。</summary>
    /// <param name="time">共同完成时点。</param>
    private static void PublishBodySurfaces(double time)
    {
        var next = new Dictionary<long, (BodySurfaceBinding, BodySurfacePose)>();
        foreach (RunningCast running in casts)
        {
            if (!running.BodyControlled || !running.Cast.Plan.Body.Construction.HasControlRelations ||
                !bodySurfaces.TryGetValue(running.TargetId, out BodySurfaceBinding binding)) continue;
            BodySurfacePose pose = BodySurfaceAt(running.TargetId, time);
            if (pose != null) next[running.TargetId] = (binding, pose);
        }

        presentedSurfaces = next;
    }

    /// <summary>外部显示取得同一轮实际表面，不建立关系、不推进时钟。</summary>
    /// <param name="id">人物编号。</param><param name="binding">真实资源接入。</param><param name="pose">共同数值姿态。</param>
    internal static bool TryGetBodySurface(long id, out BodySurfaceBinding binding, out BodySurfacePose pose)
    {
        if (presentedSurfaces.TryGetValue(id, out var value))
        {
            binding = value.Binding;
            pose = value.Pose;
            return true;
        }

        binding = null;
        pose = null;
        return false;
    }
}