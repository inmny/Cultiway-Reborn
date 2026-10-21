using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Core.Visuals.Forms;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>只读的共享计划。修改原始资料不会改变已经开始的施放。</summary>
public sealed class CompiledAbility
{
    /// <summary>稳定编号。</summary>
    public string Id { get; }
    /// <summary>资料修订号。</summary>
    public int Revision { get; }
    /// <summary>最大持续时间。</summary>
    public float Lifetime { get; }
    /// <summary>有限展开后的形体数。</summary>
    public int FormCount => Nodes.Length;
    /// <summary>展开后实际采用的材料，交给来源玩法核对支持的规则。</summary>
    public IReadOnlyList<string> Matters { get; }
    /// <summary>已经编译的身体动作，空值表示不借用身体。</summary>
    public CompiledBodyAction Body { get; }
    /// <summary>已经复制的实际根连接，空值保留首次格式的固定根。</summary>
    internal CompiledBinding Binding { get; }
    /// <summary>按父体先于子体排列的连续计划。</summary>
    internal CompiledForm[] Nodes { get; }

    /// <summary>复制已经验证的能力头部，保存有限展开结果。</summary>
    /// <param name="definition">已检查资料。</param>
    /// <param name="nodes">已经解析的部位。</param>
    /// <param name="body">已经复制及核对的身体动作。</param>
    internal CompiledAbility(AbilityDefinition definition, CompiledForm[] nodes, CompiledBodyAction body)
    {
        Id = definition.Id; Revision = definition.Revision; Lifetime = definition.Lifetime; Nodes = nodes;
        Matters = nodes.Select(node => node.Matter).Distinct().ToArray();
        Body = body;
        Binding = definition.Binding == null ? null : new CompiledBinding(definition.Binding);
    }
}

/// <summary>只含运行需要的数值及解析后的资源引用，不持有可编辑定义。</summary>
internal sealed class CompiledForm
{
    /// <summary>展开后的完整部位路径。</summary>
    internal string Path;
    /// <summary>共用资源。</summary>
    internal FormResource Resource;
    /// <summary>父体索引，负一表示根。</summary>
    internal int Parent;
    /// <summary>父挂点姿态。</summary>
    internal Matrix4x4 Anchor;
    /// <summary>本体初始局部位置。</summary>
    internal Vector3 Offset;
    /// <summary>初始局部朝向。</summary>
    internal Quaternion Rotation;
    /// <summary>实际尺寸倍率。</summary>
    internal float Scale;
    /// <summary>表面覆盖。</summary>
    internal string Surface;
    /// <summary>不从显示颜色推测的实际材料。</summary>
    internal string Matter;
    /// <summary>同组共享接触去重，跨形态交接不重复命中同一人物。</summary>
    internal int ContactGroup;
    /// <summary>编译阶段分配共用接触组所需的稳定名称。</summary>
    internal string ContactKey;
    /// <summary>实际接触后的过程变化。</summary>
    internal ConstructionContactResponse OnContact;
    /// <summary>有效时段起止。</summary>
    internal Vector2 Active;
    /// <summary>生长起止；负值表示不采用生长。</summary>
    internal Vector2 Growth = new(-1f, -1f);
    /// <summary>解除连接时点；负值表示保持连接。</summary>
    internal float Detach = -1f;
    /// <summary>旋转开始时点。</summary>
    internal float SpinAt;
    /// <summary>局部角速度。</summary>
    internal Vector3 Spin;
    /// <summary>独立飞行开始时点，负值表示不飞行。</summary>
    internal float FlyAt = -1f;
    /// <summary>飞行的初始局部速度。</summary>
    internal Vector3 Velocity;
    /// <summary>追踪的每秒转角。</summary>
    internal float TurnRate;
    /// <summary>实际解除父体或外部连接的时点。</summary>
    internal float ReleaseAt => Detach >= 0f ? Detach : Parent < 0 ? FlyAt : -1f;
}

/// <summary>在创建或改造时一次展开结构和名称，不在运行热路径解释资料。</summary>
public static class AbilityCompiler
{
    /// <summary>同一世界可共用的完整资料缓存；内容不同也不会误用相同修订号。</summary>
    private static readonly Dictionary<string, CompiledAbility> plans = new(StringComparer.Ordinal);
    /// <summary>首轮支持的有限形体预算，不允许资料无限复制。</summary>
    public const int MaxForms = 128;

    /// <summary>检查外部定义、有限展开并解析资源。所有错误带具体部位路径。</summary>
    /// <param name="definition">玩家改造、创造或存档恢复后的当前资料。</param>
    public static CompiledAbility Compile(AbilityDefinition definition)
    {
        if (definition == null || definition.Version != 1 || string.IsNullOrWhiteSpace(definition.Id) ||
            definition.Revision < 1 || definition.Root == null && definition.Body == null || !Finite(definition.Lifetime) ||
            definition.Lifetime <= 0f || definition.Lifetime > 3600f ||
            !Finite(definition.Cost) || definition.Cost < 0 || !Finite(definition.Cooldown) || definition.Cooldown < 0 ||
            !Finite(definition.Power) || definition.Power < 0)
            throw new InvalidDataException("能力头部、版本或持续时间无效");
        var nodes = new List<CompiledForm>();
        // 先展开并检查结构，再生成缓存键，循环引用及过深资料会在序列化之前被拒绝。
        bool connectedRoot = definition.Binding?.Follow == true;
        if (definition.Binding != null && (definition.Root == null || !Enum.IsDefined(typeof(AbilityAnchor), definition.Binding.Anchor) ||
            !Finite(definition.Binding.Height) || definition.Binding.Anchor == AbilityAnchor.Hand && definition.Body == null))
            throw new InvalidDataException("根连接缺少生成主体、有效对象、高度或实际身体动作");
        if (definition.Root != null) Expand(definition.Root, -1, null, Matrix4x4.identity, "root", 0, definition.Lifetime, nodes, connectedRoot);
        CompiledBodyAction body = definition.Body == null ? null : new CompiledBodyAction(definition.Body, definition.Lifetime);
        if (definition.Binding?.Anchor == AbilityAnchor.Hand && connectedRoot &&
            body.Duration < definition.Lifetime && (nodes[0].ReleaseAt < 0 || nodes[0].ReleaseAt > body.Duration))
            throw new InvalidDataException("手部连接必须在身体收势结束前解除，不能在动作结束后继续借用不存在的手姿态");
        var groups = new Dictionary<string, int>(StringComparer.Ordinal);
        groups.Add("body", nodes.Count);
        int nextGroup = 0;
        for (int i = 0; i < nodes.Count; i++)
        {
            string group = nodes[i].ContactKey;
            if (group == "body" && body == null) Fail(nodes[i].Path, "接触组body缺少实际身体动作");
            if (!groups.TryGetValue(group, out int index)) groups.Add(group, index = nextGroup++);
            nodes[i].ContactGroup = index;
        }
        string key = JsonConvert.SerializeObject(definition);
        if (plans.TryGetValue(key, out CompiledAbility found)) return found;
        var result = new CompiledAbility(definition, nodes.ToArray(), body);
        plans.Add(key, result);
        return result;
    }

    /// <summary>按挂点集合展开子体，检查首轮操作的时间及关系。</summary>
    /// <param name="node">当前结构。</param>
    /// <param name="parent">展开后的父体索引。</param>
    /// <param name="parentResource">父资源。</param>
    /// <param name="anchor">已经解析的挂点。</param>
    /// <param name="path">错误和来源路径。</param>
    /// <param name="depth">当前嵌套层数。</param>
    /// <param name="lifetime">总持续。</param>
    /// <param name="output">父体先于子体的结果。</param>
    /// <param name="connectedRoot">根部是否有实际可解除的外部连接。</param>
    private static void Expand(ConstructionNode node, int parent, FormResource parentResource,
        Matrix4x4 anchor, string path, int depth, float lifetime, List<CompiledForm> output, bool connectedRoot = false)
    {
        if (node == null || depth > 16 || output.Count >= MaxForms) Fail(path, "结构为空、嵌套过深或形体超过128个");
        if (string.IsNullOrWhiteSpace(node.Id) || string.IsNullOrWhiteSpace(node.Form) || string.IsNullOrWhiteSpace(node.Matter) || node.Children == null || node.Operations == null)
            Fail(path, "缺少部位编号、资源或操作集合");
        if (!Enum.IsDefined(typeof(ConstructionContactResponse), node.OnContact)) Fail(path, "未知的接触后行为");
        if (parent < 0 && !string.IsNullOrEmpty(node.Anchor)) Fail(path, "根部不能绑定父挂点");
        Vector3 offset = ReadVector(node.Offset, path), rotation = ReadVector(node.Rotation, path);
        if (!Finite(node.Scale) || node.Scale <= 0 || node.Scale > 10000 || !Finite(node.ActiveFrom) ||
            !Finite(node.ActiveUntil) || node.ActiveFrom < 0 || node.ActiveFrom > lifetime ||
            node.ActiveUntil >= 0 && (node.ActiveUntil < node.ActiveFrom || node.ActiveUntil > lifetime))
            Fail(path, "尺寸或有效时段无效");
        FormResource resource = FormResources.Load(node.Form);
        if (node.Surface != null) FormResources.Surface(node.Surface, true);
        var compiled = new CompiledForm
        {
            Path = path,
            Parent = parent,
            Anchor = anchor,
            Resource = resource,
            Offset = offset,
            Rotation = Quaternion.Euler(rotation),
            Scale = node.Scale,
            Surface = node.Surface,
            Matter = node.Matter,
            ContactKey = node.ContactGroup ?? path,
            OnContact = node.OnContact,
            Active = new Vector2(node.ActiveFrom, node.ActiveUntil < 0 ? lifetime : node.ActiveUntil)
        };
        var used = new HashSet<ConstructionOperationKind>();
        foreach (ConstructionOperation operation in node.Operations)
        {
            if (operation == null || !Enum.IsDefined(typeof(ConstructionOperationKind), operation.Kind) ||
                !used.Add(operation.Kind) || !Finite(operation.At) || operation.At < 0 || operation.At > lifetime ||
                !Finite(operation.Seconds) || !Finite(operation.TurnRate) || operation.TurnRate < 0)
                Fail(path, "操作重复、类型或时间无效");
            Vector3 vector = ReadVector(operation.Vector, path);
            switch (operation.Kind)
            {
                case ConstructionOperationKind.Grow:
                    if (operation.Seconds <= 0 || operation.At + operation.Seconds > lifetime) Fail(path, "生长时间无效");
                    compiled.Growth = new Vector2(operation.At, operation.At + operation.Seconds); break;
                case ConstructionOperationKind.Spin:
                    if (!Finite(vector.magnitude) || vector.magnitude > 7200f) Fail(path, "旋转超过每秒7200度的动作预算");
                    compiled.SpinAt = operation.At; compiled.Spin = vector; break;
                case ConstructionOperationKind.Detach:
                    if (parent < 0 && !connectedRoot) Fail(path, "根部没有可解除的父连接或外部连接");
                    compiled.Detach = operation.At; break;
                case ConstructionOperationKind.Fly:
                    if (operation.TurnRate > 7200f) Fail(path, "追踪转向超过每秒7200度的动作预算");
                    compiled.FlyAt = operation.At; compiled.Velocity = vector; compiled.TurnRate = operation.TurnRate; break;
            }
        }
        if ((parent >= 0 || compiled.Detach >= 0f) && compiled.FlyAt >= 0 && (compiled.Detach < 0 || compiled.FlyAt < compiled.Detach))
            Fail(path, "附着部位需要先解除连接才能独立飞行");
        int ownIndex = output.Count;
        output.Add(compiled);
        var childIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (ConstructionNode child in node.Children)
        {
            if (child == null || !childIds.Add(child.Id ?? "")) Fail(path, "子部位为空或编号重复");
            string childPath = path + "/" + child.Id;
            if (string.IsNullOrEmpty(child.Anchor))
            {
                Expand(child, ownIndex, resource, Matrix4x4.identity, childPath, depth + 1, lifetime, output); continue;
            }
            bool prefix = child.Anchor.EndsWith("*", StringComparison.Ordinal);
            string selector = prefix ? child.Anchor.Substring(0, child.Anchor.Length - 1) : child.Anchor;
            FormAnchorDescription[] matches = resource.Description.Anchors.Where(item => prefix
                ? item.Id.StartsWith(selector, StringComparison.Ordinal) : item.Id == selector).ToArray();
            if (matches.Length == 0) Fail(childPath, "父体没有挂点：" + child.Anchor);
            foreach (FormAnchorDescription point in matches)
            {
                Matrix4x4 local = Matrix4x4.TRS(ReadVector(point.Position, childPath),
                    Quaternion.FromToRotation(Vector3.forward, ReadVector(point.Direction, childPath)), Vector3.one);
                Expand(child, ownIndex, resource, local, childPath + "@" + point.Id, depth + 1, lifetime, output);
            }
        }
    }

    /// <summary>在资料边界读取有限三维向量。</summary>
    /// <param name="values">外部三个数值。</param>
    /// <param name="path">当前部位。</param>
    private static Vector3 ReadVector(float[] values, string path)
    {
        if (values?.Length != 3 || values.Any(value => !Finite(value))) Fail(path, "需要三个有限坐标");
        return new Vector3(values[0], values[1], values[2]);
    }

    /// <summary>数值不能包含无穷和无效结果。</summary>
    /// <param name="value">外部数值。</param>
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    /// <summary>给出具体不成立的结构，禁止静默替换成旧技能。</summary>
    /// <param name="path">部位路径。</param>
    /// <param name="reason">缺失内容。</param>
    private static void Fail(string path, string reason) => throw new InvalidDataException(path + "：" + reason);
    /// <summary>世界卸载时清除计划，现有施放仍持有自己开始时的计划。</summary>
    public static void Clear() => plans.Clear();
}
