using System;
using System.Linq;
using Cultiway.Core;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.Usage;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>从普通追加伤害、随机结果和实际条件继续创作，不增加整招样板。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>第十四版独立随机源不改变此前已冻结规则。</summary>
    /// <param name="definition">已有完整结构与运动的草稿。</param><param name="seed">固定种子。</param>
    /// <param name="inspiration">固定倾向。</param>
    private static AbilityDefinition GenerateContactResults(AbilityDefinition definition, int seed, ConstructionInspiration inspiration) =>
        VaryContactResults(definition, new Random(unchecked(seed ^ 0x27b83a41)), inspiration);

    /// <summary>只改一处真实接触作用，保留时序、命名、引用及其他普通效果。</summary>
    /// <param name="definition">独立草稿。</param><param name="random">确定选择。</param><param name="inspiration">固定倾向。</param>
    private static AbilityDefinition VaryContactResults(AbilityDefinition definition, Random random, ConstructionInspiration inspiration)
    {
        var sites = CreationContactSites(definition).Where(site => site.Impact.Targets != SkillUseTargetRelation.Self).ToArray();
        if (sites.Length == 0) return definition;
        var site = sites[random.Next(sites.Length)];
        var impact = Newtonsoft.Json.JsonConvert.DeserializeObject<ConstructionImpact>(Newtonsoft.Json.JsonConvert.SerializeObject(site.Impact));
        bool support = impact.Targets != SkillUseTargetRelation.Hostile;
        int kind = random.Next(3);
        if (kind == 0 && !support)
        {
            var values = (impact.AdditionalDamage ?? Array.Empty<ConstructionContactDamage>()).ToList();
            var damage = CreationContactDamage(random, inspiration);
            if (values.Count < 8) values.Add(damage); else values[random.Next(values.Count)] = damage;
            impact.AdditionalDamage = values.ToArray();
        }
        else if (kind == 1 || kind == 0)
            impact.RandomContact = new ConstructionRandomContact
            {
                Chance = .4f + (float)random.NextDouble() * .6f,
                Outcomes = Enumerable.Range(0, random.Next(2, 5)).Select(_ => CreationContactResult(random, inspiration, support)).ToArray()
            };
        else
        {
            var values = (impact.ConditionalContacts ?? Array.Empty<ConstructionConditionalContact>()).ToList();
            var rule = new ConstructionConditionalContact
            {
                MaximumHealthRatio = .25f + (float)random.NextDouble() * .5f,
                Outcome = CreationContactResult(random, inspiration, support),
                Otherwise = random.Next(2) == 0 ? CreationContactResult(random, inspiration, support) : null
            };
            if (!support)
            {
                if (random.Next(2) == 0) rule.OnKill = new ConstructionContactOutcome { Restoration = CreationRestoration(random, true) };
                if (random.Next(2) == 0) rule.OnSurvive = new ConstructionContactOutcome { Statuses = new[] { CreationStatus(random, false) } };
            }
            if (values.Count < 8) values.Add(rule); else values[random.Next(values.Count)] = rule;
            impact.ConditionalContacts = values.ToArray();
        }
        site.Set(impact);
        return definition;
    }

    /// <summary>伤害组成与归属独立确定，可表达余力追加、生命比例和接触反噬。</summary>
    /// <param name="random">固定选择。</param><param name="inspiration">元素倾向。</param>
    private static ConstructionContactDamage CreationContactDamage(Random random, ConstructionInspiration inspiration)
    {
        ElementComposition element = inspiration.Elements;
        if (random.Next(3) == 0 || !Enumerable.Range(0, 8).Any(index => element[index] > 0f))
        {
            element = default;
            element[random.Next(8)] = 1f;
        }
        bool backlash = random.Next(10) == 0;
        return new ConstructionContactDamage
        {
            PerForce = .1f + (float)random.NextDouble() * .25f,
            PerHealth = random.Next(3) == 0 ? (float)random.NextDouble() * .02f : 0f,
            PerMaxHealth = random.Next(3) == 0 ? (float)random.NextDouble() * .01f : 0f,
            MinimumBonus = random.Next(3) == 0 ? random.Next(1, 4) : 0f,
            Target = backlash ? ConstructionContactActor.Controller : ConstructionContactActor.Contact,
            Source = backlash ? ConstructionContactActor.Contact : ConstructionContactActor.Controller,
            IgnoreDamageReduction = random.Next(4) == 0,
            Element = element
        };
    }

    /// <summary>同一个结果可混合状态、恢复、伤害及击退，友方只生成支持作用。</summary>
    /// <param name="random">确定选择。</param><param name="inspiration">固定倾向。</param><param name="support">是否友方接触。</param>
    private static ConstructionContactOutcome CreationContactResult(Random random, ConstructionInspiration inspiration, bool support)
    {
        var outcome = new ConstructionContactOutcome();
        if (support) outcome.Restoration = CreationRestoration(random, false);
        else outcome.Damage = new[] { CreationContactDamage(random, inspiration) };
        if (random.Next(2) == 0) outcome.Statuses = new[] { CreationStatus(random, support) };
        if (!support && random.Next(3) == 0) outcome.Knockback = new ConstructionKnockback { Distance = 1f + (float)random.NextDouble() * 2f };
        return outcome;
    }
}

/// <summary>原样保留第十三版等待请求；新受理创作使用第十四版。</summary>
internal sealed class CreationV14ToV15 : ISaveMigration
{
    /// <summary>旧请求所在正式人物区段。</summary>
    public int FromVersion => 14;
    /// <summary>不改变已经冻结的任何输入。</summary><param name="data">保存层独立复制的旧输入。</param>
    public JObject Apply(JObject data) => data;
}
