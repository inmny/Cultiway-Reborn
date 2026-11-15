using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using State = Cultiway.Core.Skills.ConstructionOperationState;
using Resource = Cultiway.Core.Skills.ConstructionOperationResource;
using Effect = Cultiway.Core.Skills.ConstructionOperationEffect;
using Value = Cultiway.Core.Skills.ConstructionValueKind;

namespace Cultiway.Core.Skills;

/// <summary>共用操作的约定及执行登记，新增原理不需要逐个修改已有法术。</summary>
public static class ConstructionOperations
{
    /// <summary>公共时点、类型和真实条件不属于某一专用操作。</summary>
    private static readonly string[] common = { nameof(ConstructionOperation.Kind), nameof(ConstructionOperation.At), nameof(ConstructionOperation.When), nameof(ConstructionOperation.Id) };
    /// <summary>仅在编译边界读取的真实字段和格式默认值。</summary>
    internal static readonly ConstructionOperationParameter[] Fields = typeof(ConstructionOperation).GetFields(BindingFlags.Public | BindingFlags.Instance)
        .Select(field => new ConstructionOperationParameter(field, new ConstructionOperation(), false, false)).ToArray();
    /// <summary>按已提供执行实现的种类索引，不在每帧反射或查询原资料。</summary>
    private static readonly Dictionary<ConstructionOperationKind, ConstructionOperationContract> contracts = Create();
    /// <summary>每类操作只有一份不可替换的数值实现。</summary>
    private static readonly Dictionary<ConstructionOperationKind, ConstructionOperationImplementation> implementations = new();
    /// <summary>约定完成后一次登记实际编译和执行。</summary>
    static ConstructionOperations()
    {
        AbilityCast.RegisterOperationImplementations();
        if (implementations.Count != contracts.Count) throw new InvalidOperationException("操作约定与实际实现未成对登记");
    }

    /// <summary>明确登记编译、应用和真实完成；已有编号不能被静默替换。</summary>
    /// <typeparam name="T">本类具体不可变参数。</typeparam>
    /// <param name="kind">稳定操作种类。</param><param name="compile">创建时解析。</param><param name="apply">实际数值应用。</param>
    /// <param name="progress">过程事实。</param><param name="immediate">是否即时完成。</param><param name="validate">完整目标检查。</param>
    /// <param name="direct">仅既有专用定时实现使用，新原理采用共用调度。</param>
    internal static void Register<T>(ConstructionOperationKind kind, Func<ConstructionOperationCompileContext, T> compile,
        Func<AbilityCast, CompiledEventOperation, T, bool> apply,
        Func<AbilityCast, CompiledEventOperation, T, bool, ConstructionOperationProgress> progress = null,
        Func<T, bool> immediate = null, Action<CompiledForm, T, string> validate = null, bool direct = false)
    {
        if (compile == null || apply == null) throw new ArgumentException("操作登记必须提供编译和真实执行");
        if (implementations.ContainsKey(kind)) throw new InvalidOperationException("操作编号已经登记：" + kind);
        implementations.Add(kind, new ConstructionOperationImplementation<T>(Describe(kind), compile, apply, progress, immediate, validate, direct));
    }

    /// <summary>编译时一次取得实际实现，运行只保留该引用。</summary>
    /// <param name="kind">稳定操作种类。</param>
    internal static ConstructionOperationImplementation Implementation(ConstructionOperationKind kind) => implementations.TryGetValue(kind, out var found)
        ? found : throw new InvalidDataException("操作没有登记真实执行：" + kind);

    /// <summary>工具只读枚举所有已有约定。</summary>
    public static IReadOnlyList<ConstructionOperationContract> All { get; } = Array.AsReadOnly(contracts.Values.OrderBy(c => c.Kind).ToArray());

    /// <summary>取得一种已有操作的输入输出及需求。</summary>
    /// <param name="kind">已有执行种类。</param>
    public static ConstructionOperationContract Describe(ConstructionOperationKind kind) => contracts.TryGetValue(kind, out var found)
        ? found : throw new InvalidDataException("没有已实现的操作约定：" + kind);

    /// <summary>在具体资料位置解析约定并核对参数归属。</summary>
    /// <param name="source">外部操作。</param><param name="path">部位与操作索引。</param>
    /// <param name="bound">已有实际输入作为运行条件。</param>
    internal static ConstructionOperationContract Resolve(ConstructionOperation source, string path, bool bound = false)
    {
        if (source == null || !contracts.TryGetValue(source.Kind, out var found)) throw new InvalidDataException(path + "：操作为空或没有执行约定");
        found.Validate(source, path, bound); return found;
    }

    /// <summary>建立与实际执行一致的输入输出、字段及状态声明。</summary>
    private static Dictionary<ConstructionOperationKind, ConstructionOperationContract> Create()
    {
        var result = new Dictionary<ConstructionOperationKind, ConstructionOperationContract>();
        void Add(ConstructionOperationKind kind, ConstructionOperationPort[] inputs, ConstructionOperationPort[] outputs,
            State state, Resource resources, Effect effects, string[] fields, string[] required = null, string[] conditional = null, bool requiresCondition = false)
        {
            var defaults = new ConstructionOperation();
            var parameters = common.Concat(fields).Select(name => new ConstructionOperationParameter(
                typeof(ConstructionOperation).GetField(name), defaults, required?.Contains(name) == true, conditional?.Contains(name) == true)).ToArray();
            result.Add(kind, new ConstructionOperationContract(kind, inputs, outputs, state, resources, effects, parameters, requiresCondition));
        }
        var part = new ConstructionOperationPort("part", Value.Part);
        var material = new ConstructionOperationPort("material", Value.Material);
        var world = new ConstructionOperationPort("worldMaterials", Value.Material, false, true, true);
        Add(ConstructionOperationKind.Grow, new[] { part }, new[] { part, Port("growth", Value.Process) },
            State.Pose | State.Growth, Resource.None, Effect.Pose | Effect.Shape,
            new[] { nameof(ConstructionOperation.Seconds), nameof(ConstructionOperation.TargetScale) },
            new[] { nameof(ConstructionOperation.Seconds) }, new[] { nameof(ConstructionOperation.TargetScale) });
        Add(ConstructionOperationKind.Spin, new[] { part }, new[] { part, Port("rotation", Value.Process) },
            State.Pose, Resource.None, Effect.Pose,
            new[] { nameof(ConstructionOperation.Vector) });
        Add(ConstructionOperationKind.Detach, new[] { part, Port("connection", Value.Relation) },
            new[] { part, Port("released", Value.Result) }, State.Pose | State.Connection | State.Momentum, Resource.None, Effect.Connection | Effect.Movement,
            Array.Empty<string>());
        Add(ConstructionOperationKind.Fly, new[] { part, new ConstructionOperationPort("target", Value.Position, false) },
            new[] { part, Port("flight", Value.Process) }, State.Pose | State.Connection | State.Momentum, Resource.None, Effect.Movement | Effect.Connection,
            new[] { nameof(ConstructionOperation.Vector), nameof(ConstructionOperation.TurnRate), nameof(ConstructionOperation.Acceleration),
                nameof(ConstructionOperation.WorldVelocity), nameof(ConstructionOperation.Ground), nameof(ConstructionOperation.Path), nameof(ConstructionOperation.Goal), nameof(ConstructionOperation.Transfer), nameof(ConstructionOperation.InheritVelocity) });
        Add(ConstructionOperationKind.ChangeSpeed, new[] { part }, new[] { part, Port("speed", Value.Result) },
            State.Momentum, Resource.None, Effect.Movement, new[] { nameof(ConstructionOperation.Speed) });
        Add(ConstructionOperationKind.Animate, new[] { part }, new[] { part, Port("animation", Value.Process) },
            State.Pose | State.Motion, Resource.Motion, Effect.Pose | Effect.Shape,
            new[] { nameof(ConstructionOperation.Clip), nameof(ConstructionOperation.Seconds), nameof(ConstructionOperation.BlendSeconds), nameof(ConstructionOperation.Hold) },
            new[] { nameof(ConstructionOperation.Clip) }, new[] { nameof(ConstructionOperation.BlendSeconds) });
        Add(ConstructionOperationKind.ConvertMatter, new[] { part, world }, new[] { material, Port("converted", Value.Result) },
            State.Pose | State.Matter, Resource.Matter, Effect.Matter,
            new[] { nameof(ConstructionOperation.Matter), nameof(ConstructionOperation.State), nameof(ConstructionOperation.Trigger), nameof(ConstructionOperation.WorldMatter) },
            new[] { nameof(ConstructionOperation.Matter) });
        Add(ConstructionOperationKind.Stop, new[] { part, Port("event", Value.Result) }, new[] { part, Port("stopped", Value.Result) },
            State.Pose | State.Growth | State.Motion | State.Momentum | State.Connection, Resource.None, Effect.Pose | Effect.Movement | Effect.Connection,
            Array.Empty<string>(), requiresCondition: true);
        Add(ConstructionOperationKind.Gather, new[] { part, Port("parent", Value.Part) }, new[] { part, Port("gathering", Value.Process), Port("connection", Value.Relation) },
            State.Pose | State.Connection, Resource.None, Effect.Pose,
            new[] { nameof(ConstructionOperation.Vector), nameof(ConstructionOperation.Seconds), nameof(ConstructionOperation.GatherRotation) },
            new[] { nameof(ConstructionOperation.Seconds) });
        Add(ConstructionOperationKind.Fracture, new[] { part }, new[] { new ConstructionOperationPort("parts", Value.Part, true, true, true), Port("fractured", Value.Result) },
            State.Pose | State.Matter | State.Fragments | State.Momentum | State.Connection, Resource.Geometry | Resource.Parts | Resource.Terrain, Effect.Shape | Effect.Connection | Effect.Movement,
            new[] { nameof(ConstructionOperation.Fracture) }, new[] { nameof(ConstructionOperation.Fracture) });
        Add(ConstructionOperationKind.Flow, new[] { part }, new[] { part, Port("flow", Value.Process) },
            State.Pose | State.Matter | State.Flow | State.Momentum, Resource.Geometry | Resource.Terrain, Effect.Shape | Effect.Movement,
            new[] { nameof(ConstructionOperation.Flow) }, new[] { nameof(ConstructionOperation.Flow) });
        Add(ConstructionOperationKind.Recover, new[] { part, Port("payment", Value.Result), world }, new[] { Port("recovery", Value.Process), Port("receipt", Value.Result) },
            State.Pose | State.Matter | State.Recovery, Resource.Geometry | Resource.Payment, Effect.Recovery | Effect.Shape,
            new[] { nameof(ConstructionOperation.Seconds), nameof(ConstructionOperation.Recovery) }, new[] { nameof(ConstructionOperation.Seconds) });
        Add(ConstructionOperationKind.Leave, new[] { part }, new[] { material, Port("transferred", Value.Result) },
            State.Pose | State.Matter | State.WorldMaterial, Resource.Geometry | Resource.WorldMaterials, Effect.World,
            new[] { nameof(ConstructionOperation.Residue) });
        Add(ConstructionOperationKind.Conduct, new[] { part, Port("water", Value.Material) }, new[] { Port("conduction", Value.Process), Port("hits", Value.Result) },
            State.Pose | State.Conduction, Resource.Geometry | Resource.Water | Resource.Contacts, Effect.Propagation,
            new[] { nameof(ConstructionOperation.Conduction) });
        Add(ConstructionOperationKind.Deflect, new[] { part }, new[] { part, Port("deflected", Value.Result) },
            State.Pose | State.Momentum, Resource.None, Effect.Pose | Effect.Movement,
            new[] { nameof(ConstructionOperation.Vector), nameof(ConstructionOperation.WorldVelocity) },
            new[] { nameof(ConstructionOperation.Vector) });
        Add(ConstructionOperationKind.Propagate, new[] { part }, new[] { part, Port("propagation", Value.Process) },
            State.Pose | State.Connection | State.Momentum, Resource.Terrain | Resource.Contacts, Effect.Shape | Effect.Connection | Effect.Propagation,
            new[] { nameof(ConstructionOperation.Propagation) }, new[] { nameof(ConstructionOperation.Propagation) });
        Add(ConstructionOperationKind.Ray, new[] { part, new ConstructionOperationPort("target", Value.Position, false) },
            new[] { part, Port("ray", Value.Process) }, State.Pose | State.Motion, Resource.Motion, Effect.Pose | Effect.Shape,
            new[] { nameof(ConstructionOperation.Ray) }, new[] { nameof(ConstructionOperation.Ray) });
        return result;
    }

    /// <summary>统一创建必需的真实输入或输出。</summary>
    /// <param name="name">稳定端口名。</param><param name="kind">资料种类。</param>
    private static ConstructionOperationPort Port(string name, Value kind) => new(name, kind);
}
