using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>普通分布在各层独立创作及派生，不登记新的整招样板。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>改变同一完整排布，实际费用仍由派生入口按结果重算。</summary>
    /// <param name="draft">独立草稿。</param><param name="random">固定输入的随机源。</param>
    private static AbilityDefinition VaryDistribution(AbilityDefinition draft, System.Random random)
    {
        ConstructionNode[] nodes = DerivationNodes(draft.Root).Where(node => node.Distribution != null).ToArray();
        ConstructionNode node = nodes[random.Next(nodes.Length)];
        ConstructionDistribution value = node.Distribution;
        if (value.Maximum > 1 && random.Next(2) == 0) value.Maximum--;
        else
        {
            float scale = .75f + (float)random.NextDouble() * .5f;
            if (value.Source == ConstructionDistributionSource.Points)
                foreach (var point in value.Points) for (int axis = 0; axis < 3; axis++) point.Position[axis] *= scale;
            else if (value.Source == ConstructionDistributionSource.Path)
                foreach (var key in value.Path.Curve.Keys)
                    for (int axis = 0; axis < 3; axis++) { key.Point[axis] *= scale; key.In[axis] *= scale; key.Out[axis] *= scale; }
            else if (value.Source == ConstructionDistributionSource.Space)
                for (int axis = 0; axis < 3; axis++) value.Space.Size[axis] *= scale;
            else node.Offset[0] += .1f + (float)random.NextDouble() * .3f;
        }
        return draft;
    }

    /// <summary>第九版在原树的各层保留原始及实际展开两种预算。</summary>
    private sealed partial class ConstructionSynthesis
    {
        /// <summary>实际展开最多48份，给原有显化及组织支点保留整招余量。</summary>
        private const int MaximumExpanded = 48;
        /// <summary>已经明确产生的实际份数，不把重复子树只计为一个声明。</summary>
        private int expanded;

        /// <summary>依照本次固定资源选择一种来源，先限制倍数再创建，失败不重抽整招。</summary>
        /// <param name="parent">受理时固定的父模型输入。</param><param name="parentCopies">此父声明实际已有的份数。</param>
        private (ConstructionDistribution Value, int Copies) Distribute(SynthesisForm parent, int parentCopies)
        {
            int maximum = Math.Min(8, (MaximumExpanded - expanded) / parentCopies);
            if (maximum < 2) return (null, 1);
            var sources = new List<ConstructionDistributionSource>
            { ConstructionDistributionSource.Points, ConstructionDistributionSource.Path, ConstructionDistributionSource.Space };
            if (parent.Anchors.Length > 0) sources.Add(ConstructionDistributionSource.Anchors);
            if (parent.Parts.Length > 0) { sources.Add(ConstructionDistributionSource.Parts); sources.Add(ConstructionDistributionSource.Surface); }
            ConstructionDistributionSource source = sources[random.Next(sources.Count)];
            int copies = random.Next(2, maximum + 1);
            var value = new ConstructionDistribution { Source = source, Maximum = copies };
            float width = parent.Size * Range(.15f, .35f);
            if (source is ConstructionDistributionSource.Anchors or ConstructionDistributionSource.Parts)
            {
                var choices = (source == ConstructionDistributionSource.Anchors ? parent.Anchors : parent.Parts).ToList();
                copies = Math.Min(copies, choices.Count); var selected = new List<string>();
                for (int i = 0; i < copies; i++) { int at = random.Next(choices.Count); selected.Add(choices[at]); choices.RemoveAt(at); }
                value.Selectors = selected.ToArray(); value.Maximum = copies;
            }
            else if (source == ConstructionDistributionSource.Surface)
            { value.Selectors = new[] { parent.Parts[random.Next(parent.Parts.Length)] }; value.SurfaceSamples = copies; }
            else if (source == ConstructionDistributionSource.Points)
                value.Points = Enumerable.Range(0, copies).Select(i => new ConstructionDistributionPoint
                {
                    Id = "site_" + i, Position = new[] { Range(-width, width), Range(-width, width), Range(-width, width) },
                    Direction = new[] { Range(-1f, 1f), 1f, Range(-1f, 1f) }
                }).ToArray();
            else if (source == ConstructionDistributionSource.Path)
                value.Path = new ConstructionDistributionPath
                {
                    Samples = copies, Curve = new ConstructionPath { Keys = new[] {
                        new ConstructionPathKey { Out = new[] { Range(-width, width), width, width * .3f } },
                        new ConstructionPathKey { At = 1f, Point = new[] { width, 0f, width * .2f }, In = new[] { 0f, width, 0f } }
                    } }
                };
            else value.Space = new ConstructionDistributionSpace
            {
                Size = new[] { width, width, width },
                Counts = copies == 8 ? new[] { 2, 2, 2 } : copies == 4 ? new[] { 2, 2, 1 } : new[] { copies, 1, 1 },
                Rotation = new[] { Range(-30f, 30f), Range(0f, 360f), Range(-30f, 30f) },
                Shape = (ConstructionDistributionRegion)random.Next(2), Facing = (ConstructionDistributionFacing)random.Next(3)
            };
            return (value, copies);
        }
    }
}

/// <summary>旧请求只补记当时没有使用的部件集合，不改原规则或重新选模型。</summary>
internal sealed class CreationV9ToV10 : ISaveMigration
{
    /// <summary>此前已固定身体和触地传播输入的版本。</summary>
    public int FromVersion => 9;
    /// <summary>只在转换副本上补明确空集合。</summary><param name="data">原请求区段副本。</param>
    public JObject Apply(JObject data)
    {
        if (data["Resources"]?["Forms"] is JArray forms)
            foreach (JObject form in forms) form["Parts"] ??= new JArray();
        return data;
    }
}
