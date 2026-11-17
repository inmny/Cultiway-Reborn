using System;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>自主选择实际连接、并存及来源存续，旧请求不读取新选择。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>第二十一版以独立随机源选择，不影响原有创作序列。</summary>
    /// <param name="definition">本次独立草稿。</param><param name="seed">受理时冻结的种子。</param>
    private static AbilityDefinition GeneratePersistence(AbilityDefinition definition, int seed)
    {
        var random = new Random(unchecked(seed ^ 0x713fa621));
        if (random.NextDouble() < .35d) definition = VaryPersistence(definition, random);
        if (random.NextDouble() < .25d && definition.Root != null && definition.Body == null)
        {
            CompiledAbility plan = AbilityCompiler.Compile(definition, false);
            if (!plan.Nodes[0].HasRay && ConstructionPlacementReferences.Rewrite(definition, plan, false) == null)
                definition = VaryBetween(definition, random);
        }
        return definition;
    }

    /// <summary>只改变已有并存规则和真实部位的来源策略，仍由来源依赖判断能否独立。</summary>
    /// <param name="definition">独立草稿。</param><param name="random">本次固定选择。</param>
    private static AbilityDefinition VaryPersistence(AbilityDefinition definition, Random random)
    {
        definition.ConcurrentLimit = random.Next(1, 4);
        ConstructionNode[] nodes = DerivationNodes(definition.Root).Where(node => !string.IsNullOrEmpty(node.Form)).ToArray();
        if (nodes.Length > 0) nodes[random.Next(nodes.Length)].SourceLoss = random.Next(2) == 0
            ? ConstructionSourceLoss.End : ConstructionSourceLoss.Continue;
        return definition;
    }

    /// <summary>两点改造继续共同引用检查及真实比例，不制造运动完成事实。</summary>
    /// <param name="definition">已确认可以改造的独立草稿。</param><param name="random">本次固定选择。</param>
    private static AbilityDefinition VaryBetween(AbilityDefinition definition, Random random)
    {
        bool follow = random.Next(2) == 0;
        AbilityDefinition result = ConstructionPlacementEdits.Between(definition, .2f + (float)random.NextDouble() * .6f, follow);
        result.Binding.ContactAtDestination = follow && random.Next(2) == 0;
        return result;
    }
}

/// <summary>旧第二十版创作请求原样保留，新规则只用于新受理请求。</summary>
internal sealed class CreationV21ToV22 : ISaveMigration
{
    /// <summary>原正式人物区段。</summary>
    public int FromVersion => 21;
    /// <summary>不追补新选择，不改变已冻结的结果和费用。</summary><param name="data">旧独立资料。</param>
    public JObject Apply(JObject data) => data;
}
