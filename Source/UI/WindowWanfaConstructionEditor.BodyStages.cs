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
            Choice("BodyStage", StageLabel(bodyStageSelection), () =>
                menu.Show(T("BodyStage"), Enumerable.Range(0, body.Stages.Length).Select(index => new UiOptionMenuOption
                {
                    Label = StageLabel(index), Selected = index == bodyStageSelection,
                    Select = () => { bodyStageSelection = index; Refresh(); }
                }).ToArray()));
            return body.Stages[bodyStageSelection];

            string StageLabel(int index) => string.Format(T("BodyStageNumber"), index + 1) + " / " + body.Stages[index].Id;
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
            ConnectionText("StageName", stage.Id, value =>
            {
                if (value != stage.Id) EditStage((next, index) => next[index].Id = value);
            }, draftChange: false);
            StagePose("StageFromPose", stage.From, (selected, value) => selected.From = value);
            StagePose("StageUntilPose", stage.Until, (selected, value) => selected.Until = value);
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
        FloatInput(row.UseInlineControls(96f), key, value, next => EditStage((stages, index) => edit(stages[index], next)),
            true, 96f, draftChange: false);
    }

    /// <summary>按当前已命名姿态设定这次的秒数，后续可独立编辑段落时间。</summary>
    /// <param name="key">入口名称。</param><param name="time">本次已有的时间。</param><param name="edit">写入选定时点。</param>
    private void StagePose(string key, float time, Action<BodyActionStage, float> edit)
    {
        var poses = session.Draft.Body.Keys.Where(pose => pose.Id != null).ToArray();
        if (poses.Length == 0) return;
        string current = poses.FirstOrDefault(pose => pose.At == time)?.Id;
        MartialChoice(key, current, poses.Select(pose => pose.Id).ToArray(), name => name ?? T("StagePoseHint"), name =>
            EditStage((stages, index) => edit(stages[index], poses.Single(pose => pose.Id == name).At)));
    }

    /// <summary>在共同候选中安排行为和报价，成功才替换草稿。</summary>
    /// <param name="edit">明确修改当前阶段。</param>
    private void EditStage(Action<BodyActionStage[], int> edit) => ApplyConstructionEdit(() =>
    {
        BodyActionStage[] stages = BodyConstructionEdits.CopyStages(session.Draft.Body.Stages);
        edit(stages, bodyStageSelection);
        return BodyConstructionEdits.Stages(session.Draft, stages);
    });

    /// <summary>逐段覆盖与全招作用使用相同规则，关闭覆盖即恢复身体及整招继承。</summary>
    /// <param name="body">当前身体草稿。</param>
    private void BodyStageImpact(BodyActionDefinition body)
    {
        if (body.Stages?.Length is not > 0) return;
        Row("StageImpact", T("StageImpactHint"));
        BodyActionStage stage = ChooseBodyStage(body);
        BodyMotionToggle("StageImpactOverride", stage.Impact != null, value => EditStage((stages, index) => stages[index].Impact = value
            ? JsonConvert.DeserializeObject<ConstructionImpact>(JsonConvert.SerializeObject(
                body.Impact ?? session.Draft.Impact ?? new ConstructionImpact())) : null));
        if (stage.Impact != null) ImpactRows(stage.Impact, "");
    }
}
