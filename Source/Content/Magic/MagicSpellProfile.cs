using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3.Utils;
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
    public SemanticAsset PrimaryElement { get; internal set; }
    public SemanticDescriptor Semantics { get; internal set; }

    /// <summary>
    /// 优先读取魔网收录时冻结的档案，非魔网技能则即时计算。
    /// </summary>
    public static MagicSpellProfile Resolve(Entity container)
    {
        return MagicWebManager.Instance?.TryGetProfile(container, out var profile) == true
            ? profile
            : Evaluate(container, true);
    }

    /// <summary>
    /// 只读取已经存在的通用技能等级；缺少等级时返回 null，不在资格查询中修复实体。
    /// </summary>
    public static MagicSpellProfile ResolveReadOnly(Entity container)
    {
        return MagicWebManager.Instance?.TryGetProfile(container, out var profile) == true
            ? profile
            : Evaluate(container, false);
    }

    /// <summary>
    /// 从技能容器的通用 ItemLevel 组装魔网档案，并在魔法层映射为环位。
    /// </summary>
    public static MagicSpellProfile Evaluate(Entity container)
    {
        return Evaluate(container, true);
    }

    private static MagicSpellProfile Evaluate(Entity container, bool refreshItemLevel)
    {
        if (container.IsNull || !container.HasComponent<SkillContainer>()) return null;

        var skill = container.GetComponent<SkillContainer>();
        var asset = skill.Asset;
        if (asset == null) return null;
        ItemLevel itemLevel;
        if (container.HasComponent<ItemLevel>())
        {
            itemLevel = container.GetComponent<ItemLevel>();
        }
        else if (refreshItemLevel)
        {
            if (!SkillContainerEvaluator.Refresh(container)) return null;
            itemLevel = container.GetComponent<ItemLevel>();
        }
        else
        {
            if (!SkillContainerEvaluator.TryEvaluate(container, out var evaluation)) return null;
            itemLevel = evaluation.ItemLevel;
        }
        SemanticDescriptor semantics = SkillSemanticCollector.BuildDescriptor(container);

        return new MagicSpellProfile
        {
            Ring = itemLevel * MaxMagicRing / MaxItemLevelValue,
            Element = asset.Element,
            ElementRequirement = ElementRequirement.FromComposition(asset.Element),
            FamilySignature = BuildFamilySignature(container),
            PrimaryElement = ElementSemanticProfileService.ResolveDominant(asset.Element, semantics),
            Semantics = semantics
        };
    }

    private static string BuildFamilySignature(Entity container)
    {
        var result = new SkillBlueprintExporter().Export(container);
        if (!result.Success) return string.Empty;
        result.Blueprint.TrajectoryAssetId = SkillBlueprintTrajectory.ResolveDefaultId(
            container.GetComponent<SkillContainer>().Asset);
        return SkillBlueprintSignature.Build(result.Blueprint);
    }
}
