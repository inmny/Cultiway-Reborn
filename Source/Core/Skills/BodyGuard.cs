using System.IO;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>在已选择的真实手面或持物面上，以有限作用量拦截正面的来招。</summary>
public sealed class BodyGuardDefinition
{
    /// <summary>一次动作共享的拦截预量，会随本次实际作用倍率缩放。</summary>
    public float Capacity;

    /// <summary>模型局部空间中朝向来招的一面，与模型本身一起转动。</summary>
    public float[] Normal = { 0f, 0f, 1f };

    /// <summary>允许来招方向相对面朝向偏开的角度，必须小于九十度。</summary>
    public float HalfAngle = 60f;
}

/// <summary>运行只读取编译时的方向和额度，编辑资料不能改变以接受理的动作。</summary>
internal readonly struct CompiledBodyGuard
{
    /// <summary>一次动作的基础余量。</summary>
    internal readonly float Capacity;

    /// <summary>使用来向核对正面的角度下限。</summary>
    internal readonly float MinimumDot;

    /// <summary>已经归一化的模型面方向。</summary>
    internal readonly Vector3 Normal;

    /// <summary>旧动作没有明确防御面时继续原行为。</summary>
    internal bool Enabled => Capacity > 0f;

    /// <summary>核对有限额度以及与真实握持一致的局部面方向。</summary>
    /// <param name="definition">可编辑的格挡规则。</param>
    /// <param name="hand">是否使用手部或借用手部实际持物。</param>
    internal CompiledBodyGuard(BodyGuardDefinition definition, bool hand)
    {
        this = default;
        if (definition == null) return;
        float[] normal = definition.Normal;
        if (!hand || !Finite(definition.Capacity) || definition.Capacity <= 0f || definition.Capacity > 1000000f ||
            !Finite(definition.HalfAngle) || definition.HalfAngle <= 0f || definition.HalfAngle >= 90f ||
            normal?.Length != 3 || !Finite(normal[0]) || !Finite(normal[1]) || !Finite(normal[2]))
            throw new InvalidDataException("Body/Guard：需要实际手面或持物，正有限额度、0至90度之间朝向和局部方向");
        Normal = new Vector3(normal[0], normal[1], normal[2]).normalized;
        if (!(Normal.sqrMagnitude > .5f)) throw new InvalidDataException("Body/Guard/Normal：格挡面须有明确方向");
        Capacity = definition.Capacity;
        MinimumDot = Mathf.Cos(definition.HalfAngle * Mathf.Deg2Rad);
    }

    /// <summary>数值载入后可安全参与前后方向及额度判断。</summary>
    /// <param name="value">资料中的一个数值。</param>
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

public sealed partial class CompiledBodyAction
{
    /// <summary>沿实际动作窗口开启的有限防御面。</summary>
    internal readonly CompiledBodyGuard Guard;
}
