using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>命名步骤与部位操作使用同一草稿、共同约定和实际参数表单。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>仅保存当前列表位置，撤销后重新绑定实际资料。</summary>
    private int stepChoice, nativeChoice, operationScope;

    /// <summary>在原窗口内编辑混合操作和有明确输出依赖的命名步骤。</summary>
    private void StepsPage()
    {
        Row("StepsRules", T("StepsHint"));
        if (parts.Count == 0) { Row("BodyOnly", T("BodyHint")); return; }
        ConstructionNode node = parts[part];
        Choice("StepScope", T(operationScope == 0 ? "StepScope.Native" : "StepScope.Named"), () => menu.Show(T("StepScope"),
            Enumerable.Range(0, 2).Select(index => new UiOptionMenuOption
            {
                Label = T(index == 0 ? "StepScope.Native" : "StepScope.Named"), Selected = operationScope == index,
                Select = () => { operationScope = index; Refresh(); }
            }).ToArray()));
        if (operationScope == 0)
        {
            ActionRow("AddNativeOperation", T("NativeOperationHint"), () => ChooseOperationKind(node, kind => Change(() =>
            {
                ConstructionOperation operation = NewProcessOperation(node, kind, session.Draft.Lifetime);
                if (!ConstructionOperations.Describe(kind).RequiresCondition) { operation.When = null; operation.TargetScale = null; }
                nativeChoice = node.Operations.Length;
                node.Operations = node.Operations.Concat(new[] { operation }).ToArray();
            })));
            if (node.Operations.Length > 0)
            {
                nativeChoice = Math.Min(nativeChoice, node.Operations.Length - 1);
                ConstructionOperation operation = node.Operations[nativeChoice];
                Choice("NativeOperation", OperationLabel(operation, nativeChoice), () => menu.Show(T("NativeOperation"),
                    node.Operations.Select((item, index) => new UiOptionMenuOption
                    {
                        Label = OperationLabel(item, index), Selected = item == operation,
                        Select = () => { nativeChoice = index; Refresh(); }
                    }).ToArray()));
                NameParameter("OperationName", operation.Id, value => RenameNative(node, operation, value));
                ActionRow("RemoveNativeOperation", T("RemoveOperationHint"), () => Change(() =>
                    node.Operations = node.Operations.Where(item => item != operation).ToArray()));
                OperationRows(node, operation);
            }
            return;
        }
        Row("NamedSteps", T("NamedStepsHint"));
        ActionRow("AddStep", T("NamedStepsHint"), () => ChooseOperationKind(node, kind => Change(() =>
        {
            string id = "step"; int suffix = 1;
            while (session.Draft.Steps.Any(item => item.Id == id)) id = "step" + suffix++;
            ConstructionOperation operation = NewProcessOperation(node, kind, session.Draft.Lifetime);
            operation.When = null;
            var step = new ConstructionStep { Id = id, Operation = operation, Inputs = new Dictionary<string, ConstructionOutputReference>
            { ["part"] = new ConstructionOutputReference { Node = NodePath(node), Output = "part" } } };
            stepChoice = session.Draft.Steps.Length;
            session.Draft.Steps = session.Draft.Steps.Concat(new[] { step }).ToArray();
        })));
        if (session.Draft.Steps.Length == 0) return;
        stepChoice = Math.Min(stepChoice, session.Draft.Steps.Length - 1);
        ConstructionStep selected = session.Draft.Steps[stepChoice];
        Choice("StepChoice", StepLabel(selected), () => menu.Show(T("StepChoice"), session.Draft.Steps.Select((item, index) => new UiOptionMenuOption
        {
            Label = StepLabel(item), Selected = selected == item,
            Select = () => { stepChoice = index; Refresh(); }
        }).ToArray(), true));
        NameParameter("StepName", selected.Id, value => RenameStep(selected, value));
        ActionRow("RemoveStep", T("RemoveOperationHint"), () => Change(() =>
            session.Draft.Steps = session.Draft.Steps.Where(item => item != selected).ToArray()));
        foreach (ConstructionOperationPort input in ConstructionOperations.Describe(selected.Operation.Kind).Inputs)
        {
            if (input.Name is "part" or "after" or "event" or "parent" or "connection" or "worldMaterials" or "water")
                StepInputRows(selected, input);
            else Row("StepInput." + input.Name, T("StepContextInput"));
        }
        ConstructionNode target = StepNode(selected, new HashSet<ConstructionStep>());
        if (target == null) Row("StepTargetMissing", T("StepTargetMissingHint"));
        else OperationRows(target, selected.Operation);
    }

    /// <summary>只列已有执行约定，资源约束继续由共同编译确认。</summary>
    /// <param name="node">实际操作对象。</param><param name="select">选择后的草稿命令。</param>
    private void ChooseOperationKind(ConstructionNode node, Action<ConstructionOperationKind> select)
        => menu.Show(T("OperationKind"), ConstructionOperations.All.Where(contract =>
            (contract.Kind != ConstructionOperationKind.Animate || FormResources.Load(node.Form).Description.Motions.Length > 0) &&
            (contract.Kind != ConstructionOperationKind.Ray || FormResources.Load(node.Form).Span != null) &&
            (contract.Kind != ConstructionOperationKind.Fracture || FormResources.Load(node.Form).Meshes.Length > 1))
            .Select(contract => new UiOptionMenuOption { Label = T("MotionKind." + contract.Kind), Select = () => select(contract.Kind) }).ToArray());

    /// <summary>统一编辑开始时点、附加真实条件和原有全部参数。</summary>
    /// <param name="node">实际形体资料。</param><param name="operation">本次明确选择的操作。</param>
    private void OperationRows(ConstructionNode node, ConstructionOperation operation)
    {
        Number("OperationAt", operation.At, value => operation.At = value);
        Toggle("OperationCondition", operation.When != null, value =>
        {
            operation.When = value ? new ConstructionCondition() : null;
            if (!value && !session.Draft.Steps.Any(step => step.Operation == operation))
            { operation.TargetScale = null; operation.BlendSeconds = null; }
        });
        OperationConditionRows(operation);
        OperationParameters(node, operation);
    }

    /// <summary>每个输入只提供编译器实际接受的输出，保留阶段与集合选择。</summary>
    /// <param name="step">当前步骤。</param><param name="port">实际约定中的输入。</param>
    private void StepInputRows(ConstructionStep step, ConstructionOperationPort port)
    {
        step.Inputs.TryGetValue(port.Name, out ConstructionOutputReference reference);
        Choice("StepInput." + port.Name, ReferenceLabel(reference), () => ChooseStepInput(step, port));
        if (reference == null) return;
        Choice("StepInputStage." + port.Name, T("StepStage." + reference.Stage), () => menu.Show(T("StepInputStage." + port.Name),
            Enum.GetValues(typeof(ConstructionOutputStage)).Cast<ConstructionOutputStage>()
                .Where(stage => reference.Step != null || reference.Operation != null || stage != ConstructionOutputStage.Completed)
                .Select(stage => new UiOptionMenuOption { Label = T("StepStage." + stage), Selected = reference.Stage == stage,
                    Select = () => Change(() => reference.Stage = stage) }).ToArray()));
        Choice("StepInputMode." + port.Name, T("StepMode." + reference.Mode), () => menu.Show(T("StepInputMode." + port.Name),
            Enum.GetValues(typeof(ConstructionInputMode)).Cast<ConstructionInputMode>()
                .Where(mode => port.Name != "part" || mode != ConstructionInputMode.All)
                .Select(mode => new UiOptionMenuOption { Label = T("StepMode." + mode), Selected = reference.Mode == mode,
                    Select = () => Change(() => reference.Mode = mode) }).ToArray()));
    }

    /// <summary>从部位、命名部位操作和命名步骤取得真正的输出引用。</summary>
    /// <param name="step">接收输入的步骤。</param><param name="port">实际输入约定。</param>
    private void ChooseStepInput(ConstructionStep step, ConstructionOperationPort port)
    {
        var options = new List<UiOptionMenuOption>();
        if (port.Name != "part") options.Add(new UiOptionMenuOption { Label = T("StepUnbound"), Select = () => Change(() => step.Inputs.Remove(port.Name)) });
        bool world = port.Name is "worldMaterials" or "water";
        if (!world)
        {
            string[] paths = previewPlan == null ? parts.Select(NodePath).ToArray() : previewPlan.Nodes.Select(node => node.Path).ToArray();
            foreach (string path in paths)
            {
                Add(path, null, null, "part", ConstructionValueKind.Part);
                Add(path, null, null, "structure", ConstructionValueKind.Structure);
                Add(path, null, null, "connection", ConstructionValueKind.Relation);
            }
        }
        foreach (ConstructionNode node in parts)
            foreach (ConstructionOperation operation in node.Operations.Where(item => !string.IsNullOrWhiteSpace(item.Id)))
            {
                string[] paths = previewPlan == null ? new[] { NodePath(node) } : previewPlan.Nodes
                    .Where(actual => actual.DeclarationPath == NodePath(node) && actual.FragmentSource < 0)
                    .Select(actual => actual.Path).ToArray();
                foreach (string path in paths) Outputs(path, operation.Id, null, operation);
            }
        foreach (ConstructionStep producer in session.Draft.Steps.Where(item => item != step))
            Outputs(null, null, producer.Id, producer.Operation);
        menu.Show(T("StepInput." + port.Name), options.ToArray(), true);

        /// <summary>只接受世界交接完成后确实产出的材料。</summary>
        /// <param name="node">部位路径。</param><param name="operationId">部位操作名称。</param>
        /// <param name="producer">命名步骤。</param><param name="operation">真实操作。</param>
        void Outputs(string node, string operationId, string producer, ConstructionOperation operation)
        {
            if (world && operation.Kind != ConstructionOperationKind.Leave && operation.WorldMatter == null) return;
            foreach (ConstructionOperationPort output in ConstructionOperations.Describe(operation.Kind).Outputs)
                Add(node, operationId, producer, output.Name, output.Kind, output.Collection);
        }

        /// <summary>候选项只保存值，应用时写入独立的新引用。</summary>
        /// <param name="node">部位路径。</param><param name="operation">部位操作名称。</param><param name="producer">步骤名称。</param>
        /// <param name="output">输出名。</param><param name="kind">真实资料种类。</param><param name="collection">是否为集合。</param>
        void Add(string node, string operation, string producer, string output, ConstructionValueKind kind, bool collection = false)
        {
            if (!port.Kinds.Contains(kind)) return;
            var reference = new ConstructionOutputReference { Node = node, Operation = operation, Step = producer, Output = output,
                Mode = collection ? ConstructionInputMode.Each : ConstructionInputMode.Single };
            options.Add(new UiOptionMenuOption { Label = ReferenceLabel(reference), Select = () => Change(() => step.Inputs[port.Name] = reference) });
        }
    }

    /// <summary>名称修改与引用修改处于同一次撤销，拒绝空名及重名。</summary>
    /// <param name="step">要改名的步骤。</param><param name="value">用户输入。</param>
    private void RenameStep(ConstructionStep step, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || session.Draft.Steps.Any(item => item != step && item.Id == value))
        { state.text = T("OperationNameInvalid"); return; }
        if (step.Id == value) return;
        Change(() =>
        {
            string previous = step.Id; step.Id = value;
            foreach (ConstructionOutputReference input in session.Draft.Steps.SelectMany(item => item.Inputs.Values))
                if (input.Step == previous) input.Step = value;
        });
    }

    /// <summary>部位操作改名时保持现有步骤的实际指向，引用中的名称不能被直接清空。</summary>
    /// <param name="node">所属部位。</param><param name="operation">要命名的操作。</param><param name="value">用户输入。</param>
    private void RenameNative(ConstructionNode node, ConstructionOperation operation, string value)
    {
        value = string.IsNullOrWhiteSpace(value) ? null : value;
        if (operation.Id == value) return;
        ConstructionOutputReference[] references = session.Draft.Steps.SelectMany(item => item.Inputs.Values)
            .Where(input => operation.Id != null && input.Operation == operation.Id && DeclarationNode(input.Node) == node).ToArray();
        if (value == null && references.Length > 0 || value != null && node.Operations.Any(item => item != operation && item.Id == value))
        { state.text = T("OperationNameInvalid"); return; }
        Change(() => { operation.Id = value; foreach (ConstructionOutputReference input in references) input.Operation = value; });
    }

    /// <summary>名称沿用现有行内输入，不引入另外的编辑窗口。</summary>
    /// <param name="key">字段名称。</param><param name="value">当前名称。</param><param name="set">实际命名命令。</param>
    private void NameParameter(string key, string value, Action<string> set)
    {
        InputField field = UiElements.CreateInput(Row(key).UseInlineControls(176f), key, value ?? "", T(key), 172f, 22f);
        field.characterLimit = 128;
        field.onEndEdit.AddListener(next => { if (!refreshing) { set(next); if (field != null) field.SetTextWithoutNotify(value ?? ""); } });
    }

    /// <summary>依赖尚未完成编辑时仍能找到原声明，为参数表单提供资源。</summary>
    /// <param name="step">当前步骤。</param><param name="visited">仅限本次查找的循环记录。</param>
    private ConstructionNode StepNode(ConstructionStep step, HashSet<ConstructionStep> visited)
    {
        if (!visited.Add(step) || !step.Inputs.TryGetValue("part", out ConstructionOutputReference reference)) return null;
        if (reference.Node != null) return DeclarationNode(reference.Node);
        ConstructionStep producer = session.Draft.Steps.FirstOrDefault(item => item.Id == reference.Step);
        return producer == null ? null : StepNode(producer, visited);
    }

    /// <summary>展开部位或原部位路径回到所属声明，生成分件继续由共同编译核对。</summary>
    /// <param name="path">实际路径。</param>
    private ConstructionNode DeclarationNode(string path)
    {
        var actual = previewPlan?.Nodes.FirstOrDefault(node => node.Path == path);
        if (actual != null)
        {
            if (actual.FragmentSource >= 0) actual = previewPlan.Nodes[actual.FragmentSource];
            path = actual.DeclarationPath ?? path;
        }
        return path == null ? null : parts.Where(node => path == NodePath(node) || path.StartsWith(NodePath(node) + "/", StringComparison.Ordinal))
            .OrderByDescending(node => NodePath(node).Length).FirstOrDefault();
    }

    /// <summary>取得原声明的完整路径，不依赖草稿当前能否编译成功。</summary>
    /// <param name="node">原声明中的部位。</param>
    private string NodePath(ConstructionNode node)
    {
        return Find(session.Draft.Root, "root");
        string Find(ConstructionNode current, string path)
        {
            if (current == node) return path;
            foreach (ConstructionNode child in current.Children)
            { string found = Find(child, path + "/" + child.Id); if (found != null) return found; }
            return null;
        }
    }

    /// <summary>普通操作显示作者给定名称或原有顺序和真实操作名。</summary>
    /// <param name="operation">当前操作。</param><param name="index">原声明顺序。</param>
    private static string OperationLabel(ConstructionOperation operation, int index) => string.IsNullOrWhiteSpace(operation.Id)
        ? string.Format(T("MotionChoiceName"), index + 1, T("MotionKind." + operation.Kind)) : operation.Id + " · " + T("MotionKind." + operation.Kind);

    /// <summary>命名步骤保持作者给定名称并注明实际操作。</summary>
    /// <param name="step">当前步骤。</param>
    private static string StepLabel(ConstructionStep step) => step.Id + " · " + T("MotionKind." + step.Operation.Kind);

    /// <summary>明确显示部位、操作和输出，不把缺失引用伪装成当前选择。</summary>
    /// <param name="reference">已经保存的实际引用。</param>
    private string ReferenceLabel(ConstructionOutputReference reference)
    {
        if (reference == null) return T("StepUnbound");
        ConstructionNode node = DeclarationNode(reference.Node);
        string source = reference.Step ?? (node == null ? reference.Node : PartName(parts.IndexOf(node)) + " [" + reference.Node + "]");
        if (reference.Operation != null) source += " / " + reference.Operation;
        return source + " · " + T("StepOutput." + (reference.Output ?? "part"));
    }
}
