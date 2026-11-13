using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Cultiway.Core.Skills;

/// <summary>分段只改变同一招的实际作用窗口，保留原身体曲线及结果收势。</summary>
public static partial class BodyConstructionEdits
{
    /// <summary>独立替换阶段并对齐身体首末窗口，完整编译成功后才交出改造结果。</summary>
    /// <param name="source">完整原构造。</param><param name="stages">本次实际阶段；空集合恢复原首末间的单次作用。</param>
    /// <param name="reprice">是否按实际次数及作用调整原费用和恢复时间。</param>
    public static AbilityDefinition Stages(AbilityDefinition source, BodyActionStage[] stages, bool reprice = true)
    {
        if (source?.Body == null) throw new InvalidDataException("内部阶段需要实际身体动作");
        AbilityDefinition copy = ConstructionEditing.Copy(source);
        copy.Body.Stages = CopyStages(stages);
        AlignStageEnvelope(copy.Body);
        AbilityCompiler.Compile(copy, cache: false);
        return reprice ? ConstructionPricing.Reprice(source, copy) : copy;
    }

    /// <summary>创建及改造边界复制完整作用，整体改速同步缩放窗口和间隔。</summary>
    /// <param name="stages">调用方可变的输入。</param><param name="timeScale">实际秒数倍率。</param>
    internal static BodyActionStage[] CopyStages(BodyActionStage[] stages, float timeScale = 1f)
    {
        if (stages == null) return null;
        BodyActionStage[] copy = JsonConvert.DeserializeObject<BodyActionStage[]>(JsonConvert.SerializeObject(stages));
        foreach (BodyActionStage stage in copy)
            if (stage != null)
            {
                stage.From *= timeScale;
                stage.Until *= timeScale;
                stage.Interval *= timeScale;
            }
        return copy;
    }

    /// <summary>使用首末声明推导外层范围，其他合法性仍由共同编译器判断。</summary>
    /// <param name="body">本次独占的完整身体资料。</param>
    internal static void AlignStageEnvelope(BodyActionDefinition body)
    {
        if (body.Stages?.Length > 0)
        {
            BodyActionStage first = body.Stages[0], last = body.Stages.Last();
            if (first == null || last == null) throw new InvalidDataException("内部阶段不能留空");
            body.ActiveFrom = first.From;
            body.ActiveUntil = last.Until + (last.Pulses - 1) * last.Interval;
        }
    }
}
