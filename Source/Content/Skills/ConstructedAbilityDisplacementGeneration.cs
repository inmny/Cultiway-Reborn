using System;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.Usage;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>从真实人物接触部位生成普通击退或换位，不创建独立执行路径。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>第十三版使用独立随机源，旧规则的输入和抽取次序保持。</summary>
    /// <param name="definition">已有结构、运动与接触作用的完整草稿。</param><param name="seed">确定种子。</param>
    private static AbilityDefinition GenerateContactDisplacement(AbilityDefinition definition, int seed) =>
        VaryContactDisplacement(definition, new Random(unchecked(seed ^ 0x184ca675)));

    /// <summary>只改一处独立作用；友方仅生成换位，自己与自己换位没有意义。</summary>
    /// <param name="definition">独立草稿。</param><param name="random">当前确定的选择。</param>
    private static AbilityDefinition VaryContactDisplacement(AbilityDefinition definition, Random random)
    {
        var sites = CreationContactSites(definition).Where(site => site.Impact.Targets != SkillUseTargetRelation.Self).ToArray();
        if (sites.Length == 0) return definition;
        var site = sites[random.Next(sites.Length)];
        var impact = Newtonsoft.Json.JsonConvert.DeserializeObject<ConstructionImpact>(Newtonsoft.Json.JsonConvert.SerializeObject(site.Impact));
        if (impact.Targets == SkillUseTargetRelation.Hostile && random.Next(2) == 0)
            impact.Knockback = new ConstructionKnockback
            {
                Distance = 2f + (float)random.NextDouble() * 6f,
                Height = random.Next(2) == 0 ? 0f : .5f + (float)random.NextDouble() * 1.5f
            };
        else
            impact.Exchange = new ConstructionExchange
            {
                Chance = .5f + (float)random.NextDouble() * .5f,
                WaitSeconds = .1f + (float)random.NextDouble() * .5f
            };
        site.Set(impact);
        return definition;
    }
}

/// <summary>第十二版已冻结输入原样保留，只有新受理请求进入第十三版。</summary>
internal sealed class CreationV13ToV14 : ISaveMigration
{
    /// <summary>旧请求完整保存在第十三版人物区段。</summary>
    public int FromVersion => 13;
    /// <summary>不重新抽取已有创作。</summary><param name="data">保存层独立复制的旧输入。</param>
    public JObject Apply(JObject data) => data;
}
