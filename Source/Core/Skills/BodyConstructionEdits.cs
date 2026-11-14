using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>把实际身体动作改为显化投送时需要明确提供的资料，不是整招运行类型。</summary>
public sealed class BodyProjection
{
    /// <summary>显化形体；空值沿用实际借用器物的形体或手掌；肘肩显化须明确选择形体。</summary>
    public string Form;

    /// <summary>实际材料及可选物态，与显示染色分别保存。</summary>
    public string Matter = "physical", State;

    /// <summary>按实际体高缩放的显化大小。</summary>
    public float Scale = 3f;

    /// <summary>显化相对末端的三个转角；初速度仍沿真实出手方向，追踪时保留形体与速度的夹角。</summary>
    public float[] Rotation = { 0f, 25f, 0f };

    /// <summary>离手后的地图格速度。</summary>
    public float Speed = 8f;

    /// <summary>离手后最多继续的世界秒数。</summary>
    public float Seconds = 2f;

    /// <summary>追踪目标时每秒可转过的角度，零表示直行。</summary>
    public float TurnRate = 360f;

    /// <summary>相对施放的离手时点；负一沿用原动作的有效结束。</summary>
    public float ReleaseAt = -1f;

    /// <summary>明确采用的实际支付资源。</summary>
    public string Resource = "wakan";

    /// <summary>本次显化的整次实际消耗，不在离手时重复支付。</summary>
    public float Cost = 4f;
}

/// <summary>依附真实发力部位的延伸形体；动作、作用时段及结束仍由身体维护。</summary>
public sealed class BodyExtension
{
    /// <summary>明确选择的形体；手部缺省沿用持物形体或掌形，其他部位须明确指定。</summary>
    public string Form;

    /// <summary>实际材料与可选物态，不从显示颜色推断。</summary>
    public string Matter = "physical", State;

    /// <summary>按实际体高缩放的形体大小。</summary>
    public float Scale = 3f;

    /// <summary>相对真实末端的三个局部转角。</summary>
    public float[] Rotation = { 0f, 25f, 0f };

    /// <summary>本次改造明确选择的支付资源。</summary>
    public string Resource = "wakan";

    /// <summary>整次受理时支付，不按形体和身体分别扣取。</summary>
    public float Cost = 4f;
}

/// <summary>玩家改造及修士创作共用的资料变化，运行仍使用身体、连接和普通形体操作。</summary>
public static partial class BodyConstructionEdits
{
    /// <summary>由真实载体承接原发力，并在明确时点释放可独立继续的显化主体。</summary>
    /// <param name="source">原身体动作。</param><param name="projection">实际显化、投送及支付选择。</param>
    public static AbilityDefinition Carry(AbilityDefinition source, BodyProjection projection)
    {
        AbilityDefinition result = Project(source, projection);
        result.Binding.Anchor = AbilityAnchor.Carrier;
        result.Binding.FaceTarget = true;
        return result;
    }

    /// <summary>保留原发力与作用，将延伸显化的实际连接改为本次取得的外部载体。</summary>
    /// <param name="source">已有身体动作。</param><param name="extension">实际作用形体及支付选择。</param>
    public static AbilityDefinition Carry(AbilityDefinition source, BodyExtension extension)
    {
        AbilityDefinition result = Extend(source, extension);
        result.Binding.Anchor = AbilityAnchor.Carrier;
        result.Binding.FaceTarget = true;
        return result;
    }

    /// <summary>保留真实动作及支撑，增加随实际发力阶段接触和收回的延伸体。</summary>
    /// <param name="source">已有且未生成主体的实际身体动作。</param>
    /// <param name="extension">实际形体、材料、尺度及支付选择。</param>
    public static AbilityDefinition Extend(AbilityDefinition source, BodyExtension extension)
    {
        if (source?.Body == null || source.Root != null || extension == null)
            throw new InvalidDataException("延伸接触需要实际身体动作，已有主体请直接修改其连接和作用");
        CompiledAbility original = AbilityCompiler.Compile(source, cache: false);
        if (original.Body.Construction.HasControlRelations)
            throw new InvalidDataException("延伸改造尚未提供远程双方连接，不能把抓持、抵住或拨压直接搬到延伸体上");
        if (original.Body.Part.Contact != BodyContactPart.Hand && extension.Form == null)
            throw new InvalidDataException("非掌部延伸需要明确选择实际形体，不能默认为手掌");
        AbilityDefinition result = ConstructionEditing.Copy(source);
        result.Resource = extension.Resource;
        result.Cost = extension.Cost;
        result.TargetRange = null;
        result.Binding = new ConstructionBinding
            { Anchor = AbilityAnchor.Effector, Follow = true, ScaleByBody = true };
        result.Root = new ConstructionNode
        {
            Id = "extension", Form = extension.Form ?? original.Body.WeaponForm ?? "hand",
            Matter = extension.Matter, State = extension.State, Scale = extension.Scale,
            Rotation = extension.Rotation?.ToArray(), ContactGroup = "body", BodyWindow = true,
            DisappearAt = original.Body.Duration,
            Operations = new[]
            {
                new ConstructionOperation
                {
                    Kind = ConstructionOperationKind.Grow,
                    Seconds = Math.Min(original.Body.Duration, Math.Max(.01f, original.Body.ActiveFrom))
                }
            }
        };
        AbilityCompiler.Compile(result, cache: false);
        return result;
    }

    /// <summary>展开原来省略的身体接触声明，保留原动作、普通命中和真实握持。</summary>
    /// <param name="source">已经可以编译的身体动作。</param>
    public static AbilityDefinition ExplicitRelations(AbilityDefinition source)
    {
        if (source?.Body == null) throw new InvalidDataException("部位关系需要实际身体动作");
        CompiledBodyAction original = AbilityCompiler.Compile(source, cache: false).Body;
        AbilityDefinition result = ConstructionEditing.Copy(source);
        if (result.Body.Construction != null) return result;
        var parts = new System.Collections.Generic.List<BodyPartInput>
        {
            new() { Id = "action", Subject = BodyPartSubject.Source, Part = original.Effector },
            new() { Id = "target", Subject = BodyPartSubject.Target, Part = "surface" }
        };
        var relations = new System.Collections.Generic.List<BodyRelationDefinition>
        {
            new() { Id = "touch", From = original.WeaponForm == null ? "action" : "held", To = "target" }
        };
        if (original.WeaponForm != null)
        {
            parts.Add(new BodyPartInput { Id = "held", Subject = BodyPartSubject.Held, Part = "item" });
            relations.Add(new BodyRelationDefinition
            {
                Id = "grip", Kind = BodyRelationKind.Grip, From = "action", To = "held",
                FromAnchor = original.Construction.HandGrip, ToAnchor = original.Grip,
                FromMarker = "start", UntilMarker = "end"
            });
        }

        result.Body.Effector = "action";
        result.Body.Construction = new BodyConstructionDefinition
            { Parts = parts.ToArray(), Relations = relations.ToArray() };
        AbilityCompiler.Compile(result, cache: false);
        return result;
    }

    /// <summary>保留实际发力与收势，为身体末端补齐显化、离手、投送、接触及消耗。</summary>
    /// <param name="source">已有身体动作；不能静默覆盖其原有生成主体。</param>
    /// <param name="projection">这次显化的明确选择。</param>
    public static AbilityDefinition Project(AbilityDefinition source, BodyProjection projection)
    {
        if (source?.Body == null || source.Root != null || projection == null)
            throw new InvalidDataException("显化投送需要实际身体动作，已有主体请直接修改其连接和投送");
        CompiledAbility original = AbilityCompiler.Compile(source, cache: false);
        if (original.Body.Part.Contact != BodyContactPart.Hand && projection.Form == null)
            throw new InvalidDataException("非掌部显化需要明确选择实际形体，不能默认为手掌");
        AbilityDefinition result = ConstructionEditing.Copy(source);
        BodyActionDefinition body = result.Body;
        float release = projection.ReleaseAt == -1f ? original.Body.ActiveUntil : projection.ReleaseAt;
        if (float.IsNaN(release) || float.IsInfinity(release) || release <= body.ActiveFrom ||
            release > original.Body.Duration ||
            float.IsNaN(projection.Seconds) || float.IsInfinity(projection.Seconds) || projection.Seconds <= 0f)
            throw new InvalidDataException("离手必须在实际发力开始后、收势结束前，并提供有效的投送时长");
        if (projection.Rotation?.Length != 3 ||
            projection.Rotation.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
            throw new InvalidDataException("显化朝向需要三个有限转角");
        Quaternion orientation =
            Quaternion.Euler(projection.Rotation[0], projection.Rotation[1], projection.Rotation[2]);
        Vector3 velocity = Quaternion.Inverse(orientation) * Vector3.forward * projection.Speed;
        // 沿用默认有效结束时保留原动作秒数，避免先加维持再减去时产生浮点倒退。
        if (projection.ReleaseAt != -1f)
        {
            if (body.Hold != null && release < body.Hold.At + body.Hold.Seconds)
                throw new InvalidDataException("显化离手应在本次身体维持结束后");
            body.ActiveUntil = release - (body.Hold?.Seconds ?? 0f);
        }

        result.Lifetime = Math.Max(original.Body.Duration, release + projection.Seconds);
        result.Resource = projection.Resource;
        result.Cost = projection.Cost;
        result.TargetRange = Math.Abs(projection.Speed) * projection.Seconds;
        var values = ConstructionDefinitionLayers.Snapshot(result);
        values["Binding"] = original.Body.Part.Contact == BodyContactPart.Hand
            ? new JObject()
            : JObject.FromObject(new ConstructionBinding
                { Anchor = AbilityAnchor.Effector, Follow = true, ScaleByBody = true });
        var root = new JObject
        {
            ["Id"] = "manifestation",
            ["Matter"] = projection.Matter,
            ["State"] = projection.State,
            ["Surface"] = JValue.CreateNull(),
            ["DisappearAt"] = JValue.CreateNull(),
            ["Scale"] = projection.Scale,
            ["Rotation"] = new JArray(projection.Rotation),
            ["Operations"] = JArray.FromObject(new[]
            {
                new ConstructionOperation
                {
                    Kind = ConstructionOperationKind.Grow, At = body.ActiveFrom,
                    Seconds = Math.Max(.05f, body.ActiveUntil - body.ActiveFrom)
                },
                new ConstructionOperation { Kind = ConstructionOperationKind.Detach, At = release },
                new ConstructionOperation
                {
                    Kind = ConstructionOperationKind.Fly, At = release,
                    Vector = new[] { velocity.x, velocity.y, velocity.z }, TurnRate = projection.TurnRate,
                    InheritVelocity = false
                }
            })
        };
        if (body.Impact != null) root["Impact"] = JObject.FromObject(body.Impact);
        if (projection.Form != null) root["Form"] = projection.Form;
        values["Root"] = root;
        result = ConstructionDefinitionLayers.Resolve(values);
        AbilityCompiler.Compile(result, cache: false);
        return result;
    }
}
