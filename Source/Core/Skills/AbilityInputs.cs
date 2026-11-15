using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>生成主体可以实际连接的施放输入，不依靠整招编号选择。</summary>
public enum AbilityAnchor
{
    /// <summary>本次实际目标的脚下位置。</summary>
    Target = 0,

    /// <summary>来源人物的脚下位置。</summary>
    Source = 1,

    /// <summary>本次身体动作的实际腕点。</summary>
    Hand = 2,

    /// <summary>本次选择的实际发力部位，肘肩不退回腕点。</summary>
    Effector = 3,

    /// <summary>本次施放实际取得控制的外部载体，由来源玩法提供数值姿态。</summary>
    Carrier = 4
}

/// <summary>可保存的根连接，后续形体继续使用普通父子挂点。</summary>
public sealed class ConstructionBinding
{
    /// <summary>本次输入的连接对象。</summary>
    public AbilityAnchor Anchor;

    /// <summary>解除前是否跟随实际连接对象。</summary>
    public bool Follow;

    /// <summary>跟随时只在新位置接触，不把两次输入之间的空隙当成飞行路径。</summary>
    public bool ContactAtDestination;

    /// <summary>来源或目标根朝向实际出手方向；手部始终使用共同手姿态。</summary>
    public bool FaceTarget;

    /// <summary>局部尺寸及偏移按实际来源体高缩放。</summary>
    public bool ScaleByBody;

    /// <summary>在地图竖直轴额外抬高的格数，不随镜头改变。</summary>
    public float Height;
}

/// <summary>主线程采集的施放数值输入，工作计算不读取人物或Unity对象。</summary>
public readonly struct AbilityInputs
{
    /// <summary>实际来源及目标脚下。</summary>
    public readonly Vector3 Source, Target;

    /// <summary>本次锁定的出手方向。</summary>
    public readonly Vector3 Forward;

    /// <summary>来源实际体高。</summary>
    public readonly float SourceHeight;

    /// <summary>外部真实载体的数值位置与朝向；未取得载体时为空，不以来源人物替代。</summary>
    public readonly Matrix4x4? Carrier;

    /// <summary>保留已经采集的数值。</summary>
    /// <param name="source">来源脚下。</param>
    /// <param name="target">目标脚下。</param>
    /// <param name="forward">锁定朝向。</param>
    /// <param name="sourceHeight">来源体高。</param>
    /// <param name="carrier">已取得本次控制的载体姿态。</param>
    public AbilityInputs(Vector3 source, Vector3 target, Vector3 forward, float sourceHeight,
        Matrix4x4? carrier = null)
    {
        Source = source;
        Target = target;
        Forward = forward;
        SourceHeight = sourceHeight;
        Carrier = carrier;
    }

    /// <summary>按同一连接生成根姿态；手部读取已经确定的共同身体姿态。</summary>
    /// <param name="binding">已经核对的连接。</param>
    /// <param name="body">同一步身体姿态。</param>
    internal Matrix4x4 Resolve(CompiledBinding binding, BodyPose? body)
    {
        Vector3 position = binding.Anchor == AbilityAnchor.Carrier ? (Vector3)Carrier.Value.GetColumn(3) :
            binding.Anchor == AbilityAnchor.Effector ? body.Value.EffectorPoint :
            binding.Anchor == AbilityAnchor.Hand ? body.Value.Hand :
            binding.Anchor == AbilityAnchor.Source ? Source : Target;
        Vector3 aim = Forward;
        if (binding.Anchor == AbilityAnchor.Carrier)
        {
            aim = Target - position;
            if (aim.sqrMagnitude < 1e-8f) aim = Forward;
        }
        Vector3 up = binding.Anchor == AbilityAnchor.Carrier && Mathf.Abs(Vector3.Dot(aim.normalized, Vector3.forward)) > .999f
            ? Vector3.up : Vector3.forward;
        Quaternion rotation = binding.Anchor == AbilityAnchor.Effector ? body.Value.EffectorRotation :
            binding.Anchor == AbilityAnchor.Hand ? body.Value.Rotation :
            binding.FaceTarget ? Quaternion.LookRotation(aim, up) :
            binding.Anchor == AbilityAnchor.Carrier ? Carrier.Value.rotation : Quaternion.identity;
        return Matrix4x4.TRS(position + Vector3.forward * binding.Height, rotation,
            Vector3.one * (binding.ScaleByBody ? SourceHeight : 1f));
    }
}

/// <summary>已复制的根连接，不保留可编辑资料引用。</summary>
internal sealed class CompiledBinding
{
    /// <summary>实际连接对象。</summary>
    internal readonly AbilityAnchor Anchor;

    /// <summary>是否跟随。</summary>
    internal readonly bool Follow;

    /// <summary>是否只在跟随后的位置接触。</summary>
    internal readonly bool ContactAtDestination;

    /// <summary>是否使用出手朝向。</summary>
    internal readonly bool FaceTarget;

    /// <summary>是否按体高缩放。</summary>
    internal readonly bool ScaleByBody;

    /// <summary>实际附加高度。</summary>
    internal readonly float Height;

    /// <summary>编译边界核对后复制连接资料。</summary>
    /// <param name="source">已核对的原始连接。</param>
    internal CompiledBinding(ConstructionBinding source)
    {
        Anchor = source.Anchor;
        Follow = source.Follow;
        ContactAtDestination = source.ContactAtDestination;
        FaceTarget = source.FaceTarget;
        ScaleByBody = source.ScaleByBody;
        Height = source.Height;
    }
}
