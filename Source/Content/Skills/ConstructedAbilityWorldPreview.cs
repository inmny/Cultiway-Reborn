using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Combat;
using Cultiway.Core;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>独立预估得到的真实地块足迹；不包含任何已经发生收益的承诺。</summary>
internal sealed class ConstructedWorldPreview
{
    /// <summary>每种实际原理可能接触的不同地块，已经遵守共同频率和次数。</summary>
    internal readonly Dictionary<string, HashSet<int>> Tiles = new(StringComparer.Ordinal);

    /// <summary>全部原理的不同地块足迹。</summary>
    internal int[] AllTiles => Tiles.Values.SelectMany(tiles => tiles).Distinct().OrderBy(id => id).ToArray();
}

/// <summary>城市评价使用普通施放的数值、扫掠和次数，不在预估中调用世界修改。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>识别正式已学的纯世界用途，保留真实来源资源和共享计划。</summary>
    /// <param name="caster">持有者。</param><param name="ability">正式句柄。</param>
    /// <param name="plan">已经登记的计划。</param><param name="cost">真实单次费用。</param>
    internal static bool TryWorldProjectAbility(ActorExtend caster, ActiveAbilityHandle ability,
        out CompiledAbility plan, out float cost)
    {
        plan = null;
        cost = 0;
        if (ability.ProviderId != ProviderId || !definitions.TryGetValue(ability.EntryId, out var definition) ||
            definition.Power != 0 || !ActiveAbilityService.CanPrepare(caster, ability, null)) return false;
        plan = PlanFor(ability.EntryId);
        cost = definition.Cost;
        return ProjectBodySupported(plan) && plan.HasWorldEffects && !plan.HasWorldMaterials &&
               !plan.HasBarrier && !plan.HasRecovery && plan.ContactControl == 0 &&
               (plan.Use.Channels & ActiveAbilityChannel.World) != 0 &&
               plan.Use.Target is ActiveAbilityTargetMode.Point or ActiveAbilityTargetMode.ObjectOrPoint;
    }

    /// <summary>允许站定使用实际器具的工作动作，避免用预估替代对手或位移条件。</summary>
    private static bool ProjectBodySupported(CompiledAbility plan)
    {
        if (plan.Body is not { } body) return true;
        return body.Part.IsArm && body.WeaponForm != null && !body.WeaponGround && !body.WeaponObstacle &&
               !body.Construction.HasControlRelations && body.Construction.FootworkTravel <= .00001f &&
               !plan.UsesHeldWeapon && !plan.HasPropagation && plan.WorldCondition == null &&
               plan.ActorImpacts.All(impact => !impact.HasActorEffect);
    }

    /// <summary>按完整施放预估世界足迹；超过明确工作量时拒绝使用不完整结果。</summary>
    /// <param name="caster">实际来源。</param><param name="plan">已检查的纯世界计划。</param>
    /// <param name="point">地图横纵位置及离地高度。</param><param name="preview">完成的足迹。</param>
    internal static bool TryPreviewWorld(ActorExtend caster, CompiledAbility plan, Vector3 point,
        out ConstructedWorldPreview preview)
    {
        preview = null;
        Vector3 source = SpatialActorPose.Foot(caster.Base);
        Rect area = Rect.MinMaxRect(Mathf.Min(source.x, point.x) - 20, Mathf.Min(source.y, point.y) - 20,
            Mathf.Max(source.x, point.x) + 20, Mathf.Max(source.y, point.y) + 20);
        AbilityHeightField ground = GroundProvider?.Invoke(area);
        source.z += ground?.Sample(source.x, source.y) ?? 0;
        point.z += ground?.Sample(point.x, point.y) ?? 0;
        Vector3 forward = point - source;
        forward.z = 0;
        forward = forward.sqrMagnitude > 1e-8f ? forward.normalized : Vector3.right;
        float height = SpatialActorPose.Height(caster.Base);
        BodyActionBinding body = null;
        BodyLocomotion movement = null;
        BodyFrame? frame = null;
        if (plan.Body is { } bodyAction)
        {
            if (!ProjectBodySupported(plan)) return false;
            body = BodyActionBinding.TryCreate(caster.Base, bodyAction, forward, point, 0f);
            if (body == null) return false;
            frame = body.Frame(source, height);
            if (!body.CanStart(frame.Value)) return false;
            if (bodyAction.ControlsStance)
            {
                movement = new BodyLocomotion(caster.Base, bodyAction, frame.Value, ground, body.Joint);
                if (!movement.CanStart(ground)) return false;
                var pose = movement.Sample(0f, height, ground);
                frame = body.Frame(pose.Root, height, pose.Support);
            }
        }

        var input = new AbilityInputs(source, point, forward, height);
        var cast = new AbilityCast(plan, Matrix4x4.TRS(point, Quaternion.identity, Vector3.one),
            frame, input, terrain: ground, force: 0);
        var authority = new CastAuthority(caster.Base, default, null);
        var running = new RunningCast
        {
            Cast = cast, Body = body, InitialOwner = authority, Source = caster.Base, SourceId = caster.Base.id,
            Owners = Enumerable.Repeat(authority, plan.FormCount).ToArray(),
            WorldHits = new(), WorldTotals = new()
        };
        // 独立上下文复用首次接触与真实重入规则，不碰正式施放列表或共享预测状态。
        var context = new ConstructedAbilityService { terrain = ground, worldQueryBudget = 65536 };
        var result = new ConstructedWorldPreview();
        int work = 0, rounds = 0;
        while (!cast.Finished)
        {
            if (++rounds > 8192 || (work += plan.WorkSize) > 2000000) return false;
            float step = cast.NextStep(plan.Lifetime - cast.Age);
            if (step <= 0) return false;
            cast.SaveStep();
            running.Before.Clear();
            CollectPreview(running.Before, false);
            context.RefreshWorldReentry(running);
            AdvancePreview(step);
            running.After.Clear();
            CollectPreview(running.After, true);
            context.shapes.Clear();
            context.previousShapes.Clear();
            foreach (AbilityCapsule shape in running.Before)
                context.previousShapes.Add((shape.Node, shape.Shape), shape);
            foreach (AbilityCapsule shape in running.After)
            {
                bool continuous = context.previousShapes.TryGetValue((shape.Node, shape.Shape), out var from);
                context.shapes.Add(new ShapeMotion(running, continuous ? from : shape, shape, continuous));
            }

            ContactChoice? first = null;
            context.contactEvaluation++;
            foreach (ShapeMotion shape in context.shapes)
            {
                context.FindWorldContacts(shape, step, Include);
                if (context.worldQueryBudget == 0) return false;
            }

            if (!first.HasValue) continue;
            ContactChoice hit = first.Value;
            if (hit.Fraction < 1)
            {
                cast.RestoreStep();
                AdvancePreview(step * hit.Fraction);
            }

            int node = hit.Left.To.Node;
            if (hit.WorldEffect < 0)
            {
                cast.ConfirmWorldContact(node, hit.Left.To.Shape, hit.Point);
                continue;
            }

            RecordWorldAttempt(running, node, hit.WorldEffect, hit.Tile);
            var rule = cast.Impact(node).WorldEffects[hit.WorldEffect];
            cast.UseWorldMaterial(node, hit.Left.To.Shape, rule.MaterialVolume, hit.Point, out _);
            string action = rule.Action.Id;
            if (!result.Tiles.TryGetValue(action, out var ids)) result.Tiles.Add(action, ids = new());
            ids.Add(hit.Tile.tile_id);

            void Include(ContactChoice candidate)
            {
                if (!first.HasValue || ContactChoice.Compare(candidate, first.Value) < 0) first = candidate;
            }
        }

        preview = result;
        return true;

        void CollectPreview(List<AbilityCapsule> output, bool includeEnd)
        {
            cast.AppendContacts(output, includeEnd);
            if (body != null) CollectBodyContacts(body, cast, output, includeEnd);
        }

        void AdvancePreview(float step)
        {
            if (movement == null)
            {
                cast.Advance(step, point);
                return;
            }
            var pose = movement.Sample(cast.Age + step, height, ground);
            var sample = new AbilityInputs(pose.Root, point, forward, height);
            new AbilityStep(cast, sample, body.Frame(pose.Root, height, pose.Support), point, step).Execute();
        }
    }
}
