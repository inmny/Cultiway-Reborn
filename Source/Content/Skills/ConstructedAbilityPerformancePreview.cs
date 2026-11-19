using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Combat;
using Cultiway.Core;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.Visuals.Forms;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>列出真实可选的预览人物。距离在确认坐标时检查，仍可解释超出距离。</summary>
    /// <param name="actor">选择的实际施法者。</param><param name="plan">已编译规则。</param>
    /// <param name="target">候选人物。</param>
    internal static bool PreviewTargetMatches(ActorExtend actor, CompiledAbility plan, Actor target) =>
        MatchesUseActor(actor, plan, target, null) &&
        (plan.Body?.Construction.HasControlRelations != true || actor.Base != target);

    /// <summary>使用地图现有的输入创建隔离的播放实例，不取得身体、法宝、材料或资源的控制。</summary>
    internal static string TryBuildPreview(Actor actor, CompiledAbility plan, Actor target, Vector3 location,
        out PerformancePreview preview, out float? range)
    {
        preview = null;
        range = null;
        if (actor?.data == null || !actor.isAlive() || !actor.hasHealth()) return "PreviewNeedsLearner";
        ActorExtend owner = actor.GetExtend();
        bool needsActor = plan.Use.Target == ActiveAbilityTargetMode.Object || plan.Body?.Construction.HasControlRelations == true ||
                          plan.Nodes.Any(node => node.Correspondence?.Target == ConstructionCorrespondenceTarget.Target);
        if (target == null && needsActor && plan.Use.Target != ActiveAbilityTargetMode.Self) return "PreviewNeedsTargetActor";
        if (plan.Use.Target is ActiveAbilityTargetMode.Object or ActiveAbilityTargetMode.ObjectOrPoint &&
            target != null && !PreviewTargetMatches(owner, plan, target)) return "PreviewWrongTargetActor";
        if (!TryResolveUseTarget(owner, plan, new ActiveAbilityTarget(target, location), out target, out location, out float targetHeight))
            return "PreviewInvalidCoordinates";
        if (plan.Body?.Construction.HasControlRelations == true && (target == null || target == actor))
            return "PreviewWrongTargetActor";
        if (plan.Body != null)
        {
            string problem = BodyActionBinding.InputProblem(owner, plan.Body);
            if (problem != null) return problem;
            if (!BodyAvailable(owner, plan.Body)) return "PreviewBodyUnavailable";
        }

        var carrier = default(Friflo.Engine.ECS.Entity);
        Matrix4x4? carrierPose = null;
        if (plan.Binding?.Anchor == AbilityAnchor.Carrier)
        {
            carrier = ConstructedArtifactCarrier.Equipped(owner).FirstOrDefault(item => ConstructedArtifactCarrier.CanBorrow(owner, item));
            if (carrier.IsNull || !ArtifactAppearanceRenderer.TryGetSpatialPose(carrier, out var displayed)) return "PreviewNeedsCarrier";
            carrierPose = CarrierFrame(displayed);
        }
        range = Range(owner, plan, target, carrier);
        Vector3 source = SpatialActorPose.Foot(actor);
        if (Vector2.Distance(source, location) > range.Value) return "PreviewOutsideRange";

        float height = SpatialActorPose.Height(actor);
        float margin = 20f + Mathf.Max((plan.Body?.Construction.FootworkTravel ?? 0f) * height,
            plan.EventOperations.Where(operation => operation.Propagation != null)
                .Select(operation => operation.Propagation.Range * plan.Nodes[operation.Node].Scale).DefaultIfEmpty(0f).Max());
        Rect area = Rect.MinMaxRect(Mathf.Min(source.x, location.x) - margin, Mathf.Min(source.y, location.y) - margin,
            Mathf.Max(source.x, location.x) + margin, Mathf.Max(source.y, location.y) + margin);
        AbilityHeightField terrain = GroundProvider?.Invoke(area);
        source = WorldFoot(actor, source, terrain);
        if (target != null) location = WorldFoot(target, SpatialActorPose.Foot(target), terrain);
        else location.z += terrain?.Sample(location.x, location.y) ?? 0f;
        Vector3 forward = location - source;
        forward.z = 0f;
        forward = forward.sqrMagnitude > 1e-8f ? forward.normalized : Vector3.right;
        BodyActionBinding binding = null;
        BodyLocomotion movement = null;
        BodyFrame? body = null;
        if (plan.Body != null)
        {
            binding = BodyActionBinding.BindBody(actor, plan.Body, forward, location, targetHeight, out string problem);
            if (problem != null) return problem;
            binding.WeaponInConstruction = plan.UsesHeldWeapon;
            body = binding.Frame(source, height);
            if (!binding.CanStart(body.Value)) return "PreviewBodyStance";
            if (plan.Body.ControlsStance && (!plan.Body.Part.HasPartRotation || plan.Body.HasNamedSupportMotion))
            {
                movement = new BodyLocomotion(actor, plan.Body, body.Value, terrain, binding.Joint);
                if (!movement.CanStart(terrain)) return "PreviewBodyStance";
                BodyLocomotion.Pose pose = movement.Sample(0f, height, terrain);
                body = binding.Frame(pose.Root, height, pose.Support);
            }
        }
        float force = CombatDamageEffects.PrepareDamage(actor,
            actor.stats["damage"] * plan.Power, ElementComposition.Static.empty).Damage;
        var inputs = new AbilityInputs(source, location, forward, height, carrierPose);
        var cast = new AbilityCast(plan, Matrix4x4.TRS(location, Quaternion.identity, Vector3.one), body, inputs,
            force, terrain, ground: plan.HasPropagation ? PropagationGroundMap.Capture(terrain) : null);
        preview = new PerformancePreview(actor, cast, source, location, height, inputs, terrain, binding, movement);
        return null;
    }

    /// <summary>保持选定时的现场，播放共同数值动作；未确认的人物或世界作用不提交。</summary>
    internal sealed class PerformancePreview
    {
        internal readonly AbilityCast Cast;
        internal readonly Vector3 Source, Target;
        private readonly Actor actor;
        private readonly float height;
        private readonly AbilityInputs initial;
        private readonly AbilityHeightField terrain;
        private readonly BodyActionBinding binding;
        private readonly BodyLocomotion movement;

        internal PerformancePreview(Actor actor, AbilityCast cast, Vector3 source, Vector3 target, float height,
            AbilityInputs initial, AbilityHeightField terrain, BodyActionBinding binding, BodyLocomotion movement)
        {
            this.actor = actor; Cast = cast; Source = source; Target = target; this.height = height;
            this.initial = initial; this.terrain = terrain; this.binding = binding; this.movement = movement;
        }

        internal void Advance(float elapsed)
        {
            // 含身体动作时使用同一正式取样，按实际数值步边界更新支撑和部位连接。
            while (elapsed > 0f && !Cast.Finished)
            {
                float step = Cast.NextStep(elapsed);
                if (step <= 0f) break;
                if (movement != null)
                {
                    BodyLocomotion.Pose pose = movement.Commit(Cast.Age + step, height, terrain);
                    Cast.UpdateBody(binding.Frame(pose.Root, height, pose.Support));
                    Cast.UpdateInputs(new AbilityInputs(pose.Root, Target, initial.Forward, height, initial.Carrier));
                }
                Cast.Advance(step, Target);
                elapsed -= step;
            }
        }

        internal void Sample(List<FormVisualInstance> visuals, List<AbilityCapsule> contacts)
        {
            Cast.AppendVisuals(visuals);
            Cast.AppendContacts(contacts);
            if (Cast.Finished || binding == null || !Cast.BodyPose.HasValue) return;
            var pose = Cast.BodyPose.Value;
            binding.WeaponReleased = Cast.Plan.UsesHeldWeapon && !Cast.IsInputAttached;
            if (actor.data != null && actor.isAlive()) binding.AppendVisuals(actor, in pose, visuals, terrain);
            binding.AppendContactShapes(in pose, contacts, false);
        }
    }
}
