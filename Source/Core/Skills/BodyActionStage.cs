using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>同一身体曲线中的实际作用阶段，不引用另一项完整能力。</summary>
public sealed class BodyActionStage
{
    /// <summary>本招内稳定的阶段名称。</summary>
    public string Id;
    /// <summary>第一次实际作用的原动作开始秒数。</summary>
    public float From;
    /// <summary>第一次实际作用的原动作结束秒数。</summary>
    public float Until;
    /// <summary>有限作用次数，只有真实接触才结算。</summary>
    public int Pulses = 1;
    /// <summary>相邻作用起点的间隔；单次作用可为零。</summary>
    public float Interval;
    /// <summary>本段身体作用的完整覆盖；缺省继承身体及整招。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionImpact Impact;
}

/// <summary>一次已展开的身体作用，纯数值随同一身体曲线运行。</summary>
public readonly struct CompiledBodyStage
{
    /// <summary>原命名阶段及本次重复序号。</summary>
    public readonly string Id;
    /// <summary>同一命名阶段内从零开始的次数序号。</summary>
    public readonly int Repetition;
    /// <summary>包含实际姿态维持的开始和结束世界秒数。</summary>
    public readonly float From, Until;
    /// <summary>已复制的阶段作用；缺省继承身体作用。</summary>
    public readonly CompiledConstructionImpact Impact;

    /// <summary>冻结已验证的单次作用。</summary>
    /// <param name="id">原命名阶段。</param><param name="repetition">重复序号。</param>
    /// <param name="from">开始秒数。</param><param name="until">结束秒数。</param><param name="impact">独立作用。</param>
    internal CompiledBodyStage(string id, int repetition, float from, float until, CompiledConstructionImpact impact)
    { Id = id; Repetition = repetition; From = from; Until = until; Impact = impact; }
}

/// <summary>阶段只划分真实作用，姿态、步法和支撑仍共用原连续曲线。</summary>
public sealed partial class CompiledBodyAction
{
    /// <summary>与原身体关键点相同的有限展开预算。</summary>
    public const int MaximumStageWindows = 128;
    /// <summary>十进制秒数在重复相加后的边界容差，只用于对齐同一时点。</summary>
    private const float StageTimeTolerance = .00001f;
    /// <summary>编译时固定的作用及边界，不在取样时展开或分配。</summary>
    private readonly CompiledBodyStage[] stages;
    /// <summary>只读出口在编译时建立，不在逐帧读取时分配。</summary>
    private readonly ReadOnlyCollection<CompiledBodyStage> stageView;
    /// <summary>有序的作用开始及全部边界，用于二分查询和共同拆步。</summary>
    private readonly float[] stageStarts, stageBoundaries;
    /// <summary>本次资料是否显式声明内部阶段。</summary>
    public bool HasStages { get; private set; }
    /// <summary>编译后的有限作用次数。</summary>
    public int StageCount => stages.Length;
    /// <summary>公开只读阶段，编辑资料的后续修改不影响正在运行的构造。</summary>
    public IReadOnlyList<CompiledBodyStage> Stages => stageView;

    /// <summary>取得本时点最后开始的作用；起势阶段继续使用第一次的接触组。</summary>
    /// <param name="age">本次施放的世界秒数。</param>
    public int StageAt(float age)
    {
        int index = Array.BinarySearch(stageStarts, age);
        return Mathf.Max(0, index >= 0 ? index : ~index - 1);
    }

    /// <summary>判断实际窗口，不把两次作用之间的空隙当成攻击。</summary>
    /// <param name="age">本次世界秒数。</param>
    public bool IsActiveAt(float age)
    {
        CompiledBodyStage stage = stages[StageAt(age)];
        return age >= stage.From && age <= stage.Until;
    }

    /// <summary>提供下一真实边界，防止扫掠越过无作用的空隙。</summary>
    /// <param name="age">已经确认的世界秒数。</param>
    internal float NextStageBoundary(float age)
    {
        int index = Array.BinarySearch(stageBoundaries, age);
        index = index >= 0 ? index + 1 : ~index;
        return index < stageBoundaries.Length ? stageBoundaries[index] : float.PositiveInfinity;
    }

    /// <summary>先验证完整声明和次数预算，再冻结窗口及作用；旧动作只编译原窗口。</summary>
    /// <param name="definition">完整身体资料。</param>
    private CompiledBodyStage[] CompileStages(BodyActionDefinition definition)
    {
        HasStages = definition.Stages?.Length > 0;
        if (!HasStages) return new[] { new CompiledBodyStage(null, 0, definition.ActiveFrom,
            definition.ActiveUntil + hold.Seconds, Impact) };
        if (definition.Stages.Length > MaximumStageWindows)
            throw new InvalidDataException("Body/Stages：超过128次实际作用的预算");
        var result = new List<CompiledBodyStage>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        float priorRaw = definition.ActiveFrom, priorWorld = definition.ActiveFrom;
        foreach (BodyActionStage stage in definition.Stages)
        {
            if (stage == null || string.IsNullOrWhiteSpace(stage.Id) || !ids.Add(stage.Id) ||
                !Finite(stage.From) || !Finite(stage.Until) || !Finite(stage.Interval) ||
                stage.From < 0f || stage.From < priorRaw - StageTimeTolerance || stage.Until <= stage.From || stage.Pulses < 1 ||
                stage.Pulses > MaximumStageWindows - result.Count || stage.Interval < 0f ||
                stage.Pulses > 1 && stage.Interval < stage.Until - stage.From - StageTimeTolerance)
                throw new InvalidDataException("Body/Stages：需要独立名称、递增且不重叠的窗口和总计不超过128次的正整数次数");
            float last = stage.Until + (stage.Pulses - 1) * stage.Interval;
            if (!Finite(last) || last > definition.ActiveUntil + StageTimeTolerance)
                throw new InvalidDataException("Body/Stages：重复后的窗口超过身体有效范围");
            var impact = stage.Impact == null ? Impact : new CompiledConstructionImpact(stage.Impact, "Body/Stages/" + stage.Id + "/Impact");
            for (int repeat = 0; repeat < stage.Pulses; repeat++)
            {
                float from = stage.From + repeat * stage.Interval, until = stage.Until + repeat * stage.Interval;
                until = Mathf.Min(until, definition.ActiveUntil);
                // 同刻相接的窗口把维持归给前一次，后一次在维持结束后才真正开始。
                from += from > hold.At ? hold.Seconds : 0f;
                until += until >= hold.At ? hold.Seconds : 0f;
                from = Mathf.Max(from, priorWorld);
                if (result.Count == 0 && Math.Abs(from - definition.ActiveFrom) <= StageTimeTolerance) from = definition.ActiveFrom;
                if (from >= until) throw new InvalidDataException("Body/Stages：重复或维持产生了零时长窗口");
                result.Add(new CompiledBodyStage(stage.Id, repeat, from, until, impact));
                priorWorld = until;
            }
            priorRaw = last;
        }
        if (result[0].From != definition.ActiveFrom || Math.Abs(priorRaw - definition.ActiveUntil) > StageTimeTolerance)
            throw new InvalidDataException("Body/Stages：首末窗口须与身体有效范围一致");
        return result.ToArray();
    }
}
