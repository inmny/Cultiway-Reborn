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
            ConstructionOperation animation = node.Operations.FirstOrDefault(o => o.Kind == ConstructionOperationKind.Animate);
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
        AbilityCompiler.Compile(result, cache: false);
        return reprice ? ConstructionPricing.Reprice(source, result) : result;
    }
}
