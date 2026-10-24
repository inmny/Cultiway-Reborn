using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cultiway.Core.Visuals.Forms;

namespace Cultiway.Core.Skills;

/// <summary>仅在编译边界使用的操作输入，不进入共享运行参数表。</summary>
internal readonly struct ConstructionOperationCompileContext
{
    /// <summary>本次正在检查的外部资料及实际制作资源。</summary>
    internal readonly ConstructionOperation Source;
    /// <summary>当前声明部位。</summary>
    internal readonly ConstructionNode Node;
    /// <summary>实际资源。</summary>
    internal readonly FormResource Resource;
    /// <summary>已经核对的向量。</summary>
    internal readonly Vector3 Vector;
    /// <summary>整次截止。</summary>
    internal readonly float Lifetime;
    /// <summary>具体资料位置。</summary>
    internal readonly string Path;
    /// <summary>已经展开的真实分件。</summary>
    internal readonly CompiledFracture Fracture;
    /// <summary>原声明顺序。</summary>
    internal readonly int Order;
    /// <summary>一次传递编译实际需要的输入。</summary>
    /// <param name="source">原操作。</param><param name="vector">真实向量。</param><param name="lifetime">截止。</param>
    /// <param name="path">资料位置。</param><param name="resource">资源。</param><param name="node">原部位。</param>
    /// <param name="fracture">分件。</param><param name="order">声明顺序。</param>
    internal ConstructionOperationCompileContext(ConstructionOperation source, Vector3 vector, float lifetime, string path,
        FormResource resource, ConstructionNode node, CompiledFracture fracture, int order)
    { Source = source; Vector = vector; Lifetime = lifetime; Path = path; Resource = resource; Node = node; Fracture = fracture; Order = order < 0 ? Array.IndexOf(node.Operations, source) : order; }
}

/// <summary>真实过程的完成及替换事实，与部位自身结束分别处理。</summary>
internal readonly struct ConstructionOperationProgress
{
    /// <summary>操作已经完成或被替换。</summary>
    internal readonly bool Completed, Interrupted;
    /// <summary>复制本次实际状态。</summary>
    /// <param name="completed">实际完成。</param><param name="interrupted">实际中断。</param>
    internal ConstructionOperationProgress(bool completed, bool interrupted = false) { Completed = completed; Interrupted = interrupted; }
}

/// <summary>一类操作共用的编译、数值派发和完成读取，不按法术编号分流。</summary>
internal abstract class ConstructionOperationImplementation
{
    /// <summary>已经登记的真实约定。</summary>
    internal readonly ConstructionOperationContract Contract;
    /// <summary>旧原定时操作已有专用执行字段；新操作缺省使用共用时点和事件入口。</summary>
    internal readonly bool Direct;
    /// <summary>保存一种不可替换的实现。</summary>
    /// <param name="contract">共同约定。</param><param name="direct">是否已有专用定时入口。</param>
    protected ConstructionOperationImplementation(ConstructionOperationContract contract, bool direct) { Contract = contract; Direct = direct; }
    /// <summary>只在边界解析一次参数。</summary>
    /// <param name="context">当前真实输入。</param>
    internal abstract object Compile(ConstructionOperationCompileContext context);
    /// <summary>建立本类连续参数表。</summary>
    /// <param name="actions">已经展开的本类操作。</param><param name="range">连续指令范围。</param>
    internal abstract CompiledOperationGroup Group(CompiledEventOperation[] actions, CompiledRange range);
    /// <summary>创建时确定是否在实际应用后立即完成。</summary>
    /// <param name="parameters">已编译参数。</param>
    internal abstract bool Immediate(object parameters);
    /// <summary>只从本次数值状态读取过程事实。</summary>
    /// <param name="cast">本次施放。</param><param name="action">真实操作。</param><param name="native">是否观察原定时执行。</param>
    internal abstract ConstructionOperationProgress Progress(AbilityCast cast, CompiledEventOperation action, bool native);
    /// <summary>完整结构展开后核对实际目标，不在每帧重复检查。</summary>
    /// <param name="node">完整实际部位。</param><param name="action">本次操作。</param><param name="path">具体位置。</param>
    internal abstract void ValidateTarget(CompiledForm node, CompiledEventOperation action, string path);
}

/// <summary>所有操作共用这一种参数适配；T是具体不可变数值类型。</summary>
/// <typeparam name="T">该类操作自己的参数。</typeparam>
internal sealed class ConstructionOperationImplementation<T> : ConstructionOperationImplementation
{
    /// <summary>仅创建时使用的解析方法。</summary>
    private readonly Func<ConstructionOperationCompileContext, T> compile;
    /// <summary>每次真实执行的方法。</summary>
    private readonly Func<AbilityCast, CompiledEventOperation, T, bool> apply;
    /// <summary>真实完成读取。</summary>
    private readonly Func<AbilityCast, CompiledEventOperation, T, bool, ConstructionOperationProgress> progress;
    /// <summary>即时完成约定。</summary>
    private readonly Func<T, bool> immediate;
    /// <summary>完整目标检查。</summary>
    private readonly Action<CompiledForm, T, string> validate;
    /// <summary>一次绑定具体编译与数值函数，不在运行中反射。</summary>
    /// <param name="contract">共同约定。</param><param name="compile">参数编译。</param><param name="apply">实际应用，失效输入返回false。</param>
    /// <param name="progress">过程读取。</param><param name="immediate">是否即时完成。</param><param name="validate">目标检查。</param><param name="direct">原定时入口。</param>
    internal ConstructionOperationImplementation(ConstructionOperationContract contract, Func<ConstructionOperationCompileContext, T> compile,
        Func<AbilityCast, CompiledEventOperation, T, bool> apply,
        Func<AbilityCast, CompiledEventOperation, T, bool, ConstructionOperationProgress> progress,
        Func<T, bool> immediate, Action<CompiledForm, T, string> validate, bool direct) : base(contract, direct)
    { this.compile = compile; this.apply = apply; this.progress = progress; this.immediate = immediate; this.validate = validate; }
    /// <inheritdoc />
    internal override object Compile(ConstructionOperationCompileContext context) => compile(context);
    /// <inheritdoc />
    internal override CompiledOperationGroup Group(CompiledEventOperation[] actions, CompiledRange range)
        => new CompiledOperationGroup<T>(Contract.Kind, range, actions.Select(action => (T)action.Parameters).ToArray(), apply);
    /// <inheritdoc />
    internal override bool Immediate(object parameters) => immediate?.Invoke((T)parameters) == true;
    /// <inheritdoc />
    internal override ConstructionOperationProgress Progress(AbilityCast cast, CompiledEventOperation action, bool native)
        => progress == null ? default : progress(cast, action, (T)action.Parameters, native);
    /// <inheritdoc />
    internal override void ValidateTarget(CompiledForm node, CompiledEventOperation action, string path)
        => validate?.Invoke(node, (T)action.Parameters, path);
}

/// <summary>同种操作的连续参数与派发函数；原资料次序仍由指令状态映射维护。</summary>
internal abstract class CompiledOperationGroup
{
    /// <summary>稳定操作种类及连续指令范围。</summary>
    internal readonly ConstructionOperationKind Kind;
    /// <summary>实际指令范围。</summary>
    internal readonly CompiledRange Range;
    /// <summary>保存本类资料身份。</summary>
    /// <param name="kind">稳定种类。</param><param name="range">连续范围。</param>
    protected CompiledOperationGroup(ConstructionOperationKind kind, CompiledRange range) { Kind = kind; Range = range; }
    /// <summary>直接读取具体参数表并调用已登记函数。</summary>
    /// <param name="cast">真实施放。</param><param name="action">实际身份。</param><param name="parameter">参数行。</param>
    internal abstract bool Apply(AbilityCast cast, CompiledEventOperation action, int parameter);
}

/// <summary>同类T数组，不把不同种类参数混成逐帧解释的描述。</summary>
/// <typeparam name="T">本类具体参数。</typeparam>
internal sealed class CompiledOperationGroup<T> : CompiledOperationGroup
{
    /// <summary>本类连续参数表。</summary>
    internal readonly T[] Parameters;
    /// <summary>实际数值函数。</summary>
    private readonly Func<AbilityCast, CompiledEventOperation, T, bool> apply;
    /// <summary>共享同一版本数值资料。</summary>
    /// <param name="kind">稳定种类。</param><param name="range">指令范围。</param><param name="parameters">已编译参数。</param><param name="apply">数值函数。</param>
    internal CompiledOperationGroup(ConstructionOperationKind kind, CompiledRange range, T[] parameters,
        Func<AbilityCast, CompiledEventOperation, T, bool> apply) : base(kind, range) { Parameters = parameters; this.apply = apply; }
    /// <inheritdoc />
    internal override bool Apply(AbilityCast cast, CompiledEventOperation action, int parameter) => apply(cast, action, Parameters[parameter]);
}
