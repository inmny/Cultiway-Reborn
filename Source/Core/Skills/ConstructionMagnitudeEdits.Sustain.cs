using System;
using System.IO;
using System.Linq;
using Cultiway.Core.Visuals.Forms;

namespace Cultiway.Core.Skills;

/// <summary>维持形态或身体姿态，其他过程保持原速度，不用整段变慢代替持续。</summary>
public static partial class ConstructionMagnitudeEdits
{
    /// <summary>增加明确维持秒数，保留接触间隔和次数上限，按实际持续重新计算需求。</summary>
    /// <param name="source">已有完整构造。</param>
    /// <param name="seconds">本次额外维持的世界秒数。</param>
    /// <param name="reprice">是否按实际前后规模计算需求。</param>
    public static AbilityDefinition Sustain(AbilityDefinition source, float seconds, bool reprice = true)
    {
        Factor(seconds);
        CompiledAbility plan = AbilityCompiler.Compile(source, cache: false);
        AbilityDefinition result = ConstructionEditing.Copy(source);
        result.Lifetime += seconds;
        bool holdBody = result.Body != null && (result.Root == null ||
            result.Binding?.Anchor == AbilityAnchor.Hand && result.Binding.Follow && plan.Nodes[0].ReleaseAt < 0f);
        if (holdBody)
        {
            result.Body.Hold ??= new ConstructionHold { At = result.Body.ActiveUntil };
            result.Body.Hold.Seconds += seconds;
        }
        Visit(result.Root, node =>
        {
            if (node.DisappearAt.HasValue) node.DisappearAt += seconds;
            if (node.ActiveUntil >= 0f) node.ActiveUntil += seconds;
            foreach (ConstructionOperation operation in node.Operations.Where(o => o.Kind == ConstructionOperationKind.Animate && (o.When != null || o.Hold != null)))
                Hold(operation, node, new[] { FormResources.Load(node.Form) });
            ConstructionOperation animation = node.Operations.FirstOrDefault(o => o.Kind == ConstructionOperationKind.Animate && o.When == null && o.Hold == null);
            if (animation == null) return;
            if (node.Hold == null)
            {
                FormMotion motion = FormResources.Load(node.Form).Rig.Motion(animation.Clip);
                float speed = animation.Seconds == 0f ? 1f : motion.Duration / animation.Seconds;
                float from = string.IsNullOrEmpty(node.ActiveFromMarker) ? Math.Max(node.ActiveFrom, animation.At) :
                    animation.At + motion.MarkerTime(node.ActiveFromMarker) / speed;
                float until = string.IsNullOrEmpty(node.ActiveUntilMarker) ? Math.Min(
                    node.ActiveUntil < 0f ? (node.DisappearAt ?? source.Lifetime + seconds) - seconds : node.ActiveUntil - seconds,
                    animation.At + motion.Duration / speed) : animation.At + motion.MarkerTime(node.ActiveUntilMarker) / speed;
                if (until <= from) throw new InvalidDataException(node.Id + "：制作动作没有可维持的有效阶段");
                node.Hold = new ConstructionHold { At = (from + until) * .5f };
            }
            node.Hold.Seconds += seconds;
        });
        foreach (ConstructionStep step in result.Steps.Where(s => s.Operation.Kind == ConstructionOperationKind.Animate))
        {
            CompiledForm node = plan.Nodes[plan.Steps.First(s => s.Id == step.Id).Node];
            ConstructionNode declaration = result.Root;
            foreach (string part in node.DeclarationPath.Split('/').Skip(1)) declaration = declaration.Children.Single(child => child.Id == part);
            Hold(step.Operation, declaration, plan.Steps.Where(s => s.Id == step.Id).Select(s => plan.Nodes[s.Node].Resource).Distinct().ToArray());
        }
        // 等待前一步完成的动作会依次延期；整次截止时间须容纳最长实际依赖链。
        var starts = new float[plan.Steps.Length]; var ends = new float[plan.Steps.Length]; float padding = seconds;
        for (int i = 0; i < plan.Steps.Length; i++)
        {
            CompiledConstructionStep step = plan.Steps[i];
            foreach (CompiledStepInput input in step.Inputs)
                if (input.Step >= 0) starts[i] = Math.Max(starts[i], input.Completed ? ends[input.Step] : starts[input.Step]);
            if (holdBody && step.Action.When is { Source: -1, Trigger: ConstructionChangeTrigger.End }) starts[i] = Math.Max(starts[i], seconds);
            ends[i] = starts[i] + (step.Action.Motion == null ? 0f : seconds);
            padding = Math.Max(padding, ends[i]);
        }
        if (padding > seconds)
        {
            result.Lifetime += padding - seconds;
            Visit(result.Root, node =>
            {
                if (node.DisappearAt.HasValue) node.DisappearAt += padding - seconds;
                if (node.ActiveUntil >= 0f) node.ActiveUntil += padding - seconds;
            });
        }
        AbilityCompiler.Compile(result, cache: false);
        return reprice ? ConstructionPricing.Reprice(source, result) : result;

        // 动作自己的维持以真实启动为零点，等待依赖或事件的时间不会耗掉它。
        void Hold(ConstructionOperation operation, ConstructionNode node, FormResource[] resources)
        {
            if (operation.Hold == null)
            {
                float from = 0f, until = float.PositiveInfinity;
                foreach (FormResource resource in resources)
                {
                    FormMotion motion = resource.Rig.Motion(operation.Clip);
                    float speed = operation.Seconds == 0f ? 1f : motion.Duration / operation.Seconds;
                    from = Math.Max(from, string.IsNullOrEmpty(node.ActiveFromMarker) ? 0f : motion.MarkerTime(node.ActiveFromMarker) / speed);
                    until = Math.Min(until, string.IsNullOrEmpty(node.ActiveUntilMarker) ? motion.Duration / speed : motion.MarkerTime(node.ActiveUntilMarker) / speed);
                }
                if (until <= from) throw new InvalidDataException(node.Id + "：制作动作没有可维持的有效阶段");
                operation.Hold = new ConstructionHold { At = (from + until) * .5f };
            }
            operation.Hold.Seconds += seconds;
        }
    }
}
