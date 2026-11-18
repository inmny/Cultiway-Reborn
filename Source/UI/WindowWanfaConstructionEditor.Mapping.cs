using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Skills;

namespace Cultiway.UI;

public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>双门只出现在需要互相引用的命名步骤中。</summary>
    /// <param name="kind">实际操作种类。</param>
    private static bool IsMapping(ConstructionOperationKind kind) =>
        kind is ConstructionOperationKind.MapSpace or ConstructionOperationKind.MapActor or ConstructionOperationKind.MapArtifact;

    /// <summary>从当前真实部位开始，选完所需门才新增完整步骤。</summary>
    /// <param name="node">现选声明。</param>
    private void AddMappingRow(ConstructionNode node)
    {
        string path = NodePath(node);
        var subject = previewPlan?.Nodes.FirstOrDefault(form => form.Path == path);
        var options = subject == null ? Array.Empty<UiOptionMenuOption>() : ConstructionOperations.All.Where(contract =>
            IsMapping(contract.Kind) && MappingSubject(contract.Kind, subject) &&
            MappingGates(contract.Kind, subject.Path).Count() >= (contract.Kind == ConstructionOperationKind.MapActor ? 1 : 2))
            .Select(contract => new UiOptionMenuOption
            {
                Label = T("MotionKind." + contract.Kind), Select = () => ChooseGates(contract.Kind)
            }).ToArray();
        if (options.Length == 0)
            Row("AddMapping", T("MappingUnavailable"));
        else
            ActionRow("AddMapping", T("AddMappingHint"), () => menu.Show(T("AddMapping"), options));

        void ChooseGates(ConstructionOperationKind kind)
        {
            if (kind == ConstructionOperationKind.MapActor) SelectExit(path);
            else ChooseMappingPart("MappingEntrance", MappingGates(kind, path).ToArray(), null, SelectExit);

            void SelectExit(string entrance) => ChooseMappingPart("MappingExit", MappingGates(kind, path, entrance).ToArray(),
                null, exit => EditMapping(candidate =>
                {
                    string id = "transfer";
                    int suffix = 1;
                    while (candidate.Steps.Any(step => step.Id == id)) id = "transfer" + suffix++;
                    var step = new ConstructionStep { Id = id, Operation = new ConstructionOperation { Kind = kind }, Inputs = new() };
                    step.Inputs["part"] = new ConstructionOutputReference { Node = path };
                    if (kind != ConstructionOperationKind.MapActor)
                        step.Inputs["entrance"] = new ConstructionOutputReference { Node = entrance };
                    step.Inputs["exit"] = new ConstructionOutputReference { Node = exit };
                    candidate.Steps = candidate.Steps.Append(step).ToArray();
                }, session.Draft.Steps.Length));
        }
    }

    /// <summary>入口和出口直接指向实际部位；等待结果仍可用共同依赖输入。</summary>
    /// <param name="step">选中的双门步骤。</param>
    private void MappingRows(ConstructionStep step)
    {
        ConstructionOperation operation = step.Operation;
        var kind = operation.Kind;
        Row("MappingRules", T("MappingHint." + kind));
        string Path(string port) => step.Inputs.TryGetValue(port, out var reference) ? reference.Node : null;
        string subject = Path("part"), entrance = Path("entrance"), exit = Path("exit");
        if (kind == ConstructionOperationKind.MapActor) entrance = subject;

        Input("part", kind == ConstructionOperationKind.MapActor ? "MappingEntrance" : "MappingSubject",
            previewPlan?.Nodes.Where(form => MappingSubject(kind, form) && form.Path != exit &&
                (kind == ConstructionOperationKind.MapActor || GateFits(kind, form.Path, entrance) && GateFits(kind, form.Path, exit))).ToArray());
        if (kind != ConstructionOperationKind.MapActor)
            Input("entrance", "MappingEntrance", MappingGates(kind, subject, exit).ToArray());
        Input("exit", "MappingExit", MappingGates(kind, subject, entrance).ToArray());
        StepInputRows(step, ConstructionOperations.Describe(kind).Inputs.First(port => port.Name == "after"));
        NumberRow("OperationAt", operation.At, (op, next) => op.At = next);
        NumberRow("MappingRadius", operation.MappingRadius ?? 1f, (op, next) => op.MappingRadius = next);
        if (kind != ConstructionOperationKind.MapActor) return;
        SwitchRow("MappingCapacityEnabled", operation.MappingCapacity.HasValue, (op, on) => op.MappingCapacity = on ? 1 : null);
        if (operation.MappingCapacity.HasValue)
            ContactInteger("MappingCapacity", operation.MappingCapacity.Value, value => Edit(op => op.MappingCapacity = value), draftChange: false);
        SwitchRow("MappingWaitEnabled", operation.MappingWaitSeconds.HasValue,
            (op, on) => op.MappingWaitSeconds = on ? Math.Min(.5f, session.Draft.Lifetime) : null);
        if (operation.MappingWaitSeconds.HasValue)
            NumberRow("MappingWaitSeconds", operation.MappingWaitSeconds.Value, (op, next) => op.MappingWaitSeconds = next);

        void Input(string port, string label, CompiledForm[] available)
        {
            step.Inputs.TryGetValue(port, out var current);
            Choice(label, ReferenceLabel(current), () => ChooseMappingPart(label, available ?? Array.Empty<CompiledForm>(), current?.Node,
                value => EditMappingStep(step, next => next.Inputs[port] = new ConstructionOutputReference { Node = value })));
        }

        void Edit(Action<ConstructionOperation> edit) => EditMappingStep(step, next => edit(next.Operation));
        void NumberRow(string key, float value, Action<ConstructionOperation, float> set) =>
            FloatInput(Row(key).UseInlineControls(104f), key, value, next => Edit(op => set(op, next)), enabled: true, width: 100f, draftChange: false);
        void SwitchRow(string key, bool value, Action<ConstructionOperation, bool> set)
        {
            var toggle = UiElements.CreateIconToggle(Row(key).UseInlineControls(32f), key, UiIcons.Options, value, 28f, 22f);
            toggle.onValueChanged.AddListener(next => Edit(op => set(op, next)));
        }
    }

    /// <summary>只列这类搬运能接受的真实对象；法宝必须是根部借用的原载体。</summary>
    /// <param name="kind">双门类型。</param><param name="form">候选部位。</param>
    private bool MappingSubject(ConstructionOperationKind kind, CompiledForm form) => kind switch
    {
        ConstructionOperationKind.MapSpace => !form.BodyWindow && form.Resource.Meshes.Length > 0,
        ConstructionOperationKind.MapActor => !form.BodyWindow && form.AffectsActors && form.Resource.Contacts.Length > 0,
        ConstructionOperationKind.MapArtifact => form.Path == "root" && session.Draft.Binding is { Anchor: AbilityAnchor.Carrier, Follow: true },
        _ => false
    };

    /// <summary>列出不会把两端混成同一点的实际部位。</summary>
    /// <param name="kind">双门类型。</param><param name="subject">当前搬运对象。</param><param name="other">已经选中的另一端。</param>
    private IEnumerable<CompiledForm> MappingGates(ConstructionOperationKind kind, string subject, string other = null) =>
        previewPlan?.Nodes.Where(form => form.Path != other && GateFits(kind, subject, form.Path)) ?? Enumerable.Empty<CompiledForm>();

    /// <summary>生成形体不能用自己或随自己移动的部位作门。</summary>
    /// <param name="kind">双门类型。</param><param name="subject">搬运对象的实际路径。</param><param name="gate">候选门的实际路径。</param>
    private static bool GateFits(ConstructionOperationKind kind, string subject, string gate) => subject == null || gate == null ||
        gate != subject && (kind != ConstructionOperationKind.MapSpace || !gate.StartsWith(subject + "/", StringComparison.Ordinal));

    /// <summary>复用同一个部位选择菜单，成功后再进入下一步。</summary>
    /// <param name="key">当前要选的角色。</param><param name="forms">实际候选部位。</param>
    /// <param name="current">已选部位。</param><param name="select">选择时执行。</param>
    private void ChooseMappingPart(string key, CompiledForm[] forms, string current, Action<string> select) => menu.Show(T(key),
        forms.Select(form => new UiOptionMenuOption
        {
            Label = EventPartName(form, Array.IndexOf(previewPlan.Nodes, form)) + " [" + form.Path + "]",
            Selected = form.Path == current, Select = () => select(form.Path)
        }).ToArray(), true);

    /// <summary>沿原步骤编号写独立候选，拒绝时保留已有整招。</summary>
    /// <param name="step">正在编辑的步骤。</param><param name="edit">要改的输入或参数。</param>
    private void EditMappingStep(ConstructionStep step, Action<ConstructionStep> edit) =>
        EditMapping(candidate => edit(candidate.Steps.First(item => item.Id == step.Id)));

    /// <summary>只有完整候选通过实际编译才写入一次撤销历史。</summary>
    /// <param name="edit">对独立候选的修改。</param><param name="selected">新增后选择的行。</param>
    private void EditMapping(Action<AbilityDefinition> edit, int? selected = null) => ApplyConstructionEdit(() =>
    {
        AbilityDefinition candidate = ConstructionEditing.Copy(session.Draft);
        edit(candidate);
        access.Validate(candidate);
        if (selected.HasValue) stepChoice = selected.Value;
        return candidate;
    });
}
