using System;
using System.Collections.Generic;
using Cultiway.Content.CreatureCompositions.Libraries;
using Cultiway.Content.Combat;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>为实际身体动作和被查询的人物准备表面，不逐帧重建整个世界的网格。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>仍被实际施放使用的目标身体资源。</summary>
    private static readonly Dictionary<long, BodySurfaceBinding> bodySurfaces = new();

    /// <summary>远根点目标的选择查询在同一帧、同一逻辑时点共用一次真实采集，不取得身体控制。</summary>
    private static readonly Dictionary<long, (BodySurfaceBinding Binding, BodySurfacePose Pose)> selectionSurfaces =
        new();

    /// <summary>配色图尚未就绪时，同帧的施放查询可复用仅含实际网格的身体。</summary>
    private static readonly Dictionary<long, BodySurfacePose> castingSurfaces = new();

    internal static Func<Actor, BodySurfaceBinding> CastingSurfaceProvider;

    /// <summary>选择查询缓存的实际帧，世界切换也会清空。</summary>
    private static int selectionSurfaceFrame = -1;

    /// <summary>同帧内不同接触回调也分别读取自己的共同逻辑时点。</summary>
    private static double selectionSurfaceTime;

    /// <summary>只有根点超出普通选择距离时才采集外伸，已在出招的身体直接读取当前共同表面。</summary>
    /// <param name="target">实际目标。</param>
    internal static BodySurfacePose BodySelectionSurface(Actor target)
    {
        if (ConstructedActorImpulse.TryBody(target.id, out var flying)) return flying.Surface;
        if (bodySurfaces.TryGetValue(target.id, out BodySurfaceBinding activeBinding) &&
            activeBinding.Actor == target && activeBinding.ChangedReason == null)
            return BodySurfaceAt(target.id, LogicTime);
        if (selectionSurfaceFrame != Time.frameCount || selectionSurfaceTime != LogicTime)
        {
            selectionSurfaces.Clear();
            castingSurfaces.Clear();
            selectionSurfaceFrame = Time.frameCount;
            selectionSurfaceTime = LogicTime;
        }

        if (selectionSurfaces.TryGetValue(target.id, out var cached) && cached.Binding.ChangedReason == null)
            return cached.Pose;
        BodySurfaceBinding binding = BodySurfaceBinding.Create(target);
        if (binding == null) return null;
        StartingPose(target, out Vector3 foot, out float height);
        binding.Capture(foot, height, LogicTime);
        selectionSurfaces[target.id] = (binding, binding.To);
        return binding.To;
    }

    /// <summary>真实骨段候选复用同一选择采集，不另建人物或人形骨架。</summary>
    /// <param name="actor">实际来源。</param>
    internal static (BodySurfaceBinding Binding, BodyStructure Structure) BodyStructureFor(Actor actor)
    {
        BodySurfacePose pose = BodySelectionSurface(actor);
        if (pose == null) return default;
        BodySurfaceBinding binding = bodySurfaces.TryGetValue(actor.id, out var active) ? active :
            selectionSurfaces.TryGetValue(actor.id, out var selected) ? selected.Binding : null;
        return (binding, binding?.Structure(binding.To));
    }

    /// <summary>第一发也要在实际地表已知后采集身体，吻端和角尖直接返回世界高度。</summary>
    internal static BodySurfacePose BodyCastingSurface(Actor actor)
    {
        if (GroundProvider != null && simulation.terrain == null)
            simulation.CaptureTerrain(new Rect(actor.current_position, Vector2.zero));
        BodySurfacePose pose = BodySelectionSurface(actor);
        if (pose != null || CastingSurfaceProvider == null) return pose;
        if (castingSurfaces.TryGetValue(actor.id, out pose)) return pose;
        BodySurfaceBinding binding = CastingSurfaceProvider(actor);
        if (binding == null) return null;
        StartingPose(actor, out Vector3 foot, out float height);
        binding.Capture(foot, height, LogicTime);
        castingSurfaces[actor.id] = binding.To;
        return binding.To;
    }

    /// <summary>主线程冻结关节输入时读取本轮共同地表，工作线程只接收数值快照。</summary>
    internal static AbilityHeightField BodyTerrain => simulation.terrain;

    /// <summary>真实关节和其他身体读取同一共同地表脚下。</summary>
    /// <param name="actor">实际人物。</param>
    internal static Vector3 BodyFoot(Actor actor) { StartingPose(actor, out Vector3 foot, out _); return foot; }

    /// <summary>已经完成的一轮表面，显示只读这里，不再运行骨架或接触。</summary>
    private static Dictionary<long, (BodySurfaceBinding Binding, BodySurfacePose Pose)> presentedSurfaces = new();

    /// <summary>同一预测步中的完整出招表面，前后态都来自实际身体及动作。</summary>
    private readonly Dictionary<long, (BodySurfacePose From, BodySurfacePose To)> actingSurfaces = new();

    /// <summary>本次预测表面对应的共同时间区间。</summary>
    private double actingSurfacesFrom, actingSurfacesTo;

    /// <summary>预测前后分别采集真实动作；一个目标的全部查询共享这份结果。</summary>
    /// <param name="time">共同时间。</param><param name="before">是否为本步开始。</param>
    private void CaptureActingSurfaces(double time, bool before)
    {
        if (before)
        {
            actingSurfaces.Clear();
            actingSurfacesFrom = time;
        }
        else actingSurfacesTo = time;

        foreach (long id in bodySurfaces.Keys)
        {
            if (!CreateActingSurface(id, time, active, out BodySurfacePose pose)) continue;
            if (before) actingSurfaces[id] = (pose, pose);
            else actingSurfaces[id] = (actingSurfaces[id].From, pose);
        }
    }

    /// <summary>先取得身体，再应用本时点实际占用的支撑、头部和肢体，最后合并替换网格。</summary>
    /// <param name="id">真实人物。</param><param name="time">共同时间。</param>
    /// <param name="controllers">当前确认或正在预测的真实施放。</param><param name="pose">完整实际表面。</param>
    private static bool CreateActingSurface(long id, double time, IReadOnlyList<RunningCast> controllers,
        out BodySurfacePose pose)
    {
        if (passiveWeaponBodies.TryGetValue(id, out BodyConnection controlled))
        { pose = controlled.PassiveWeapon.At(time); return true; }
        BodyPose? stance = null, head = null, right = null, left = null;
        BodyJointPose joint = null;
        BodyOrganBinding organ = null;
        BodyPose? organRoot = null;
        var additions = new List<BodySurfacePose>();
        List<int> backgrounds = null;
        BodyOccupancy arms = BodyOccupancy.None;
        bool held = false, found = false;
        foreach (RunningCast running in controllers)
        {
            if (!running.BodyControlled || running.SourceId != id || running.StartedAt > time) continue;
            BodyPose action = running.Cast.BodyPose.Value;
            found = true;
            if (running.Cast.Plan.Body.ControlsStance) stance = action;
            if (action.Head.HasValue) head = action;
            if (action.Joint != null)
            {
                joint = action.Joint;
                if (running.Body.Organ == null) continue;
            }
            if (organ == null && running.Body.Organ != null)
            {
                organ = running.Body.Organ;
                organRoot = action;
            }
            if (running.Cast.Plan.Body.Part.IsArm)
            {
                if (running.Cast.Plan.Body.Part.Left)
                {
                    left = action;
                    arms |= BodyOccupancy.LeftArm;
                }
                else
                {
                    right = action;
                    arms |= BodyOccupancy.RightArm;
                }
            }
            if (action.SupportingGrip.HasValue)
            {
                left = action.SupportArm();
                arms |= BodyOccupancy.LeftArm;
            }

            BodySurfacePose replacement = running.Body.ActionSurface(in action);
            if (replacement.Parts.Length > 0)
            {
                if (running.Body.Organ != null && running.Body.Organ.Model == null &&
                    running.Body.Organ.Layer.Depth == CreatureLayerDepth.BehindBody)
                    (backgrounds ??= new List<int>()).Add(additions.Count);
                additions.Add(replacement);
            }

            held |= running.Body.Weapon != null;
        }

        pose = null;
        if (!found) return false;
        BodySurfaceBinding binding = bodySurfaces[id];
        // 器官安装使用同刻锁定的身体方向；这里只匹配基准表面，不取得全身或高度控制。
        BodySurfacePose basis = joint?.Surface ?? binding.Acting(binding.At(time), stance ?? organRoot, head, right, left, organ);
        foreach (RunningCast running in controllers)
            if (running.SourceId == id && running.BodyControlled && running.StartedAt <= time &&
                running.Body.Organ?.DrivesBody == true && running.Cast.BodyPose.Value.Organ.Value.ParentMotion.HasValue)
                basis = basis.TransformPart("head", running.Cast.BodyPose.Value.Organ.Value.ParentMotion.Value);
        basis = binding.IncludeActionOcclusion(basis, additions);
        if (backgrounds != null)
        {
            BodySurfaceOcclusion cover = BodySurfaceOcclusion.Create(basis.Opaque);
            foreach (int i in backgrounds) additions[i] = additions[i].WithOcclusion(cover);
        }

        additions.Insert(0, basis);
        pose = binding.Compose(additions, arms, held);
        return true;
    }

    /// <summary>数值采集从当前实际动作取得已独立接管的器官，不等下一帧画面发布后才移除旧挂件。</summary>
    /// <param name="actor">实际身体编号。</param><param name="time">本轮共同时间。</param>
    internal static IReadOnlyCollection<string> BodyOrganLayersAt(long actor, double time)
    {
        List<string> layers = null;
        foreach (RunningCast running in casts)
            if (running.BodyControlled && running.SourceId == actor && running.StartedAt <= time &&
                running.Body.Organ != null)
                (layers ??= new List<string>()).Add(running.Body.Organ.LayerKey);
        return (IReadOnlyCollection<string>)layers ?? Array.Empty<string>();
    }

    /// <summary>接触查询只读取刚刚预测的同一步；没有出招的目标沿原移动表面取样。</summary>
    /// <param name="id">实际人物。</param><param name="time">共同时间。</param>
    private static BodySurfacePose BodyContactSurfaceAt(long id, double time)
    {
        if (ConstructedActorImpulse.TryBody(id, out var flying)) return flying.At(time);
        if (TryShadowBurialPose(id, out _, out var shadow)) return shadow;
        foreach (RunningCast running in casts)
            if (running.Connection?.Active == true && !running.Connection.WeaponOnly && running.Connection.TargetId == id)
                return ConnectionSurface(running.Connection, time);
        if (simulation.actingSurfaces.TryGetValue(id, out var step))
            return BodySurfacePose.Lerp(step.From, step.To, simulation.actingSurfacesTo <= simulation.actingSurfacesFrom
                ? 1f
                : Mathf.Clamp01((float)((time - simulation.actingSurfacesFrom) /
                                        (simulation.actingSurfacesTo - simulation.actingSurfacesFrom))));
        return bodySurfaces.TryGetValue(id, out BodySurfaceBinding binding) ? binding.At(time) : null;
    }

    /// <summary>正式施放边界准备目标真实表面，缺少实际模型时不能改用全身胶囊。</summary>
    /// <param name="target">实际目标。</param>
    private static bool PrepareBodySurface(Actor target)
    {
        if (bodySurfaces.TryGetValue(target.id, out BodySurfaceBinding current) && current.Actor == target &&
            current.ChangedReason == null &&
            BodySurfaceInUse(target.id)) return true;
        BodySurfaceBinding binding = selectionSurfaceFrame == Time.frameCount && selectionSurfaceTime == LogicTime &&
                                     selectionSurfaces.TryGetValue(target.id, out var sampled) &&
                                     sampled.Binding.ChangedReason == null
            ? sampled.Binding
            : BodySurfaceBinding.Create(target);
        if (binding == null) return false;
        bodySurfaces[target.id] = binding;
        return true;
    }

    /// <summary>身体动作及仍在查询它的关系共同持有表面，结束的施放不能给下一招带入旧轨迹。</summary>
    /// <param name="target">目标身份。</param>
    private static bool BodySurfaceInUse(long target)
    {
        foreach (RunningCast running in casts)
            if (running.BodyControlled && (running.SourceId == target || running.TargetId == target &&
                    running.Cast.Plan.Body.Construction.HasControlRelations))
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
            if (!running.BodyControlled) continue;
            // 三维的全部正式人形动作共用快照，是否被别人的接触查询不能改变其动画。
            if (running.Body.Joint != null) bodySurfaces[running.SourceId] = running.Body.Joint.Binding;
            if (BodySurfaceBinding.Provider != null && running.Source.asset.is_humanoid || running.Body.Organ?.DrivesBody == true || running.Body.Joint != null)
                Capture(running.Source, running.SourceId);
            if (running.Cast.Plan.Body.Construction.HasControlRelations)
                Capture(running.Target, running.TargetId);
        }

        /// <summary>同一人物本轮只采集一次，读取实际世界对象边界后才进入数值采样。</summary>
        void Capture(Actor actor, long id)
        {
            if (actor?.data?.id != id || !actorIndices.TryGetValue(id, out int index) || !keep.Add(id) ||
                !PrepareBodySurface(actor)) return;
            ActorMotion motion = actors[index];
            bodySurfaces[id].Capture(motion.To, motion.Height, time);
        }

        var removed = new List<long>();
        foreach (long id in bodySurfaces.Keys)
            if (!keep.Contains(id))
                removed.Add(id);
        foreach (long id in removed) bodySurfaces.Remove(id);
    }

    /// <summary>受理和发布使用已经确认的当前动作，不能沿用先前预测中已经结束或回退的施放。</summary>
    /// <param name="id">实际人物。</param><param name="time">共同时间。</param>
    private static BodySurfacePose BodySurfaceAt(long id, double time)
    {
        if (ConstructedActorImpulse.TryBody(id, out var flying)) return flying.Surface;
        if (TryShadowBurialPose(id, out _, out var shadow)) return shadow;
        foreach (RunningCast running in casts)
            if (running.Connection?.Active == true && !running.Connection.WeaponOnly && running.Connection.TargetId == id)
                return ConnectionSurface(running.Connection, time);
        if (CreateActingSurface(id, time, casts, out BodySurfacePose action)) return action;
        return bodySurfaces.TryGetValue(id, out BodySurfaceBinding binding) ? binding.At(time) : null;
    }

    /// <summary>离手运动归还后立即刷新仍被其他招式使用的真实表面与人物索引。</summary>
    /// <param name="actor">实际目标。</param><param name="time">共同归还时间。</param>
    internal static void BodyFlightReturned(Actor actor, double time)
    {
        if (bodySurfaces.TryGetValue(actor.id, out BodySurfaceBinding binding) && binding.ChangedReason == null)
            binding.RestartCapture(PhysicsFoot(actor), SpatialActorPose.Height(actor), time);
        selectionSurfaces.Remove(actor.id);
        NotifyActorChanged(actor);
    }

    /// <summary>只发布还在实际使用的表面，不让已经结束的施放继续接管显示。</summary>
    /// <param name="time">共同完成时点。</param>
    private static void PublishBodySurfaces(double time)
    {
        var next = new Dictionary<long, (BodySurfaceBinding, BodySurfacePose)>();
        var pinned = new HashSet<long>();
        foreach (RunningCast running in casts)
            if (running.Connection?.Active == true && running.Connection.WeaponTarget == null) pinned.Add(running.Connection.TargetId);
        foreach (var entry in bodySurfaces)
        {
            if (!BodySurfaceInUse(entry.Key)) continue;
            entry.Value.IsPinned = pinned.Contains(entry.Key);
            entry.Value.PinsWholeBody = entry.Value.IsPinned && !passiveWeaponBodies.ContainsKey(entry.Key);
            BodySurfacePose pose = BodySurfaceAt(entry.Key, time);
            if (pose != null) next[entry.Key] = (entry.Value, pose);
        }

        foreach (var body in ConstructedActorImpulse.Bodies)
        {
            body.Binding.IsPinned = true;
            body.Binding.PinsWholeBody = true;
            next[body.Motion.ActorId] = (body.Binding, body.Surface);
        }
        foreach (long id in heldByShadow.Keys)
            if (TryShadowBurialPose(id, out var binding, out var pose))
            {
                binding.IsPinned = true;
                binding.PinsWholeBody = true;
                next[id] = (binding, pose);
            }
        presentedSurfaces = next;
    }

    /// <summary>绘制只读取已经确认的维持图片，不采样、不创建关系。</summary>
    /// <param name="id">实际人物。</param>
    internal static Sprite PinnedNativeSprite(long id) => presentedSurfaces.TryGetValue(id, out var value) && (value.Binding.IsPinned || DrivesNativeBody(id) || Array.Exists(value.Pose.Parts, part => part.HasColors))
        ? value.Binding.PinnedSprite(NativeDisplayBody(id, value.Pose)) : null;

    /// <summary>原版准备完成后提交实际固定表面，不让后来的动画帧覆盖它。</summary>
    /// <param name="id">实际人物。</param><param name="data">本轮绘制资料。</param><param name="index">实际顺序。</param>
    internal static bool PresentPinnedNative(long id, ActorRenderData data, int index) =>
        presentedSurfaces.TryGetValue(id, out var value) && (value.Binding.IsPinned || DrivesNativeBody(id) || Array.Exists(value.Pose.Parts, part => part.HasColors)) &&
        value.Binding.PresentPinned(NativeDisplayBody(id, value.Pose), data, index);

    /// <summary>正常动作分别绘制身体和借出的器官，完整固定关系继续原完整表面。</summary>
    /// <param name="id">实际身体。</param><param name="pose">完整物理表面。</param>
    private static BodySurfacePose NativeDisplayBody(long id, BodySurfacePose pose) =>
        DrivesNativeBody(id) && pose.CompositionBase != null ? pose.CompositionBase : pose;

    /// <summary>只读取已经发布的固定自然武器，不在绘制中访问正在推进的动作。</summary>
    /// <param name="id">实际身体。</param>
    private static bool DrivesNativeBody(long id)
    {
        if (TryGetBodyPose(id, out BodyPose pose) && pose.Joint != null) return true;
        foreach (var organ in GetBodyOrgans(id)) if (organ.Binding.DrivesBody) return true;
        return false;
    }

    /// <summary>原版旋转图片或完整模型表面，绘制不读取运行中的关系或推进时间。</summary>
    /// <param name="id">实际人物。</param><param name="pose">已经发布的共同表面。</param>
    internal static bool TryGetNativeBodySurface(long id, out BodySurfacePose pose)
    {
        if (presentedSurfaces.TryGetValue(id, out var value) &&
            (DrivesNativeBody(id) || value.Binding.IsPinned && (passiveWeaponBodies.ContainsKey(id) || value.Pose.HasDeformation || value.Pose.RigidBasis != null && value.Pose.RigidTransform.Rotation != Quaternion.identity) ||
             Array.Exists(value.Pose.Parts, part => part.HasColors)) &&
            value.Binding.PinnedSprite(NativeDisplayBody(id, value.Pose)) != null)
        { pose = NativeDisplayBody(id, value.Pose); return true; }
        pose = null;
        return false;
    }

    /// <summary>只有确有整身表面时才额外遍历持物池，普通战斗不增加全体物品扫描。</summary>
    internal static bool HasNativeBodySurfaces
    {
        get
        {
            foreach (long id in presentedSurfaces.Keys)
                if (TryGetNativeBodySurface(id, out _)) return true;
            return false;
        }
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
