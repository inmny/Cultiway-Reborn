using System;
using System.Globalization;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using Cultiway.Utils.Extension;
using Cultiway.UI.Prefab;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>构造参数、材料与路径的普通表单；不建立节点连线或多招组合。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>建立实际支持的基本参数。</summary>
    private void Properties()
    {
        Number("Power", session.Draft.Power, value => session.Draft.Power = value);
        Number("Lifetime", session.Draft.Lifetime, value => session.Draft.Lifetime = value);
        Number("Cooldown", session.Draft.Cooldown, value => session.Draft.Cooldown = value);
        Choice("Resource", T("Resource." + session.Draft.Resource), () => menu.Show(T("Resource"),
            new[] { "none", "wakan", "vigor", "mana", "stamina" }.Select(resource => new UiOptionMenuOption
            { Label = T("Resource." + resource), Selected = session.Draft.Resource == resource,
                Select = () => Change(() => { session.Draft.Resource = resource; if (resource == "none") session.Draft.Cost = 0; }) }).ToArray()));
        Number("Cost", session.Draft.Cost, value => session.Draft.Cost = value);
        Toggle("ExplicitRange", session.Draft.TargetRange.HasValue, value => session.Draft.TargetRange = value ? 12f : (float?)null);
        if (session.Draft.TargetRange.HasValue) Number("TargetRange", session.Draft.TargetRange.Value, value => session.Draft.TargetRange = value);
        Row("SelectionRange", T("SelectionRangeHint"));
        MagnitudeRows();
        if (parts.Count == 0) { Row("BodyOnly", T("BodyHint")); return; }
        ConstructionNode node = parts[part];
        Choice("Form", LMTools.GetOrFallback("Cultiway.Construction.Form." + node.Form, node.Form), () => menu.Show(T("Form"),
            FormResources.Ids.Where(id => FormResources.Load(id).Meshes.Length > 0).Select(id => new UiOptionMenuOption
            { Label = LMTools.GetOrFallback("Cultiway.Construction.Form." + id, id), Selected = node.Form == id,
                Select = () => Change(() => node.Form = id) }).ToArray(), true));
        Number("Scale", node.Scale, value => node.Scale = value);
        Toggle("ExplicitVolume", node.Volume.HasValue, value => node.Volume = value ? 1f : (float?)null);
        if (node.Volume.HasValue) Number("Volume", node.Volume.Value, value => node.Volume = value);
        foreach (ConstructionOperation operation in node.Operations.Where(o =>
                     o.Kind == ConstructionOperationKind.ConvertMatter && o.When == null))
        {
            Number("MatterAt", operation.At, value => operation.At = value);
            MatterConversionParameters(operation);
            ActionRow("RemoveMatterConversion", "",
                () => Change(() => node.Operations = node.Operations.Where(item => item != operation).ToArray()));
        }

        if (!node.Operations.Any(o => o.Kind == ConstructionOperationKind.ConvertMatter && o.When == null))
            ActionRow("AddMatterConversion", "", () => Change(() => node.Operations = node.Operations.Concat(new[]
            {
                new ConstructionOperation { Kind = ConstructionOperationKind.ConvertMatter, Matter = "ice", At = 0 }
            }).ToArray()));
        foreach (ConstructionOperation operation in node.Operations.Where(o =>
                     o.Kind == ConstructionOperationKind.Flow && o.When == null))
        {
            Number("FlowAt", operation.At, value => operation.At = value);
            FlowParameters(operation.Flow);
            ActionRow("RemoveFlow", T("RemoveFlowHint"),
                () => Change(() => node.Operations = node.Operations.Where(item => item != operation).ToArray()));
        }

        if (!node.Operations.Any(o => o.Kind == ConstructionOperationKind.Flow))
            ActionRow("AddFlow", T("AddFlowHint"), () => Change(() => node.Operations = node.Operations.Concat(new[] { new ConstructionOperation { Kind = ConstructionOperationKind.Flow, Flow = new ConstructionFlow() } }).ToArray()));
        foreach (ConstructionOperation operation in node.Operations.Where(o => o.Kind == ConstructionOperationKind.Recover && o.When == null))
        { Number("RecoverAt", operation.At, value => operation.At = value); RecoveryParameters(operation);
            ActionRow("RemoveRecovery", T("RemoveRecoveryHint"), () => Change(() => node.Operations = node.Operations.Where(item => item != operation).ToArray())); }
        if (!node.Operations.Any(o => o.Kind == ConstructionOperationKind.Recover))
            ActionRow("AddRecovery", T("AddRecoveryHint"), () => Change(() => node.Operations = node.Operations.Concat(new[] { new ConstructionOperation
            { Kind = ConstructionOperationKind.Recover, At = Mathf.Max(0, (node.DisappearAt ?? session.Draft.Lifetime) - 1f), Seconds = Mathf.Min(.6f, (node.DisappearAt ?? session.Draft.Lifetime) * .5f), Recovery = new ConstructionRecovery()
                    }
                }).ToArray()));
        foreach (ConstructionOperation operation in node.Operations.Where(o =>
                     o.Kind == ConstructionOperationKind.Leave && o.When == null))
        {
            Number("LeaveAt", operation.At, value => operation.At = value);
            ResidueParameters(operation);
            ActionRow("RemoveResidue", T("RemoveResidueHint"),
                () => Change(() => node.Operations = node.Operations.Where(item => item != operation).ToArray()));
        }

        if (!node.Operations.Any(o => o.Kind == ConstructionOperationKind.Leave))
            ActionRow("AddResidue", T("AddResidueHint"), () => Change(() => node.Operations = node.Operations.Concat(
                new[]
                {
                    new ConstructionOperation
                    {
                        Kind = ConstructionOperationKind.Leave,
                        At = Mathf.Max(0, (node.DisappearAt ?? session.Draft.Lifetime) - .1f),
                        Residue = new ConstructionResidue() } }).ToArray()));
        Number("Strength", node.Strength, value => node.Strength = value);
        Number("AppearAt", node.AppearAt ?? 0f, value => node.AppearAt = value);
        Number("DisappearAt", node.DisappearAt ?? session.Draft.Lifetime, value => node.DisappearAt = value);
        if (node.Hold != null)
        {
            Number("HoldAt", node.Hold.At, value => node.Hold.At = value);
            Number("HoldSeconds", node.Hold.Seconds, value => node.Hold.Seconds = value);
        }
        for (int i = 0; i < 3; i++) { int axis = i; Number(new[] { "FormRotationX", "FormRotationY", "FormRotationZ" }[i], node.Rotation[i], value => node.Rotation[axis] = value); }
        AbilityMatter matter = AbilityMatters.Resolve(node.Matter, node.State, "编辑部位");
        Choice("Material", MaterialName(matter), () => menu.Show(T("Material"), AbilityMatters.All.Select(item =>
            new UiOptionMenuOption
            {
                Label = MaterialName(item),
                Selected = item.Id == matter.Id && item.State == matter.State,
                Select = () => Change(() => { node.Matter = item.Id; node.State = item.State; node.Surface = null; })
            }).ToArray(), true));
        Toggle("Dye", node.Tint != null, value => node.Tint = value ? new[] { 1f, 1f, 1f } : null);
        if (node.Tint != null)
            for (int i = 0; i < 3; i++) { int channel = i; Number(new[] { "Red", "Green", "Blue" }[i], node.Tint[i], value => node.Tint[channel] = value); }
    }

    /// <summary>显示当前飞行与曲线的实际资料。</summary>
    private void PathPage()
    {
        if (parts.Count == 0) { Row("BodyOnly", T("BodyHint")); return; }
        ConstructionNode node = parts[part];
        ConstructionOperation fly = SelectedFlight(node);
        if (fly == null)
        { ActionRow("AddPath", T("AddPathHint"), () => AddPath(node)); return; }
        ConstructionOperation[] flights = node.Operations.Where(o => o.Kind == ConstructionOperationKind.Fly).ToArray();
        if (flights.Length > 1) Choice("FlightChoice", FlightName(fly, flightChoice), () => menu.Show(T("FlightChoice"),
            flights.Select((item, index) => new UiOptionMenuOption { Label = FlightName(item, index), Selected = item == fly,
                Select = () => { flightChoice = index; Refresh(); } }).ToArray()));
        if (fly.When != null) Row("ConditionalFlight", T("ConditionalFlightHint"));
        Number("FlyAt", fly.At, value => fly.At = value);
        for (int i = 0; i < 3; i++) { int axis = i; Number(new[] { "VelocityX", "VelocityY", "VelocityZ" }[i], fly.Vector[i], value => fly.Vector[axis] = value); }
        Vector3 velocity = new(fly.Vector[0], fly.Vector[1], fly.Vector[2]);
        Number("FlightSpeed", velocity.magnitude, value =>
        {
            Vector3 next = velocity.sqrMagnitude > .0000001f ? velocity.normalized * value : Vector3.forward * value;
            fly.Vector = new[] { next.x, next.y, next.z };
        });
        Number("Homing", fly.TurnRate, value => fly.TurnRate = value);
        FlightPhysicsRows(fly, "");
        Toggle("InheritVelocity", fly.InheritVelocity, value => fly.InheritVelocity = value);
        if (fly.Path == null) { ActionRow("AddPath", T("AddPathHint"), () => AddPath(node)); return; }
        ConstructionPath path = fly.Path;
        Choice("Space", T(path.Space == ConstructionPathSpace.Local ? "Local" : "TargetLine"), () => menu.Show(T("Space"),
            new[] { ConstructionPathSpace.Local, ConstructionPathSpace.TargetLine }.Select(space => new UiOptionMenuOption
            { Label = T(space == ConstructionPathSpace.Local ? "Local" : "TargetLine"), Selected = path.Space == space, Select = () => Change(() => path.Space = space) }).ToArray()));
        Row("Coordinates", T(path.Space == ConstructionPathSpace.Local ? "LocalHint" : "TargetLineHint"));
        Toggle("Loop", path.Loop, value =>
        {
            path.Loop = value;
            if (value) path.Keys[path.Keys.Length - 1].Point = (float[])path.Keys[0].Point.Clone();
        });
        Toggle("FacePath", path.Forward != null, value => path.Forward = value ? new[] { 0f, 0f, 1f } : null);
        if (path.Forward != null) Number("TurnRate", path.TurnRate, value => path.TurnRate = value);
        Choice("Handles", T(new[] { "Point", "InHandle", "OutHandle" }[handle]), () => menu.Show(T("Handles"),
            Enumerable.Range(0, 3).Select(index => new UiOptionMenuOption
            {
                Label = T(new[] { "Point", "InHandle", "OutHandle" }[index]),
                Selected = index == handle,
                Select = () => { handle = index; Refresh(); }
            }).ToArray()));
        for (int i = 0; i < path.Keys.Length; i++)
        {
            int index = i; ConstructionPathKey key = path.Keys[i];
            WanfaEditorRow row = rows.GetNext();
            row.Setup(string.Format(T("Key"), i + 1), "", T("RemovePoint"), true,
                i > 0 && path.Keys.Length > 2 ? () => Change(() => path.Keys = path.Keys.Where((_, n) => n != index).ToArray()) : null, UiIcons.Delete);
            Transform controls = row.UseInlineControls(260f);
            FloatInput(controls, "At", key.At, value => key.At = value, i != 0, 56f);
            float[] vector = handle == 0 ? key.Point : handle == 1 ? key.In : key.Out;
            for (int axis = 0; axis < 3; axis++)
            {
                int coordinate = axis;
                FloatInput(controls, new[] { "X", "Y", "Z" }[axis], vector[axis], value => vector[coordinate] = value,
                    handle != 0 || i != 0 && (!path.Loop || i != path.Keys.Length - 1), 56f);
            }
        }
        if (path.Keys.Length < 65) ActionRow("AddPoint", T("AddPointHint"), () => Change(() =>
        {
            ConstructionPathKey last = path.Keys[path.Keys.Length - 1];
            path.Keys = path.Keys.Concat(new[] { new ConstructionPathKey { At = last.At + .25f, Point = (float[])last.Point.Clone() } }).ToArray();
        }));
    }

    /// <summary>把所选形体明确改成从实际来源投送的路径，保留其他动作和材料。</summary>
    /// <param name="node">实际修改的部位。</param>
    private void AddPath(ConstructionNode node)
    {
        ConstructionOperation fly = SelectedFlight(node);
        ConstructionOperation detached = node.Operations.FirstOrDefault(o => o.Kind == ConstructionOperationKind.Detach);
        float at = fly?.At ?? detached?.At ?? (part == 0 && session.Draft.Binding?.Anchor == AbilityAnchor.Hand
            ? session.Draft.Body.ActiveUntil : node.ActiveFrom);
        float seconds = Mathf.Min(1f, session.Draft.Lifetime - at);
        if (seconds < .001f) { state.text = T("TooLate"); return; }
        Change(() =>
        {
            if (part > 0 && detached == null && fly?.When == null)
                node.Operations = node.Operations.Concat(new[] { new ConstructionOperation { Kind = ConstructionOperationKind.Detach, At = at } }).ToArray();
            if (part == 0 && fly?.When == null && (session.Draft.Binding == null || session.Draft.Binding.Anchor == AbilityAnchor.Target))
                session.Draft.Binding = new ConstructionBinding { Anchor = AbilityAnchor.Source };
            if (fly == null)
            {
                fly = new ConstructionOperation { Kind = ConstructionOperationKind.Fly, At = at };
                node.Operations = node.Operations.Concat(new[] { fly }).ToArray();
            }
            fly.Vector = new float[3]; fly.InheritVelocity = false;
            fly.Path = new ConstructionPath
            {
                Space = ConstructionPathSpace.TargetLine,
                Keys = new[]
            {
                new ConstructionPathKey { Out = new[] { 1f / 3f, 0f, 2f } },
                new ConstructionPathKey { At = seconds, Point = new[] { 1f, 0f, 0f }, In = new[] { -1f / 3f, 0f, 2f } }
            }
            };
        });
    }

    /// <summary>复用一行的标题、说明与固定控件槽。</summary>
    /// <param name="key">标题本地化键。</param>
    /// <param name="detail">实际说明。</param>
    private WanfaEditorRow Row(string key, string detail = "")
    { WanfaEditorRow row = rows.GetNext(); row.Setup(T(key), detail, "", true, null); return row; }

    /// <summary>参数选择使用已有可搜索菜单。</summary>
    /// <param name="key">参数名称。</param>
    /// <param name="current">当前显示值。</param>
    /// <param name="open">打开选择。</param>
    private void Choice(string key, string current, Action open)
    { WanfaEditorRow row = rows.GetNext(); row.Setup(T(key), current, T("Choose"), true, open); }

    /// <summary>明确命令复用行右侧动作。</summary>
    /// <param name="key">命令名称。</param>
    /// <param name="detail">命令作用。</param>
    /// <param name="action">实际命令。</param>
    private void ActionRow(string key, string detail, Action action)
    { WanfaEditorRow row = rows.GetNext(); row.Setup(T(key), detail, T(key), true, action, UiIcons.Add); }

    /// <summary>浮点参数复用固定数值输入。</summary>
    /// <param name="key">参数名。</param>
    /// <param name="value">当前值。</param>
    /// <param name="set">写入独立草稿。</param>
    private void Number(string key, float value, Action<float> set)
    { WanfaEditorRow row = Row(key); FloatInput(row.UseInlineControls(104f), key, value, set, true, 100f); }

    /// <summary>输入边界只解析有限数字，实际结构规则交给共用编译。</summary>
    /// <param name="parent">固定控件槽。</param>
    /// <param name="key">参数名。</param>
    /// <param name="value">当前值。</param>
    /// <param name="set">草稿修改。</param>
    /// <param name="enabled">是否允许编辑。</param>
    /// <param name="width">控件宽度。</param>
    /// <param name="draftChange">是否直接修改草稿；命令参数在点击应用时才改变草稿。</param>
    private void FloatInput(Transform parent, string key, float value, Action<float> set, bool enabled, float width, bool draftChange = true)
    {
        InputField field = UiElements.CreateInput(parent, key, value.ToString("0.###", CultureInfo.InvariantCulture), T(key), width, 22f);
        field.interactable = enabled;
        UiTooltip.Set(field, "Cultiway.Construction." + key, "Cultiway.Construction." + key);
        field.onEndEdit.AddListener(text =>
        {
            if (refreshing) return;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) || float.IsNaN(parsed) || float.IsInfinity(parsed))
            { field.SetTextWithoutNotify(value.ToString("0.###", CultureInfo.InvariantCulture)); state.text = T("NumberRequired"); return; }
            if (draftChange) Change(() => set(parsed)); else set(parsed);
        });
    }

    /// <summary>二元参数使用统一开关。</summary>
    /// <param name="key">参数名。</param>
    /// <param name="value">当前状态。</param>
    /// <param name="set">写入草稿。</param>
    private void Toggle(string key, bool value, Action<bool> set)
    {
        WanfaEditorRow row = Row(key);
        Toggle toggle = UiElements.CreateIconToggle(row.UseInlineControls(32f), key, UiIcons.Options, value, 28f, 22f);
        toggle.onValueChanged.AddListener(next => Change(() => set(next)));
        UiTooltip.Set(toggle, "Cultiway.Construction." + key, "Cultiway.Construction." + key);
    }

    /// <summary>实际材料的可读名称。</summary>
    /// <param name="matter">已提供规则的材料及物态。</param>
    private static string MaterialName(AbilityMatter matter)
        => LocalizedTextManager.getText("Cultiway.Construction.Material." + matter.Id + "." + matter.State);
}