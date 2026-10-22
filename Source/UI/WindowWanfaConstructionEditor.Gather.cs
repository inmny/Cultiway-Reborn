using System.Linq;
using Cultiway.Core.Skills;

namespace Cultiway.UI;

/// <summary>部件聚合使用现有参数表单，定时与事件过程共用目标字段。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>对真实子结构开放初始聚合，不把根部误当成可汇入的部件。</summary>
    /// <param name="node">当前草稿部件。</param>
    private void InitialGatherRows(ConstructionNode node)
    {
        if (part == 0) return;
        ConstructionOperation gather = node.Operations.FirstOrDefault(o => o.Kind == ConstructionOperationKind.Gather && o.When == null);
        Toggle("GatherEnabled", gather != null, value => node.Operations = value ?
            node.Operations.Concat(new[] { new ConstructionOperation { Kind = ConstructionOperationKind.Gather, Seconds = 1f } }).ToArray() :
            node.Operations.Where(o => o != gather).ToArray());
        if (gather == null) return;
        Number("GatherAt", gather.At, value => gather.At = value);
        GatherParameters(node, gather);
    }

    /// <summary>目标采用父挂点坐标，朝向覆盖与原有连续旋转分别保存。</summary>
    /// <param name="node">提供原朝向的真实部件。</param>
    /// <param name="gather">初始或事件聚合操作。</param>
    private void GatherParameters(ConstructionNode node, ConstructionOperation gather)
    {
        Number("GatherSeconds", gather.Seconds, value => gather.Seconds = value);
        for (int axis = 0; axis < 3; axis++)
        {
            int component = axis;
            Number("GatherTarget" + new[] { "X", "Y", "Z" }[axis], gather.Vector[axis], value => gather.Vector[component] = value);
        }
        Toggle("GatherFacing", gather.GatherRotation != null, value => gather.GatherRotation = value ? (float[])node.Rotation.Clone() : null);
        if (gather.GatherRotation == null) return;
        for (int axis = 0; axis < 3; axis++)
        {
            int component = axis;
            Number("GatherRotation" + new[] { "X", "Y", "Z" }[axis], gather.GatherRotation[axis], value => gather.GatherRotation[component] = value);
        }
    }
}
