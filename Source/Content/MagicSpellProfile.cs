using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Content.Libraries;
using Friflo.Engine.ECS;

namespace Cultiway.Content;

/// <summary>
/// 魔网在收录法术时计算出的稳定档案，供学习与环级限制使用。
/// </summary>
public sealed class MagicSpellProfile
{
    private const int MaxMagicRing = 12;
    private const int MaxItemLevelValue = 35;

    public int Ring { get; internal set; }
    public ElementComposition Element { get; internal set; }
    public ElementRequirement ElementRequirement { get; internal set; }
    public string FamilySignature { get; internal set; }
    public string PrimaryElementTag { get; internal set; }

    /// <summary>
    /// 从技能容器的通用 ItemLevel 组装魔网档案，并在魔法层映射为环位。
    /// </summary>
    public static MagicSpellProfile Evaluate(Entity container)
    {
        if (container.IsNull || !container.HasComponent<SkillContainer>()) return null;

        var skill = container.GetComponent<SkillContainer>();
        var asset = skill.Asset;
        if (asset == null) return null;
        if (!container.HasComponent<ItemLevel>() && !SkillContainerEvaluator.Refresh(container)) return null;
        var itemLevel = container.GetComponent<ItemLevel>();

        return new MagicSpellProfile
        {
            Ring = itemLevel * MaxMagicRing / MaxItemLevelValue,
            Element = asset.Element,
            ElementRequirement = ElementRequirement.FromComposition(asset.Element),
            FamilySignature = BuildFamilySignature(container),
            PrimaryElementTag = ResolvePrimaryElementTag(asset.Element)
        };
    }

    private static string BuildFamilySignature(Entity container)
    {
        var result = new SkillBlueprintExporter().Export(container);
        if (!result.Success) return string.Empty;
        result.Blueprint.AnimationIndex = 0;
        return SkillBlueprintSignature.Build(result.Blueprint);
    }

    private static string ResolvePrimaryElementTag(ElementComposition element)
    {
        var tags = new[]
        {
            SkillTags.Element.Iron, SkillTags.Element.Wood, SkillTags.Element.Water, SkillTags.Element.Fire,
            SkillTags.Element.Earth, SkillTags.Element.Neg, SkillTags.Element.Pos, SkillTags.Element.Entropy
        };
        var bestIndex = -1;
        var bestValue = 0f;
        for (var i = 0; i < tags.Length; i++)
        {
            if (element[i] <= bestValue) continue;
            bestValue = element[i];
            bestIndex = i;
        }
        return bestIndex < 0 ? SkillTags.Element.Generic : tags[bestIndex];
    }
}
