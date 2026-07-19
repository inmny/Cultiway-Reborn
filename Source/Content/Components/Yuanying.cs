using Cultiway.Content.Libraries;
using Cultiway.Core;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

public struct Yuanying : IComponent
{
    /// <summary>继承金丹并发生结婴蜕变后的组合结果。</summary>
    public CoreFormationSnapshot formation;

    /// <summary>结婴前金丹的稳定签名。</summary>
    public string source_jindan_signature;

    /// <summary>结婴前金丹的规范名称。</summary>
    public string source_jindan_name;

    /// <summary>结婴时金丹已经完成的转数。</summary>
    public int inherited_jindan_stage;

    /// <summary>元婴继承与蜕变后的总体强度倍率。</summary>
    public float strength;

    /// <summary>元婴自身的后续演化阶段。</summary>
    public int stage;

    /// <summary>使用结婴组合、来源金丹谱系和继承转数创建现行元婴组件。</summary>
    public Yuanying(CoreFormationSnapshot formation, CoreFormationSnapshot sourceJindan,
                    int inheritedJindanStage, float strength)
    {
        this.formation = formation;
        source_jindan_signature = sourceJindan.signature;
        source_jindan_name = sourceJindan.canonical_name;
        inherited_jindan_stage = inheritedJindanStage;
        this.strength = strength;
        stage = 0;
    }

    /// <summary>返回组合快照固化的规范名称。</summary>
    public string GetName()
    {
        return formation.IsValid ? formation.canonical_name : string.Empty;
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
