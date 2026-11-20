using System;
using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Content.KnightCombat;
using Cultiway.Core.Performance;
using Cultiway.Core.Combat;
using Cultiway.Core.EventSystem.Systems;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Core.Visuals.Forms;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>不同施放在共同时间段预测，回到最早接触处确认变化，再处理余下时间。</summary>
public sealed partial class ConstructedAbilityService : ICooperativeSystemStep
{
    /// <summary>初始化的实际系统，供静态施放及切世界入口重置同一采集历史。</summary>
    private static ConstructedAbilityService simulation;

    /// <summary>形体之间真正吸收或抵消后发布。</summary>
    public static event Action<ConstructedClash> ClashConfirmed;

    /// <summary>上轮末端的真实身体位置，包含刚生成人物。</summary>
    private readonly Dictionary<long, (Vector3 Foot, float Height)> history = new();

    /// <summary>本轮全部真实身体，与数值空间索引顺序一致。</summary>
    private readonly List<ActorMotion> actors = new();

    /// <summary>按稳定编号取得共同时间的实际身体。</summary>
    private readonly Dictionary<long, int> actorIndices = new();

    /// <summary>本轮实际身体动作，空间筛选包含其全部重心位移。</summary>
    private readonly Dictionary<long, RunningCast> bodyCasts = new();

    /// <summary>身体扫过范围的空间筛选，本轮采集一次。</summary>
    private readonly AbilityContactIndex actorIndex = new();

    /// <summary>共同数值步的全部生成作用形状。</summary>
    private readonly List<ShapeMotion> shapes = new();

    /// <summary>当前形体的空间索引，按实际路径及高度筛选。</summary>
    private readonly AbilityContactIndex shapeIndex = new();

    /// <summary>只有同一施放的同一部位时，其各作用面不会互相敌对、淬灭或传热。</summary>
    private bool singleActingNode;

    /// <summary>复用的空间查询结果。</summary>
    private readonly List<int> nearby = new();

    /// <summary>共同数值步中实际需要推进的施放。</summary>
    private readonly List<RunningCast> active = new();

    /// <summary>采集后才能提交的纯数值输入，预测及接触后重新推进均复用。</summary>
    private readonly List<AbilityStep> steps = new();

    /// <summary>拨压目标在来源本步姿态完成后取得实际接触点，第二批仍只提交纯数值工作。</summary>
    private readonly List<(RunningCast Running, AbilityInputs Input, BodyFrame Body)> weaponFollowers = new();

    /// <summary>存在真实火体时才为无攻击水体建立反应形状。</summary>
    private static bool includeReactiveMaterials;

    /// <summary>身体及形体合计达到此工作量后复用工作池，小批保持主线程计算。</summary>
    private const int MinimumParallelWork = 1024;

    /// <summary>单次施放前后同一部位及形状的对应关系。</summary>
    private readonly Dictionary<(int, int), AbilityCapsule> previousShapes = new();

    /// <summary>上轮实际采集的世界时间。</summary>
    private double sampledAt;

    /// <summary>本轮完整推进的世界时间。</summary>
    private double sampleUntil;

    /// <summary>本轮已经完整确认的共同时间，世界材料不得抢先推进到未来。</summary>
    internal static double ConfirmedWorldTime { get; private set; }

    /// <summary>本轮共同模拟，可由现有调度跨帧继续，不丢弃接触或世界时间。</summary>
    private IEnumerator<bool> pending;

    /// <summary>供现有帧耗时记录识别当前阶段。</summary>
    string ICooperativeSystemStep.CooperativePhaseName => "constructed.contact";

    /// <summary>按现有主线程调度完成一个共同数值步，不在此等待工作线程。</summary>
    bool ICooperativeSystemStep.StepCooperatively() => StepSimulation();

    /// <summary>沿用普通系统调用时完整完成本轮，正式调度则从协作入口逐步调用。</summary>
    protected override void OnUpdateGroup()
    {
        while (!StepSimulation())
        {
        }
    }

    /// <summary>推进到下一个完整接触边界，保留世界及系统之间的先后关系。</summary>
    private bool StepSimulation()
    {
        IEnumerator<bool> current = pending ??= Simulate();
        if (current.MoveNext()) return false;
        current.Dispose();
        if (ReferenceEquals(pending, current)) pending = null;
        return true;
    }

    /// <summary>先采集、再共同预测及确认，最后发布完整结果；绘制不会推进。</summary>
    private IEnumerator<bool> Simulate()
    {
        ProcessCreation();
        ProcessWorldNeeds();
        visuals.Clear();
        long generation = worldGeneration;
        double now = World.world.getCurWorldTime();
        ConstructedActorImpulse.ValidateBodies();
        Rect? moving = ConstructedActorImpulse.ActiveBounds();
        if (casts.Count > 0 || moving.HasValue) CaptureTerrain(moving, moving.HasValue);
        if (casts.Count > 0 || HasBodyFlights)
        {
            CaptureActors(now);
            double clock = BodyFlightClock(now);
            foreach (RunningCast running in casts) clock = Math.Min(clock, running.LastWorldTime);
            while (clock < now)
            {
                RefreshChangedActors(clock);
                UpdateBodyConnections(clock);
                ConstructedActorImpulse.ValidateBodies();
                if (generation != worldGeneration) yield break;
                float step = BodyFlightStep(clock, (float)(now - clock));
                active.Clear();
                foreach (RunningCast running in casts)
                {
                    ValidateSource(running);
                    UpdateSupportGrips(running, clock);
                    if (running.Cast.Finished) continue;
                    if (running.LastWorldTime > clock)
                    {
                        step = Mathf.Min(step, (float)(running.LastWorldTime - clock));
                        continue;
                    }

                    active.Add(running);
                    step = Mathf.Min(step, BodyConnectionStep(running, running.Cast.NextStep(step)));
                }

                if (active.Count == 0 && !HasBodyFlights)
                {
                    clock += step;
                    continue;
                }

                if (terrainNeedsRefresh)
                {
                    AdvanceAll(clock, 0f, true);
                    terrainNeedsRefresh = false;
                }

                PublishBarriers();
                Predict(clock, step);
                ContactChoice? hit = FindFirst(clock, step);
                RefineBodyContact(clock, ref step, ref hit);
                float advance = step * (hit?.Fraction ?? 1f);
                if (hit.HasValue && hit.Value.Fraction < 1f)
                {
                    foreach (RunningCast running in active) running.Cast.RestoreStep();
                    AdvanceAll(clock + advance, advance);
                }

                clock += advance;
                foreach (RunningCast running in active) running.LastWorldTime = clock;
                foreach (RunningCast running in active) PublishBodyMovement(running);
                CommitBodyCarrying(clock);
                CommitPassiveWeapons(clock);
                CommitBodyFlights(clock);
                ConstructedActorImpulse.CommitRadial(clock);
                if (generation != worldGeneration) yield break;
                if (hit.HasValue && hit.Value.WorldRecord > 0)
                {
                    using (var materials = WorldMaterialService.AdvanceTo(clock))
                        while (materials.MoveNext())
                        {
                            if (generation != worldGeneration) yield break;
                            yield return false;
                        }

                    if (generation != worldGeneration) yield break;
                    ValidateSource(hit.Value.Left.Running);
                }

                if (hit.HasValue)
                {
                    contactTime = clock;
                    confirmingContact = true;
                    try
                    {
                        Resolve(hit.Value);
                    }
                    finally
                    {
                        confirmingContact = false;
                    }
                }

                if (generation != worldGeneration) yield break;
                UpdateBodyConnections(clock);
                if (generation != worldGeneration) yield break;
                foreach (RunningCast running in active)
                    if (running.WorldScope != null || running.Replaced)
                        ValidateSource(running);
                ConfirmRadialMotions(clock);
                if (generation != worldGeneration) yield break;
                for (int i = 0; i < active.Count; i++)
                {
                    RunningCast running = active[i];
                    UpdateSupportGrips(running, clock);
                    if (generation != worldGeneration) yield break;
                    if (running.ActorTransferHolds?.Count > 0) ConfirmActorTransferHolds(running, clock, generation);
                    if (generation != worldGeneration) yield break;
                    ConfirmEnclosures(running, clock);
                    if (generation != worldGeneration) yield break;
                    PublishActionEvents(running);
                    if (generation != worldGeneration) yield break;
                }

                if (WorldMaterialService.Count > 0)
                    using (var materials = WorldMaterialService.AdvanceTo(clock))
                        while (materials.MoveNext())
                        {
                            if (generation != worldGeneration) yield break;
                            yield return false;
                        }

                foreach (RunningCast running in active)
                {
                    if (running.Replaced || running.WorldScope != null && running.Cast.Phase == AbilityCastPhase.Interrupted) continue;
                    if (running.Cast.Plan.ArtifactTransferSteps.Length != 0) ConfirmArtifactTransfers(running, clock);
                    ConfirmInventory(running, clock);
                    if (generation != worldGeneration) yield break;
                    if (running.Cast.Plan.DirectSignalSteps.Length != 0) ConfirmDirectSignals(running, clock);
                    if (generation != worldGeneration) yield break;
                    ConfirmRecovery(running);
                    if (generation != worldGeneration) yield break;
                    ConfirmWorldMaterial(running, clock);
                    if (generation != worldGeneration) yield break;
                }

                if (generation != worldGeneration) yield break;
                RefreshChangedActors(clock);
                if (terrainNeedsRefresh)
                {
                    AdvanceAll(clock, 0f, true);
                    terrainNeedsRefresh = false;
                }

                foreach (RunningCast running in active)
                    if (running.BodyControlled && !running.Cast.ControlsBody)
                        ReleaseBody(running);
                yield return false;
            }

            RefreshChangedActors(now);
            RememberActors(now);
        }

        foreach (var running in casts)
        {
            ConfirmBurnProgress(running, now);
            if (generation != worldGeneration) yield break;
        }

        for (int index = casts.Count - 1; index >= 0; index--)
        {
            RunningCast running = casts[index];
            ValidateSource(running);
            PublishActionEvents(running);
            if (generation != worldGeneration) yield break;
            if (running.Cast.Finished)
            {
                Finish(index, running);
                if (generation != worldGeneration) yield break;
                continue;
            }

            if (!contactDebugOnlyShapes || running.Id != contactDebugCast)
            {
                running.Cast.AppendVisuals(visuals);
                AppendSpatialViews(running, now);
                AppendSoulPact(running, now);
            }
            if (!running.BodyControlled) continue;
            BodyPose pose = running.Cast.BodyPose.Value;
            if (running.Cast.Plan.Body.ControlsStance)
            {
                running.Source.lookTowardsPosition(running.Source.current_position + (Vector2)pose.Forward);
            }

            running.Body.WeaponReleased = running.Cast.Plan.UsesHeldWeapon && !running.Cast.IsInputAttached;
            running.Body.AppendVisuals(running.Source, in pose, visuals, terrain);
        }

        AppendSteamVisuals(now);
        AppendConductionVisuals(now);
        AppendWindScars(now);
        AppendFrostCoatings(now);
        AppendYinTransfers(now);
        AppendBalancedWakan(now);
        AppendLifeRepairs(now);
        AppendShadows(now);
        AppendSoulPactTransfers(now);
        AppendObservedSignals(now);
        AppendContactVolumes(now);
        AppendMediumLinks(now);
        ConfirmedWorldTime = now;
        PublishBarriers();
        PublishPresentation(now);
        if (casts.Count == 0 && !HasBodyFlights) ResetSimulation();
    }

    /// <summary>人物失效或身体被真正中断时解除对应控制，不让池中重用对象继续施法。</summary>
    /// <param name="running">本次施放。</param>
    private static void ValidateSource(RunningCast running)
    {
        if (running.Replaced)
        {
            running.Cast.Cancel();
            ReleaseBody(running);
            return;
        }
        // 已确认的自然结束不再被来源后来失效改写成中断。
        if (running.Cast.Finished) { ReleaseActorTransferHolds(running); return; }
        if (running.ActorTransferHolds?.Count > 0 && !ActorTransferSourceLive(running)) ReleaseActorTransferHolds(running);
        ValidateCorrespondences(running);
        if (running.Inventory != null && !running.Inventory.IsValid)
        {
            running.Cast.Cancel(); running.Inventory.Dispose(); ReleaseBody(running); return;
        }
        if (running.Carrier != null && (!running.Cast.IsInputAttached || !running.Carrier.IsValid))
        {
            bool lost = running.Cast.IsInputAttached;
            running.Carrier.Dispose(); running.Carrier = null;
            if (lost)
            {
                running.Cast.EndNode(0, true);
                running.Cast.InterruptBody();
                ReleaseBody(running);
                if (running.Cast.Finished) running.Cast.Cancel();
            }
        }
        if (running.WorldScope != null && !running.WorldScope.IsValid())
        {
            running.Cast.Cancel();
            ReleaseBody(running);
            return;
        }

        bool sourceLive = running.InitialOwner.WorldSource || running.InitialOwner.IsLive,
            spatialLive = running.InitialOwner.WorldSource || running.SpatialSourceLive;
        if (running.Cast.Plan.UsesHeldWeapon && (!sourceLive || !spatialLive || !running.Body.IsWeaponCurrent(running.Source)))
        {
            running.Cast.Cancel();
            ReleaseBody(running);
            return;
        }
        running.InitialOwner.RefreshRelations();
        if (!sourceLive)
        {
            running.Cast.InterruptBody();
            ReleaseBody(running);
        }

        if (!sourceLive || !spatialLive || running.HasTransfers)
        {
            for (int i = 0; i < running.Owners.Length; i++)
            {
                CastAuthority owner = running.Owner(i);
                owner.RefreshRelations();
                if ((!owner.WorldSource && !owner.IsLive || !spatialLive && ReferenceEquals(owner, running.InitialOwner)) && !running.Cast.CanContinueWithoutSource(i))
                    running.Cast.EndNode(i, true);
            }

            if (running.Cast.Finished) running.Cast.Cancel();
        }

        if (!sourceLive) return;
        if (running.BodyControlled && (!running.Body.IsValid(running.Source) || running.Source.isFlying() ||
                                       running.Source.under_forces ||
                                       running.Movement?.OwnsPosition == false ||
                                       (running.Cast.BodyResult != BodyActionResult.Interrupted || running.BodyReactionUntil < LogicTime) &&
                                       (running.Source.stats.hasTag("frozen_ai") || running.Source.stats.hasTag("immovable"))))
        {
            if (running.Practice) running.Cast.Cancel();
            else running.Cast.InterruptBody();
            ReleaseBody(running);
            return;
        }

        if (running.BodyControlled && running.Movement?.LostSupport(simulation.terrain) == true)
        {
            if (running.Practice)
            {
                running.Cast.Cancel();
                ReleaseBody(running);
                return;
            }
            ReleaseBodyConnection(running, "SupportLost", fade: false);
            if (!RespondToBodyResult(running, BodyActionResult.SupportLost))
            {
                // 未指定恢复时也保留已确认世界高度；由原受力正常下落，不贴回新地面。
                PublishBodyPosition(running.Source, running.Movement.Root);
                running.Movement.AcceptHeight(running.Source.position_height);
                running.Cast.InterruptBody();
                ReleaseBody(running);
            }
        }
    }

    /// <summary>读取立即登记的实际人物字典，包含尚未并入原版队列及分区的新生人物。</summary>
    /// <param name="now">本轮世界时间。</param>
    private void CaptureActors(double now)
    {
        actorsSampled = true;
        changedActors.Clear();
        sampleUntil = now;
        actors.Clear();
        actorIndices.Clear();
        actorIndex.Clear();
        bodyCasts.Clear();
        foreach (RunningCast running in casts)
            if (running.BodyControlled && running.Cast.Plan.Body.ControlsStance)
                bodyCasts[running.SourceId] = running;
        foreach (Actor actor in World.world.units.dict.Values)
        {
            if (actor.isRekt() || !actor.isAlive() || !actor.hasHealth()) continue;
            Vector3 foot = SpatialActorPose.Foot(actor);
            float height = SpatialActorPose.Height(actor);
            var prior = history.TryGetValue(actor.id, out var old) ? old : (foot, height);
            foot = WorldFoot(actor, foot);
            Vector3 previousFoot = WorldFoot(actor, prior.Item1);
            bodyCasts.TryGetValue(actor.id, out RunningCast body);
            var motion = new ActorMotion(actor, actor.id, previousFoot, foot, prior.Item2, height, body);
            actorIndices.Add(actor.id, actors.Count);
            actors.Add(motion);
            float reach = Mathf.Max(height, prior.Item2) * (1f + (body?.Cast.Plan.Body.MaximumTorsoOffset ?? 0f) +
                                                            (body?.Cast.Plan.Body.Construction.FootworkTravel ?? 0f));
            var from = new AbilityCapsule(0, previousFoot, previousFoot, reach, null);
            var to = new AbilityCapsule(0, foot, foot, reach, null);
            actorIndex.Add(AbilityContactIndex.Swept(from, to));
        }

        RetireFrostProbes(now);
        CaptureBodySurfaces(now);
    }

    /// <summary>保存真实帧末位置，下轮在共同时间内插；首个施放也先留存实际前态。</summary>
    /// <param name="now">真实世界时间。</param>
    private void RememberActors(double now)
    {
        actorsSampled = false;
        changedActors.Clear();
        history.Clear();
        foreach (Actor actor in World.world.units.dict.Values)
            if (!actor.isRekt() && actor.isAlive())
                history[actor.id] = (SpatialActorPose.Foot(actor), SpatialActorPose.Height(actor));
        sampledAt = now;
    }

    /// <summary>换世界或没有活动施放时不保留上个世界的人物对象及运动历史。</summary>
    /// <param name="cancel">换世界时丢弃尚未完成的旧世界模拟。</param>
    private void ResetSimulation(bool cancel = false)
    {
        if (cancel)
        {
            pending?.Dispose();
            pending = null;
        }

        barrierIndex.Clear();
        barriers.Clear();
        confirmingContact = false;
        actorsSampled = false;
        changedActors.Clear();
        history.Clear();
        actors.Clear();
        actorIndices.Clear();
        bodyCasts.Clear();
        actorIndex.Clear();
        shapes.Clear();
        shapeIndex.Clear();
        nearby.Clear();
        active.Clear();
        steps.Clear();
        weaponFollowers.Clear();
        previousShapes.Clear();
        actingSurfaces.Clear();
        actingSurfacesFrom = actingSurfacesTo = 0d;
        sampledAt = sampleUntil = 0d;
        contactIntervals.Clear();
        correspondenceStarts.Clear();
        correspondenceStartIndex.Clear();
        correspondenceCandidates.Clear();
        bodyCorrespondenceCandidates.Clear();
    }

    /// <summary>保存前态，再用同一段时间预测所有施放；不在预测阶段造成伤害。</summary>
    /// <param name="clock">共同起点。</param>
    /// <param name="step">共同步长。</param>
    private void Predict(double clock, float step)
    {
        shapes.Clear();
        shapeIndex.Clear();
        hasBodyGuardShapes = false;
        includeReactiveMaterials = WorldMaterialService.HasHeat;
        foreach (RunningCast running in active)
            for (int node = 0; node < running.Cast.Plan.FormCount; node++)
                if (running.Cast.Matter(node).Id == "fire" && running.Cast.RemainingForce(node) > 0 ||
                    running.Cast.Burning(node).Heat > 0)
                {
                    includeReactiveMaterials = true;
                    break;
                }

        foreach (RunningCast running in active)
        {
            running.BeforeTorso = running.BodyControlled ? running.Cast.BodyPose.Value.Torso : Vector3.zero;
            running.BeforeJoint = running.BodyControlled && running.Cast.Plan.Body.Part.IsJoint ? running.Cast.BodyPose.Value.Joint : null;
            if (running.BodyControlled && running.BodyGuardRemaining > 0f)
                running.BeforeGuardNormal = running.Body.GuardNormal(running.Cast.BodyPose.Value);
            running.Cast.SaveStep();
            Collect(running, running.Before, false);
        }

        IndexCorrespondenceStarts();
        foreach (RunningCast running in active) RefreshReentry(running, clock);

        CaptureActingSurfaces(clock, true);
        AdvanceAll(clock + step, step);
        CaptureActingSurfaces(clock + step, false);
        foreach (RunningCast running in active)
        {
            Collect(running, running.After, true);
            previousShapes.Clear();
            foreach (AbilityCapsule shape in running.Before) previousShapes.Add((shape.Node, shape.Shape), shape);
            foreach (AbilityCapsule shape in running.After)
            {
                bool continuous = previousShapes.TryGetValue((shape.Node, shape.Shape), out AbilityCapsule from);
                if (running.Cast.TryTransferDeparture(shape.Node, out Matrix4x4 departure))
                {
                    if (continuous)
                    {
                        Vector3 half = shape.HalfExtents;
                        Vector3 departedHalf = new(
                            Mathf.Abs(departure.m00) * half.x + Mathf.Abs(departure.m01) * half.y + Mathf.Abs(departure.m02) * half.z,
                            Mathf.Abs(departure.m10) * half.x + Mathf.Abs(departure.m11) * half.y + Mathf.Abs(departure.m12) * half.z,
                            Mathf.Abs(departure.m20) * half.x + Mathf.Abs(departure.m21) * half.y + Mathf.Abs(departure.m22) * half.z);
                        var departed = new AbilityCapsule(shape.Node, departure.MultiplyPoint3x4(shape.A), departure.MultiplyPoint3x4(shape.B),
                            shape.Radius, shape.Matter, shape.Shape, shape.Revision, shape.Material, departedHalf, .99999994f, shape.Prism?.Move(departure),
                            shape.SweepPose?.Move(departure));
                        shapes.Add(new ShapeMotion(running, from, departed, true));
                    }
                    continuous = false;
                }
                if (running.Cast.ContactAtDestination(shape.Node)) continuous = false;
                if (!continuous) from = shape;
                var motion = new ShapeMotion(running, from, shape, continuous);
                shapes.Add(motion);
                hasBodyGuardShapes |= shape.Node < 0 && running.BodyGuardRemaining > 0f;
            }
        }
        singleActingNode = shapes.Count > 0;
        for (int i = 1; i < shapes.Count && singleActingNode; i++)
            singleActingNode = shapes[i].Running == shapes[0].Running && shapes[i].To.Node == shapes[0].To.Node;
        if (!singleActingNode)
            foreach (ShapeMotion shape in shapes) shapeIndex.Add(shape.Bounds);
    }

    /// <summary>在主线程取得共同时间的连接及目标，之后只提交纯数值批次。</summary>
    /// <param name="time">共同末端时间。</param>
    /// <param name="step">本次真实推进量。</param>
    /// <param name="refresh">外部事实改变后只重新采样，不推进或扫过瞬移路径。</param>
    private void AdvanceAll(double time, float step, bool refresh = false)
    {
        steps.Clear();
        weaponFollowers.Clear();
        int work = 0;
        foreach (RunningCast running in active)
        {
            if (refresh && !terrainNeedsRefresh && !changedActors.ContainsKey(running.SourceId) &&
                !changedActors.ContainsKey(running.TargetId) && !changedActors.ContainsKey(running.SpatialSourceId) &&
                !(running.WeaponConstraint?.Active == true &&
                  changedActors.ContainsKey(running.WeaponConstraint.Owner.SourceId))) continue;
            Vector3 foot = running.LastSourceFoot;
            float height = running.LastSourceHeight;
            if (running.SpatialSourceLive && actorIndices.TryGetValue(running.SpatialSourceId, out int sourceIndex))
                Sample(actors[sourceIndex], time, out foot, out height);
            running.LastSourceFoot = foot;
            running.LastSourceHeight = height;
            if (running.Target?.data?.id == running.TargetId && running.Target.isAlive() &&
                running.Target.hasHealth() &&
                actorIndices.TryGetValue(running.TargetId, out int targetIndex))
            {
                Sample(actors[targetIndex], time, out Vector3 target, out float targetHeight);
                running.LastTargetFoot = target;
                running.LastTarget = target + Vector3.forward * targetHeight * .5f;
            }

            BodyLocomotion.Pose? movement = running.Movement != null && running.BodyControlled
                ? running.Movement.Sample((float)(time - running.StartedAt), height, terrain)
                : null;
            // 地面候选已无法保持真实骨长或承托时，先退出旧动作，不能把无效足面送去结算和绘制。
            if (movement?.Support.Joint?.Supported == false)
            {
                running.Cast.InterruptBody();
                ReleaseBody(running);
                movement = null;
                StartingPose(running.Source, out foot, out height);
                running.LastSourceFoot = foot;
                running.LastSourceHeight = height;
            }
            if (movement.HasValue) running.Cast.SetBodyResponseDelay(movement.Value.ResponseDelay);
            BodySupportPose? support = movement?.Support;
            BodyFrame? body = running.BodyControlled ? running.Body.Frame(foot, height, support) : null;
            bool followsWeapon = body.HasValue && running.WeaponConstraint?.Active == true &&
                                 running.WeaponConstraint.Relation.Kind == BodyRelationKind.Deflect;
            if (body.HasValue && !followsWeapon) body = ApplyBodyConnection(running, body.Value, time);
            if (running.Carrier != null) running.CarrierPose = running.Carrier.Pose;
            var input = new AbilityInputs(foot, running.LastTargetFoot, running.Direction, height, running.CarrierPose);
            if (running.HasTransfers)
                for (int i = 0; i < running.Owners.Length; i++)
                {
                    CastAuthority owner = running.Owner(i);
                    if (ReferenceEquals(owner, running.InitialOwner) ||
                        owner.Target?.data?.id != owner.TargetId || !owner.Target.isAlive() ||
                        !owner.Target.hasHealth() ||
                        !actorIndices.TryGetValue(owner.TargetId, out int aimIndex)) continue;
                    Sample(actors[aimIndex], time, out Vector3 aim, out float aimHeight);
                    running.Cast.SetTarget(i, aim + Vector3.forward * aimHeight * .5f);
                }

            if (followsWeapon) weaponFollowers.Add((running, input, body.Value));
            else
            {
                steps.Add(new AbilityStep(running.Cast, input, body, running.LastTarget, step, refresh));
                work += running.Cast.Plan.WorkSize;
            }
        }

        AbilityBatch.Advance(steps, work >= MinimumParallelWork);
        if (weaponFollowers.Count > 0)
        {
            steps.Clear();
            work = 0;
            foreach (var follower in weaponFollowers)
            {
                BodyFrame body = ApplyBodyConnection(follower.Running, follower.Body, time);
                steps.Add(new AbilityStep(follower.Running.Cast, follower.Input, body,
                    follower.Running.LastTarget, step, refresh));
                work += follower.Running.Cast.Plan.WorkSize;
            }
            AbilityBatch.Advance(steps, work >= MinimumParallelWork);
        }
        PredictPassiveWeapons(time);
        PredictBodyCarrying(time);
        PredictBodyFlights(time);
    }

    /// <summary>按采集时间内插原版已完成的位移，新生人物以前后同一姿态参与。</summary>
    /// <param name="motion">采集到的真实人物。</param>
    /// <param name="time">本次共同时间。</param>
    /// <param name="foot">该时点脚下。</param>
    /// <param name="height">该时点体高。</param>
    private void Sample(in ActorMotion motion, double time, out Vector3 foot, out float height)
    {
        float at = sampleUntil > sampledAt
            ? Mathf.Clamp01((float)((time - sampledAt) / (sampleUntil - sampledAt)))
            : 1f;
        foot = Vector3.Lerp(motion.From, motion.To, at);
        height = Mathf.Lerp(motion.FromHeight, motion.Height, at);
        if (ConstructedActorImpulse.TryRadialFoot(motion.Id, time, out Vector3 radialFoot))
        { foot = radialFoot; return; }
        if (ConstructedActorImpulse.TryBody(motion.Id, out var flying))
        { foot = flying.At(time).Foot; return; }
        BodyConnection carrying = Carrying(motion.Id);
        if (carrying != null)
        {
            foot = ConnectionSurface(carrying, time).Foot;
            return;
        }
        if (motion.Body?.BodyControlled == true && motion.Body.Movement != null)
        {
            float age = (float)(time - motion.Body.StartedAt);
            foot = motion.Body.Movement.Sample(age, height, terrain).Root;
        }
    }

    /// <summary>仅将已经确认的动作位置及高度交给实际人物，预测和回退不改变游戏对象。</summary>
    /// <param name="running">本步已经确认的施放。</param>
    private static void PublishBodyMovement(RunningCast running)
    {
        if (!running.BodyControlled || running.Movement == null) return;
        Actor actor = running.Source;
        if (actor.data?.id != running.SourceId || !actor.isAlive() || actor.under_forces) return;
        BodyLocomotion.Pose pose = running.Movement.Commit((float)(running.LastWorldTime - running.StartedAt),
            running.LastSourceHeight, simulation.terrain);
        PublishBodyPosition(actor, pose.Root);
    }

    /// <summary>真实结果同时切换姿态和已经确认的运动，旧步法不能继续补走。</summary>
    /// <param name="running">已经结算接触的施放。</param><param name="result">实际攻击结果。</param>
    private static bool RespondToBodyResult(RunningCast running, BodyActionResult result)
    {
        bool continuing = result == BodyActionResult.Interrupted && running.Cast.BodyResult == result;
        if (!running.Cast.RespondToBodyResult(result)) return false;
        if (!continuing) running.Movement?.BeginResponse(running.Cast.BodyResponse.Value);
        return true;
    }

    /// <summary>已经确认的来源或目标共同根点，使用同一原版位置及高度入口。</summary>
    /// <param name="actor">实际人物。</param><param name="root">含共同地表的世界根点。</param>
    private static void PublishBodyPosition(Actor actor, Vector3 root)
    {
        float height = Mathf.Max(0f, root.z - (simulation.terrain?.Sample(root.x, root.y) ?? 0f));
        bool landed = actor.position_height > 0f && height == 0f;
        actor.stopMovement();
        actor.current_position = root;
        actor.setCurrentTile(World.world.GetTileSimple((int)root.x, (int)root.y));
        actor.dirty_current_tile = true;
        actor.position_height = height;
        actor.checkIsInLiquid();
        if (landed) actor.stopForce();
        actor.updatePos();
    }

    /// <summary>读取相同姿态下的生成作用形状及实际掌面或借用器物。</summary>
    /// <param name="running">本次施放。</param>
    /// <param name="output">复用形状集合。</param>
    /// <param name="includeEnd">是否保留最后有效路径。</param>
    private static void Collect(RunningCast running, List<AbilityCapsule> output, bool includeEnd)
    {
        output.Clear();
        running.Cast.AppendContacts(output, includeEnd, includeMaterials: includeReactiveMaterials);
        if (!running.Practice && running.BodyControlled && running.Cast.BodyShapeContactEnabled(includeEnd))
        {
            int first = output.Count;
            running.Body.AppendContactShapes(running.Cast.BodyPose.Value, output, includeEnd);
            for (int i = first; i < output.Count; i++)
            {
                AbilityCapsule shape = output[i];
                output[i] = new AbilityCapsule(shape.Node, shape.A, shape.B, shape.Radius, shape.Matter, shape.Shape,
                    running.Cast.BodyContactRevision, shape.Material, shape.HalfExtents, shape.Until, shape.Prism, shape.SweepPose);
            }
        }
    }

    /// <summary>携带环境或兵器相抵的关节短段可能偏离整段弦线，重新查找实际成立的接触再确认。</summary>
    /// <param name="clock">同一已确认起点。</param><param name="step">可确认的实际短段。</param>
    /// <param name="hit">短段重新计算后仍成立的最早接触。</param>
    private void RefineBodyContact(double clock, ref float step, ref ContactChoice? hit)
    {
        // 当前时点已经成立的接触没有可缩短的前段；零段会丢失来向，不重复预测它。
        while (hit.HasValue && hit.Value.Fraction > 0f && hit.Value.Fraction < 1f)
        {
            ContactChoice candidate = hit.Value;
            bool carrying = candidate.Carrying.HasValue && candidate.Flight == null;
            bool weapon = candidate.BodyRelation >= 0 &&
                          candidate.Left.Running.Cast.Plan.Body.Construction.Relations[candidate.BodyRelation].Kind is BodyRelationKind.Brace or BodyRelationKind.Deflect or BodyRelationKind.Disarm &&
                          candidate.Left.Running.Cast.Plan.Body.Construction.Relations[candidate.BodyRelation].To.Part == "item";
            if (!carrying && !weapon) break;
            float shorter = step * candidate.Fraction;
            if (shorter >= step) break;
            foreach (RunningCast running in active) running.Cast.RestoreStep();
            step = shorter;
            Predict(clock, step);
            hit = FindFirst(clock, step);
        }
    }

    /// <summary>按实际空间筛选，再比较相撞和人物接触的共同首次时点。</summary>
    /// <param name="clock">共同起点。</param>
    /// <param name="step">共同步长。</param>
    private ContactChoice? FindFirst(double clock, float step)
    {
        ContactChoice? first = null;
        contactEvaluation++;
        for (int index = 0; index < shapes.Count; index++)
        {
            ShapeMotion shape = shapes[index];
            RunningCast running = shape.Running;
            CastAuthority owner = running.Owner(shape.To.Node);
            bool canTouch = TouchPath(shape, step, out float touchFrom);
            if (canTouch && (running.Cast.Plan.HasWorldEffects || running.Cast.Plan.HasWorldContactEvents))
            {
                if (shape.To.Node < 0 && running.Cast.ContactScopeChanged(shape.To.Node))
                {
                    FindWorldContacts(shape, step, Include, running.Cast.PreviousBodyStage, oldScope: true);
                    FindWorldContacts(shape, step, Include, running.Cast.BodyStage, endpointOnly: true);
                }
                else FindWorldContacts(shape, step, Include);
            }
            bool powered = shape.To.Node < 0 || running.Cast.ContactSweepForce(shape.To.Node) > 0f;
            bool fire = powered && shape.To.Material?.Id == "fire" && shape.To.Material.State == "energy";
            bool ordinary = powered && running.Cast.Role(shape.To.Node) != ConstructionContactRole.Decoration;
            bool thermalSource = fire || shape.To.Node >= 0 && running.Cast.Burning(shape.To.Node).Heat > 0;
            bool attack = canTouch && ordinary && running.Cast.Role(shape.To.Node) == ConstructionContactRole.Attack;
            if (shape.To.Node < 0 && attack) FindBodyCorrespondences(shape, clock, step, touchFrom, Include);
            if (attack && hasBodyGuardShapes)
                FindBodyGuards(shape, step, touchFrom, Include);
            if (shape.To.Node >= 0 && (ordinary || thermalSource))
            {
                if (!singleActingNode)
                {
                    shapeIndex.Query(shape.Bounds, nearby);
                    foreach (int other in nearby)
                    {
                        if (other == index) continue;
                        ShapeMotion right = shapes[other];
                        if (right.To.Node < 0) continue;
                        bool quench = fire && AbilityQuenching.Matches(shape.To.Material, right.To.Material);
                        ThermalReaction reaction = thermalSource ? ThermalPair(shape, right) : ThermalReaction.None;
                        if (!quench && reaction == ThermalReaction.None &&
                            (other <= index || IsQuenching(shape, right) ||
                             ThermalPair(right, shape) != ThermalReaction.None || !ordinary ||
                             right.Running.Cast.ContactSweepForce(right.To.Node) <= 0f ||
                             running.Cast.Role(shape.To.Node) == ConstructionContactRole.Decoration ||
                             right.Running.Cast.Role(right.To.Node) == ConstructionContactRole.Decoration ||
                             running.Cast.Role(shape.To.Node) == ConstructionContactRole.Barrier &&
                             right.Running.Cast.Role(right.To.Node) == ConstructionContactRole.Barrier)) continue;
                        bool requiresCorrespondence = !quench && reaction == ThermalReaction.None &&
                            !owner.IsHostileTo(right.Running.Owner(right.To.Node)) &&
                            !right.Running.Owner(right.To.Node).IsHostileTo(owner);
                        float leftFromTime = float.PositiveInfinity, rightFromTime = float.PositiveInfinity;
                        float leftUntil = 0f, rightUntil = 0f;
                        if (requiresCorrespondence)
                        {
                            bool rightImage = CorrespondenceRange(shape, right, out rightFromTime, out rightUntil);
                            bool leftImage = CorrespondenceRange(right, shape, out leftFromTime, out leftUntil);
                            if (!rightImage && !leftImage) continue;
                            if (!rightImage) rightFromTime = float.PositiveInfinity;
                            if (!leftImage) leftFromTime = float.PositiveInfinity;
                        }
                        bool continuous = shape.Continuous && right.Continuous &&
                                          (reaction != ThermalReaction.Ignite || running.Cast.StartHeat(shape.To.Node) > 0);
                        float sweepFrom = continuous ? (requiresCorrespondence ? Mathf.Min(leftFromTime, rightFromTime) : 0f) : 1f;
                        AbilityCapsule leftFrom = !continuous ? shape.To : sweepFrom == 0f ? shape.From : Slice(shape.From, shape.To, sweepFrom),
                            rightFrom = !continuous ? right.To : sweepFrom == 0f ? right.From : Slice(right.From, right.To, sweepFrom);
                        if (AbilityPairSweep.Find(leftFrom, shape.To, rightFrom, right.To, out float at, out Vector3 point,
                                out Vector3 normal))
                        {
                            float fraction = Mathf.Lerp(sweepFrom, 1f, at);
                            if (requiresCorrespondence && !(fraction >= leftFromTime && fraction < leftUntil) &&
                                !(fraction >= rightFromTime && fraction < rightUntil)) continue;
                            Include(new ContactChoice(shape, default, right, fraction, point, normal, step, quench, reaction: reaction));
                        }
                    }

                }
                if (thermalSource) FindWorldThermal(shape, step, Include);
                if (fire)
                {
                    WorldMaterialService.QueryQuenchingWater(shape.Bounds, quenchingWater);
                    foreach (var water in quenchingWater)
                        if (AbilityPairSweep.Find(shape.Continuous ? shape.From : shape.To, shape.To, water.Shape,
                                water.Shape,
                                out float at, out Vector3 point, out Vector3 normal))
                            Include(new ContactChoice(shape, default, null, shape.Continuous ? at : 1f, point, normal,
                                step,
                                quenching: true, worldRecord: water.Record, worldShape: water.Shape));
                }
            }

            if (!powered || !running.Cast.AffectsActors(shape.To.Node)) continue;
            // 已被真实器物接触抵住的兵器不能仅因进入伤害时段而穿透补伤；独立法术和其他肢体继续运行。
            if (shape.To.Node < 0 && (running.WeaponConstraint?.Active == true ||
                                    running.Connection?.Active == true && running.Connection.WeaponOnly)) continue;
            actorIndex.Query(shape.Bounds, nearby);
            // 双方关系只查询已选目标；长尾等真实表面可能超出原版人物粗筛球。
            if (shape.To.Node < 0 && running.Cast.Plan.Body.Construction.HasControlRelations &&
                actorIndices.TryGetValue(running.TargetId, out int selected) && !nearby.Contains(selected))
                nearby.Add(selected);
            foreach (int id in nearby)
            {
                ActorMotion actor = actors[id];
                if (actor.Actor.data?.id != actor.Id || !actor.Actor.isAlive() || !actor.Actor.hasHealth()) continue;
                Sample(actor, clock, out Vector3 fromFoot, out float fromHeight);
                Sample(actor, clock + step, out Vector3 toFoot, out float toHeight);
                var b0 = ActorHitBoundary.Create(fromFoot, fromHeight,
                    actor.Body?.BodyControlled == true ? actor.Body.BeforeTorso : Vector3.zero);
                var b1 = ActorHitBoundary.Create(toFoot, toHeight,
                    actor.Body?.BodyControlled == true ? actor.Body.Cast.BodyPose.Value.Torso : Vector3.zero);
                if (shape.To.Node < 0)
                {
                    // 双方关系仍按原整身资料选择目标，阶段覆盖只改变该次攻击作用。
                    if (owner.Matches((running.Cast.Plan.Body.Impact ?? running.Cast.Plan.Impact).Targets, actor.Actor))
                        FindBodyConnections(shape, actor, clock, step, Include);
                    if (!canTouch) continue;
                }

                bool changed = running.Cast.ContactScopeChanged(shape.To.Node);
                if (changed) AtStage(running.Cast.PreviousBodyStage, true, false);
                AtStage(running.Cast.BodyStage, false, changed);

                // 边界前保留旧作用的最后路径；新作用只接受真实边界处的接触，不能向过去借路径。
                /// <summary>按实际实例确认同一几何路径，跨界时分别检查旧段和新起点。</summary>
                /// <param name="stage">实际作用实例。</param><param name="oldScope">只接受边界前的旧作用。</param>
                /// <param name="endpointOnly">只接受新边界起点。</param>
                void AtStage(int stage, bool oldScope, bool endpointOnly)
                {
                    CompiledConstructionImpact effect = running.Cast.ImpactAtStage(shape.To.Node, stage);
                    if (effect.SelectedTargetOnly && owner.TargetId != actor.Id || !owner.Matches(effect.Targets, actor.Actor) ||
                        !running.Cast.ContactInterval(shape.To.Node, stage, out float from, out float until)) return;
                    int node = shape.To.Node;
                    float originalUntil = float.PositiveInfinity;
                    if (node >= 0 && running.Cast.Plan.Nodes[node].Correspondence?.OriginalContactOnly == true)
                    {
                        if (!owner.IsLive || CorrespondenceReceiver(running, node, current: false) != actor.Actor ||
                            !running.Cast.CorrespondenceInterval(node, out float originalFrom, out originalUntil)) return;
                        from = Mathf.Max(from, originalFrom);
                        until = Mathf.Min(until, originalUntil);
                    }
                    from = Mathf.Max(from, touchFrom);
                    float earliest = Mathf.Max(from, ContactFraction(shape, actor, b0, b1, step, stage, from));
                    if (earliest > 1f || earliest > until) return;
                    bool pulse = running.Cast.Plan.ContactPolicies[running.Cast.ContactGroup(shape.To.Node)].Mode == ConstructionContactMode.Pulse;
                    if (pulse && (!shape.Continuous || endpointOnly) && earliest < 1f - .000001f) return;
                    if (!shape.Continuous || endpointOnly) earliest = 1f;
                    AbilityCapsule a0 = Slice(shape.From, shape.To, earliest), target0 = Slice(b0, b1, earliest);
                    float at; Vector3 point, normal;
                    BodySurfaceBinding frost = null;
                    BodySurfacePose frostPose = null;
                    BodySurfaceAnchor frostAnchor = default;
                    if (effect.FrostCoating != null)
                    {
                        double start = clock + step * earliest;
                        if (!FindFrostSurface(actor, effect.FrostCoating, a0, pulse ? a0 : shape.To, start,
                                pulse ? start : clock + step, out at, out point, out normal, out frost,
                                out frostPose, out frostAnchor)) return;
                    }
                    else if (shape.To.Node < 0 && running.Cast.Plan.Body?.Part.IsJoint == true)
                    {
                        BodySurfacePose end = running.Cast.BodyPose.Value.Joint.Contact;
                        BodySurfacePose start = BodySurfacePose.Lerp(running.BeforeJoint.Contact, end, earliest);
                        if (pulse) end = start;
                        if (!BodySurfacePose.Find(start, end, target0, pulse ? target0 : b1, "surface", out at, out var anchor, out normal)) return;
                        point = anchor.Point(BodySurfacePose.Lerp(start, end, at)); normal = -normal;
                    }
                    else if (!AbilityPairSweep.Find(a0, pulse ? a0 : shape.To, target0, pulse ? target0 : b1, out at, out point, out normal)) return;
                    float fraction = Mathf.Lerp(earliest, 1f, at);
                    if (fraction > until || fraction >= originalUntil || oldScope && fraction >= 1f) return;
                    Include(new ContactChoice(shape, actor, null, fraction, point, normal, step,
                        bodySurface: frostPose, bodyAnchor: frostAnchor, actorShape: Slice(b0, b1, fraction),
                        frostBinding: frost, frostRule: effect.FrostCoating));
                }
            }
        }

        FindCarryingContacts(clock, step, Include);
        FindBodyFlightContacts(clock, step, Include);
        if (WorldMaterialService.HasHeat) FindWorldHeatSources(step, Include);
        return SelectRayContact(first);

        void Include(ContactChoice value)
        {
            if (value.Carrying.HasValue)
            {
                if (!first.HasValue || ContactChoice.Compare(value, first.Value) < 0) first = value;
                return;
            }
            // 预测边界可能正好是普通接触结束；即使为控制保留了形状，也不能在终点多结算一次。
            if (value.Fraction == 1f &&
                (value.BodyRelation < 0 && value.Left.To.Node < 0 && !TouchesAtEnd(value.Left) ||
                 value.BodyGuardFacing != Vector3.zero && !TouchesAtEnd(value.Right.Value))) return;
            if (value.Fraction > value.Left.To.Until ||
                value.Right.HasValue && value.Fraction > value.Right.Value.To.Until) return;
            if (!value.Left.Running.Cast.CanSweepAt(value.Left.To.Node, value.Fraction) ||
                value.Right.HasValue &&
                !value.Right.Value.Running.Cast.CanSweepAt(value.Right.Value.To.Node, value.Fraction)) return;
            if (value.LeftRayLength >= 0f || value.RightRayLength >= 0f)
            {
                rayContacts.Add(value);
                return;
            }
            if (!first.HasValue || ContactChoice.Compare(value, first.Value) < 0) first = value;
        }
    }

    /// <summary>共同预测已按关系标记切段：内部可扫掠，边界新开的表面只接受末端接触。</summary>
    /// <param name="shape">粗筛保留的共用形状。</param><param name="step">本段共同消耗的世界秒数。</param><param name="from">普通接触可用的路径起点。</param>
    private static bool TouchPath(in ShapeMotion shape, float step, out float from)
    {
        from = 0f;
        if (shape.To.Node >= 0) return true;
        float mid = shape.Running.Cast.Age - step * .5f;
        if (shape.Continuous && step > 0f && shape.Running.Cast.BodyContactDuringStep(mid) &&
            shape.Running.Body.UsesTouchShape(shape.To.Shape, mid)) return true;
        from = 1f;
        return TouchesAtEnd(shape);
    }

    /// <summary>在同一预测终点核对普通接触，结束标记不属于该窗口。</summary>
    /// <param name="shape">实际作用面。</param>
    private static bool TouchesAtEnd(in ShapeMotion shape) => shape.Running.Cast.BodyContactEnabled &&
        shape.Running.Body.UsesTouchShape(shape.To.Shape, shape.Running.Cast.Age);

    /// <summary>只在回到真实接触时点后修改状态及游戏，随后重新查询余段。</summary>
    /// <param name="hit">共同时间最早的真实接触。</param>
    private static void Resolve(in ContactChoice hit)
    {
        if (hit.Flight != null)
        {
            ConstructedActorImpulse.ContactBody(hit.Flight, hit.Carrying.Value, simulation.contactTime);
            return;
        }
        ValidateSource(hit.Left.Running);
        if (hit.Left.Running.Replaced) return;
        if (hit.Right.HasValue)
        {
            ValidateSource(hit.Right.Value.Running);
            if (hit.Right.Value.Running.Replaced) return;
        }
        if (hit.Carrying.HasValue)
        {
            RunningCast carrier = hit.Left.Running;
            if (carrier.Connection?.Active == true)
            {
                BodyConnection connection = carrier.Connection;
                double time = simulation.contactTime;
                connection.EnvironmentContact = hit.Carrying.Value;
                ReleaseBodyConnection(carrier, hit.Carrying.Value.Reason, fade: false, time: time);
                ResolveBodyImpact(connection.ImpactSource, hit.Carrying.Value,
                    connection.Motion.Matrix.MultiplyPoint3x4(connection.Geometry.Center),
                    connection.LinearVelocity, connection.AngularVelocity, time);
            }
            return;
        }
        if (hit.BodyRelation >= 0)
        {
            ResolveBodyConnection(hit);
            return;
        }

        if (hit.Tile != null)
        {
            ResolveWorldContact(hit);
            return;
        }

        if (hit.Quenching)
        {
            ResolveQuenching(hit);
            return;
        }

        if (hit.Reaction != ThermalReaction.None)
        {
            ResolveThermal(hit);
            return;
        }

        RunningCast running = hit.Left.Running;
        int node = hit.Left.To.Node;
        if (hit.BodyGuardFacing != Vector3.zero)
        {
            ResolveBodyGuard(hit);
            return;
        }
        if (hit.Right.HasValue)
        {
            if (node < 0 || hit.Right.Value.To.Node < 0)
            {
                ResolveBodyCorrespondence(hit);
                return;
            }
            if (TryReflect(in hit)) return;
            ShapeMotion right = hit.Right.Value;
            Actor leftReceiver = CorrespondenceReceiver(running, node);
            Actor rightReceiver = CorrespondenceReceiver(right.Running, right.To.Node);
            long leftOwner = running.Owner(node).Id, rightOwner = right.Running.Owner(right.To.Node).Id;
            float leftBefore = running.Cast.RemainingForce(node), rightBefore = right.Running.Cast.RemainingForce(right.To.Node);
            float absorbed = Mathf.Min(leftBefore, rightBefore);
            bool leftSurface = running.Cast.Role(node) == ConstructionContactRole.Barrier ||
                right.Running.Cast.Role(right.To.Node) != ConstructionContactRole.Barrier && leftBefore <= rightBefore;
            AbilityMatter contactSurface = leftSurface ? running.Cast.Matter(node) : right.Running.Cast.Matter(right.To.Node);
            if (hit.LeftRayLength >= 0f && absorbed >= running.Cast.RemainingForce(node))
                running.Cast.ClipRay(node, hit.LeftRayLength);
            if (hit.RightRayLength >= 0f && absorbed >= right.Running.Cast.RemainingForce(right.To.Node))
                right.Running.Cast.ClipRay(right.To.Node, hit.RightRayLength);
            if (node < 0) ConfirmBodyFeedback(running, hit.Point);
            if (right.To.Node < 0) ConfirmBodyFeedback(right.Running, hit.Point);
            running.Cast.ConfirmClash(node, hit.Point, absorbed);
            right.Running.Cast.ConfirmClash(right.To.Node, hit.Point, absorbed);
            running.Cast.ConsumeForce(node, absorbed);
            right.Running.Cast.ConsumeForce(right.To.Node, absorbed);
            float leftAfter = running.Cast.RemainingForce(node), rightAfter = right.Running.Cast.RemainingForce(right.To.Node);
            long clashGeneration = worldGeneration;
            ApplyCorrespondence(right, hit.Left, leftReceiver, absorbed, hit.Point);
            if (clashGeneration != worldGeneration) return;
            ApplyCorrespondence(hit.Left, right, rightReceiver, absorbed, hit.Point);
            if (clashGeneration != worldGeneration) return;
            CombatContactFeedback.Clash(hit.Point, hit.Normal, contactSurface, leftSurface ? leftBefore - leftAfter : rightBefore - rightAfter,
                leftSurface ? leftAfter : rightAfter);
            ClashConfirmed?.Invoke(new ConstructedClash(running.Id, right.Running.Id, leftOwner, rightOwner,
                node, right.To.Node, hit.Point, hit.Normal, LogicTime, running.Cast.Age, right.Running.Cast.Age, absorbed,
                leftAfter, rightAfter, running.SourceId, right.Running.SourceId, leftBefore - leftAfter,
                rightBefore - rightAfter, ContactPartPresent(running, node), ContactPartPresent(right.Running, right.To.Node)));
            return;
        }

        long contactGeneration = worldGeneration;
        if (node >= 0 && running.Cast.Plan.Nodes[node].SupportGrip != null && !running.Cast.HasGripSupport(node)) return;
        ActorMotion actor = hit.Actor;
        if (actor.Actor.data?.id != actor.Id || !actor.Actor.isAlive() || !actor.Actor.hasHealth()) return;
        simulation.Sample(actor, simulation.contactTime, out Vector3 spreadCenter, out _);
        CastAuthority contactOwner = running.Owner(node);
        if (node >= 0 && running.Cast.Plan.Nodes[node].Correspondence?.OriginalContactOnly == true &&
            (!contactOwner.IsLive || CorrespondenceReceiver(running, node) != actor.Actor)) return;
        int reflections = running.Cast.Reflections(node);
        ContactMemory memory = RememberContact(running, node, actor.Id);
        AbilityMatter beforeMatter = node < 0 ? null : running.Cast.Matter(node);
        CompiledConstructionImpact impact = running.Cast.Impact(node);
        float strength = node < 0 ? running.BodyStrikeScale(running.Cast.BodyStage) :
            running.Cast.ContactAttenuation(node, hit.Left.To.Shape);
        int healthBefore = actor.Actor.data.health;
        DamageSettlement primaryDamage = default;
        DamageObservation observation = null;
        // 明确发力继承接触前的运动；原版同次命中附带的击退不再重复计入。
        Vector3? priorVelocity = impact.Impulse.HasValue ? ConstructedActorImpulse.Velocity(actor.Actor) : null;
        ForceReceiver? forceReceiver = impact.Impulse.HasValue ? new ForceReceiver(actor.Actor) : null;
        ApplyAttackState result = ApplyAttackState.Hit;
        if (node < 0 && impact.DamageScale > 0f)
        {
            var owner = running.Source.GetExtend();
            observation = ObserveQueuedContact(running.Id, running.Cast.Plan.Id, contactOwner.Id, actor.Id,
                memory.Count, LogicTime);
            AttackDataResult strike = running.Body.Weapon == null
                ? KnightWeaponStrikeResolver.StrikeUnarmed(owner, actor.Actor, null, running.Power * impact.DamageScale * strength,
                    observer: observation)
                : KnightWeaponStrikeResolver.StrikeBorrowed(owner, running.Body.Weapon, actor.Actor,
                    running.Power * impact.DamageScale * strength, observer: observation);
            result = strike.state;
        }
        else if (node >= 0 && impact.DamageScale > 0f)
        {
            var damage = contactOwner.Damage;
            damage.TargetID = actor.Id;
            damage.Damage = running.Cast.DamageForce(node) * impact.DamageScale *
                strength;
            damage.Element = running.Cast.FixedDamageElement(node, impact) ?? Composition(beforeMatter.DamageKind);
            damage.ContactSound = CombatContactFeedback.SoundFor(beforeMatter);
            primaryDamage = GetHitEventSystem.Resolve(damage);
        }

        if (contactGeneration != worldGeneration) return;
        ApplyRestoration(running, node, actor.Actor, impact.Restoration, strength);
        ConstructedTargetResponse response = ApplyTargetResponse(running, node, actor.Actor, actor.Id, impact, result,
            priorVelocity: priorVelocity, forceReceiver: forceReceiver,
            strength: strength, settled: primaryDamage, contact: hit.Point);
        if (contactGeneration != worldGeneration) return;
        if (result == ApplyAttackState.Hit && impact.WakanTransfer > 0f &&
            actor.Actor.data?.id == actor.Id && actor.Actor.isAlive() && actor.Actor.hasHealth() &&
            (response.Damage.ShieldAbsorbed <= 0f || response.Damage.HealthLost > 0))
            TransferContactWakan(contactOwner, actor.Actor, impact.WakanTransfer * strength, impact.WakanTransferMode);
        if (contactGeneration != worldGeneration) return;
        if (result == ApplyAttackState.Hit && node >= 0 && running.Cast.Plan.Nodes[node].Correspondence?.ShowBody == true &&
            (response.ForceApplied || response.StaggerSeconds > 0f || response.BindSeconds > 0f) &&
            CorrespondenceReceiver(running, node) == actor.Actor)
            ShowShadow(running, node, actor.Actor);
        if (result == ApplyAttackState.Hit && hit.FrostBinding != null)
            ConfirmFrostCoating(hit, hit.FrostRule);
        if (primaryDamage.HealthLost > 0 && running.Cast.Plan.Id == BalefulWindConstruction.WaveId)
            AddWindScar(running, node, actor.Actor, hit.Point, false);
        if (result == ApplyAttackState.Hit)
            ApplyContactSpreads(running, node, actor.Id, impact, spreadCenter, simulation.contactTime, strength);
        if (contactGeneration != worldGeneration) return;
        if (node >= 0 && result == ApplyAttackState.Hit && running.Cast.Plan.ActorTransferSteps.Length != 0)
            ResolveActorTransfer(running, node, actor.Actor, actor.Id, contactGeneration);
        if (contactGeneration != worldGeneration) return;
        if (result == ApplyAttackState.Hit) ConfirmSupportGrip(hit);
        if (contactGeneration != worldGeneration) return;
        running.Cast.ConfirmContact(node, 1f, hit.Point, actor.Id,
            hit.LeftRayLength >= 0f ? hit.LeftRayLength : null);
        simulation.SelectContactTargets(running, node, actor);
        if (node < 0) ConfirmBodyFeedback(running, hit.Point);
        if (running.Cast.UsesBodyWindow(node) && result == ApplyAttackState.Hit) RespondToBodyResult(running, BodyActionResult.Contact);
        else if (running.Cast.UsesBodyWindow(node) && result is ApplyAttackState.Block or ApplyAttackState.Deflect)
            RespondToBodyResult(running, BodyActionResult.Blocked);
        EndExhaustedContactGroup(running, node);
        AbilityMatter afterMatter = node < 0 ? null : running.Cast.Matter(node);
        int? healthAfter = actor.Actor.data?.id == actor.Id ? actor.Actor.data.health : null;
        ContactConfirmed?.Invoke(new ConstructedContact(running.Id, running.Cast.Plan.Id, contactOwner.Id, actor.Id,
            node, hit.Point, hit.Normal, LogicTime, running.Cast.Age, result, ContactPartPresent(running, node),
            running.Cast.UsesBodyWindow(node) ? running.Cast.BodyResult : null, response.Damage,
            observation?.Pending == true, healthBefore, healthAfter, running.SourceId, reflections,
            memory.Count, running.GroupHits[running.Cast.ContactGroup(node)], beforeMatter?.Id, beforeMatter?.State,
            afterMatter?.Id, afterMatter?.State,
            impact.DamageScale > 0f || response.AdditionalDamageAttempted, response.VelocityChange, response.ForceApplied, response.StaggerSeconds,
            response.BindSeconds, response.StrainSeconds, response.ExposureSeconds));
    }

    /// <summary>是否仍留下真实部位；存在的部位也可能已没有攻击余力。</summary>
    /// <param name="running">实际施放。</param><param name="node">实际部位，负一为身体。</param>
    private static bool ContactPartPresent(RunningCast running, int node) =>
        node < 0 ? running.BodyControlled : running.Cast.IsNodePresent(node);

    /// <summary>已确认接触复用共享恢复规则；可按接触余力恢复当前控制人物，不重新读取攻击力。</summary>
    /// <param name="running">实际施放。</param><param name="node">实际部位。</param>
    /// <param name="target">已确认的人物。</param><param name="effect">可选恢复资料。</param>
    /// <param name="attenuation">接触位置给出的实际传播衰减。</param>
    /// <param name="forceBasis">实际映像交锋消耗，省略时读取原部位作用量。</param>
    private static void ApplyRestoration(RunningCast running, int node, Actor target, CompiledConstructionRestoration effect,
        float attenuation = 1f, float? forceBasis = null)
    {
        if (effect == null) return;
        CastAuthority owner = running.Owner(node);
        if (effect.Target == ConstructionRestorationTarget.Controller)
        {
            if (!owner.IsLive) return;
            target = owner.Actor;
        }
        if (effect.RemovedStatus != null && (!effect.RemovalOwnSourceOnly || owner.IsLive))
            CombatStatusEffects.RemoveStatus(target, effect.RemovedStatus,
                effect.RemovalOwnSourceOnly ? owner.Actor : null, effect.RemovalMaximum);
        float amount = effect.Health + Mathf.RoundToInt((forceBasis ?? (node < 0 ? running.Power : running.Cast.RemainingForce(node))) * attenuation * effect.HealthPerForce);
        if (amount > 0f) RestoreLivingActor(target, amount);
        if (effect.HealthPerSecond > 0f && target.data.health < target.getMaxHealth() && owner.IsLive)
            CombatStatusEffects.ApplyStrongestTickingStatus(target, StatusEffects.Rejuvenating, effect.Duration,
                effect.HealthPerSecond, effect.HealthPerSecond, owner.Damage.Element, owner.Actor);
        for (int i = 0; i < effect.CleanseCount; i++)
            if (!CombatStatusEffects.CleanseHighestPriorityNegativeStatus(target)) break;
    }

    /// <summary>在已确认接触处计算目标比例并沿用共享状态规则，不创建第二套状态更新。</summary>
    /// <param name="running">实际施放。</param><param name="node">实际部位。</param>
    /// <param name="target">已确认的人物。</param><param name="effects">已编译属性状态。</param>
    /// <param name="attenuation">真实接触位置或传播路程给出的本体强度倍率。</param>
    /// <param name="forceBasis">实际映像交锋消耗，省略时读取原部位作用量。</param>
    private static void ApplyStatuses(RunningCast running, int node, Actor target, IReadOnlyList<CompiledConstructionStatus> effects,
        float attenuation = 1f, float? forceBasis = null)
    {
        if (effects.Count == 0) return;
        CastAuthority owner = running.Owner(node);
        if (!owner.IsLive) return;
        foreach (CompiledConstructionStatus effect in effects)
        {
            BaseStats stats = null;
            if (effect.Stats != null)
            {
                stats = new BaseStats();
                foreach (var pair in effect.Stats) stats[pair.Key] = pair.Value;
            }
            if (effect.TargetStatRatios?.Count > 0)
            {
                // 未指定固定表时保留模板标记，比例项替换模板同名值；明确固定值则与比例相加。
                stats ??= (BaseStats)effect.Asset.stats.Clone();
                foreach (var pair in effect.TargetStatRatios)
                {
                    float fixedValue = effect.Stats != null && effect.Stats.TryGetValue(pair.Key, out float value) ? value : 0f;
                    stats[pair.Key] = fixedValue + Mathf.Max(0f, target.stats[pair.Key]) * pair.Value;
                }
                // 空属性不能吞掉同一声明的周期作用；明确刷新还需能清除原来的值。
                if (!stats.hasStats() && !stats.hasTags() && effect.Tick == null &&
                    (effect.Application != ConstructionStatusApplication.Refresh ||
                     !CombatStatusEffects.TryGetStatus(target, effect.Asset, null, out _))) continue;
            }
            if (effect.Application == ConstructionStatusApplication.Strongest)
            {
                bool applied = CombatStatusEffects.ApplyStrongestStatus(target, effect.Asset, effect.Duration, effect.Potency, owner.Actor, stats);
                if (effect.WaitForDuration && applied && CombatStatusEffects.TryGetStatus(target, effect.Asset, null, out var strongest))
                    target.makeWait(strongest.GetComponent<Core.Components.AliveTimeLimit>().value);
                continue;
            }
            float tickValue = effect.Tick == null ? 0f : (forceBasis ?? (node < 0 ? running.Power : running.Cast.RemainingForce(node))) * attenuation * effect.Tick.ForcePerSecond;
            if (effect.Tick != null && tickValue <= 0f) continue;
            var status = CombatStatusEffects.ApplyStatusDeclaration(target, effect, owner.Actor,
                owner.Damage.AttackerPowerLevel ?? 0f, stats, tickValue);
            if (effect.WaitForDuration && !status.IsNull) target.makeWait(status.GetComponent<Core.Components.AliveTimeLimit>().value);
        }
    }

    /// <summary>直接接触和真实水路等传播都从这一入口应用目标响应。</summary>
    /// <param name="running">真实施放。</param><param name="node">作用部位。</param><param name="target">实际目标。</param>
    /// <param name="targetId">伤害回调之前确认的人物编号。</param>
    /// <param name="impact">已编译作用。</param><param name="result">真实攻击结果。</param>
    /// <param name="origin">传播时的真实作用面中心，普通接触读取当前部位。</param>
    /// <param name="priorVelocity">直接接触前已有的运动，不包含同次原版自动击退。</param>
    /// <param name="forceReceiver">同次伤害或停顿解除动作之前的承力身体。</param>
    /// <param name="strength">真实介质路程给出的强度倍率，普通接触为一。</param>
    /// <param name="settled">已结算的本体扣血，用于合计追加作用。</param>
    /// <param name="contact">直接相交的形体位置；传播没有形体相交点。</param>
    private static ConstructedTargetResponse ApplyTargetResponse(RunningCast running, int node, Actor target,
        long targetId,
        CompiledConstructionImpact impact, ApplyAttackState result, Vector3? origin = null,
        Vector3? priorVelocity = null, ForceReceiver? forceReceiver = null, float strength = 1f,
        DamageSettlement settled = default, Vector3? contact = null)
    {
        if (target.data?.id != targetId || !target.isAlive() || !target.hasHealth())
            return new ConstructedTargetResponse(targetId, Vector3.zero, false, 0f, 0f, damage: settled);
        long generation = worldGeneration;
        if (result == ApplyAttackState.Hit && impact.ReleaseGrab != ConstructionGrabRelease.None)
        {
            TryReleaseGrab(running, node, target, impact.ReleaseGrab, strength);
            if (generation != worldGeneration || target.data?.id != targetId || !target.isAlive() || !target.hasHealth())
                return new ConstructedTargetResponse(targetId, Vector3.zero, false, 0f, 0f, damage: settled);
        }
        ApplyContactExchange(running.Owner(node), target, impact.Exchange, result, strength);
        if (generation != worldGeneration || target.data?.id != targetId || !target.isAlive() || !target.hasHealth())
            return new ConstructedTargetResponse(targetId, Vector3.zero, false, 0f, 0f, damage: settled);
        CompiledConstructionContactOutcome outcome = SelectContactOutcome(impact.RandomContact, result);
        bool additionalDamage = false;
        if (result == ApplyAttackState.Hit)
        {
            bool valid = ApplyAdditionalDamage(running, node, target, targetId, impact.AdditionalDamage, strength, out bool direct, out _, out var directDamage);
            settled += directDamage;
            additionalDamage = direct;
            if (valid && outcome != null)
            {
                valid = ApplyAdditionalDamage(running, node, target, targetId, outcome.Damage, strength, out bool chosen, out _, out var chosenDamage);
                settled += chosenDamage;
                additionalDamage |= chosen;
            }
            if (!valid) return new ConstructedTargetResponse(targetId, Vector3.zero, false, 0f, 0f, additionalDamage: additionalDamage, damage: settled);
        }
        ApplyStatuses(running, node, target, impact.Statuses, strength);
        if (outcome != null)
        {
            ApplyStatuses(running, node, target, outcome.Statuses, strength);
            ApplyRestoration(running, node, target, outcome.Restoration, strength);
        }
        if (result == ApplyAttackState.Hit && impact.Signal != null)
        {
            if (generation == worldGeneration && target.data?.id == targetId && target.isAlive() && target.hasHealth())
                DeliverSignal(running, node, target, impact.Signal, strength);
            if (generation != worldGeneration || target.data?.id != targetId || !target.isAlive() || !target.hasHealth())
                return new ConstructedTargetResponse(targetId, Vector3.zero, false, 0f, 0f, additionalDamage: additionalDamage, damage: settled);
        }
        ConstructedTargetResponse conditional = result == ApplyAttackState.Hit ? ApplyConditionalContacts(running, node, target, targetId,
            impact.ConditionalContacts, origin, strength) : default;
        additionalDamage |= conditional.AdditionalDamageAttempted;
        settled += conditional.Damage;
        if (generation != worldGeneration || target.data?.id != targetId || !target.isAlive() || !target.hasHealth())
            return new ConstructedTargetResponse(targetId, conditional.VelocityChange, conditional.ForceApplied, 0f, 0f, additionalDamage: additionalDamage, damage: settled);
        if (!ApplyInventoryAging(running, node, target, impact.InventoryAging, result, strength) ||
            !ApplyEquipmentErosion(running, node, target, impact.EquipmentErosion, result, strength, contact) ||
            !ApplyEquipmentRepair(running, node, target, impact.EquipmentRepair, result, strength) ||
            !ApplyContactAging(running, node, target, impact.AgeYears, result, strength))
            return new ConstructedTargetResponse(targetId, conditional.VelocityChange, conditional.ForceApplied, 0f, 0f, additionalDamage: additionalDamage, damage: settled);
        ApplyContactReactions(target, impact, running.Owner(node), result, out float stagger, out float bound,
            out float strain, out float exposure);
        Vector3 velocity = ApplyContactForce(running, node, target, impact, result, out bool force, origin,
            priorVelocity, forceReceiver, strength);
        velocity += ApplyContactKnockback(running, node, target, impact.Knockback, result, out bool knocked, origin, strength);
        velocity += conditional.VelocityChange;
        force |= knocked || conditional.ForceApplied;
        if (outcome?.Knockback != null)
        {
            velocity += ApplyContactKnockback(running, node, target, outcome.Knockback, result, out bool chosenForce, origin, strength);
            force |= chosenForce;
        }
        if (force) InterruptContactBody(target);
        else if (stagger > 0f || bound > 0f) InterruptContactBody(target, Mathf.Max(stagger, bound));
        return new ConstructedTargetResponse(targetId, velocity, force, stagger, bound, strain, exposure, additionalDamage, settled);
    }

    /// <summary>先确认共享状态，再限制实际行动；受力仍单独经过原有抗击退规则。</summary>
    /// <param name="target">真正接触的目标。</param><param name="impact">已编译作用。</param>
    /// <param name="owner">当前控制者及保存的境界。</param><param name="result">真实攻击结果。</param>
    /// <param name="stagger">本次受理后的停顿秒数。</param><param name="bound">本次受理后的束缚秒数。</param>
    private static void ApplyContactReactions(Actor target, CompiledConstructionImpact impact, CastAuthority owner,
        ApplyAttackState result, out float stagger, out float bound, out float strain, out float exposure)
    {
        stagger = bound = strain = exposure = 0f;
        if (result != ApplyAttackState.Hit || !target.isAlive()) return;
        Actor source = owner.Actor?.data?.id == owner.Id ? owner.Actor : null;
        if (impact.StrainSeconds > 0f)
        {
            var state = CombatStatusEffects.ApplyConfirmedStatus(target, StatusEffects.BodyStrain,
                impact.StrainSeconds, source, owner.Damage.AttackerPowerLevel ?? 0f);
            if (!state.IsNull) strain = state.GetComponent<Core.Components.AliveTimeLimit>().value;
        }

        if (impact.ExposureSeconds > 0f)
        {
            var state = CombatStatusEffects.ApplyConfirmedStatus(target, StatusEffects.BodyExposure,
                impact.ExposureSeconds, source, owner.Damage.AttackerPowerLevel ?? 0f);
            if (!state.IsNull) exposure = state.GetComponent<Core.Components.AliveTimeLimit>().value;
        }

        if (strain > 0f || exposure > 0f) target.updateStats();
        if (impact.StaggerSeconds > 0f)
        {
            var state = CombatStatusEffects.ApplyConfirmedStatus(target, StatusEffects.Stagger,
                impact.StaggerSeconds, source, owner.Damage.AttackerPowerLevel ?? 0f);
            if (!state.IsNull) stagger = state.GetComponent<Core.Components.AliveTimeLimit>().value;
        }

        if (impact.BindSeconds > 0f)
        {
            var state = CombatStatusEffects.ApplyConfirmedStatus(target, StatusEffects.Bound,
                impact.BindSeconds, source, owner.Damage.AttackerPowerLevel ?? 0f);
            if (!state.IsNull) bound = state.GetComponent<Core.Components.AliveTimeLimit>().value;
        }

        if (stagger == 0f && bound == 0f) return;
        target.updateStats();
        target.stopMovement();
        if (bound > 0f) ConstructedActorImpulse.HoldHorizontal(target);
    }

    /// <summary>先解除旧关系，再让实际停顿或束缚连续收势；强制交接只归还原控制。</summary>
    /// <param name="target">实际受击目标。</param><param name="seconds">实际受理的停顿或束缚，零为强制交接。</param>
    private static void InterruptContactBody(Actor target, float seconds = 0f)
    {
        InterruptBodyConnections(target);
        foreach (RunningCast running in casts)
            if (running.SourceId == target.id && running.BodyControlled)
            {
                if (seconds > 0f && running.Body.IsCurrent(target) && !target.under_forces &&
                    running.Movement?.OwnsPosition != false && RespondToBodyResult(running, BodyActionResult.Interrupted))
                {
                    running.BodyReactionUntil = Math.Max(running.BodyReactionUntil, LogicTime + seconds);
                    continue;
                }
                running.Cast.InterruptBody();
                ReleaseBody(running);
            }
    }

    /// <summary>真实接触后按共同时间位置施力，记录经过境界及抗击退后的速度变化。</summary>
    /// <param name="running">本次真实施放。</param>
    /// <param name="node">实际作用部位。</param>
    /// <param name="target">仍有效的接触人物。</param>
    /// <param name="impact">已经复制的接触作用。</param>
    /// <param name="result">身体攻击的实际格挡等结果。</param>
    /// <param name="applied">实际受力规则是否受理。</param>
    /// <param name="origin">传播时真实作用面的中心。</param>
    /// <param name="priorVelocity">接触前已有的真实运动，空值读取当前受力。</param>
    /// <param name="forceReceiver">受击前的承力姿态。</param>
    /// <param name="strength">实际传播强度，普通形体为一。</param>
    /// <param name="allowRouting">是否允许沿来力规则转交其他人物。</param>
    private static Vector3 ApplyContactForce(RunningCast running, int node, Actor target,
        CompiledConstructionImpact impact, ApplyAttackState result, out bool applied, Vector3? origin = null,
        Vector3? priorVelocity = null, ForceReceiver? forceReceiver = null, float strength = 1f, bool allowRouting = true)
    {
        applied = false;
        if (impact.Force == 0f && !impact.Launch && !impact.Impulse.HasValue || result != ApplyAttackState.Hit ||
            !target.isAlive())
            return Vector3.zero;
        CastAuthority owner = running.Owner(node);
        Vector2 direction = Vector2.zero;
        if (impact.Force != 0f ||
            impact.Impulse.HasValue && (impact.Impulse.Value.x != 0f || impact.Impulse.Value.y != 0f))
        {
            StartingPose(target, out Vector3 position, out _);
            Vector3 center;
            if (impact.Origin == ConstructionForceOrigin.Shape) center = origin ?? running.Cast.ContactOrigin(node);
            else if (owner.IsLive) StartingPose(owner.Actor, out center, out _);
            else center = owner.LastFoot;
            direction = (Vector2)(position - center);
            if (direction.sqrMagnitude < .000001f) return Vector3.zero;
            direction.Normalize();
        }

        Vector3 before = target.velocity;
        if (impact.Impulse.HasValue)
        {
            Vector3 local = impact.Impulse.Value * strength;
            Vector3 change = new(direction.x * local.x - direction.y * local.y,
                direction.y * local.x + direction.x * local.y, local.z);
            ForceReceiver receiver = forceReceiver ?? new ForceReceiver(target);
            return allowRouting && (impact.ForceRouting != null || receiver.Response != null)
                ? ApplyRoutedForce(running, node, receiver, impact, change, priorVelocity, out applied)
                : DeliverForce(owner, target, change, impact.SlideSeconds, priorVelocity, out applied, out _);
        }

        if (target.isImmovable()) return Vector3.zero;
        applied = target.GetExtend().TryGetForce(owner.IsLive ? owner.Actor : null, direction.x * impact.Force * strength,
            direction.y * impact.Force * strength, impact.SlideSeconds > 0f ? 0f : impact.Lift * strength,
            owner.Damage.AttackerPowerLevel);
        if (applied)
        {
            Cultiway.Content.Persistence.ActorPositionSaveService.Remember(target.GetExtend());
            ConstructedActorImpulse.BeginCombat(target, impact.SlideSeconds, simulation.terrain);
        }

        return target.velocity - before;
    }

    /// <summary>只含主线程采集值及结果绑定的一个真实人物。</summary>
    private readonly struct ActorMotion
    {
        /// <summary>真实人物及稳定编号。</summary>
        internal readonly Actor Actor;

        /// <summary>对象池重用时仍能核对原人物。</summary>
        internal readonly long Id;

        /// <summary>真实前后脚下位置。</summary>
        internal readonly Vector3 From, To;

        /// <summary>真实前后体高。</summary>
        internal readonly float FromHeight, Height;

        /// <summary>快速识别尺寸及身体资源改变，不在每次位置通知中读取帧资源。</summary>
        internal readonly float NativeScale;

        /// <summary>采集时的实际身体资源。</summary>
        internal readonly AnimationContainerUnit Appearance;

        /// <summary>本轮控制此身体的动作。</summary>
        internal readonly RunningCast Body;

        /// <summary>保存采集结果。</summary>
        /// <param name="actor">实际人物。</param>
        /// <param name="id">稳定编号。</param>
        /// <param name="from">前态脚下。</param>
        /// <param name="to">后态脚下。</param>
        /// <param name="fromHeight">前态体高。</param>
        /// <param name="height">后态体高。</param>
        /// <param name="body">实际身体动作。</param>
        internal ActorMotion(Actor actor, long id, Vector3 from, Vector3 to, float fromHeight, float height,
            RunningCast body)
        {
            Actor = actor;
            Id = id;
            From = from;
            To = to;
            FromHeight = fromHeight;
            Height = height;
            Body = body;
            NativeScale = actor.actor_scale;
            Appearance = actor.animation_container;
        }
    }

    /// <summary>共同时间段内一个实际作用形状的数值前后态。</summary>
    private readonly struct ShapeMotion
    {
        /// <summary>结果确认时绑定的实际施放。</summary>
        internal readonly RunningCast Running;

        /// <summary>同一形状的前后姿态。</summary>
        internal readonly AbilityCapsule From, To;

        /// <summary>是否具有本段有效的前态；刚出现的形状只检查出现时点。</summary>
        internal readonly bool Continuous;

        /// <summary>实际扫掠范围。</summary>
        internal readonly Bounds Bounds;

        /// <summary>保存数值路径。</summary>
        /// <param name="running">本次施放。</param>
        /// <param name="from">前态。</param>
        /// <param name="to">后态。</param>
        /// <param name="continuous">两端是否均有效。</param>
        internal ShapeMotion(RunningCast running, AbilityCapsule from, AbilityCapsule to, bool continuous)
        {
            Running = running;
            From = from;
            To = to;
            Continuous = continuous;
            Bounds = AbilityContactIndex.Swept(from, to);
        }

        /// <summary>按实际轴段位移及半径变化取得接触表面速度。</summary>
        /// <param name="point">接触位置。</param>
        /// <param name="outward">从表面向外的方向。</param>
        /// <param name="fraction">共同步内接触进度。</param>
        /// <param name="seconds">共同步长。</param>
        internal Vector3 SurfaceVelocity(Vector3 point, Vector3 outward, float fraction, float seconds)
        {
            if (!Continuous || seconds <= 0f) return Vector3.zero;
            var turn = new AbilitySweepTurn(From.SweepPose, To.SweepPose);
            if (To.Prism != null) return From.Prism.VelocityTo(To.Prism, turn, point, fraction) / seconds;
            if (To.IsBox)
            {
                Vector3 half = Vector3.Lerp(From.HalfExtents, To.HalfExtents, fraction),
                    offset = point - Vector3.Lerp(From.A, To.A, fraction);
                for (int i = 0; i < 3; i++)
                    offset[i] = half[i] > .000001f ? Mathf.Clamp(offset[i] / half[i], -1f, 1f) : 0;
                return (To.A - From.A + Vector3.Scale(To.HalfExtents - From.HalfExtents, offset)) / seconds;
            }

            Vector3 a = turn.Point(From.A, To.A, fraction), axis = turn.Point(From.B, To.B, fraction) - a;
            float along = axis.sqrMagnitude > 1e-10f
                ? Mathf.Clamp01(Vector3.Dot(point - a, axis) / axis.sqrMagnitude)
                : 0f;
            return (Vector3.Lerp(turn.Velocity(From.A, To.A, fraction), turn.Velocity(From.B, To.B, fraction), along) + outward * (To.Radius - From.Radius)) / seconds;
        }
    }

    /// <summary>候选共同接触；同一时点先拦截，再按稳定来源及部位确认人物受击。</summary>
    private readonly struct ContactChoice
    {
        /// <summary>双方身体关系序号，负一表示普通接触。</summary>
        internal readonly int BodyRelation;

        /// <summary>携带身体对真实环境的首次接触，其他候选为空。</summary>
        internal readonly BodySurfaceHit? Carrying;

        /// <summary>离手后的实际身体，其他接触为空。</summary>
        internal readonly ConstructedActorImpulse.BodyFlight Flight;

        /// <summary>双方关系实际接触时的目标表面，普通命中为空。</summary>
        internal readonly BodySurfacePose BodySurface;

        /// <summary>实际三角面上的连接位置。</summary>
        internal readonly BodySurfaceAnchor BodyAnchor;

        /// <summary>实际结霜部位所用的原模型，没有结霜规则时为空。</summary>
        internal readonly BodySurfaceBinding FrostBinding;

        /// <summary>实际接触阶段的薄霜规则，跨越招式阶段边界时仍用这次接触的资料。</summary>
        internal readonly CompiledFrostCoating FrostRule;

        /// <summary>左侧实际作用形状。</summary>
        internal readonly ShapeMotion Left;

        /// <summary>人物接触的实际对象。</summary>
        internal readonly ActorMotion Actor;

        /// <summary>世界接触的实际地块，其他接触为空。</summary>
        internal readonly WorldTile Tile;

        /// <summary>实际世界操作序号。</summary>
        internal readonly int WorldEffect;

        /// <summary>形体交锋的右侧，人物接触为空。</summary>
        internal readonly ShapeMotion? Right;

        /// <summary>非零表示右侧是真实防御面，方向按接触姿态取样。</summary>
        internal readonly Vector3 BodyGuardFacing;

        /// <summary>明确水火反应优先于同一时点的人物受击。</summary>
        internal readonly bool Quenching;

        /// <summary>明确的点火或燃烧木体冷却，不替换普通交锋。</summary>
        internal readonly ThermalReaction Reaction;

        /// <summary>独立世界主体是本次热源，左侧活动主体接收热量或提供水。</summary>
        internal readonly bool WorldSource;

        /// <summary>独立世界水体编号；活动水体为零。</summary>
        internal readonly long WorldRecord;

        /// <summary>独立水体的实际格子或刚性形状。</summary>
        internal readonly AbilityCapsule WorldShape;

        /// <summary>共同步的首次进度。</summary>
        internal readonly float Fraction;

        /// <summary>共同预测的时长，用于实际表面速度。</summary>
        internal readonly float Seconds;

        /// <summary>实际表面接触点及方向。</summary>
        internal readonly Vector3 Point, Normal;

        /// <summary>双方射线到共同接触面的整体长度，非射线为负。</summary>
        internal readonly float LeftRayLength, RightRayLength;

        /// <summary>保存本次真实接触。</summary>
        /// <param name="left">左作用形状。</param>
        /// <param name="actor">实际人物。</param>
        /// <param name="right">右形状。</param>
        /// <param name="fraction">首次相交进度。</param>
        /// <param name="point">作用面位置。</param>
        /// <param name="normal">作用方向。</param>
        /// <param name="seconds">共同预测时长。</param>
        /// <param name="quenching">是否为优先确认的水火反应。</param>
        /// <param name="worldRecord">独立材料稳定编号，活动水体为零。</param>
        /// <param name="worldShape">独立水体的真实格子或形状。</param>
        /// <param name="reaction">已定义的有限材料反应。</param>
        /// <param name="worldSource">世界主体是否为热源。</param>
        /// <param name="tile">真实地块接触。</param><param name="worldEffect">本作用面的世界操作序号。</param>
        /// <param name="bodyRelation">双方关系序号，负一表示普通命中。</param>
        /// <param name="bodySurface">接触时共同目标表面，普通命中为空。</param>
        /// <param name="bodyAnchor">目标原三角面内的位置。</param>
        /// <param name="carrying">真实携带环境接触。</param>
        /// <param name="flight">真实离手身体。</param>
        /// <param name="actorShape">同一接触时点的真实人物作用体。</param>
        /// <param name="frostBinding">实际结霜模型。</param>
        /// <param name="frostRule">实际接触的薄霜规则。</param>
        internal ContactChoice(ShapeMotion left, ActorMotion actor, ShapeMotion? right, float fraction, Vector3 point,
            Vector3 normal, float seconds, bool quenching = false, long worldRecord = 0,
            AbilityCapsule worldShape = default, ThermalReaction reaction = ThermalReaction.None,
            bool worldSource = false, WorldTile tile = null, int worldEffect = 0, int bodyRelation = -1,
            BodySurfacePose bodySurface = null, BodySurfaceAnchor bodyAnchor = default, BodySurfaceHit? carrying = null, ConstructedActorImpulse.BodyFlight flight = null,
            AbilityCapsule actorShape = default, Vector3 bodyGuardFacing = default, BodySurfaceBinding frostBinding = null,
            CompiledFrostCoating frostRule = null)
        {
            BodyGuardFacing = bodyGuardFacing;
            BodyRelation = bodyRelation;
            Carrying = carrying;
            Flight = flight;
            BodySurface = bodySurface;
            BodyAnchor = bodyAnchor;
            FrostBinding = frostBinding;
            FrostRule = frostRule;
            Tile = tile;
            WorldEffect = worldEffect;
            Quenching = quenching;
            Reaction = reaction;
            WorldSource = worldSource;
            WorldRecord = worldRecord;
            WorldShape = worldShape;
            if (right.HasValue && bodyGuardFacing == Vector3.zero && CompareShape(left, right.Value) > 0)
            {
                Left = right.Value;
                Right = left;
                Normal = -normal;
            }
            else
            {
                Left = left;
                Right = right;
                Normal = normal;
            }

            Actor = actor;
            Fraction = fraction;
            Point = point;
            Seconds = seconds;
            LeftRayLength = RightRayLength = -1f;
            if (carrying.HasValue) return;
            bool leftRay = Left.Running.Cast.IsRay(Left.To.Node),
                rightRay = Right.HasValue && Right.Value.Running.Cast.IsRay(Right.Value.To.Node);
            if (!leftRay && !rightRay) return;
            AbilityCapsule leftShape = Slice(Left.From, Left.To, fraction);
            Vector3 actualPoint = Point, actualNormal = Normal;
            if (leftRay)
            {
                if (Right.HasValue)
                    LeftRayLength = RayContact(leftShape, Slice(Right.Value.From, Right.Value.To, fraction), ref actualPoint, ref actualNormal);
                else if (actor.Id != 0 || worldRecord > 0)
                    LeftRayLength = RayContact(leftShape, actor.Id != 0 ? actorShape : worldShape, ref actualPoint, ref actualNormal);
                else LeftRayLength = AbilityPairSweep.RayLengthAt(leftShape, actualPoint, actualNormal);
                if (rightRay)
                    RightRayLength = AbilityPairSweep.RayLengthAt(Slice(Right.Value.From, Right.Value.To, fraction), actualPoint, -actualNormal);
            }
            else
            {
                actualNormal = -actualNormal;
                RightRayLength = RayContact(Slice(Right.Value.From, Right.Value.To, fraction), leftShape, ref actualPoint, ref actualNormal);
                actualNormal = -actualNormal;
            }
            Point = actualPoint; Normal = actualNormal;
        }

        /// <summary>共同时间及稳定来源排序，施放列表顺序不决定谁先造成伤害。</summary>
        /// <param name="a">左候选。</param>
        /// <param name="b">右候选。</param>
        internal static int Compare(in ContactChoice a, in ContactChoice b)
        {
            int order = a.Fraction.CompareTo(b.Fraction);
            if (order != 0) return order;
            order = b.Carrying.HasValue.CompareTo(a.Carrying.HasValue);
            if (order != 0) return order;
            order = (b.Flight != null).CompareTo(a.Flight != null);
            if (order != 0) return order;
            if (a.Flight != null) return a.Flight.Motion.ActorId.CompareTo(b.Flight.Motion.ActorId);
            int aThermal = a.Quenching || a.Reaction == ThermalReaction.CoolWood ? 3 :
                a.Reaction == ThermalReaction.Ignite ? 2 : 0;
            int bThermal = b.Quenching || b.Reaction == ThermalReaction.CoolWood ? 3 :
                b.Reaction == ThermalReaction.Ignite ? 2 : 0;
            order = bThermal.CompareTo(aThermal);
            if (order != 0) return order;
            order = b.Right.HasValue.CompareTo(a.Right.HasValue);
            if (order != 0) return order;
            order = CompareShape(a.Left, b.Left);
            if (order != 0) return order;
            order = a.BodyRelation.CompareTo(b.BodyRelation);
            if (order != 0) return order;
            if (a.WorldRecord > 0 || b.WorldRecord > 0)
            {
                order = a.WorldRecord.CompareTo(b.WorldRecord);
                return order != 0 ? order : a.WorldShape.Shape.CompareTo(b.WorldShape.Shape);
            }

            if (a.Tile != null || b.Tile != null)
            {
                order = (a.Tile != null).CompareTo(b.Tile != null);
                if (order != 0) return order;
                order = a.Tile.pos.y.CompareTo(b.Tile.pos.y);
                if (order != 0) return order;
                order = a.Tile.pos.x.CompareTo(b.Tile.pos.x);
                return order != 0 ? order : a.WorldEffect.CompareTo(b.WorldEffect);
            }

            return a.Right.HasValue ? CompareShape(a.Right.Value, b.Right.Value) : a.Actor.Id.CompareTo(b.Actor.Id);
        }

        /// <summary>形体双方均使用来源、构造、施放和部位的稳定顺序，不受查询插入顺序影响。</summary>
        /// <param name="a">左形状。</param>
        /// <param name="b">右形状。</param>
        private static int CompareShape(in ShapeMotion a, in ShapeMotion b)
        {
            int order = a.Running.SourceId.CompareTo(b.Running.SourceId);
            if (order != 0) return order;
            order = StringComparer.Ordinal.Compare(a.Running.Cast.Plan.Id, b.Running.Cast.Plan.Id);
            if (order != 0) return order;
            order = a.Running.Id.CompareTo(b.Running.Id);
            if (order != 0) return order;
            order = a.To.Node.CompareTo(b.To.Node);
            return order != 0 ? order : a.To.Shape.CompareTo(b.To.Shape);
        }
    }
}
