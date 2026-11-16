using System;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.Usage;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>通过真实接触部位创作范围或介质分送，保持普通形体和过程。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>第十五版使用独立随机源，不改变旧规则已经冻结的选择。</summary>
    /// <param name="definition">已有完整构造。</param><param name="seed">固定种子。</param><param name="inspiration">固定倾向。</param>
    private static AbilityDefinition GenerateContactSpreads(AbilityDefinition definition, int seed, ConstructionInspiration inspiration) =>
        VaryContactSpreads(definition, new Random(unchecked(seed ^ 0x42d9617b)), inspiration);

    /// <summary>先选真实作用位置，再补球形或该部位的介质分送，最后保留其余原资料。</summary>
    /// <param name="definition">当前独立草稿。</param><param name="random">确定选择。</param><param name="inspiration">固定元素和性格。</param>
    private static AbilityDefinition VaryContactSpreads(AbilityDefinition definition, Random random, ConstructionInspiration inspiration)
    {
        var sites = CreationContactSites(definition).Where(site => site.Impact.Targets != SkillUseTargetRelation.Self).ToArray();
        if (sites.Length == 0) return definition;
        var site = sites[random.Next(sites.Length)];
        var impact = Newtonsoft.Json.JsonConvert.DeserializeObject<ConstructionImpact>(Newtonsoft.Json.JsonConvert.SerializeObject(site.Impact));
        bool support = impact.Targets != SkillUseTargetRelation.Hostile;
        var spread = new ConstructionContactSpread
        {
            Radius = 1.5f + (float)random.NextDouble() * 3f,
            Outcome = CreationContactResult(random, inspiration, support),
            BuildingDamagePerForce = !support && random.Next(3) == 0 ? .05f + (float)random.NextDouble() * .15f : 0f,
            VisualSeconds = .2f + (float)random.NextDouble() * .3f
        };
        if (site.Node != null && random.Next(2) == 0)
        {
            site.Node.MediumChannel ??= "created.contact";
            spread.Radius = 0f;
            spread.Medium = new ConstructionSpreadMedium
            {
                Channel = site.Node.MediumChannel, MaximumTargets = random.Next(2, 6),
                OncePerCast = random.Next(3) != 0, InheritContactEffects = random.Next(3) == 0,
                LinkForm = "thunder_channel", LinkRadius = .06f + (float)random.NextDouble() * .08f
            };
            if (random.Next(2) == 0) { spread.Medium.Matter = site.Node.Matter; spread.Medium.State = site.Node.State; }
        }
        var values = (impact.Spreads ?? Array.Empty<ConstructionContactSpread>()).ToList();
        if (values.Count < 8) values.Add(spread); else values[random.Next(values.Count)] = spread;
        impact.Spreads = values.ToArray(); site.Set(impact);
        return definition;
    }
}

/// <summary>第十四版正式等待请求保留全部输入，只有新请求使用第十五版。</summary>
internal sealed class CreationV15ToV16 : ISaveMigration
{
    /// <summary>旧正式人物区段。</summary>
    public int FromVersion => 15;
    /// <summary>不重抽已冻结资料。</summary><param name="data">旧输入独立副本。</param>
    public JObject Apply(JObject data) => data;
}
