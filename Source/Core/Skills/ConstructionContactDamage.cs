using System;
using System.IO;
using Newtonsoft.Json;

namespace Cultiway.Core.Skills;

/// <summary>普通接触作用中明确的人物，不根据画面或招式名称推测。</summary>
public enum ConstructionContactActor
{
    /// <summary>真实接触人物。</summary>
    Contact = 0,
    /// <summary>本部位当前有效控制人物。</summary>
    Controller = 1
}

/// <summary>同一次真实接触附带的独立元素伤害，不改变主体交锋量或再次支付。</summary>
public sealed class ConstructionContactDamage
{
    /// <summary>接触时本体余量对应的伤害比例，零值不提交伤害。</summary>
    public float PerForce;

    /// <summary>受伤人物，省略时为真实接触人物。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public ConstructionContactActor Target;

    /// <summary>攻击归属，默认沿用当前控制者；反噬可明确归属接触人物。</summary>
    public ConstructionContactActor Source = ConstructionContactActor.Controller;

    /// <summary>按受伤人物当前生命计算的附加比例。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float PerHealth;

    /// <summary>按受伤人物最大生命计算的附加比例。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float PerMaxHealth;

    /// <summary>最大生命项至少贡献的伤害；最后与当前生命项及本体量项相加。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float MinimumBonus;

    /// <summary>明确忽略共同减伤，仍经过标准受击与死亡规则。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool IgnoreDamageReduction;

    /// <summary>此次追加伤害的明确元素构成。</summary>
    public ElementComposition Element;
}

/// <summary>在创建、编辑和保存读回边界检查并独立复制的追加伤害。</summary>
public sealed class CompiledConstructionContactDamage
{
    /// <summary>已经检查的本体余量比例。</summary>
    public float PerForce { get; }

    /// <summary>已经确认的受伤人物。</summary>
    public ConstructionContactActor Target { get; }

    /// <summary>已经确认的攻击归属。</summary>
    public ConstructionContactActor Source { get; }

    /// <summary>当前生命项的比例。</summary>
    public float PerHealth { get; }

    /// <summary>最大生命项的比例。</summary>
    public float PerMaxHealth { get; }

    /// <summary>最大生命项的最低附加量。</summary>
    public float MinimumBonus { get; }

    /// <summary>是否明确忽略减伤。</summary>
    public bool IgnoreDamageReduction { get; }

    /// <summary>是否有可能提交正伤害。</summary>
    public bool HasEffect => PerForce > 0f || PerHealth > 0f || PerMaxHealth > 0f || MinimumBonus > 0f;

    /// <summary>已经检查并复制的元素构成，全零表示通用伤害。</summary>
    public ElementComposition Element { get; }

    /// <summary>检查有限比例及有效元素，不读取世界或人物。</summary>
    /// <param name="source">可保存的追加伤害。</param><param name="path">错误资料位置。</param>
    internal CompiledConstructionContactDamage(ConstructionContactDamage source, string path)
    {
        if (source == null || !Amount(source.PerForce) || !Amount(source.PerHealth) || !Amount(source.PerMaxHealth) || !Amount(source.MinimumBonus) ||
            !Enum.IsDefined(typeof(ConstructionContactActor), source.Target) || !Enum.IsDefined(typeof(ConstructionContactActor), source.Source))
            throw new InvalidDataException(path + "：追加伤害需要有效受伤者与来源，比例及最低附加量为零至一百万内的有限值");
        float total = 0f;
        for (int i = 0; i < 8; i++)
        {
            float value = source.Element[i];
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f)
                throw new InvalidDataException(path + "/Element：元素权重需要零至一内的有限值");
            total += value;
        }
        PerForce = source.PerForce;
        Target = source.Target; Source = source.Source;
        PerHealth = source.PerHealth; PerMaxHealth = source.PerMaxHealth; MinimumBonus = source.MinimumBonus;
        IgnoreDamageReduction = source.IgnoreDamageReduction;
        var element = source.Element;
        if (total > 0f) element.Normalize();
        Element = element;
    }

    /// <summary>只在外部资料边界检查非负有限数值。</summary>
    /// <param name="value">待编译的伤害系数。</param>
    private static bool Amount(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1000000f;

}
