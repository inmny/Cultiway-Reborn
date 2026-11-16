using System;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>创作实际交锋用途，仍使用既有形体、容量和归属交接。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>第二十版独立选择交锋规则，不改变旧请求的随机序列。</summary>
    /// <param name="definition">本次独立资料。</param><param name="seed">受理时冻结的种子。</param>
    private static AbilityDefinition GenerateClash(AbilityDefinition definition, int seed)
    {
        var random = new Random(unchecked(seed ^ 0x39a164cb));
        return random.NextDouble() < .3d && ClashCandidates(definition).Length > 0
            ? VaryClash(definition, random) : definition;
    }

    /// <summary>只选择有真实形体和交锋用途的部位，保留原运动及各层作用。</summary>
    /// <param name="definition">完整原术或独立草稿。</param>
    private static ConstructionNode[] ClashCandidates(AbilityDefinition definition) => DerivationNodes(definition.Root)
        .Where(node => !string.IsNullOrEmpty(node.Form) && node.ContactRole != ConstructionContactRole.Decoration)
        .ToArray();

    /// <summary>普通攻击可成为屏障；已有屏障改变吸收、反射及地面阻挡。</summary>
    /// <param name="definition">当前独立草稿。</param><param name="random">本次固定选择。</param>
    private static AbilityDefinition VaryClash(AbilityDefinition definition, Random random)
    {
        ConstructionNode[] candidates = ClashCandidates(definition);
        ConstructionNode node = candidates[random.Next(candidates.Length)];
        return ConstructionContactEdits.SetClash(definition, node, ConstructionContactRole.Barrier,
            random.Next(2) == 0 ? ConstructionInterception.Absorb : ConstructionInterception.Reflect,
            random.Next(2) == 0, random.Next(0, 9));
    }
}

/// <summary>第十九版请求原样保留，新请求才选择交锋规则。</summary>
internal sealed class CreationV20ToV21 : ISaveMigration
{
    /// <summary>已有范围推拉创作的正式人物区段。</summary>
    public int FromVersion => 20;
    /// <summary>不重抽已有输入或重算其费用。</summary><param name="data">旧请求独立资料。</param>
    public JObject Apply(JObject data) => data;
}
