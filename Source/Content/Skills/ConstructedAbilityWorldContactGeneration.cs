using System;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>受理时记录的一种世界操作，不持有运行时委托或地块。</summary>
public sealed class SynthesisWorldAction
{
    /// <summary>当时可用的稳定操作编号。</summary>
    [JsonProperty(Required = Required.Always)]
    public readonly string Id;

    /// <summary>当时的默认持续秒数；零表示不接受持续参数。</summary>
    [JsonProperty(Required = Required.Always)]
    public readonly float DefaultDuration;

    /// <summary>在请求读取边界检查明确的操作与持续数值。</summary>
    /// <param name="id">受理时编号。</param><param name="defaultDuration">受理时默认持续秒数。</param>
    [JsonConstructor]
    public SynthesisWorldAction(string id, float defaultDuration)
    {
        if (string.IsNullOrWhiteSpace(id) || float.IsNaN(defaultDuration) || float.IsInfinity(defaultDuration) ||
            defaultDuration < 0f || defaultDuration > 3600f)
            throw new ArgumentException("创作世界操作需要有效编号和持续时间");
        Id = id;
        DefaultDuration = defaultDuration;
    }
}

/// <summary>为已有真实作用位置组合世界操作，运动、形体及人物作用继续共用原定义。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>独立随机源不影响旧请求，也不为每次创作强制增加地块作用。</summary>
    /// <param name="definition">已经生成的独立资料。</param><param name="seed">固定种子。</param>
    /// <param name="resources">受理时固定的操作目录。</param>
    private static AbilityDefinition GenerateWorldContacts(AbilityDefinition definition, int seed, SynthesisResources resources)
    {
        var random = new Random(unchecked(seed ^ 0x193c52a7));
        if (resources.WorldActions.Length == 0 || random.NextDouble() >= .3d || CreationContactSites(definition).Count == 0)
            return definition;
        return VaryWorldContacts(definition, random, resources);
    }

    /// <summary>只替换选中作用的独立副本，保留原术及其他部位；次数与费用沿用共同规则。</summary>
    /// <param name="definition">本次独立草稿。</param><param name="random">固定选择。</param>
    /// <param name="resources">已冻结的世界操作。</param>
    private static AbilityDefinition VaryWorldContacts(AbilityDefinition definition, Random random, SynthesisResources resources)
    {
        var sites = CreationContactSites(definition);
        var site = sites[random.Next(sites.Count)];
        var impact = JsonConvert.DeserializeObject<ConstructionImpact>(JsonConvert.SerializeObject(site.Impact));
        var effects = (impact.WorldEffects ?? Array.Empty<ConstructionWorldEffect>()).ToList();
        int count = 1 + random.Next(Math.Min(2, resources.WorldActions.Length));
        var choices = resources.WorldActions.ToList();
        for (int i = 0; i < count; i++)
        {
            int index = random.Next(choices.Count);
            SynthesisWorldAction action = choices[index];
            choices.RemoveAt(index);
            var effect = new ConstructionWorldEffect
            {
                Action = action.Id, MaximumTiles = random.Next(4, 33), TileCentreOnly = random.Next(2) == 0,
                Duration = action.DefaultDuration > 0f ? action.DefaultDuration : null,
                Contact = new ConstructionContactPolicy
                {
                    Mode = (ConstructionContactMode)random.Next(4), Interval = .3f + (float)random.NextDouble() * .7f,
                    MaximumPerTarget = random.Next(1, 4), MaximumTotal = random.Next(8, 65)
                }
            };
            int same = effects.FindIndex(value => value.Action == action.Id);
            if (same >= 0) effects[same] = effect;
            else if (effects.Count < 8) effects.Add(effect);
            else effects[random.Next(effects.Count)] = effect;
        }
        impact.WorldEffects = effects.ToArray();
        site.Set(impact);
        return definition;
    }
}

/// <summary>旧请求原样保留规则与输入；只有新受理的请求固定世界操作。</summary>
internal sealed class CreationV17ToV18 : ISaveMigration
{
    /// <summary>已有第十六版命名放置改造规则的正式区段。</summary>
    public int FromVersion => 17;

    /// <summary>不从当前目录补写旧请求或重抽结果。</summary><param name="data">旧输入独立副本。</param>
    public JObject Apply(JObject data) => data;
}
