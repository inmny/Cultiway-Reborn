using System;
using System.Linq;
using Cultiway.Core.Skills;
using Newtonsoft.Json;

namespace Cultiway.UI;

/// <summary>身体页编辑真实分段，作用页复用同一份阶段资料及原有作用表单。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>当前查看的阶段序号，刷新时裁到真实阶段范围内。</summary>
    private int bodyStageSelection;

    /// <summary>选定一个真实阶段，只显示该段参数，避免把所有阶段堆成大表。</summary>
    /// <param name="body">当前独立草稿中的身体。</param>
    private BodyActionStage ChooseBodyStage(BodyActionDefinition body)
    {
        if (body.Stages?.Length > 0)
        {
            bodyStageSelection = Math.Min(bodyStageSelection, body.Stages.Length - 1);
            Choice("BodyStage", string.Format(T("BodyStageNumber"), bodyStageSelection + 1), () =>
                menu.Show(T("BodyStage"), Enumerable.Range(0, body.Stages.Length).Select(index => new UiOptionMenuOption
                {
                    Label = string.Format(T("BodyStageNumber"), index + 1), Selected = index == bodyStageSelection,
                    Select = () => { bodyStageSelection = index; Refresh(); }
                }).ToArray()));
            return body.Stages[bodyStageSelection];
        }
        return null;
    }

    /// <summary>阶段声明沿用原姿态，接触或受阻后的提前收势仍需由作者明确选择。</summary>
    private void BodyStages()
    {
        BodyActionDefinition body = session.Draft.Body;
        Row("BodyStages", T("BodyStagesHint"));
        BodyActionStage stage = ChooseBodyStage(body);
        if (stage != null)
        {
            StageNumber("StageFrom", stage.From, (s, value) => s.From = value);
            StageNumber("StageUntil", stage.Until, (s, value) => s.Until = value);
            StageNumber("StagePulses", stage.Pulses, (s, value) =>
            {
                if (value != Math.Floor(value) || value < 1 || value > CompiledBodyAction.MaximumStageWindows)
                    throw new System.IO.InvalidDataException("阶段次数需要1至128的整数");
                s.Pulses = (int)value;
            });
            StageNumber("StageInterval", stage.Interval, (s, value) => s.Interval = value);
            ActionRow("RemoveBodyStage", T("RemoveBodyStageHint"), () => ApplyConstructionEdit(() =>
                BodyConstructionEdits.Stages(session.Draft, body.Stages.Where((_, i) => i != bodyStageSelection).ToArray())));
        }
        ActionRow("SplitBodyStage", T("SplitBodyStageHint"), () => ApplyConstructionEdit(() =>
        {
            var stages = (ConstructionEditing.Copy(session.Draft).Body.Stages ?? Array.Empty<BodyActionStage>()).ToList();
            if (stages.Count == 0)
                stages.Add(new BodyActionStage { Id = "stage.1", From = body.ActiveFrom, Until = body.ActiveUntil });
            int index = Math.Min(bodyStageSelection, stages.Count - 1);
            BodyActionStage first = stages[index];
            BodyActionStage next = JsonConvert.DeserializeObject<BodyActionStage>(JsonConvert.SerializeObject(first));
            string id = "stage.2";
            for (int suffix = 3; stages.Any(s => s.Id == id); suffix++) id = "stage." + suffix;
            next.Id = id;
            if (first.Pulses > 1)
            {
                next.From += (first.Pulses - 1) * first.Interval;
                next.Until += (first.Pulses - 1) * first.Interval;
                next.Pulses = 1;
                first.Pulses--;
            }
            else
            {
                float width = first.Until - first.From;
                next.From = first.From + width * .55f;
                first.Until = first.From + width * .45f;
            }
            stages.Insert(index + 1, next);
            return BodyConstructionEdits.Stages(session.Draft, stages.ToArray());
        }));
        if (body.Responses?.Any(rule => rule.Result is BodyActionResult.Contact or BodyActionResult.Blocked) == true)
            Row("StageEarlyRecovery", T("StageEarlyRecoveryHint"));
    }

    /// <summary>候选阶段完整编译并计算费用后才写入草稿，非法输入保留原资料。</summary>
    /// <param name="key">已本地化的参数名。</param><param name="value">当前数值。</param>
    /// <param name="edit">只修改独立候选阶段的命令。</param>
    private void StageNumber(string key, float value, Action<BodyActionStage, float> edit)
    {
        var row = Row(key);
        FloatInput(row.UseInlineControls(96f), key, value, next => ApplyConstructionEdit(() =>
        {
            BodyActionStage[] stages = ConstructionEditing.Copy(session.Draft).Body.Stages;
            edit(stages[bodyStageSelection], next);
            return BodyConstructionEdits.Stages(session.Draft, stages);
        }), true, 96f, draftChange: false);
    }

    /// <summary>逐段覆盖与全招作用使用相同规则，关闭覆盖即恢复身体及整招继承。</summary>
    /// <param name="body">当前身体草稿。</param>
    private void BodyStageImpact(BodyActionDefinition body)
    {
        if (body.Stages?.Length is not > 0) return;
        Row("StageImpact", T("StageImpactHint"));
        BodyActionStage stage = ChooseBodyStage(body);
        Toggle("StageImpactOverride", stage.Impact != null, value => stage.Impact = value
            ? JsonConvert.DeserializeObject<ConstructionImpact>(JsonConvert.SerializeObject(
                body.Impact ?? session.Draft.Impact ?? new ConstructionImpact())) : null);
        if (stage.Impact != null) ImpactRows(stage.Impact, "");
    }
}
