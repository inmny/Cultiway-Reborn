using System;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Semantics;
using NeoModLoader.General;

namespace Cultiway.Content.Libraries;

[Flags]
public enum CoreFormationRealmMask : byte
{
    /// <summary>不参与任何境界组合。</summary>
    None = 0,

    /// <summary>可在结丹时被选择。</summary>
    Jindan = 1,

    /// <summary>可在结婴时被选择。</summary>
    Yuanying = 2,

    /// <summary>可用于金丹和元婴。</summary>
    All = Jindan | Yuanying
}

/// <summary>组合器每个槽位只能选取一个原子的分类。</summary>
public enum CoreFormationAtomCategory : byte
{
    /// <summary>由五气和灵根组成决定的主元素。</summary>
    Element,

    /// <summary>由三花比例和元素均衡度决定的内核结构。</summary>
    Structure,

    /// <summary>由功法和已学技能语义决定的修行道路。</summary>
    Path,

    /// <summary>由种族、血脉等固有来源决定的主题。</summary>
    Theme,

    /// <summary>结婴时决定元婴外在形态的原子。</summary>
    Manifestation,

    /// <summary>结婴时满足特殊元素结构后附加的蜕变。</summary>
    Transformation
}

/// <summary>金丹与元婴组合时读取的规则原子。</summary>
public sealed class CoreFormationAtomAsset : Asset
{
    /// <summary>该原子允许出现的境界。</summary>
    public CoreFormationRealmMask realms;

    /// <summary>互斥选择槽位。</summary>
    public CoreFormationAtomCategory category;

    /// <summary>原子显示名称的本地化键。</summary>
    public string name_key;

    /// <summary>原子效果说明的本地化键。</summary>
    public string description_key;

    /// <summary>规则命名时可选择的短词干。</summary>
    public string[] name_stems = [];

    /// <summary>被选中后提供的属性模板。</summary>
    public CoreFormationStatValue[] stats = [];

    /// <summary>被选中后写入组合快照的语义。</summary>
    public SemanticDescriptor semantics = new();

    /// <summary>低于此分数时不能直接显化，只可能成为潜在原子。</summary>
    public float minimum_score;

    /// <summary>评分相同时的稳定优先级。</summary>
    public int priority;

    /// <summary>按角色形成上下文计算适配分数的委托。</summary>
    internal Func<CoreFormationContext, float> ScoreContext;

    /// <summary>取得原子的本地化显示名称。</summary>
    public string GetName()
    {
        return !string.IsNullOrEmpty(name_key) && LM.Has(name_key) ? LM.Get(name_key) : id;
    }

    /// <summary>取得原子的本地化效果说明。</summary>
    public string GetDescription()
    {
        return !string.IsNullOrEmpty(description_key) && LM.Has(description_key)
            ? LM.Get(description_key)
            : string.Empty;
    }

    /// <summary>使用稳定种子从候选词干中选择一个名称片段。</summary>
    public string PickNameStem(int seed)
    {
        if (name_stems == null || name_stems.Length == 0) return string.Empty;
        return name_stems[(int)(unchecked((uint)seed) % (uint)name_stems.Length)];
    }

    /// <summary>在指定形成上下文中计算非负适配分数。</summary>
    internal float ScoreFor(CoreFormationContext context)
    {
        return Math.Max(0f, ScoreContext?.Invoke(context) ?? 0f);
    }
}
