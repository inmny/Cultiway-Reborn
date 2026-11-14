using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>制作顶点的独立数值绑定，成形及蒙皮顺序与共用材质一致。</summary>
internal readonly struct FormSurfaceVertex
{
    /// <summary>完整制作位置与收拢中心。</summary>
    internal readonly Vector3 Position, Centre;
    /// <summary>成形开始进度，负值表示始终完整。</summary>
    internal readonly float BuildAt;
    /// <summary>顶点的四个真实控制及各自权重。</summary>
    private readonly Vector4 controls, weights;
    /// <summary>刚性部件的单控制，负值表示固定。</summary>
    private readonly int rigid;
    /// <summary>是否采用顶点蒙皮。</summary>
    private readonly bool skin;

    /// <summary>只复制已经在资源边界检查的一个顶点。</summary>
    /// <param name="position">制作位置。</param><param name="build">截面中心及成形进度。</param>
    /// <param name="controls">四个控制。</param><param name="weights">对应权重。</param>
    /// <param name="rigid">刚性部件控制。</param><param name="skin">是否蒙皮。</param>
    internal FormSurfaceVertex(Vector3 position, Vector4 build, Vector4 controls, Vector4 weights, int rigid, bool skin)
    {
        Position = position; Centre = new Vector3(build.x, build.y, build.z); BuildAt = build.w;
        this.controls = controls; this.weights = weights; this.rigid = rigid; this.skin = skin;
    }

    /// <summary>先按同一进度展开截面，再独立施加本顶点的实际控制。</summary>
    /// <param name="pose">父形体的同一完成姿态，空值取制作姿态。</param>
    internal Vector3 Sample(FormPose pose)
    {
        if (pose == null) return Position;
        Vector3 point = BuildAt < 0f ? Position : Vector3.Lerp(Centre, Position,
            Mathf.Clamp01((pose.BuildProgress - BuildAt) / FormFormation.TipSpan));
        if (!skin) return rigid < 0 ? point : pose.Skin[rigid].MultiplyPoint3x4(point);
        Vector3 result = Vector3.zero;
        for (int i = 0; i < 4; i++) result += pose.Skin[(int)controls[i]].MultiplyPoint3x4(point) * weights[i];
        return result;
    }

    /// <summary>从真实控制链与截面端点保守覆盖此顶点，不把平均权重作用于平均位置。</summary>
    /// <param name="rig">父体骨架。</param><param name="motions">本计划实际采用的动作。</param>
    internal float RadiusBound(FormRig rig, FormMotion[] motions)
    {
        Vector3 position = Position, centre = Centre;
        /// <summary>覆盖同一顶点的完整位置及收拢中心。</summary><param name="control">单个实际控制。</param>
        float Radius(int control) => control < 0 ? Mathf.Max(position.magnitude, centre.magnitude) :
            Mathf.Max(rig.RadiusBound(control, position, motions), rig.RadiusBound(control, centre, motions));
        if (!skin) return Radius(rigid);
        float result = 0f;
        for (int i = 0; i < 4; i++) if (weights[i] > 0f) result += weights[i] * Radius((int)controls[i]);
        return result;
    }
}

/// <summary>有限三角面上的真实附着点，运行只读取三个顶点和共同姿态。</summary>
internal sealed class FormSurfaceBinding
{
    /// <summary>在同一资源版本中的实际部件及三角面，供追溯与检查。</summary>
    internal readonly int Part, Face;
    /// <summary>面内三个权重，和为一。</summary>
    internal readonly Vector3 Barycentric;
    /// <summary>三个顶点必须均开始成形后才使用该面。</summary>
    internal readonly float BuildAt;
    /// <summary>三个制作顶点，保留各自独立的蒙皮和截面。</summary>
    private readonly FormSurfaceVertex a, b, c;
    /// <summary>退化姿态仍保留有限朝向，实际作用由Ready拒绝。</summary>
    private readonly Quaternion restRotation;

    /// <summary>保存同一实际面与有限面内位置，不引用网格对象。</summary>
    /// <param name="part">实际部件。</param><param name="face">实际三角面。</param><param name="barycentric">面内位置。</param>
    /// <param name="a">第一个顶点。</param><param name="b">第二个顶点。</param><param name="c">第三个顶点。</param>
    internal FormSurfaceBinding(int part, int face, Vector3 barycentric, FormSurfaceVertex a, FormSurfaceVertex b, FormSurfaceVertex c)
    {
        Part = part; Face = face; Barycentric = barycentric; this.a = a; this.b = b; this.c = c;
        BuildAt = Mathf.Max(0f, Mathf.Max(a.BuildAt, Mathf.Max(b.BuildAt, c.BuildAt)));
        Vector3 tangent = b.Position - a.Position, normal = Vector3.Cross(tangent, c.Position - a.Position);
        restRotation = Quaternion.LookRotation(normal / Mathf.Sqrt(normal.sqrMagnitude), tangent / Mathf.Sqrt(tangent.sqrMagnitude));
    }

    /// <summary>原面退去或完全折叠时不能继续提供附着作用。</summary>
    /// <param name="pose">同一父体姿态。</param>
    internal bool Ready(FormPose pose) => (pose?.BuildProgress ?? 1f) > BuildAt &&
        Vector3.Cross(b.Sample(pose) - a.Sample(pose), c.Sample(pose) - a.Sample(pose)).sqrMagnitude > 1e-20f;

    /// <summary>先分别变换三个顶点，再取得面内位置及实际法向，不混合整个部件的变换。</summary>
    /// <param name="pose">父体的完成姿态。</param>
    internal Matrix4x4 Frame(FormPose pose)
    {
        Vector3 p = a.Sample(pose), q = b.Sample(pose), r = c.Sample(pose);
        Vector3 tangent = q - p, normal = Vector3.Cross(tangent, r - p);
        Quaternion rotation = normal.sqrMagnitude > 1e-20f
            ? Quaternion.LookRotation(normal / Mathf.Sqrt(normal.sqrMagnitude), tangent / Mathf.Sqrt(tangent.sqrMagnitude))
            : restRotation;
        return Matrix4x4.TRS(p * Barycentric.x + q * Barycentric.y + r * Barycentric.z, rotation, Vector3.one);
    }

    /// <summary>三个实际顶点的独立控制包络按面内权重合成。</summary>
    /// <param name="rig">父骨架。</param><param name="motions">实际采用的制作动作。</param>
    internal float RadiusBound(FormRig rig, FormMotion[] motions) =>
        a.RadiusBound(rig, motions) * Barycentric.x + b.RadiusBound(rig, motions) * Barycentric.y + c.RadiusBound(rig, motions) * Barycentric.z;
}
