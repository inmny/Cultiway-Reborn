using System;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using Cultiway.UI.Prefab;

namespace Cultiway.UI;

/// <summary>普通子结构分布的现有表单，继续草稿、撤销、实际编译和正式学习。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>展示所选子结构的分布来源与全部相关参数。</summary>
    /// <param name="node">独立草稿中选中的真实声明。</param>
    private void DistributionRows(ConstructionNode node)
    {
        ActionRow("DistributionAddChild", T("DistributionChildHint"), () => Change(() =>
        {
            int suffix = 0; while (node.Children.Any(child => child.Id == "distributed_" + suffix)) suffix++;
            part = parts.IndexOf(node) + 1 + node.Children.Sum(DistributionSubtreeSize);
            node.Children = node.Children.Concat(new[] { new ConstructionNode
            {
                Id = "distributed_" + suffix, Form = "sword", Matter = node.Matter, Scale = .3f,
                Distribution = DefaultDistribution(ConstructionDistributionSource.Space, FormResources.Load(node.Form))
            } }).ToArray();
        }));
        ConstructionNode parent = parts.FirstOrDefault(candidate => candidate.Children.Contains(node));
        if (parent == null) { Row("DistributionRoot", T("DistributionRootHint")); return; }
        FormResource resource = FormResources.Load(parent.Form);
        ConstructionDistribution distribution = node.Distribution;
        Choice("DistributionSource", distribution == null ? T("DistributionNone") : T("DistributionSource." + distribution.Source), () =>
            menu.Show(T("DistributionSource"), new[] { new UiOptionMenuOption
            {
                Label = T("DistributionNone"), Selected = distribution == null,
                Select = () => Change(() => node.Distribution = null)
            } }.Concat(Enum.GetValues(typeof(ConstructionDistributionSource)).Cast<ConstructionDistributionSource>()
                .Where(source => source != ConstructionDistributionSource.Anchors || resource.Anchors.Length > 0)
                .Where(source => source is not (ConstructionDistributionSource.Parts or ConstructionDistributionSource.Surface) || resource.Meshes.Length > 0)
                .Select(source => new UiOptionMenuOption
                {
                    Label = T("DistributionSource." + source), Selected = distribution?.Source == source,
                    Select = () => Change(() => { node.Anchor = null; node.Distribution = DefaultDistribution(source, resource); })
                })).ToArray()));
        if (distribution != null)
        {
            Number("DistributionFirst", distribution.First, value => distribution.First = (int)value);
            Number("DistributionEvery", distribution.Every, value => distribution.Every = (int)value);
            Number("DistributionMaximum", distribution.Maximum, value => distribution.Maximum = (int)value);
            if (distribution.Source is ConstructionDistributionSource.Anchors or ConstructionDistributionSource.Parts or ConstructionDistributionSource.Surface)
            {
                bool anchors = distribution.Source == ConstructionDistributionSource.Anchors;
                var names = anchors ? resource.Anchors.Select(anchor => anchor.Id).ToArray() : resource.Description.Parts.Select(item => item.Id)
                    .Concat(resource.Description.Parts.SelectMany(item => item.Roles).Distinct().Select(role => "role:" + role)).ToArray();
                Choice("DistributionSelection", distribution.Selectors.Length == 0 ? T("DistributionEmpty") : string.Join(", ", distribution.Selectors), () =>
                    menu.Show(T("DistributionSelection"), names.Concat(distribution.Selectors).Distinct().Select(name => new UiOptionMenuOption
                    {
                        Label = name, Selected = distribution.Selectors.Contains(name),
                        Select = () => Change(() => distribution.Selectors = distribution.Selectors.Contains(name)
                            ? distribution.Selectors.Where(item => item != name).ToArray() : distribution.Selectors.Concat(new[] { name }).ToArray())
                    }).ToArray(), true));
                if (distribution.Source == ConstructionDistributionSource.Surface)
                    Number("DistributionSamples", distribution.SurfaceSamples, value => distribution.SurfaceSamples = (int)value);
            }
            if (distribution.Source == ConstructionDistributionSource.Points) DistributionPoints(distribution);
            if (distribution.Source == ConstructionDistributionSource.Path)
            {
                Number("DistributionSamples", distribution.Path.Samples, value => distribution.Path.Samples = (int)value);
                CurveRows(distribution.Path.Curve, layout: true);
            }
            if (distribution.Source == ConstructionDistributionSource.Space) DistributionSpace(distribution.Space);
        }
        WanfaEditorRow remove = rows.GetNext();
        remove.Setup(T("DistributionRemoveChild"), "", T("DistributionRemoveChild"), true,
            () => Change(() => parent.Children = parent.Children.Where(child => child != node).ToArray()), UiIcons.Delete);
    }

    /// <summary>只统计草稿声明，用于新增后选择刚加入的子结构。</summary>
    /// <param name="node">现有子树。</param>
    private static int DistributionSubtreeSize(ConstructionNode node) => 1 + node.Children.Sum(DistributionSubtreeSize);

    /// <summary>提供可立即编辑的普通参数，切换来源时只保留该来源实际读取的字段。</summary>
    /// <param name="source">明确选择的来源。</param><param name="parent">当前父资源。</param>
    private static ConstructionDistribution DefaultDistribution(ConstructionDistributionSource source, FormResource parent)
    {
        var value = new ConstructionDistribution { Source = source, Maximum = 3 };
        float width = parent.RestSize > 0f ? parent.RestSize : 3f;
        if (source == ConstructionDistributionSource.Anchors) value.Selectors = parent.Anchors.Take(2).Select(anchor => anchor.Id).ToArray();
        if (source is ConstructionDistributionSource.Parts or ConstructionDistributionSource.Surface)
        { value.Selectors = new[] { parent.Description.Parts[0].Id }; value.SurfaceSamples = source == ConstructionDistributionSource.Surface ? 3 : 1; }
        if (source == ConstructionDistributionSource.Points)
            value.Points = Enumerable.Range(0, 3).Select(i => new ConstructionDistributionPoint { Id = "point_" + i, Position = new[] { (i - 1) * width * .3f, 0f, 0f } }).ToArray();
        if (source == ConstructionDistributionSource.Path)
            value.Path = new ConstructionDistributionPath { Samples = 3, Curve = new ConstructionPath { Keys = new[] {
                new ConstructionPathKey { Out = new[] { 0f, width * .4f, 0f } },
                new ConstructionPathKey { At = 1f, Point = new[] { width, 0f, 0f }, In = new[] { 0f, width * .4f, 0f } }
            } } };
        if (source == ConstructionDistributionSource.Space)
            value.Space = new ConstructionDistributionSpace { Size = new[] { width, 0f, 0f }, Counts = new[] { 3, 1, 1 } };
        return value;
    }

    /// <summary>命名点保持稳定身份，位置方向可独立修改或增减。</summary>
    /// <param name="value">当前明确点分布。</param>
    private void DistributionPoints(ConstructionDistribution value)
    {
        foreach (ConstructionDistributionPoint point in value.Points)
        {
            Row("DistributionPoint", point.Id);
            DistributionVector("DistributionPosition", point.Position);
            DistributionVector("DistributionDirection", point.Direction);
            if (value.Points.Length > 1) ActionRow("DistributionRemovePoint", "", () => Change(() => value.Points = value.Points.Where(item => item != point).ToArray()));
        }
        if (value.Points.Length < AbilityCompiler.MaxForms)
            ActionRow("DistributionAddPoint", "", () => Change(() =>
            {
                int suffix = 0; while (value.Points.Any(point => point.Id == "point_" + suffix)) suffix++;
                value.Points = value.Points.Concat(new[] { new ConstructionDistributionPoint { Id = "point_" + suffix } }).ToArray();
            }));
    }

    /// <summary>区域出生位置、轴向和朝向共用同一表单及编译边界。</summary>
    /// <param name="value">当前区域。</param>
    private void DistributionSpace(ConstructionDistributionSpace value)
    {
        Row("DistributionSpaceRules", T("DistributionSpaceHint"));
        Choice("DistributionShape", T("DistributionShape." + value.Shape), () => menu.Show(T("DistributionShape"),
            Enum.GetValues(typeof(ConstructionDistributionRegion)).Cast<ConstructionDistributionRegion>().Select(shape => new UiOptionMenuOption
            { Label = T("DistributionShape." + shape), Selected = value.Shape == shape, Select = () => Change(() => value.Shape = shape) }).ToArray()));
        Choice("DistributionFacing", T("DistributionFacing." + value.Facing), () => menu.Show(T("DistributionFacing"),
            Enum.GetValues(typeof(ConstructionDistributionFacing)).Cast<ConstructionDistributionFacing>().Select(facing => new UiOptionMenuOption
            { Label = T("DistributionFacing." + facing), Selected = value.Facing == facing, Select = () => Change(() => value.Facing = facing) }).ToArray()));
        DistributionVector("DistributionCentre", value.Centre); DistributionVector("DistributionSize", value.Size); DistributionVector("DistributionRotation", value.Rotation);
        WanfaEditorRow row = Row("DistributionCounts"); var controls = row.UseInlineControls(260f);
        for (int axis = 0; axis < 3; axis++)
        { int at = axis; FloatInput(controls, new[] { "X", "Y", "Z" }[axis], value.Counts[axis], number => value.Counts[at] = (int)number, true, 78f); }
    }

    /// <summary>三轴参数统一在同一行编辑，不新增公共控件。</summary>
    /// <param name="key">实际参数名称。</param><param name="vector">当前草稿独占的三轴值。</param>
    private void DistributionVector(string key, float[] vector)
    {
        WanfaEditorRow row = Row(key); var controls = row.UseInlineControls(260f);
        for (int axis = 0; axis < 3; axis++)
        { int at = axis; FloatInput(controls, new[] { "X", "Y", "Z" }[axis], vector[axis], number => vector[at] = number, true, 78f); }
    }
}
