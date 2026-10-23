using System;
using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>把本次生成主体收束为原支付资源；不把攻击力量或借用物件换成资源。</summary>
public sealed class ConstructionRecovery
{
    /// <summary>尚存支付份额中实际收回的比例，其余在转化中损失。</summary>
    public float Efficiency = .75f;

    /// <summary>同时选择真实形状范围内、属于当前控制者的世界材料。</summary>
    public bool WorldMaterials;

    /// <summary>只选择有明确到期时间的世界材料，保留永久结构和设施。</summary>
    public bool TemporaryOnly;
}

/// <summary>已检查的收束历时及效率。</summary>
internal sealed class CompiledRecovery
{
    /// <summary>世界秒数上的收束历时。</summary>
    internal readonly float Seconds;
    /// <summary>不超过尚存支付份额的回收比例。</summary>
    internal readonly float Efficiency;

    /// <summary>由世界材料维护者处理一次空间选择。</summary>
    internal readonly bool WorldMaterials;

    /// <summary>实际世界选择排除永久留置，不由自主使用临时改变规则。</summary>
    internal readonly bool TemporaryOnly;

    /// <summary>在资料入口检查并固定，运行和工作线程只读。</summary>
    /// <param name="source">实际收束操作。</param>
    /// <param name="lifetime">本次施放上限。</param>
    /// <param name="path">错误所在部位。</param>
    internal CompiledRecovery(ConstructionOperation source, float lifetime, string path)
    {
        WorldMaterials = source.Recovery?.WorldMaterials ?? false;
        TemporaryOnly = source.Recovery?.TemporaryOnly ?? false;
        Seconds = source.Seconds;
        Efficiency = source.Recovery?.Efficiency ?? .75f;
        if (float.IsNaN(Seconds) || float.IsInfinity(Seconds) || Seconds <= 0 || Seconds > lifetime ||
            float.IsNaN(Efficiency) || float.IsInfinity(Efficiency) || Efficiency < 0 || Efficiency > 1)
            throw new InvalidDataException(path + "：收束需要正数历时，回收比例须在0至1内");
    }
}
