using System.Linq;

namespace Cultiway.Core.Skills;

/// <summary>比较同一真实部位的过程声明；时间和依赖仍由原有共同程序执行。</summary>
public static partial class AbilityCompiler
{
    /// <summary>独占过程不能由普通、条件、命名入口同时占用同一主体。</summary>
    /// <param name="form">已展开的实际部位。</param><param name="kind">将加入的操作。</param>
    /// <param name="path">当前操作的声明位置。</param>
    private static void CheckProcessConflict(CompiledForm form, ConstructionOperationKind kind, string path)
    {
        if (kind is not (ConstructionOperationKind.Flow or ConstructionOperationKind.Fracture or
            ConstructionOperationKind.Leave or ConstructionOperationKind.Recover or ConstructionOperationKind.Ray)) return;
        foreach (var previous in form.Operations)
        {
            if (previous == null) continue;
            ConstructionOperationKind earlier = previous.Contract.Kind;
            if (earlier == kind)
                ProcessConflict(form, path, previous.Location, "同一部位只支持一份这样的实际过程；请保留一项或放到不同部位");
            if (kind == ConstructionOperationKind.Flow && earlier == ConstructionOperationKind.Fracture ||
                kind == ConstructionOperationKind.Fracture && earlier == ConstructionOperationKind.Flow)
                ProcessConflict(form, path, previous.Location, "流散和固定碎解不能操作同一主体；请用独立后段交接");
            if (kind == ConstructionOperationKind.Ray && ConflictsWithRay(earlier) ||
                earlier == ConstructionOperationKind.Ray && ConflictsWithRay(kind))
                ProcessConflict(form, path, previous.Location, "射线不能与流散、碎解或留置共用同一主体；请使用独立后续部位");
        }
    }

    /// <summary>这些过程占用射线同一主体，沿用原有的互斥规则。</summary>
    /// <param name="kind">实际过程。</param>
    private static bool ConflictsWithRay(ConstructionOperationKind kind) =>
        kind is ConstructionOperationKind.Flow or ConstructionOperationKind.Fracture or ConstructionOperationKind.Leave;

    /// <summary>射线与其他事件骨架过程各有原始声明，按已经编译的参数区分。</summary>
    /// <param name="form">同一实际部位。</param>
    private static void CheckRayMotionConflict(CompiledForm form)
    {
        var ray = form.EventOperations.FirstOrDefault(action => action.Ray != null);
        if (ray == null) return;
        foreach (var action in form.EventOperations)
            if (action.Motion != null || action.Propagation != null)
                ProcessConflict(form, ray.Path, action.Path, "射线不能与其他事件骨架过程共用同一主体；请使用独立后续部位");
    }

    /// <summary>携带两项真实声明及展开位置，万法阁可以分别定位。</summary>
    /// <param name="form">共同操作目标。</param><param name="path">当前声明。</param>
    /// <param name="previous">先前声明。</param><param name="reason">不能叠加的原因。</param>
    private static void ProcessConflict(CompiledForm form, string path, string previous, string reason) =>
        throw ConstructionReferences.Error(path, reason, related: previous, part: form.Path, declaration: form.DeclarationPath);
}
