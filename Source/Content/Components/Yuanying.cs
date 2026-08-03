using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

public struct Yuanying : IComponent
{
    /// <summary>继承金丹并发生结婴蜕变后的组合结果。</summary>
    public CoreFormationSnapshot formation;

    /// <summary>结婴前金丹的稳定签名。</summary>
    public string source_jindan_signature => formation.source_signature;

    /// <summary>结婴前金丹的规范名称。</summary>
    public string source_jindan_name => formation.source_name;

    /// <summary>结婴时金丹已经完成的转数。</summary>
    public int inherited_jindan_stage => formation.source_refinement;

    /// <summary>元婴继承与蜕变后的总体强度倍率。</summary>
    public float strength
    {
        readonly get => formation.strength;
        set => formation.strength = value;
    }

    /// <summary>元婴自身的后续演化阶段。</summary>
    public int stage
    {
        readonly get => formation.refinement;
        set => formation.refinement = value;
    }

    /// <summary>使用结婴组合、来源金丹谱系和继承转数创建现行元婴组件。</summary>
    public Yuanying(CoreFormationSnapshot formation, CoreFormationSnapshot sourceJindan,
                    int inheritedJindanStage, float strength)
    {
        this.formation = formation;
        this.formation.source_signature = sourceJindan.signature;
        this.formation.source_name = sourceJindan.canonical_name;
        this.formation.source_refinement = inheritedJindanStage;
        this.formation.strength = strength;
        this.formation.refinement = 0;
    }

    /// <summary>返回组合快照固化的规范名称。</summary>
    public string GetName()
    {
        return formation.IsValid ? formation.canonical_name : string.Empty;
    }

    /// <summary>返回结婴时独立评定并固化的品阶。</summary>
    public ItemLevel GetQuality()
    {
        return formation.quality;
    }

    /// <summary>返回元婴当前已经显化的组合原子说明。</summary>
    public string GetDescription()
    {
        return formation.IsValid
            ? CoreFormationComposer.GetDescription(formation, stage)
            : string.Empty;
    }

    /// <summary>返回从金丹继承并固化的元素组成。</summary>
    public ElementComposition GetComposition()
    {
        return formation.IsValid ? formation.composition : default;
    }
}
