using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>旧构造只清除原先无作用的错位参数，保留实际行为与其余世界资料。</summary>
internal sealed class ConstructionsV1ToV2 : ISaveMigration
{
    /// <summary>原正式构造部分。</summary>
    public int FromVersion => 1;
    /// <summary>此前会被无关操作忽略的四个字段，其余非法字段仍由原边界报告。</summary>
    private static readonly string[] formerlyIgnored = { nameof(ConstructionOperation.Clip), nameof(ConstructionOperation.Seconds), nameof(ConstructionOperation.Vector), nameof(ConstructionOperation.TurnRate) };

    /// <summary>只修改存档框架提供的副本，原文件及其他部分保持不变。</summary>
    /// <param name="data">旧构造部分的独立副本。</param>
    public JObject Apply(JObject data)
    {
        if (data["Definitions"] is JArray definitions)
            foreach (var definition in definitions.OfType<JObject>())
                if (definition["Root"] is JObject root) Visit(root);
        return data;
    }

    /// <summary>沿原有部位层级清理旧版忽略的字段，未知种类留给原局部恢复报告。</summary>
    /// <param name="node">真实构造部位。</param>
    private static void Visit(JObject node)
    {
        if (node["Operations"] is JArray operations)
            foreach (var operation in operations.OfType<JObject>())
            {
                if (!Enum.TryParse(operation["Kind"]?.ToString() ?? "0", out ConstructionOperationKind kind) || !Enum.IsDefined(typeof(ConstructionOperationKind), kind)) continue;
                var accepted = new HashSet<string>(ConstructionOperations.Describe(kind).Parameters.Select(p => p.Name), StringComparer.Ordinal);
                foreach (string field in formerlyIgnored)
                    if (!accepted.Contains(field)) operation.Remove(field);
            }
        if (node["Children"] is JArray children)
            foreach (var child in children.OfType<JObject>()) Visit(child);
    }
}
