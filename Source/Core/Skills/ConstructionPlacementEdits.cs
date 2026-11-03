using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>随动和环绕共用的资料改造，作用与材料保留，运动继续使用原有连接及旋转。</summary>
public static class ConstructionPlacementEdits
{
    /// <summary>将原主体改为跟随实际来源或目标，不增加作用、次数、消耗或持续时间。</summary>
    /// <param name="source">带有实际生成主体的原法术。</param>
    /// <param name="anchor">来源、目标或已有身体的末端。</param>
    public static AbilityDefinition Follow(AbilityDefinition source, AbilityAnchor anchor)
    {
        if (source?.Root == null) throw new InvalidDataException("随动改造需要已有生成主体");
        CompiledAbility plan = AbilityCompiler.Compile(source, cache: false);
        var result = ConstructionEditing.Copy(source);
        result.Binding ??= new ConstructionBinding();
        // 手部始终采用实际手姿态；转换为人物连接时保留原有模型坐标约定。
        if (((result.Binding.Anchor is AbilityAnchor.Hand or AbilityAnchor.Effector) ||
             result.Root.Activation?.Source == "body" &&
             result.Root.Activation.Placement == ConstructionEventPlacement.Event) &&
            anchor is not (AbilityAnchor.Hand or AbilityAnchor.Effector)) result.Binding.FaceTarget = true;
        result.Binding.Anchor = anchor;
        result.Binding.Follow = true;
        if (result.Root.Activation != null) result.Root.Activation.Placement = ConstructionEventPlacement.Attached;
        result.Root.Operations = result.Root.Operations
            .Where(o => o.Kind is not (ConstructionOperationKind.Fly or ConstructionOperationKind.Detach)).ToArray();
        result.Steps = result.Steps.Where(step =>
            step.Operation.Kind is not (ConstructionOperationKind.Fly or ConstructionOperationKind.Detach) ||
            !plan.Steps.Any(compiled => compiled.Id == step.Id && compiled.Node == 0)).ToArray();
        AbilityCompiler.Compile(result, cache: false);
        return result;
    }

    /// <summary>在实际随动中心建立旋转支点，原主体作为子结构沿圆周运动，内部变化及作用规则继续有效。</summary>
    /// <param name="source">原有单个主体及其完整子结构。</param>
    /// <param name="anchor">实际随动中心。</param>
    /// <param name="radius">现有根坐标中的环绕半径，保留原有高度。</param>
    /// <param name="seconds">完成一周的世界秒数，正数顺转、负数逆转。</param>
    public static AbilityDefinition Orbit(AbilityDefinition source, AbilityAnchor anchor, float radius, float seconds)
    {
        if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f || radius > 10000f ||
            float.IsNaN(seconds) || float.IsInfinity(seconds) || Mathf.Abs(seconds) < .05f)
            throw new InvalidDataException("环绕需要0至一万格内的半径，每周至少0.05秒");
        AbilityDefinition result = Follow(source, anchor);
        ConstructionNode subject = result.Root;
        ConstructionEditing.RebaseReferences(result, "root/" + subject.Id);
        bool bodyAxes = result.Binding.FaceTarget || (anchor is AbilityAnchor.Hand or AbilityAnchor.Effector);
        float height = subject.Offset[bodyAxes ? 1 : 2];
        subject.Offset = bodyAxes ? new[] { 0f, height, radius } : new[] { radius, 0f, height };
        result.Root = new ConstructionNode
        {
            Id = "orbit_origin",
            Form = "pivot",
            ContactRole = ConstructionContactRole.Decoration,
            Operations = new[]
            {
                new ConstructionOperation
                {
                    Kind = ConstructionOperationKind.Spin,
                    Vector = bodyAxes ? new[] { 0f, 360f / seconds, 0f } : new[] { 0f, 0f, 360f / seconds }
                }
            },
            Children = new[] { subject }
        };
        AbilityCompiler.Compile(result, cache: false);
        return result;
    }
}