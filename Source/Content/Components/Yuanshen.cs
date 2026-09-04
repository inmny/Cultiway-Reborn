using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>化神修士由九转元婴确定性蜕变而成的元神成果。</summary>
public struct Yuanshen : IComponent
{
    /// <summary>继承九转元婴并重建为元神境界的组合结果。</summary>
    public CoreFormationSnapshot formation;

    /// <summary>元神初成时固定的本相肉身印记，后续换体不会覆盖。</summary>
    public PhysicalBodySnapshot original_body;

    /// <summary>形成元神前九转元婴的稳定签名。</summary>
    public readonly string source_yuanying_signature => formation.source_signature;

    /// <summary>形成元神前九转元婴的规范名称。</summary>
    public readonly string source_yuanying_name => formation.source_name;

    /// <summary>形成元神时元婴已经完成的转数。</summary>
    public readonly int inherited_yuanying_stage => formation.source_refinement;

    /// <summary>元神从九转元婴精确继承的总体强度。</summary>
    public float strength
    {
        readonly get => formation.strength;
        set => formation.strength = value;
    }

    /// <summary>元神自身已经完成的蕴养层数。</summary>
    public int stage
    {
        readonly get => formation.refinement;
        set => formation.refinement = value;
    }

    /// <summary>使用元神快照和同时冻结的本相肉身创建成果组件。</summary>
    /// <param name="formation">已经重建名称、属性和语义的元神快照。</param>
    /// <param name="originalBody">元神初成时的本相肉身印记。</param>
    public Yuanshen(CoreFormationSnapshot formation, PhysicalBodySnapshot originalBody)
    {
        this.formation = formation;
        original_body = originalBody.DeepClone();
    }

    /// <summary>深拷贝组合快照，避免不同人物共享内部数组。</summary>
    public readonly Yuanshen DeepClone()
    {
        var clone = this;
        clone.formation = formation.DeepClone();
        clone.original_body = original_body.DeepClone();
        return clone;
    }

    /// <summary>返回组合快照固化的元神名称。</summary>
    public readonly string GetName()
    {
        return formation.IsFinalized ? formation.canonical_name : string.Empty;
    }

    /// <summary>返回元神从九转元婴继承的品阶。</summary>
    public readonly ItemLevel GetQuality()
    {
        return formation.quality;
    }

    /// <summary>返回当前蕴养层数已经显化的组合原子说明。</summary>
    public readonly string GetDescription()
    {
        return formation.IsValid
            ? CoreFormationComposer.GetDescription(formation, stage)
            : string.Empty;
    }

    /// <summary>返回元神从元婴继承的元素组成。</summary>
    public readonly ElementComposition GetComposition()
    {
        return formation.IsValid ? formation.composition : default;
    }
}
