using System;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Usage;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>独立尝试映像构造，不改动前二十八版的种子或选择。</summary>
    /// <param name="definition">已经生成部位和动作的构造。</param><param name="seed">固定创作种子。</param>
    private static AbilityDefinition GenerateCorrespondence(AbilityDefinition definition, int seed)
    {
        var random = new Random(unchecked(seed ^ 0x164ad71b));
        if (definition.Root == null || random.NextDouble() >= .25d) return definition;
        CompiledAbility plan = ValidateDefinition(definition, cache: false);
        var sites = CorrespondenceSites(definition, plan);
        return sites.Length == 0 ? definition : VaryCorrespondence(definition, plan, sites, random);
    }

    /// <summary>只使用真实有交锋面的部位；时段按已展开的最大存在时间选择。</summary>
    /// <param name="definition">本次独占构造。</param><param name="plan">同一构造的已编译资料。</param>
    private static (ConstructionNode Part, float Span)[] CorrespondenceSites(AbilityDefinition definition, CompiledAbility plan)
    {
        var sites = plan.Nodes.Where(form => form.DeclarationPath != null && form.Resource.Contacts.Length > 0 &&
                                            form.ContactRole != ConstructionContactRole.Decoration)
            .GroupBy(form => form.DeclarationPath).ToDictionary(group => group.Key,
                group => group.Max(form => form.Presence.y - form.Presence.x), StringComparer.Ordinal);
        return ConstructionContactEdits.Parts(definition.Root)
            .Where(part => sites.TryGetValue(part.Path, out float span) && span >= .25f)
            .Select(part => (part.Node, sites[part.Path])).ToArray();
    }

    /// <summary>仅附加或改写本次选中的对应，保留原运动、接触及命名步骤。</summary>
    /// <param name="definition">本次独占构造。</param><param name="plan">同一构造的已编译资料。</param>
    /// <param name="sites">可支持实际映像的部位。</param><param name="random">受理时确定的随机源。</param>
    private static AbilityDefinition VaryCorrespondence(AbilityDefinition definition, CompiledAbility plan,
        (ConstructionNode Part, float Span)[] sites, Random random)
    {
        var site = sites[random.Next(sites.Length)];
        bool hostile = plan.Impact.Targets == SkillUseTargetRelation.Hostile;
        bool otherActor = plan.Use.Target == ActiveAbilityTargetMode.Object;
        var old = site.Part.Correspondence;
        float from = random.Next(2) == 0 ? 0f : site.Span * (.1f + (float)random.NextDouble() * .25f);
        site.Part.Correspondence = new ConstructionCorrespondence
        {
            Id = old?.Id ?? "image",
            Target = otherActor ? ConstructionCorrespondenceTarget.Target : ConstructionCorrespondenceTarget.Source,
            DamageFraction = hostile && otherActor ? .35f + (float)random.NextDouble() * .65f : 0f,
            Effects = hostile && otherActor ? (ConstructionCorrespondenceEffects)random.Next(8) :
                (ConstructionCorrespondenceEffects)random.Next(1, 4),
            ActiveFrom = from,
            ActiveUntil = random.Next(2) == 0 ? null : from + (site.Span - from) * (.4f + (float)random.NextDouble() * .5f)
        };
        return definition;
    }
}

/// <summary>旧等待请求继续使用原规则，不补写输入或重算需求。</summary>
internal sealed class CreationV29ToV30 : ISaveMigration
{
    /// <summary>原侵蚀及老化创作区段。</summary>
    public int FromVersion => 29;

    /// <summary>原样迁移冻结请求。</summary><param name="data">保存的完整输入。</param>
    public JObject Apply(JObject data) => data;
}
