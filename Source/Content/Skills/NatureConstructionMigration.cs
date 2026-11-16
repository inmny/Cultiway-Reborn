using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core;
using Cultiway.Core.Skills;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Settings;
using Cultiway.Core.Visuals.Forms;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Skills;

/// <summary>指定性质和本次动态元素只在发射时展开为实际材料及明确组成。</summary>
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

    /// <summary>指定性质优先，其余动态伤害完整保留本次八项组成。</summary>
    /// <param name="skill">实际原容器。</param><param name="runtime">只属于本次发射的覆盖。</param>
    internal static ElementComposition Element(Entity skill, SkillCastRuntimeData runtime = default) => HasNature(skill)
        ? SkillSettingsCompiler.Element(skill.GetComponent<SkillSettings>().NatureId)
        : runtime.ResolveElement(skill.GetComponent<SkillContainer>().Asset.Element);

    /// <summary>动态雷不依赖原容器的固定外观，原固定雷继续原资格。</summary>
    /// <param name="skill">原容器。</param><param name="runtime">本次发射参数。</param>
    internal static bool IsLightning(Entity skill, SkillCastRuntimeData runtime) => HasNature(skill) || runtime.HasElementOverride
        ? SelectedNature(skill, runtime) == "lightning"
        : skill.GetComponent<SkillContainer>().VfxElement == SkillVfxElements.Lightning;

    /// <summary>先识别登记组合，再用已有带权语义选择混合主体；完整伤害不被主体选择缩减。</summary>
    /// <param name="skill">原来源。</param><param name="runtime">本次覆盖，不写回容器。</param>
    private static string SelectedNature(Entity skill, SkillCastRuntimeData runtime)
    {
        if (HasNature(skill)) return skill.GetComponent<SkillSettings>().NatureId;
        if (!runtime.HasElementOverride) return null;
        ElementComposition element = runtime.ElementOverride;
        double total = 0;
        for (int i = 0; i < 8; i++) total += element[i];
        if (total <= 0) return null;
        foreach (string id in materials.Keys)
        {
            ElementComposition known = SkillNatureCatalog.Natures[id].Element;
            bool matches = true;
            for (int i = 0; i < 8 && matches; i++) matches = Math.Abs(element[i] / total - known[i]) < .000001;
            if (matches) return id;
        }
        var dominant = ElementSemanticProfileService.ResolveDominant(element);
        if (dominant == SkillSemantics.Element.Iron) return "metal";
        if (dominant == SkillSemantics.Element.Wood) return "wood";
        if (dominant == SkillSemantics.Element.Water) return "water";
        if (dominant == SkillSemantics.Element.Fire) return "fire";
        if (dominant == SkillSemantics.Element.Earth) return "earth";
        if (dominant == SkillSemantics.Element.Neg) return "yin";
        if (dominant == SkillSemantics.Element.Pos) return "yang";
        if (dominant == SkillSemantics.Element.Entropy) return "chaos";
        return null;
    }

    /// <summary>保留形状、动作和独立附加元素，将本次选择展开到本体及后段。</summary>
    /// <param name="definition">完整普通构造。</param><param name="skill">本次原容器。</param>
    /// <param name="runtime">本次覆盖及原一次倍率。</param>
    internal static AbilityDefinition Bind(AbilityDefinition definition, Entity skill, SkillCastRuntimeData runtime = default)
    {
        string id = SelectedNature(skill, runtime);
        ElementComposition? composition = !HasNature(skill) && runtime.HasElementOverride ? runtime.ElementOverride : null;
        if (composition.HasValue)
        {
            definition.Impact ??= new ConstructionImpact();
            definition.Impact.Element = composition;
        }
        if (id != null || composition.HasValue)
            BindNode(definition.Root, id == null ? null : materials[id].Matter, id == null ? null : materials[id].Surface, composition);
        return definition;
    }

    /// <summary>相同材料保留原物态及细节；跨材料选择默认物态，显示继续复用共同部件资料。</summary>
    /// <param name="node">当前本体部位。</param><param name="material">目标实际材料。</param>
    /// <param name="surface">目标主体表面。</param><param name="composition">可选本次完整伤害。</param>
    private static void BindNode(ConstructionNode node, string material, string surface, ElementComposition? composition)
    {
        if (composition.HasValue && node.Impact != null) node.Impact.Element = composition;
        if (material != null && node.Matter != material)
        {
            AbilityMatter matter = AbilityMatters.Resolve(material, null, node.Id);
            node.Matter = matter.Id; node.State = matter.State;
            // 可换材质的主体保留握柄等细节；纯能量及没有主体材质标记的支持形体明确覆盖表面。
            node.Surface = matter.Surface == null || !FormResources.Load(node.Form).Description.Parts.Any(part => part.UseMatterSurface)
                ? surface : null;
        }
        if (node.Children != null)
            foreach (ConstructionNode child in node.Children) BindNode(child, material, surface, composition);
    }
}
