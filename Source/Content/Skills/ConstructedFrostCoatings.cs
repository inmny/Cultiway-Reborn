using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>只有实际查询过结霜的近处人物才采集模型，同一人物本轮共用一次采集。</summary>
    private static readonly Dictionary<long, BodySurfaceBinding> frostProbes = new();

    /// <summary>已经接触并附着的薄霜，不占用身体控制，也不进入存档。</summary>
    private static readonly List<FrostCoating> frostCoatings = new();

    /// <summary>过时的探测不会继续持有人物或模型。</summary>
    /// <param name="now">正在采集的世界时点。</param>
    private static void RetireFrostProbes(double now)
    {
        if (frostProbes.Count == 0) return;
        List<long> stale = null;
        foreach (var entry in frostProbes)
            if (now - entry.Value.ToTime > .5 || !entry.Value.Actor.isAlive())
                (stale ??= new List<long>()).Add(entry.Key);
        if (stale != null) foreach (long id in stale) frostProbes.Remove(id);
    }

    /// <summary>从正式碰撞候选继续找真实露出的部件；人体包络只用于初选。</summary>
    /// <param name="actor">候选人物及其同一轮移动。</param><param name="rule">该次作用的结霜资料。</param>
    /// <param name="from">作用面的起始姿态。</param><param name="to">作用面的最终姿态。</param>
    /// <param name="start">查询开始时间。</param><param name="end">查询结束时间。</param>
    /// <param name="at">首次接触进度。</param><param name="point">真实面上位置。</param>
    /// <param name="normal">来向。</param><param name="binding">本次实际模型。</param>
    /// <param name="pose">接触时的表面。</param><param name="anchor">表面上的固定点。</param>
    private bool FindFrostSurface(in ActorMotion actor, CompiledFrostCoating rule, in AbilityCapsule from,
        in AbilityCapsule to, double start, double end, out float at, out Vector3 point, out Vector3 normal,
        out BodySurfaceBinding binding, out BodySurfacePose pose, out BodySurfaceAnchor anchor)
    {
        at = 0f; point = normal = default; anchor = default; pose = null;
        BodySurfacePose first = null, last = null;
        if (bodySurfaces.TryGetValue(actor.Id, out binding) && binding.ChangedReason == null)
        {
            first = BodyContactSurfaceAt(actor.Id, start);
            last = BodyContactSurfaceAt(actor.Id, end);
        }
        if (first == null || last == null)
        {
            if (!frostProbes.TryGetValue(actor.Id, out binding) || binding.Actor != actor.Actor || binding.ChangedReason != null)
            {
                binding = BodySurfaceBinding.Create(actor.Actor);
                if (binding == null) return false;
                frostProbes[actor.Id] = binding;
            }
            binding.Capture(actor.To, actor.Height, sampleUntil);
            first = binding.At(start);
            last = binding.At(end);
            if (first == null || last == null) return false;
            Sample(actor, start, out Vector3 firstFoot, out _);
            Sample(actor, end, out Vector3 lastFoot, out _);
            first = first.Translated(firstFoot - first.Foot);
            last = last.Translated(lastFoot - last.Foot);
        }
        if (!ReferenceEquals(first.Parts, last.Parts)) first = last.Translated(first.Foot - last.Foot);
        if (!BodySurfacePose.Find(first, last, from, to, rule.Part, out at, out anchor, out normal)) return false;
        pose = BodySurfacePose.Lerp(first, last, at);
        point = anchor.Point(pose);
        return true;
    }

    /// <summary>只在同一轮确认命中后留下霜渍；预测、未命中和格挡都不留下。</summary>
    /// <param name="hit">已经确认的真实接触。</param><param name="rule">本次霜渍资料。</param>
    private static void ConfirmFrostCoating(in ContactChoice hit, CompiledFrostCoating rule)
    {
        if (hit.FrostBinding.ChangedReason != null || !hit.FrostBinding.Actor.isAlive()) return;
        BodySurfacePart part = hit.BodySurface.Parts[hit.BodyAnchor.Part];
        if (frostCoatings.Count == 48) RemoveFrostCoating(0);
        frostCoatings.Add(new FrostCoating(hit.FrostBinding, part, hit.BodyAnchor,
            hit.BodyAnchor.Local(hit.BodySurface), rule, simulation.contactTime));
    }

    /// <summary>沿原身体或同一件原装备的当前表面结霜，再随时间逐渐退掉。</summary>
    /// <param name="now">已完成的世界时点。</param>
    private void AppendFrostCoatings(double now)
    {
        if (frostCoatings.Count == 0) return;
        FormResource resource = FormResources.Load("ice_shard");
        for (int i = frostCoatings.Count - 1; i >= 0; i--)
        {
            FrostCoating mark = frostCoatings[i];
            Actor actor = mark.Binding.Actor;
            float age = (float)(now - mark.At);
            if (age >= mark.Rule.Seconds || !actor.isAlive() || mark.Binding.ChangedReason != null)
            {
                RemoveFrostCoating(i);
                continue;
            }
            BodySurfacePose pose = BodySurfaceAt(actor.id, now);
            if (pose == null)
            {
                StartingPose(actor, out Vector3 foot, out float height);
                mark.Binding.Capture(foot, height, now);
                pose = mark.Binding.To;
            }
            if (pose == null) continue;
            int part = Array.IndexOf(pose.Parts, mark.Part);
            bool same = part == mark.Anchor.Part;
            if (part < 0)
                part = Array.FindIndex(pose.Parts, item => item.Id == mark.Part.Id && item.Group == mark.Part.Group &&
                    item.Attachment == mark.Part.Attachment && item.TransformIndex == mark.Part.TransformIndex);
            if (part < 0) continue;
            BodySurfacePart piece = pose.Parts[part];
            Matrix4x4 matrix = pose.Matrices[piece.TransformIndex < 0 ? part : piece.TransformIndex];
            Vector3 point = same ? mark.Anchor.Point(pose) : matrix.MultiplyPoint3x4(mark.Local);
            float grow = Mathf.SmoothStep(.31f, 1f, age / Mathf.Min(.32f, mark.Rule.Seconds * .4f));
            float opacity = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(mark.Rule.Seconds * .6f, mark.Rule.Seconds, age));
            if (!mark.Patch.Update(pose, part, point, mark.Rule.Radius * grow)) continue;
            var tint = new Color(.67f, .86f, .92f, opacity);
            visuals.Add(new FormVisualInstance(resource, mark.Patch.Mesh, Matrix4x4.identity, tint, terrain, "ice",
                actor.id, pose.Foot));
        }
    }

    /// <summary>释放本次附着的短命网格，不留给下一世界或新装备。</summary>
    /// <param name="index">本次实际霜渍。</param>
    private static void RemoveFrostCoating(int index)
    {
        Object.Destroy(frostCoatings[index].Patch.Mesh);
        frostCoatings.RemoveAt(index);
    }

    /// <summary>切换世界释放所有局部薄霜及探测。</summary>
    private static void ClearFrostCoatings()
    {
        frostProbes.Clear();
        while (frostCoatings.Count > 0) RemoveFrostCoating(frostCoatings.Count - 1);
    }

    /// <summary>真实接触位置、实际表面及其剩余时间。</summary>
    private sealed class FrostCoating(BodySurfaceBinding binding, BodySurfacePart part, BodySurfaceAnchor anchor,
        Vector3 local, CompiledFrostCoating rule, double at)
    {
        /// <summary>原身体及装备身份。</summary>
        internal readonly BodySurfaceBinding Binding = binding;
        /// <summary>当时真正碰到的部件及其面内位置。</summary>
        internal readonly BodySurfacePart Part = part;
        internal readonly BodySurfaceAnchor Anchor = anchor;
        /// <summary>原版动画换图时，保留同一部位内的局部位置。</summary>
        internal readonly Vector3 Local = local;
        /// <summary>范围与寿命。</summary>
        internal readonly CompiledFrostCoating Rule = rule;
        /// <summary>确认接触的世界时点。</summary>
        internal readonly double At = at;
        /// <summary>与可见部件相贴的局部霜白。</summary>
        internal readonly FrostSurfacePatch Patch = new();
    }
}
