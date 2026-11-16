using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>同一次真实接触附带的独立元素伤害，不改变主体交锋量或再次支付。</summary>
public sealed class ConstructionContactDamage
{
    /// <summary>接触时本体余量对应的伤害比例，零值不提交伤害。</summary>
    public float PerForce;

    /// <summary>此次追加伤害的明确元素构成。</summary>
    public ElementComposition Element;
}

/// <summary>在创建、编辑和保存读回边界检查并独立复制的追加伤害。</summary>
public sealed class CompiledConstructionContactDamage
{
    /// <summary>已经检查的本体余量比例。</summary>
    public float PerForce { get; }

    /// <summary>已经检查并复制的元素构成，全零表示通用伤害。</summary>
    public ElementComposition Element { get; }

    /// <summary>检查有限比例及有效元素，不读取世界或人物。</summary>
    /// <param name="source">可保存的追加伤害。</param><param name="path">错误资料位置。</param>
    internal CompiledConstructionContactDamage(ConstructionContactDamage source, string path)
    {
        if (source == null || float.IsNaN(source.PerForce) || float.IsInfinity(source.PerForce) || source.PerForce < 0f || source.PerForce > 1000000f)
            throw new InvalidDataException(path + "：追加伤害比例需要零至一百万内的有限值");
        float total = 0f;
        for (int i = 0; i < 8; i++)
        {
            float value = source.Element[i];
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f)
                throw new InvalidDataException(path + "/Element：元素权重需要零至一内的有限值");
            total += value;
        }
        PerForce = source.PerForce;
        var element = source.Element;
        if (total > 0f) element.Normalize();
        Element = element;
    }
}
