using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Cultiway.Core.Skills;

/// <summary>操作之间可以传递的资料种类，具体对象在编译或运行时绑定。</summary>
public enum ConstructionValueKind
{
    /// <summary>完整结构。</summary>
    Structure,
    /// <summary>结构中的真实部位，也可明确声明为部件集合。</summary>
    Part,
    /// <summary>持续推进的过程。</summary>
    Process,
    /// <summary>实际连接或控制关系。</summary>
    Relation,
    /// <summary>有来源及数量的材料。</summary>
    Material,
    /// <summary>真实空间位置。</summary>
    Position,
    /// <summary>一次确认的实际结果或支付凭据。</summary>
    Result
}

/// <summary>单次操作需要保存的状态，不能写回共享制作资料。</summary>
[Flags]
public enum ConstructionOperationState
{
    /// <summary>没有额外状态。</summary>
    None = 0,
    /// <summary>位置、方向及实际范围。</summary>
    Pose = 1,
    /// <summary>尺寸进度。</summary>
    Growth = 2,
    /// <summary>速度及加速度。</summary>
    Momentum = 4,
    /// <summary>父连接及脱离时点。</summary>
    Connection = 8,
    /// <summary>骨架时钟与过渡姿态。</summary>
    Motion = 16,
    /// <summary>真实材料及保留份额。</summary>
    Matter = 32,
    /// <summary>真实碎片及余力。</summary>
    Fragments = 64,
    /// <summary>流散水格、体积和动量。</summary>
    Flow = 128,
    /// <summary>尚存支付份额及收束进度。</summary>
    Recovery = 256,
    /// <summary>材料转交及世界接收状态。</summary>
    WorldMaterial = 512,
    /// <summary>有限导电波次及余力。</summary>
    Conduction = 1024,
    /// <summary>真实条件的接受、延迟与执行记录。</summary>
    Event = 2048
}

/// <summary>操作所需的共享资料或运行输入，不把运行对象保存进能力定义。</summary>
[Flags]
public enum ConstructionOperationResource
{
    /// <summary>没有额外资源。</summary>
    None = 0,
    /// <summary>真实生成几何。</summary>
    Geometry = 1,
    /// <summary>明确命名的制作动作。</summary>
    Motion = 2,
    /// <summary>可分离的真实分件及连接。</summary>
    Parts = 4,
    /// <summary>本轮只读地表。</summary>
    Terrain = 8,
    /// <summary>真实连通水格及接触入口。</summary>
    Water = 16,
    /// <summary>来源维护的实际支付记录。</summary>
    Payment = 32,
    /// <summary>世界材料维护者。</summary>
    WorldMaterials = 64,
    /// <summary>实际材料与物态规则。</summary>
    Matter = 128,
    /// <summary>受理时提供的真实目标位置。</summary>
    Target = 256,
    /// <summary>显示及实际接触共用的形状。</summary>
    Contacts = 512
}

/// <summary>操作真正改变的内容，显示不是另一套结算。</summary>
[Flags]
public enum ConstructionOperationEffect
{
    /// <summary>改变实际姿态及由姿态带动的作用区。</summary>
    Pose = 1,
    /// <summary>改变运动与路径。</summary>
    Movement = 2,
    /// <summary>改变真实连接。</summary>
    Connection = 4,
    /// <summary>改变真实材料及作用规则。</summary>
    Matter = 8,
    /// <summary>改变实际部件或动态作用形状。</summary>
    Shape = 16,
    /// <summary>向世界维护者提交实际结果。</summary>
    World = 32,
    /// <summary>按真实份额回收支付资源。</summary>
    Recovery = 64,
    /// <summary>沿真实介质传播并结算作用。</summary>
    Propagation = 128
}

/// <summary>一个命名输入或输出，明确资料种类及是否为集合。</summary>
public sealed class ConstructionOperationPort
{
    /// <summary>同一方向内稳定的端口名。</summary>
    public string Name { get; }
    /// <summary>可接受或产生的资料种类。</summary>
    public ConstructionValueKind Kind { get; }
    /// <summary>是否为必须成立的输入；输出恒为真。</summary>
    public bool Required { get; }
    /// <summary>是否在施放或实际事件中取得。</summary>
    public bool Runtime { get; }
    /// <summary>是否为真实对象集合。</summary>
    public bool Collection { get; }
    /// <summary>保存统一端口约定。</summary>
    /// <param name="name">端口名。</param><param name="kind">资料种类。</param><param name="required">是否必需。</param>
    /// <param name="runtime">是否运行时取得。</param><param name="collection">是否为集合。</param>
    internal ConstructionOperationPort(string name, ConstructionValueKind kind, bool required = true, bool runtime = true, bool collection = false)
    { Name = name; Kind = kind; Required = required; Runtime = runtime; Collection = collection; }
}

/// <summary>操作字段的真实类型及适用条件，数值范围仍由原有对应编译代码核对。</summary>
public sealed class ConstructionOperationParameter
{
    /// <summary>可保存的真实字段名。</summary>
    public string Name => field.Name;
    /// <summary>真实字段类型，嵌套参数沿用现有资料类型。</summary>
    public Type ValueType => field.FieldType;
    /// <summary>此操作必须显式提供非默认值。</summary>
    public bool Required { get; }
    /// <summary>只在明确事件条件下使用。</summary>
    public bool RequiresCondition { get; }
    /// <summary>编译边界使用的真实字段，不在运行中反射。</summary>
    private readonly FieldInfo field;
    /// <summary>现有格式的默认值，用于接受统一序列化的无作用字段。</summary>
    private readonly object defaultValue;
    /// <summary>从真实资料字段建立说明。</summary>
    /// <param name="field">已经存在的字段。</param><param name="defaults">当前格式默认值。</param>
    /// <param name="required">是否必需。</param><param name="condition">是否要求事件。</param>
    internal ConstructionOperationParameter(FieldInfo field, ConstructionOperation defaults, bool required, bool condition)
    { this.field = field; defaultValue = field.GetValue(defaults); Required = required; RequiresCondition = condition; }
    /// <summary>只在编译边界识别作者实际填写的参数。</summary>
    /// <param name="source">外部操作资料。</param>
    internal bool Supplied(ConstructionOperation source)
    {
        object value = field.GetValue(source);
        return value is Array array && defaultValue is Array original
            ? !array.Cast<object>().SequenceEqual(original.Cast<object>())
            : !Equals(value, defaultValue);
    }
}

/// <summary>一种已有执行操作的统一约定，供编译和构造工具共用。</summary>
public sealed class ConstructionOperationContract
{
    /// <summary>已经提供共同执行实现的操作。</summary>
    public ConstructionOperationKind Kind { get; }
    /// <summary>命名的输入和输出。</summary>
    public IReadOnlyList<ConstructionOperationPort> Inputs { get; }
    /// <summary>实际输出。</summary>
    public IReadOnlyList<ConstructionOperationPort> Outputs { get; }
    /// <summary>包括公共时点和条件的可用参数。</summary>
    public IReadOnlyList<ConstructionOperationParameter> Parameters { get; }
    /// <summary>基础状态需求。</summary>
    public ConstructionOperationState State { get; }
    /// <summary>基础共享及运行资源需求。</summary>
    public ConstructionOperationResource Resources { get; }
    /// <summary>对实际作用的影响。</summary>
    public ConstructionOperationEffect Effects { get; }
    /// <summary>仅在事件成立后运行，当前用于停止。</summary>
    public bool RequiresCondition { get; }
    /// <summary>用于拒绝无关参数的索引。</summary>
    private readonly HashSet<string> accepted;
    /// <summary>保存不可改写的声明集合。</summary>
    /// <param name="kind">操作种类。</param><param name="inputs">输入。</param><param name="outputs">输出。</param>
    /// <param name="state">状态。</param><param name="resources">资源。</param><param name="effects">实际影响。</param>
    /// <param name="parameters">真实参数。</param><param name="requiresCondition">是否必须有事件。</param>
    internal ConstructionOperationContract(ConstructionOperationKind kind, ConstructionOperationPort[] inputs, ConstructionOperationPort[] outputs,
        ConstructionOperationState state, ConstructionOperationResource resources, ConstructionOperationEffect effects,
        ConstructionOperationParameter[] parameters, bool requiresCondition)
    {
        Kind = kind; Inputs = Array.AsReadOnly(inputs); Outputs = Array.AsReadOnly(outputs); State = state; Resources = resources;
        Effects = effects; Parameters = Array.AsReadOnly(parameters); RequiresCondition = requiresCondition;
        accepted = new HashSet<string>(parameters.Select(p => p.Name), StringComparer.Ordinal);
    }
    /// <summary>在原有数值编译之前拒绝错误字段和缺失输入参数。</summary>
    /// <param name="source">外部操作。</param><param name="path">真实部位及操作索引。</param>
    internal void Validate(ConstructionOperation source, string path)
    {
        foreach (var parameter in ConstructionOperations.Fields)
            if (parameter.Supplied(source) && !accepted.Contains(parameter.Name))
                throw new InvalidDataException(path + "：" + Kind + "不接受参数" + parameter.Name);
        foreach (var parameter in Parameters)
            if (parameter.Required && !parameter.Supplied(source) || parameter.RequiresCondition && parameter.Supplied(source) && source.When == null)
                throw new InvalidDataException(path + "：" + Kind + "缺少必需参数或事件条件：" + parameter.Name);
        if (RequiresCondition && source.When == null) throw new InvalidDataException(path + "：" + Kind + "需要明确事件条件");
    }
}

/// <summary>计划中的实际操作需求，不持有可编辑操作对象。</summary>
public sealed class CompiledOperationRequirement
{
    /// <summary>已经展开的真实部位。</summary>
    public string NodePath { get; }
    /// <summary>原部位资料中的操作索引。</summary>
    public int Index { get; }
    /// <summary>共同的输入输出和参数约定。</summary>
    public ConstructionOperationContract Contract { get; }
    /// <summary>按本次真实参数补齐的状态需求。</summary>
    public ConstructionOperationState State { get; }
    /// <summary>按本次真实参数补齐的资源需求。</summary>
    public ConstructionOperationResource Resources { get; }
    /// <summary>按实际条件和世界选取补齐的输入，不持有运行对象。</summary>
    public IReadOnlyList<ConstructionOperationPort> Inputs { get; }
    /// <summary>本操作按实际参数产生的作用。</summary>
    public ConstructionOperationEffect Effects { get; }
    /// <summary>在编译边界复制实际需求。</summary>
    /// <param name="path">实际部位路径。</param><param name="index">原操作索引。</param><param name="source">已检查操作。</param><param name="contract">已有约定。</param>
    internal CompiledOperationRequirement(string path, int index, ConstructionOperation source, ConstructionOperationContract contract)
    {
        NodePath = path; Index = index; Contract = contract; State = contract.State; Resources = contract.Resources; Effects = contract.Effects;
        bool eventInput = source.When != null || source.Trigger != ConstructionChangeTrigger.Time;
        if (eventInput) State |= ConstructionOperationState.Event;
        if (source.Kind == ConstructionOperationKind.Fly)
        {
            if (source.Ground != ConstructionGroundResponse.Ignore) Resources |= ConstructionOperationResource.Terrain | ConstructionOperationResource.Contacts;
            if (source.Path?.Space == ConstructionPathSpace.TargetLine || source.TurnRate > 0) Resources |= ConstructionOperationResource.Target;
        }
        if (source.WorldMatter != null || source.Recovery?.WorldMaterials == true)
        { Resources |= ConstructionOperationResource.WorldMaterials | ConstructionOperationResource.Geometry; State |= ConstructionOperationState.WorldMaterial; Effects |= ConstructionOperationEffect.World; }
        var inputs = contract.Inputs.Select(port => new ConstructionOperationPort(port.Name, port.Kind,
            port.Required || port.Name == "target" && (Resources & ConstructionOperationResource.Target) != 0 ||
            port.Name == "worldMaterials" && (Resources & ConstructionOperationResource.WorldMaterials) != 0, port.Runtime, port.Collection)).ToList();
        if (eventInput && inputs.All(port => port.Name != "event")) inputs.Add(new ConstructionOperationPort("event", ConstructionValueKind.Result));
        Inputs = inputs.AsReadOnly();
    }
}
