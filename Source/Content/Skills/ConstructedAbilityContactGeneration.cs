using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.Usage;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>从普通接触作用继续创作，不把附加效果做成完整法术样板。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>列出真实攻击部位和身体接触窗口；纯装饰及纯防御面不接受人物作用。</summary>
    /// <param name="definition">当前独立构造。</param>
    private static List<(ConstructionImpact Impact, Action<ConstructionImpact> Set)> CreationContactSites(AbilityDefinition definition)
    {
        var sites = new List<(ConstructionImpact, Action<ConstructionImpact>)>();
        CompiledAbility plan = ValidateDefinition(definition, cache: false);
        if (plan.Body?.Construction?.MayContact == true)
        {
            sites.Add((definition.Body.Impact ?? definition.Impact ?? new ConstructionImpact(), value => definition.Body.Impact = value));
            foreach (BodyActionStage stage in definition.Body.Stages ?? Array.Empty<BodyActionStage>())
                sites.Add((stage.Impact ?? definition.Body.Impact ?? definition.Impact ?? new ConstructionImpact(), value => stage.Impact = value));
        }
        foreach (string path in plan.Nodes.Where(node => node.AffectsActors && node.Resource.Contacts.Length > 0 && node.DeclarationPath != null)
                     .Select(node => node.DeclarationPath).Distinct(StringComparer.Ordinal))
        {
            ConstructionNode node = definition.Root;
            foreach (string id in path.Split('/').Skip(1)) node = node.Children.Single(child => child.Id == id);
            sites.Add((node.Impact ?? definition.Impact ?? new ConstructionImpact(), value => node.Impact = value));
        }
        return sites;
    }

    /// <summary>独立随机源只用于第十二版，保持前十一版生成及定价的原输入结果。</summary>
    /// <param name="definition">已有真实形体、运动和接触窗口的独立结果。</param>
    /// <param name="seed">本次固定种子。</param><param name="inspiration">已冻结的元素及性格倾向。</param>
    private static AbilityDefinition GenerateContactEffects(AbilityDefinition definition, int seed, ConstructionInspiration inspiration)
    {
        var random = new Random(unchecked(seed ^ 0x36a21b57));
        var sites = CreationContactSites(definition);
        if (sites.Count == 0) return definition;
        bool support = definition.Body == null && random.NextDouble() < .25d;
        if (support)
        {
            definition.Impact = new ConstructionImpact { Targets = SkillUseTargetRelation.Friendly, DamageScale = 0f };
            foreach (var site in sites)
            {
                var impact = new ConstructionImpact { Targets = SkillUseTargetRelation.Friendly, DamageScale = 0f };
                if (random.Next(2) == 0) impact.Restoration = CreationRestoration(random, false);
                else impact.Statuses = new[] { CreationStatus(random, true) };
                site.Set(impact);
            }
        }
        else
        {
            var site = sites[random.Next(sites.Count)];
            var impact = Newtonsoft.Json.JsonConvert.DeserializeObject<ConstructionImpact>(Newtonsoft.Json.JsonConvert.SerializeObject(site.Impact));
            VaryContactEffect(impact, random, inspiration);
            site.Set(impact);
        }
        return definition;
    }

    /// <summary>改变独立部位的一组普通作用；已有时序、引用和其他部位保持。</summary>
    /// <param name="definition">原术独立草稿。</param><param name="random">本次确定选择。</param><param name="inspiration">实际倾向。</param>
    private static AbilityDefinition VaryContactEffects(AbilityDefinition definition, Random random, ConstructionInspiration inspiration)
    {
        var sites = CreationContactSites(definition);
        var site = sites[random.Next(sites.Count)];
        var impact = Newtonsoft.Json.JsonConvert.DeserializeObject<ConstructionImpact>(Newtonsoft.Json.JsonConvert.SerializeObject(site.Impact));
        VaryContactEffect(impact, random, inspiration);
        site.Set(impact);
        return definition;
    }

    /// <summary>状态、恢复及伤害性质可独立叠合，友方作用只生成增益或接触恢复。</summary>
    /// <param name="impact">本次独占作用。</param><param name="random">本次确定选择。</param><param name="inspiration">实际倾向。</param>
    private static void VaryContactEffect(ConstructionImpact impact, Random random, ConstructionInspiration inspiration)
    {
        bool support = impact.Targets != SkillUseTargetRelation.Hostile;
        ConstructionStatus status = CreationStatus(random, support);
        var statuses = (impact.Statuses ?? Array.Empty<ConstructionStatus>()).ToList();
        int same = statuses.FindIndex(value => value.Id == status.Id);
        if (same >= 0) statuses[same] = status;
        else if (statuses.Count < 16) statuses.Add(status);
        else statuses[random.Next(statuses.Count)] = status;
        impact.Statuses = statuses.ToArray();
        if (support || random.NextDouble() < .4d) impact.Restoration = CreationRestoration(random, !support);
        if (!support)
        {
            impact.SelectedTargetOnly = random.Next(3) == 0;
            impact.DamageBasis = random.Next(2) == 0 ? ConstructionDamageBasis.RemainingForce : ConstructionDamageBasis.InitialForce;
            if (Enumerable.Range(0, 8).Any(index => inspiration.Elements[index] > 0f) && random.Next(3) == 0) impact.Element = inspiration.Elements;
        }
    }

    /// <summary>生成已登记状态的普通属性或周期声明，不增加新状态种类。</summary>
    /// <param name="random">本次确定选择。</param><param name="support">友方恢复或祝福。</param>
    private static ConstructionStatus CreationStatus(Random random, bool support)
    {
        float Between(float low, float high) => low + (float)random.NextDouble() * (high - low);
        var result = new ConstructionStatus { Duration = Between(1.5f, 5f), Application = ConstructionStatusApplication.Refresh };
        if (support)
        {
            int kind = random.Next(3);
            float amount = Between(.1f, .35f);
            result.Id = kind == 0 ? StatusEffects.BattleBlessing.id : kind == 1 ? StatusEffects.GuardBlessing.id : StatusEffects.HasteBlessing.id;
            result.Application = ConstructionStatusApplication.Strongest;
            result.Potency = amount;
            result.Stats = new Dictionary<string, float> { [kind == 0 ? "multiplier_damage" : kind == 1 ? "armor" : "multiplier_speed"] = kind == 1 ? amount * 30f : amount };
            return result;
        }
        switch (random.Next(9))
        {
            case 0:
                result.Id = StatusEffects.Slow.id;
                result.Stats = new Dictionary<string, float> { ["multiplier_speed"] = -Between(.15f, .45f) };
                if (random.Next(2) == 0) result.Application = ConstructionStatusApplication.Stack;
                break;
            case 1: result.Id = StatusEffects.Freeze.id; result.Duration = Between(.2f, .8f); break;
            case 2: result.Id = StatusEffects.Daze.id; result.WaitForDuration = true; result.Duration = Between(.1f, .4f); break;
            case 3:
            case 4:
                bool weaken = random.Next(2) == 0;
                result.Id = weaken ? StatusEffects.Weaken.id : StatusEffects.ArmorBreak.id;
                result.TargetStatRatios = new Dictionary<string, float> { [weaken ? "damage" : "armor"] = -Between(.1f, .3f) };
                break;
            case 5: result.Id = StatusEffects.Silence.id; break;
            default:
                int kind = random.Next(3);
                result.Id = kind == 0 ? StatusEffects.Burn.id : kind == 1 ? StatusEffects.Poison.id : StatusEffects.EternalCurse.id;
                result.Tick = new ConstructionStatusTick
                {
                    ForcePerSecond = Between(.02f, .08f),
                    Element = kind == 0 ? new ElementComposition { fire = 1f } : new ElementComposition { entropy = 1f }
                };
                if (kind == 1) { result.Application = ConstructionStatusApplication.SourceStack; result.StackLimit = random.Next(2, 5); }
                if (kind == 2) result.TargetStatRatios = new Dictionary<string, float> { ["damage"] = -Between(.05f, .15f) };
                break;
        }
        return result;
    }

    /// <summary>恢复作用与真实人物接触相连，控制人物回能不被误写成资源退款。</summary>
    /// <param name="random">本次固定选择。</param><param name="controller">敌方接触后恢复实际控制人物。</param>
    private static ConstructionRestoration CreationRestoration(Random random, bool controller)
    {
        var result = new ConstructionRestoration { Target = controller ? ConstructionRestorationTarget.Controller : ConstructionRestorationTarget.Contact };
        if (controller) result.HealthPerForce = .05f + (float)random.NextDouble() * .2f;
        else
        {
            result.Health = random.Next(1, 6);
            if (random.Next(2) == 0) { result.HealthPerSecond = random.Next(1, 4); result.Duration = 1.5f + (float)random.NextDouble() * 2f; }
            if (random.Next(3) == 0) result.CleanseCount = random.Next(1, 3);
        }
        return result;
    }
}

/// <summary>正式旧请求保留原输入；只有升级后受理的活动使用第十二版。</summary>
internal sealed class CreationV12ToV13 : ISaveMigration
{
    /// <summary>前十一版生成规则所在的正式人物区段。</summary>
    public int FromVersion => 12;
    /// <summary>完整输入无需补写或重抽。</summary><param name="data">保存层独立复制的旧输入。</param>
    public JObject Apply(JObject data) => data;
}
