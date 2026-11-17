using System;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>已有世界作用可由同一真实生成部位交出有限材料。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>独立随机源只用于新规则，旧等待输入保持原结果。</summary>
    /// <param name="definition">已有完整独立结构。</param><param name="seed">固定种子。</param>
    private static AbilityDefinition GenerateMaterialTransfer(AbilityDefinition definition, int seed)
    {
        var random = new Random(unchecked(seed ^ 0x492d6a13));
        if (random.NextDouble() >= .5d) return definition;
        var plan = AbilityCompiler.Compile(definition, false);
        return MaterialTransferSites(definition, plan).Length > 0 ? VaryMaterialTransfer(definition, random, plan) : definition;
    }

    /// <summary>只选择有世界作用和真实材料的单份部位，复用声明尺寸及已编译流散预算。</summary>
    /// <param name="definition">当前完整资料。</param><param name="plan">同一编译结果。</param>
    private static (ConstructionNode Node, ConstructionImpact Impact, double Volume, int Cells)[] MaterialTransferSites(
        AbilityDefinition definition, CompiledAbility plan) => ConstructionContactEdits.Parts(definition.Root)
        .Select(part => (part.Node, part.Scale, Index: Array.FindIndex(plan.Nodes, node => node.DeclarationPath == part.Path)))
        .Where(part => part.Index >= 0 && part.Node.Activation == null &&
            plan.Nodes.Count(node => node.DeclarationPath == plan.Nodes[part.Index].DeclarationPath) == 1 &&
            !plan.Nodes[part.Index].BodyWindow && plan.Nodes[part.Index].FragmentSource < 0 && plan.Nodes[part.Index].RemainderRoot < 0 &&
            plan.Nodes[part.Index].Resource.Meshes.Length > 0 && plan.Nodes[part.Index].Resource.Contacts.Length > 0 &&
            part.Node.ContactRole != ConstructionContactRole.Decoration)
        .Select(part => (part.Node, Impact: part.Node.Impact ?? definition.Impact,
            Volume: plan.Nodes[part.Index].Volume * Math.Pow(part.Scale, 3), Cells: plan.Nodes[part.Index].FlowSettings?.Cells ?? 1))
        .Where(part => part.Impact?.WorldEffects?.Length > 0 && part.Volume > 0 &&
            !double.IsNaN(part.Volume) && !double.IsInfinity(part.Volume) &&
            part.Volume / (part.Cells * part.Cells) * .025d >= float.Epsilon).ToArray();

    /// <summary>只改变选中作用的材料数量，原世界规则、时序和命名依赖保留。</summary>
    /// <param name="definition">独立草稿。</param><param name="random">本次固定选择。</param><param name="plan">原完整编译。</param>
    private static AbilityDefinition VaryMaterialTransfer(AbilityDefinition definition, Random random, CompiledAbility plan)
    {
        var sites = MaterialTransferSites(definition, plan); var site = sites[random.Next(sites.Length)];
        var impact = JsonConvert.DeserializeObject<ConstructionImpact>(JsonConvert.SerializeObject(site.Impact));
        var effect = impact.WorldEffects[random.Next(impact.WorldEffects.Length)];
        double portion = site.Volume / (site.Cells * site.Cells) * (.025d + random.NextDouble() * .05d);
        effect.MaterialVolume = (float)Math.Min(1e9d, portion);
        site.Node.Impact = impact;
        return definition;
    }
}

/// <summary>旧请求保留实际库存及原世界作用，新请求才生成材料交出参数。</summary>
internal sealed class CreationV27ToV28 : ISaveMigration
{
    /// <summary>已经冻结真实库存材料的正式区段。</summary>
    public int FromVersion => 27;
    /// <summary>不从当前世界补写输入或重新选择结果。</summary><param name="data">原保存资料。</param>
    public JObject Apply(JObject data) => data;
}
