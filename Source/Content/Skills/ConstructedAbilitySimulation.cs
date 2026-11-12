using System;
using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Content.KnightCombat;
using Cultiway.Core.Performance;
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
                CommitBodyFlights(clock);
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
                    if (running.WorldScope != null)
                        ValidateSource(running);
                for (int i = 0; i < active.Count; i++)
                {
                    PublishActionEvents(active[i]);
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
                    if (running.WorldScope != null && running.Cast.Phase == AbilityCastPhase.Interrupted) continue;
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

            running.Cast.AppendVisuals(visuals);
            if (!running.BodyControlled) continue;
            BodyPose pose = running.Cast.BodyPose.Value;
            if (running.Cast.Plan.Body.ControlsStance)
            {
                running.Source.lookTowardsPosition(running.Source.current_position + (Vector2)pose.Forward);
            }

            running.Body.AppendVisuals(running.Source, in pose, visuals, terrain);
        }

        AppendSteamVisuals(now);
        ConfirmedWorldTime = now;
        PublishPresentation(now);
        if (casts.Count == 0 && !HasBodyFlights) ResetSimulation();
    }

    /// <summary>人物失效或身体被真正中断时解除对应控制，不让池中重用对象继续施法。</summary>
    /// <param name="running">本次施放。</param>
    private static void ValidateSource(RunningCast running)
    {
        // 已确认的自然结束不再被来源后来失效改写成中断。
        if (running.Cast.Finished) return;
        if (running.WorldScope != null && !running.WorldScope.IsValid())
        {
            running.Cast.Cancel();
            ReleaseBody(running);
            return;
        }

        bool sourceLive = running.InitialOwner.IsLive;
        running.InitialOwner.RefreshRelations();
        if (!sourceLive)
        {
            running.Cast.InterruptBody();
            ReleaseBody(running);
        }

        if (!sourceLive || running.HasTransfers)
        {
            for (int i = 0; i < running.Owners.Length; i++)
            {
                CastAuthority owner = running.Owner(i);
                owner.RefreshRelations();
                if (!owner.IsLive && !running.Cast.CanContinueWithoutSource(i))
                    running.Cast.EndNode(i, true);
            }

            if (running.Cast.Finished) running.Cast.Cancel();
        }

        if (!sourceLive) return;
        if (running.BodyControlled && (!running.Body.IsValid(running.Source) || running.Source.isFlying() ||
                                       running.Movement?.OwnsPosition == false ||
                                       running.Source.stats.hasTag("frozen_ai") ||
                                       running.Source.stats.hasTag("immovable")))
        {
            running.Cast.InterruptBody();
            ReleaseBody(running);
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
    }

    /// <summary>保存前态，再用同一段时间预测所有施放；不在预测阶段造成伤害。</summary>
    /// <param name="clock">共同起点。</param>
    /// <param name="step">共同步长。</param>
    private void Predict(double clock, float step)
    {
        shapes.Clear();
        shapeIndex.Clear();
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
            running.Cast.SaveStep();
            Collect(running, running.Before, false);
            RefreshReentry(running, clock);
        }

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
                if (!continuous) from = shape;
                var motion = new ShapeMotion(running, from, shape, continuous);
                shapes.Add(motion);
                shapeIndex.Add(motion.Bounds);
            }
        }
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
                !changedActors.ContainsKey(running.TargetId) &&
                !(running.WeaponConstraint?.Active == true &&
                  changedActors.ContainsKey(running.WeaponConstraint.Owner.SourceId))) continue;
            Vector3 foot = running.LastSourceFoot;
            float height = running.LastSourceHeight;
            if (running.InitialOwner.IsLive && actorIndices.TryGetValue(running.SourceId, out int sourceIndex))
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

            BodySupportPose? support = running.Movement != null && running.BodyControlled
                ? running.Movement.Sample((float)(time - running.StartedAt), height, terrain).Support
                : null;
            BodyFrame? body = running.BodyControlled ? running.Body.Frame(foot, height, support) : null;
            bool followsWeapon = body.HasValue && running.WeaponConstraint?.Active == true &&
                                 running.WeaponConstraint.Relation.Kind == BodyRelationKind.Deflect;
            if (body.HasValue && !followsWeapon) body = ApplyBodyConnection(running, body.Value, time);
            var input = new AbilityInputs(foot, running.LastTargetFoot, running.Direction, height);
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
        CompiledBodyAction body = running.Cast.Plan.Body;
        if (running.BodyControlled && running.Cast.Age >= body.ActiveFrom && running.Cast.Age <= body.ActiveUntil)
        {
            int first = output.Count;
            running.Body.AppendContactShapes(running.Cast.BodyPose.Value, output, includeEnd);
            for (int i = first; i < output.Count; i++)
            {
                AbilityCapsule shape = output[i];
                output[i] = new AbilityCapsule(shape.Node, shape.A, shape.B, shape.Radius, shape.Matter, shape.Shape,
                    running.Cast.BodyContactRevision);
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
                          candidate.Left.Running.Cast.Plan.Body.Construction.Relations[candidate.BodyRelation].Kind is BodyRelationKind.Brace or BodyRelationKind.Deflect &&
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
            if (running.Cast.Plan.HasWorldEffects) FindWorldContacts(shape, step, Include);
            bool powered = shape.To.Node < 0 || running.Cast.ContactSweepForce(shape.To.Node) > 0f;
            bool fire = powered && shape.To.Material?.Id == "fire" && shape.To.Material.State == "energy";
            bool ordinary = powered && running.Cast.Role(shape.To.Node) != ConstructionContactRole.Decoration;
            bool thermalSource = fire || shape.To.Node >= 0 && running.Cast.Burning(shape.To.Node).Heat > 0;
            if (shape.To.Node >= 0 && (ordinary || thermalSource))
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
                         right.Running.Cast.Role(right.To.Node) == ConstructionContactRole.Barrier ||
                         !owner.IsHostileTo(right.Running.Owner(right.To.Node)) &&
                         !right.Running.Owner(right.To.Node).IsHostileTo(owner))) continue;
                    bool continuous = shape.Continuous && right.Continuous &&
                                      (reaction != ThermalReaction.Ignite || running.Cast.StartHeat(shape.To.Node) > 0);
                    AbilityCapsule leftFrom = continuous ? shape.From : shape.To,
                        rightFrom = continuous ? right.From : right.To;
                    if (AbilityPairSweep.Find(leftFrom, shape.To, rightFrom, right.To, out float at, out Vector3 point,
                            out Vector3 normal))
                        Include(new ContactChoice(shape, default, right, continuous ? at : 1f, point, normal, step,
                            quench, reaction: reaction));
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

            if (!powered || running.Cast.Role(shape.To.Node) != ConstructionContactRole.Attack) continue;
            // 已被真实器物接触抵住的兵器不能仅因进入伤害时段而穿透补伤；独立法术和其他肢体继续运行。
            if (shape.To.Node < 0 && (running.WeaponConstraint?.Active == true ||
                                    running.Connection?.Active == true && running.Connection.WeaponTarget != null)) continue;
            actorIndex.Query(shape.Bounds, nearby);
            // 双方关系只查询已选目标；长尾等真实表面可能超出原版人物粗筛球。
            if (shape.To.Node < 0 && running.Cast.Plan.Body.Construction.HasControlRelations &&
                actorIndices.TryGetValue(running.TargetId, out int selected) && !nearby.Contains(selected))
                nearby.Add(selected);
            foreach (int id in nearby)
            {
                ActorMotion actor = actors[id];
                if (actor.Actor.data?.id != actor.Id || !actor.Actor.isAlive() || !actor.Actor.hasHealth() ||
                    !owner.Matches(running.Cast.Impact(shape.To.Node).Targets, actor.Actor)) continue;
                Sample(actor, clock, out Vector3 fromFoot, out float fromHeight);
                Sample(actor, clock + step, out Vector3 toFoot, out float toHeight);
                var b0 = ActorContact(fromFoot, fromHeight,
                    actor.Body?.BodyControlled == true ? actor.Body.BeforeTorso : Vector3.zero);
                var b1 = ActorContact(toFoot, toHeight,
                    actor.Body?.BodyControlled == true ? actor.Body.Cast.BodyPose.Value.Torso : Vector3.zero);
                if (shape.To.Node < 0)
                {
                    FindBodyConnections(shape, actor, clock, step, Include);
                    if (!running.Cast.Plan.Body.Construction.HasTouchRelations) continue;
                }

                float earliest = ContactFraction(shape, actor, b0, b1, step);
                if (earliest > 1f) continue;
                if (!shape.Continuous) earliest = 1f;
                AbilityCapsule a0 = Slice(shape.From, shape.To, earliest), target0 = Slice(b0, b1, earliest);
                if (AbilityPairSweep.Find(a0, shape.To, target0, b1, out float at, out Vector3 point,
                        out Vector3 normal))
                    Include(new ContactChoice(shape, actor, null, Mathf.Lerp(earliest, 1f, at), point, normal, step));
            }
        }

        FindCarryingContacts(clock, step, Include);
        FindBodyFlightContacts(clock, step, Include);
        if (WorldMaterialService.HasHeat) FindWorldHeatSources(step, Include);
        return first;

        void Include(ContactChoice value)
        {
            if (value.Carrying.HasValue)
            {
                if (!first.HasValue || ContactChoice.Compare(value, first.Value) < 0) first = value;
                return;
            }
            if (value.Fraction > value.Left.To.Until ||
                value.Right.HasValue && value.Fraction > value.Right.Value.To.Until) return;
            if (!value.Left.Running.Cast.CanSweepAt(value.Left.To.Node, value.Fraction) ||
                value.Right.HasValue &&
                !value.Right.Value.Running.Cast.CanSweepAt(value.Right.Value.To.Node, value.Fraction)) return;
            if (!first.HasValue || ContactChoice.Compare(value, first.Value) < 0) first = value;
        }
    }

    /// <summary>只在回到真实接触时点后修改状态及游戏，随后重新查询余段。</summary>
    /// <param name="hit">共同时间最早的真实接触。</param>
    private static void Resolve(in ContactChoice hit)
    {
        if (hit.Flight != null)
        {
            ConstructedActorImpulse.ContactBody(hit.Flight, hit.Carrying.Value, simulation.contactTime);
            return;
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
        if (hit.Right.HasValue)
        {
            if (TryReflect(in hit)) return;
            ShapeMotion right = hit.Right.Value;
            float absorbed = Mathf.Min(running.Cast.RemainingForce(node),
                right.Running.Cast.RemainingForce(right.To.Node));
            running.Cast.ConfirmClash(node, hit.Point, absorbed);
            right.Running.Cast.ConfirmClash(right.To.Node, hit.Point, absorbed);
            running.Cast.ConsumeForce(node, absorbed);
            right.Running.Cast.ConsumeForce(right.To.Node, absorbed);
            ClashConfirmed?.Invoke(new ConstructedClash(running.Id, right.Running.Id, running.Owner(node).Id,
                right.Running.Owner(right.To.Node).Id,
                node, right.To.Node, hit.Point, hit.Normal, running.Cast.Age, right.Running.Cast.Age, absorbed,
                running.Cast.RemainingForce(node), right.Running.Cast.RemainingForce(right.To.Node)));
            return;
        }

        ActorMotion actor = hit.Actor;
        if (actor.Actor.data?.id != actor.Id || !actor.Actor.isAlive() || !actor.Actor.hasHealth()) return;
        ContactMemory memory = RememberContact(running, node, actor.Id);
        AbilityMatter beforeMatter = node < 0 ? null : running.Cast.Matter(node);
        CompiledConstructionImpact impact = running.Cast.Impact(node);
        // 明确发力继承接触前的运动；原版同次命中附带的击退不再重复计入。
        Vector3? priorVelocity = impact.Impulse.HasValue ? ConstructedActorImpulse.Velocity(actor.Actor) : null;
        ForceReceiver? forceReceiver = impact.Impulse.HasValue ? new ForceReceiver(actor.Actor) : null;
        ApplyAttackState result = ApplyAttackState.Hit;
        if (node < 0 && impact.DamageScale > 0f)
        {
            var owner = running.Source.GetExtend();
            AttackDataResult strike = running.Body.Weapon == null
                ? KnightWeaponStrikeResolver.StrikeUnarmed(owner, actor.Actor, null, running.Power * impact.DamageScale)
                : KnightWeaponStrikeResolver.StrikeBorrowed(owner, running.Body.Weapon, actor.Actor,
                    running.Power * impact.DamageScale);
            result = strike.state;
        }
        else if (node >= 0 && impact.DamageScale > 0f)
        {
            var damage = running.Owner(node).Damage;
            damage.TargetID = actor.Id;
            damage.Damage = running.Cast.RemainingForce(node) * impact.DamageScale;
            damage.Element = Composition(beforeMatter.DamageKind);
            GetHitEventSystem.Resolve(damage);
        }

        ConstructedTargetResponse response = ApplyTargetResponse(running, node, actor.Actor, actor.Id, impact, result,
            priorVelocity: priorVelocity, forceReceiver: forceReceiver);
        running.Cast.ConfirmContact(node, 1f, hit.Point, actor.Id);
        EndExhaustedContactGroup(running, node);
        AbilityMatter afterMatter = node < 0 ? null : running.Cast.Matter(node);
        ContactConfirmed?.Invoke(new ConstructedContact(running.Id, running.Cast.Plan.Id, running.Owner(node).Id,
            actor.Id,
            node, hit.Point, hit.Normal, running.Cast.Age, result, running.SourceId, running.Cast.Reflections(node),
            memory.Count, running.GroupHits[running.Cast.ContactGroup(node)], beforeMatter?.Id, beforeMatter?.State,
            afterMatter?.Id, afterMatter?.State,
            impact.DamageScale > 0f, response.VelocityChange, response.ForceApplied, response.StaggerSeconds,
            response.BindSeconds, response.StrainSeconds, response.ExposureSeconds));
    }

    /// <summary>直接接触和真实水路等传播都从这一入口应用目标响应。</summary>
    /// <param name="running">真实施放。</param><param name="node">作用部位。</param><param name="target">实际目标。</param>
    /// <param name="targetId">伤害回调之前确认的人物编号。</param>
    /// <param name="impact">已编译作用。</param><param name="result">真实攻击结果。</param>
    /// <param name="origin">传播时的真实作用面中心，普通接触读取当前部位。</param>
    /// <param name="priorVelocity">直接接触前已有的运动，不包含同次原版自动击退。</param>
    /// <param name="forceReceiver">同次伤害或停顿解除动作之前的承力身体。</param>
    private static ConstructedTargetResponse ApplyTargetResponse(RunningCast running, int node, Actor target,
        long targetId,
        CompiledConstructionImpact impact, ApplyAttackState result, Vector3? origin = null,
        Vector3? priorVelocity = null, ForceReceiver? forceReceiver = null)
    {
        if (target.data?.id != targetId || !target.isAlive() || !target.hasHealth())
            return new ConstructedTargetResponse(targetId, Vector3.zero, false, 0f, 0f);
        ApplyContactReactions(target, impact, running.Owner(node), result, out float stagger, out float bound,
            out float strain, out float exposure);
        Vector3 velocity = ApplyContactForce(running, node, target, impact, result, out bool force, origin,
            priorVelocity, forceReceiver);
        if (force) InterruptContactBody(target);
        return new ConstructedTargetResponse(targetId, velocity, force, stagger, bound, strain, exposure);
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
        InterruptContactBody(target);
    }

    /// <summary>受击后立即解除目标身体控制，已经离手的法术继续维护自身过程。</summary>
    /// <param name="target">实际受击目标。</param>
    private static void InterruptContactBody(Actor target)
    {
        InterruptBodyConnections(target);
        foreach (RunningCast running in casts)
            if (running.SourceId == target.id && running.BodyControlled)
            {
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
    private static Vector3 ApplyContactForce(RunningCast running, int node, Actor target,
        CompiledConstructionImpact impact, ApplyAttackState result, out bool applied, Vector3? origin = null,
        Vector3? priorVelocity = null, ForceReceiver? forceReceiver = null)
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
            Vector3 local = impact.Impulse.Value;
            Vector3 change = new(direction.x * local.x - direction.y * local.y,
                direction.y * local.x + direction.x * local.y, local.z);
            ForceReceiver receiver = forceReceiver ?? new ForceReceiver(target);
            return impact.ForceRouting != null || receiver.Response != null
                ? ApplyRoutedForce(running, node, receiver, impact, change, priorVelocity, out applied)
                : DeliverForce(owner, target, change, impact.SlideSeconds, priorVelocity, out applied, out _);
        }

        if (target.isImmovable()) return Vector3.zero;
        applied = target.GetExtend().TryGetForce(owner.IsLive ? owner.Actor : null, direction.x * impact.Force,
            direction.y * impact.Force, impact.SlideSeconds > 0f ? 0f : impact.Lift,
            owner.Damage.AttackerPowerLevel);
        if (applied)
        {
            Cultiway.Content.Persistence.ActorPositionSaveService.Remember(target.GetExtend());
            if (impact.SlideSeconds > 0f)
                ConstructedActorImpulse.BeginSlide(target, impact.SlideSeconds, simulation.terrain);
            else ConstructedActorImpulse.Begin(target, simulation.terrain);
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
            if (To.IsBox)
            {
                Vector3 half = Vector3.Lerp(From.HalfExtents, To.HalfExtents, fraction),
                    offset = point - Vector3.Lerp(From.A, To.A, fraction);
                for (int i = 0; i < 3; i++)
                    offset[i] = half[i] > .000001f ? Mathf.Clamp(offset[i] / half[i], -1f, 1f) : 0;
                return (To.A - From.A + Vector3.Scale(To.HalfExtents - From.HalfExtents, offset)) / seconds;
            }

            Vector3 a = Vector3.Lerp(From.A, To.A, fraction), axis = Vector3.Lerp(From.B, To.B, fraction) - a;
            float along = axis.sqrMagnitude > 1e-10f
                ? Mathf.Clamp01(Vector3.Dot(point - a, axis) / axis.sqrMagnitude)
                : 0f;
            return (Vector3.Lerp(To.A - From.A, To.B - From.B, along) + outward * (To.Radius - From.Radius)) / seconds;
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
        internal ContactChoice(ShapeMotion left, ActorMotion actor, ShapeMotion? right, float fraction, Vector3 point,
            Vector3 normal, float seconds, bool quenching = false, long worldRecord = 0,
            AbilityCapsule worldShape = default, ThermalReaction reaction = ThermalReaction.None,
            bool worldSource = false, WorldTile tile = null, int worldEffect = 0, int bodyRelation = -1,
            BodySurfacePose bodySurface = null, BodySurfaceAnchor bodyAnchor = default, BodySurfaceHit? carrying = null, ConstructedActorImpulse.BodyFlight flight = null)
        {
            BodyRelation = bodyRelation;
            Carrying = carrying;
            Flight = flight;
            BodySurface = bodySurface;
            BodyAnchor = bodyAnchor;
            Tile = tile;
            WorldEffect = worldEffect;
            Quenching = quenching;
            Reaction = reaction;
            WorldSource = worldSource;
            WorldRecord = worldRecord;
            WorldShape = worldShape;
            if (right.HasValue && CompareShape(left, right.Value) > 0)
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
