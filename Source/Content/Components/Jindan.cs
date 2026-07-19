using Cultiway.Content.Libraries;
using Cultiway.Core;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

public struct Jindan : IComponent
{
    /// <summary>角色独有的组合结果。</summary>
    public CoreFormationSnapshot formation;

    /// <summary>金丹形成与淬炼累计得到的强度倍率。</summary>
    public float strength;

    /// <summary>已经完成的金丹淬炼转数。</summary>
    public int stage;

    /// <summary>使用已完成的组合快照和初始强度创建现行金丹组件。</summary>
    public Jindan(CoreFormationSnapshot formation, float strength)
    {
        this.formation = formation;
        this.strength = strength;
        stage = 0;
    }

    /// <summary>返回组合快照固化的规范名称。</summary>
    public string GetName()
    {
        return formation.IsValid ? formation.canonical_name : string.Empty;
    }

    /// <summary>返回当前转数已经显化的组合原子说明。</summary>
    public string GetDescription()
    {
        return formation.IsValid
            ? CoreFormationComposer.GetDescription(formation, stage)
            : string.Empty;
    }

    /// <summary>返回金丹固化的元素组成。</summary>
    public ElementComposition GetComposition()
    {
        return formation.IsValid ? formation.composition : default;
    }
}
