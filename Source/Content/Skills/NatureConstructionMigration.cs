using System.Collections.Generic;
using System.Linq;
using Cultiway.Core;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Settings;
using Cultiway.Core.Visuals.Forms;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Skills;

/// <summary>原指定性质只在发射时展开为实际材料，不给普通构造留下旧性质分派。</summary>
internal static class NatureConstructionMigration
{
    /// <summary>性质编号与已登记实际材料、主体表面之间的明确对应。</summary>
    private static readonly Dictionary<string, (string Matter, string Surface)> materials = new()
    {
        ["metal"] = ("iron", "metal"), ["wood"] = ("wood", "wood"),
        ["water"] = ("water", "water"), ["fire"] = ("fire", "flame"),
        ["earth"] = ("earth", "stone"), ["yin"] = ("yin", "yin_depth"),
        ["yang"] = ("yang", "solar_light"), ["chaos"] = ("entropy", "entropy_veil"),
        ["lightning"] = ("lightning", "lightning_dense"), ["ice"] = ("ice", "ice"),
        ["wind"] = ("wind", "wind"), ["poison"] = ("poison", "poison_liquid")
    };

    /// <summary>指定性质沿用原优先级，高于本次动态元素。</summary>
    /// <param name="skill">实际原容器。</param>
    internal static bool HasNature(Entity skill) => skill.TryGetComponent(out SkillSettings settings) && settings.NatureId != null;

    /// <summary>只接管原资产允许的性质及明确有效的可导电水设置。</summary>
    /// <param name="skill">已经通过原设置入口的实际容器。</param>
    internal static bool Supports(Entity skill)
    {
        if (!skill.TryGetComponent(out SkillSettings settings)) return true;
        var asset = skill.GetComponent<SkillContainer>().Asset;
        if (settings.ConductiveWater && (!SkillNatureCatalog.Natures.TryGetValue(settings.NatureId ?? "", out var nature) ||
            !nature.CanConduct || !asset.ImpactProfile.IsField)) return false;
        if (settings.NatureId == null) return true;
        return materials.ContainsKey(settings.NatureId) && SkillNatureCatalog.Natures.ContainsKey(settings.NatureId) &&
            asset.CarrierRequirement != SkillCarrierRequirement.PhysicalBody && asset.SupportsNature(settings.NatureId);
    }

    /// <summary>伤害记录保留本体选定元素；独立附加作用继续自己的元素。</summary>
    /// <param name="skill">本次原容器。</param>
    internal static ElementComposition Element(Entity skill) => HasNature(skill)
        ? SkillSettingsCompiler.Element(skill.GetComponent<SkillSettings>().NatureId)
        : skill.GetComponent<SkillContainer>().Asset.Element;

    /// <summary>保留原形状和运动，只转换本体及后段本体的实际材料和主体表面。</summary>
    /// <param name="definition">已展开的完整普通构造。</param><param name="skill">本次原容器。</param>
    internal static AbilityDefinition Bind(AbilityDefinition definition, Entity skill)
    {
        if (!HasNature(skill)) return definition;
        var selected = materials[skill.GetComponent<SkillSettings>().NatureId];
        BindNode(definition.Root, selected.Matter, selected.Surface);
        return definition;
    }

    /// <summary>相同材料保留原物态及细节；跨材料选择默认物态，显示继续复用共同部件资料。</summary>
    /// <param name="node">当前本体部位。</param><param name="material">目标实际材料。</param>
    /// <param name="surface">目标主体表面。</param>
    private static void BindNode(ConstructionNode node, string material, string surface)
    {
        if (node.Matter != material)
        {
            AbilityMatter matter = AbilityMatters.Resolve(material, null, node.Id);
            node.Matter = matter.Id; node.State = matter.State;
            // 可换材质的主体保留握柄等细节；纯能量及没有主体材质标记的支持形体明确覆盖表面。
            node.Surface = matter.Surface == null || !FormResources.Load(node.Form).Description.Parts.Any(part => part.UseMatterSurface)
                ? surface : null;
        }
        if (node.Children != null)
            foreach (ConstructionNode child in node.Children) BindNode(child, material, surface);
    }
}
