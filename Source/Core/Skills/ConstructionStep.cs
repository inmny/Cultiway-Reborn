using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>操作输出被使用的真实阶段。</summary>
public enum ConstructionOutputStage
{
    /// <summary>部位和关系在开始后可用，过程和结果等待完成。</summary>
    Auto = 0,
    /// <summary>实际开始后可用。</summary>
    Started = 1,
    /// <summary>实际完成后可用。</summary>
    Completed = 2
}

/// <summary>引用已存在部位或另一操作的命名输出。</summary>
public sealed class ConstructionOutputReference
{
    /// <summary>直接引用的完整展开部位路径，与Step二选一。</summary>
    public string Node;
    /// <summary>产生输出的步骤编号，与Node二选一。</summary>
    public string Step;
    /// <summary>共同约定中的输出名；直接部位默认part，也可用structure。</summary>
    public string Output;
    /// <summary>所需真实阶段，不按预估历时虚构完成。</summary>
    public ConstructionOutputStage Stage;
}

/// <summary>可保存的命名操作，输入可引用之前或之后声明的其他输出。</summary>
public sealed class ConstructionStep
{
    /// <summary>能力内唯一的稳定编号。</summary>
    public string Id;
    /// <summary>共同操作及其参数，沿用原有执行实现。</summary>
    public ConstructionOperation Operation;
    /// <summary>按共同端口名绑定输入。</summary>
    public Dictionary<string, ConstructionOutputReference> Inputs;
}

/// <summary>一次运行中真实发生的输出事实，不保存Unity资源或人物对象。</summary>
public readonly struct ConstructionOutputValue
{
    /// <summary>真实部位及其实际父结构索引。</summary>
    public readonly int Node, Parent;
    /// <summary>输出的资料种类。</summary>
    public readonly ConstructionValueKind Kind;
    /// <summary>事实确认时刻。</summary>
    public readonly float At;
    /// <summary>事实当时的真实位置。</summary>
    public readonly Vector3 Position;
    /// <summary>事实当时的实际材料。</summary>
    public readonly AbilityMatter Matter;
    /// <summary>同次施放中真实的控制归属索引。</summary>
    public readonly int Authority;
    /// <summary>复制一次已发生的输出。</summary>
    /// <param name="node">实际部位。</param><param name="parent">实际父体。</param><param name="kind">资料种类。</param>
    /// <param name="at">确认时刻。</param><param name="position">实际位置。</param><param name="matter">实际材料。</param><param name="authority">实际归属。</param>
    internal ConstructionOutputValue(int node, int parent, ConstructionValueKind kind, float at, Vector3 position, AbilityMatter matter, int authority)
    { Node = node; Parent = parent; Kind = kind; At = at; Position = position; Matter = matter; Authority = authority; }
}

/// <summary>只在计划中保存的直接输入索引。</summary>
internal readonly struct CompiledStepInput
{
    /// <summary>来源步骤，负一为直接部位。</summary>
    internal readonly int Step, Node;
    /// <summary>已经核对的输出种类及阶段。</summary>
    internal readonly ConstructionValueKind Kind;
    /// <summary>是否等待完成。</summary>
    internal readonly bool Completed;
    /// <summary>是否需要仍有效的实际父连接。</summary>
    internal readonly bool Connection;
    /// <summary>一次解析名称后的固定输入。</summary>
    /// <param name="step">来源步骤。</param><param name="node">实际部位。</param><param name="kind">资料种类。</param>
    /// <param name="completed">是否等待完成。</param><param name="connection">是否核对活连接。</param>
    internal CompiledStepInput(int step, int node, ConstructionValueKind kind, bool completed, bool connection)
    { Step = step; Node = node; Kind = kind; Completed = completed; Connection = connection; }
}

/// <summary>已经解析并按依赖排序的单个操作，不持有可编辑输入。</summary>
internal sealed class CompiledConstructionStep
{
    /// <summary>稳定编号、真实目标及共同约定。</summary>
    internal readonly string Id;
    /// <summary>实际目标部位。</summary>
    internal readonly int Node;
    /// <summary>已经解析的输入。</summary>
    internal readonly CompiledStepInput[] Inputs;
    /// <summary>沿用条件操作的数值执行资料。</summary>
    internal readonly CompiledEventOperation Action;
    /// <summary>公开的共同输入输出约定。</summary>
    internal readonly ConstructionOperationContract Contract;
    /// <summary>构造只含索引和只读参数的步骤。</summary>
    /// <param name="id">稳定编号。</param><param name="node">目标部位。</param><param name="inputs">已解析输入。</param>
    /// <param name="action">共同执行参数。</param><param name="contract">共同约定。</param>
    internal CompiledConstructionStep(string id, int node, CompiledStepInput[] inputs, CompiledEventOperation action, ConstructionOperationContract contract)
    { Id = id; Node = node; Inputs = inputs; Action = action; Contract = contract; }
}
