using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>在资料合成边界按真实需求补缺项；结果是普通构造，运行时不再推断。</summary>
internal static class ConstructionCompletion
{
    /// <summary>先确定身体与根连接，再逐部位补材料和操作；明确的空值、零和空列表始终保留。</summary>
    /// <param name="definition">本次合成独占的资料。</param>
    internal static void Apply(JObject definition)
    {
        if (Read(definition, "Root") is not JObject root) return;
        JObject body = Read(definition, "Body") as JObject;
        JObject[] steps = Read(definition, "Steps") is JArray declared ? declared.OfType<JObject>().ToArray() : Array.Empty<JObject>();
        bool rootFlight = Operations(root).Any(op => Kind(op) == ConstructionOperationKind.Fly) ||
            steps.Any(step => StepPath(step, 0) == "root" && Read(step, "Operation") is JObject op && Kind(op) == ConstructionOperationKind.Fly);
        bool projected = body != null && rootFlight;
        if (projected)
        {
            Put(root, "Form", Read(body, "WeaponForm")?.Type == JTokenType.String ? Read(body, "WeaponForm").DeepClone() : new JValue("hand"));
            Put(definition, "Binding", new JObject());
            if (Read(definition, "Binding") is JObject binding)
            {
                Put(binding, "Anchor", new JValue((int)AbilityAnchor.Hand));
                Put(binding, "Follow", new JValue(true));
                Put(binding, "ScaleByBody", new JValue(true));
            }
        }
        else if (rootFlight)
            Put(definition, "Binding", new JObject { ["Anchor"] = (int)AbilityAnchor.Source, ["FaceTarget"] = true });
        float lifetime = Read(definition, "Lifetime")?.Value<float>() ?? 3f;
        float release = projected ? (Read(body, "ActiveUntil")?.Value<float>() ?? 0f) +
            (Read(body, "Hold") is JObject hold ? Read(hold, "Seconds")?.Value<float>() ?? 0f : 0f) : 0f;
        int count = 0;
        var nodes = new Dictionary<string, JObject>(StringComparer.Ordinal);
        CompleteNode(root, "root", 0, projected);
        foreach (JObject step in steps)
            if (Read(step, "Operation") is JObject operation)
            {
                string path = StepPath(step, 0);
                JObject node = path != null && nodes.TryGetValue(path, out var found) ? found : null;
                CompleteOperation(operation, node, projected && node == root);
            }

        // 这里只追溯同一part输入的原部位，不替代编译器对集合、输出类型和循环的完整核对。
        string StepPath(JObject step, int depth)
        {
            if (depth > 128 || Read(step, "Inputs") is not JObject inputs || Read(inputs, "part") is not JObject part) return null;
            if (Read(part, "Node")?.Value<string>() is string path) return path;
            string source = Read(part, "Step")?.Value<string>();
            JObject previous = source == null ? null : steps.FirstOrDefault(item => Read(item, "Id")?.Value<string>() == source);
            return previous == null ? null : StepPath(previous, depth + 1);
        }

        // 部位的实际作用形状仍由正式资源解析，不复制旧术的圆形范围。
        void CompleteNode(JObject node, string path, int depth, bool manifestation)
        {
            if (++count > AbilityCompiler.MaxForms || depth > 16)
                throw new InvalidDataException(path + "：补全结构超过128部位或16层，请缩减实际构造");
            nodes[path] = node;
            JObject[] operations = Operations(node);
            JObject[] bound = steps.Where(step => StepPath(step, 0) == path).Select(step => Read(step, "Operation")).OfType<JObject>().ToArray();
            string matterId = Read(node, "Matter")?.Value<string>() ?? "physical";
            if (operations.Concat(bound).Any(op => Kind(op) == ConstructionOperationKind.Flow))
                Put(node, "State", new JValue("liquid"));
            AbilityMatter matter = AbilityMatters.Resolve(matterId, Read(node, "State")?.Value<string>(), path);
            Put(node, "State", new JValue(matter.State));
            // 表面继续读取实际材料；不把当前默认表面写死，妨碍后续冻结或换材。
            Put(node, "DisappearAt", new JValue(lifetime));
            if (!Has(node, "Operations") && matter.State == "liquid")
            {
                Put(node, "Operations", new JArray(new JObject { ["Kind"] = (int)ConstructionOperationKind.Flow }));
                operations = Operations(node);
            }
            foreach (JObject operation in operations) CompleteOperation(operation, node, manifestation);
            if (manifestation)
            {
                JObject flight = operations.Concat(bound).First(op => Kind(op) == ConstructionOperationKind.Fly);
                CompleteOperation(flight, node, true);
                Put(node, "ActiveFrom", Read(flight, "At").DeepClone());
                Put(node, "ContactGroup", new JValue("body"));
                Put(node, "OnContact", new JValue((int)ConstructionContactResponse.End));
            }
            if (Read(node, "Children") is JArray children)
                foreach (JObject child in children.OfType<JObject>())
                    CompleteNode(child, path + "/" + (Read(child, "Id")?.Value<string>() ?? "?"), depth + 1, false);
        }

        // 只展开已选择操作所需的资料，不向明确操作列表偷偷添加其他玩法。
        void CompleteOperation(JObject operation, JObject node, bool manifestation)
        {
            ConstructionOperationKind? kind = Kind(operation);
            float at = Read(operation, "At")?.Value<float>() ?? (manifestation && kind == ConstructionOperationKind.Fly ? release : 0f);
            Put(operation, "At", new JValue(at));
            switch (kind)
            {
                case ConstructionOperationKind.Fly:
                    if (!Has(operation, "Vector") && Read(operation, "Path") is not JObject)
                    {
                        Vector3 forward = Vector3.forward * 8f;
                        if (node != null && Read(node, "Rotation") is JArray rotation && rotation.Count == 3)
                            forward = Quaternion.Inverse(Quaternion.Euler(rotation[0].Value<float>(), rotation[1].Value<float>(), rotation[2].Value<float>())) * forward;
                        Put(operation, "Vector", new JArray(forward.x, forward.y, forward.z));
                    }
                    if (manifestation) Put(operation, "InheritVelocity", new JValue(false));
                    if (node == root && Read(operation, "Vector") is JArray velocity && velocity.Count == 3)
                    {
                        float speed = new Vector3(velocity[0].Value<float>(), velocity[1].Value<float>(), velocity[2].Value<float>()).magnitude;
                        if (speed > 0f && lifetime > at) Put(definition, "TargetRange", new JValue(speed * (lifetime - at)));
                    }
                    break;
                case ConstructionOperationKind.Grow:
                    Put(operation, "Seconds", new JValue(Math.Min(.25f, lifetime - at)));
                    break;
                case ConstructionOperationKind.Flow:
                    Put(operation, "Flow", new JObject());
                    break;
                case ConstructionOperationKind.Fracture:
                    Put(operation, "Fracture", new JObject());
                    if (Read(operation, "Fracture") is JObject fracture)
                        Put(fracture, "Seconds", new JValue(Math.Min(2f, lifetime - at)));
                    break;
                case ConstructionOperationKind.Leave:
                    Put(operation, "Residue", new JObject { ["Lifetime"] = 10f });
                    break;
                case ConstructionOperationKind.ConvertMatter:
                    string id = Read(operation, "Matter")?.Value<string>();
                    if (id != null && !Has(operation, "State"))
                        Put(operation, "State", new JValue(AbilityMatters.Resolve(id, null, "操作/ConvertMatter").State));
                    break;
            }
        }
    }

    /// <summary>读取正式资料允许的同名大小写，不创建第二个别名字段。</summary>
    /// <param name="value">当前资料对象。</param><param name="name">正式字段名。</param>
    private static JToken Read(JObject value, string name) => value?.GetValue(name, StringComparison.OrdinalIgnoreCase);

    /// <summary>明确空值也是存在的选择。</summary>
    /// <param name="value">当前资料对象。</param><param name="name">正式字段名。</param>
    private static bool Has(JObject value, string name) => value.Property(name, StringComparison.OrdinalIgnoreCase) != null;

    /// <summary>只填没有提供的字段，调用方传入的默认对象由本次结果独占。</summary>
    /// <param name="value">独占资料。</param><param name="name">字段名。</param><param name="fallback">缺项值。</param>
    private static void Put(JObject value, string name, JToken fallback)
    { if (!Has(value, name)) value.Add(name, fallback); }

    /// <summary>只读取确实声明的操作，损坏的列表仍交给正式资料检查。</summary>
    /// <param name="node">当前部位。</param>
    private static JObject[] Operations(JObject node) => Read(node, "Operations") is JArray array ? array.OfType<JObject>().ToArray() : Array.Empty<JObject>();

    /// <summary>未知种类保留给正式编译拒绝，不从技能名称猜测。</summary>
    /// <param name="operation">外部选择的操作。</param>
    private static ConstructionOperationKind? Kind(JObject operation) => Read(operation, "Kind")?.ToObject<ConstructionOperationKind>();
}
