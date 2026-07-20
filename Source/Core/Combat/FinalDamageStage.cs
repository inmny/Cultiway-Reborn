namespace Cultiway.Core.Combat;

/// <summary>最终伤害修正规则的稳定执行阶段。</summary>
public enum FinalDamageStage : byte
{
    /// <summary>把整次命中改写为未命中或无效命中。</summary>
    Avoidance,

    /// <summary>按已建立的适应、元素防护等规则继续降低伤害。</summary>
    Adaptation,

    /// <summary>使用可消耗的护盾池吸收剩余伤害。</summary>
    Shield,

    /// <summary>对单次伤害施加固定上限。</summary>
    Cap,

    /// <summary>在确定会致命时执行保命与重生规则。</summary>
    Survival,
}
