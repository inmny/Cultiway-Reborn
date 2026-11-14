using System.IO;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>制作明确声明的纵向伸展；横截面和两端厚度由原网格保持。</summary>
public sealed class FormSpan
{
    /// <summary>末端实际控制索引。</summary>
    public readonly int Control;
    /// <summary>制作时的整体长度与端部半径。</summary>
    public readonly float Length, Radius;
    /// <summary>模型空间中的制作前向。</summary>
    public readonly Vector3 Axis;

    /// <summary>在资源读取边界解析已声明能力，不按模型编号猜测。</summary>
    /// <param name="resource">已加载的网格和控制。</param>
    internal FormSpan(FormResource resource)
    {
        var parameters = resource.Structure.Parameters;
        if (!parameters.TryGetValue("span.length", out var length) || length.Count != 1 ||
            !parameters.TryGetValue("span.radius", out var radius) || radius.Count != 1 ||
            !FormRig.Finite(length[0]) || !FormRig.Finite(radius[0]) || radius[0] <= 0f || length[0] <= radius[0] * 2f)
            throw new InvalidDataException(resource.Description.Id + "：伸展能力需要明确的制作长度及端部厚度");
        Length = length[0]; Radius = radius[0]; Control = resource.Rig.Control("extent");
        if (Control < 0 || resource.Meshes.Length == 0 || resource.Builds || resource.Description.Motions.Length != 0)
            throw new InvalidDataException(resource.Description.Id + "：伸展需要真实静态蒙皮及extent末端控制");
        Axis = new Vector3(resource.Description.Forward[0], resource.Description.Forward[1], resource.Description.Forward[2]).normalized;
    }
}
