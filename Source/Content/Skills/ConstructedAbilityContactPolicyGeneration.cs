using System;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>从已有真实接触组继续创作频率与收势，不增加另一套命中执行。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>只影响第十八版的新请求，原完整作用和世界接触保持。</summary>
    /// <param name="definition">已有真实作用位置的草稿。</param><param name="seed">受理时固定种子。</param>
    private static AbilityDefinition GenerateContactPolicy(AbilityDefinition definition, int seed)
    {
        var random = new Random(unchecked(seed ^ 0x31a0745b));
        if (random.NextDouble() >= .35d || CreationContactSites(definition).Count == 0) return definition;
        return VaryContactPolicy(definition, random);
    }

    /// <summary>选择真实组并整体修改；连续作用不随机改成命中立即结束。</summary>
    /// <param name="definition">独立草稿。</param><param name="random">固定选择。</param>
    private static AbilityDefinition VaryContactPolicy(AbilityDefinition definition, Random random)
    {
        var sites = CreationContactSites(definition);
        var site = sites[random.Next(sites.Count)];
        var policy = new ConstructionContactPolicy
        {
            Mode = (ConstructionContactMode)random.Next(4), Interval = .12f + (float)random.NextDouble() * .38f,
            MaximumPerTarget = random.Next(2, 5), MaximumTotal = random.Next(4, 13)
        };
        if (site.Node != null)
            site.Node.OnContact = (ConstructionContactResponse)random.Next(policy.Mode == ConstructionContactMode.Once ? 3 : 2);
        return ConstructionContactEdits.Set(definition, policy, site.Node);
    }
}

/// <summary>旧请求继续原规则及冻结输入，升级不重新抽取接触频率。</summary>
internal sealed class CreationV18ToV19 : ISaveMigration
{
    /// <summary>已有第十七版世界作用输入的正式人物区段。</summary>
    public int FromVersion => 18;

    /// <summary>完整原输入直接保留。</summary><param name="data">保存层独立旧资料。</param>
    public JObject Apply(JObject data) => data;
}
