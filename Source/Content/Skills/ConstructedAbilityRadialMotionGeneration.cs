using System;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.Usage;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>已有范围推拉进入普通创作，不新增另一套受力或命中规则。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>第十九版才加入这份独立选择，旧请求的抽取与费用保持。</summary>
    /// <param name="definition">本次独立草稿。</param><param name="seed">受理时冻结的种子。</param>
    private static AbilityDefinition GenerateRadialMotion(AbilityDefinition definition, int seed)
    {
        var random = new Random(unchecked(seed ^ 0x6c270a41));
        return random.NextDouble() < .35d ? VaryRadialMotion(definition, random) : definition;
    }

    /// <summary>从真实形体选择中心，速度符号区分推拉，人物关系沿用该部位实际作用。</summary>
    /// <param name="definition">独立草稿，身体和命名运动保持不变。</param><param name="random">本次固定选择。</param>
    private static AbilityDefinition VaryRadialMotion(AbilityDefinition definition, Random random)
    {
        var nodes = DerivationNodes(definition.Root).Where(node => !string.IsNullOrEmpty(node.Form)).ToArray();
        if (nodes.Length == 0) return definition;
        var node = nodes[random.Next(nodes.Length)];
        node.RadialMotion = new ConstructionRadialMotion
        {
            Radius = 1.5f + (float)random.NextDouble() * 4f,
            Speed = (random.Next(2) == 0 ? -1f : 1f) * (.5f + (float)random.NextDouble() * 2f),
            Interval = .1f + (float)random.NextDouble() * .15f,
            Targets = (node.Impact ?? definition.Impact)?.Targets ?? SkillUseTargetRelation.Hostile
        };
        return definition;
    }
}

/// <summary>第十八版等待请求原样保留，只有新请求加入范围运动选择与需求。</summary>
internal sealed class CreationV19ToV20 : ISaveMigration
{
    /// <summary>已有第十八版人物接触创作的正式人物区段。</summary>
    public int FromVersion => 19;

    /// <summary>原完整输入不增加当前随机选择。</summary><param name="data">独立旧资料。</param>
    public JObject Apply(JObject data) => data;
}
