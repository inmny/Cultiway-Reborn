using System;
using System.Linq;

namespace Cultiway.Core.SkillLibV3.Blueprints;

/// <summary>维护法术蓝图与法术实体必需词条之间的结构约束。</summary>
public static class SkillBlueprintModifierRules
{
    /// <summary>补入当前法术实体声明但蓝图尚未持有的必需词条。</summary>
    public static bool EnsureRequiredModifiers(SkillBlueprint blueprint, SkillEntityAsset entityAsset)
    {
        if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
        if (entityAsset == null) return false;

        bool changed = false;
        foreach (SkillModifierSpec required in entityAsset.RequiredModifiers)
        {
            if (blueprint.Modifiers.Any(spec => spec?.AssetId == required.AssetId)) continue;
            blueprint.Modifiers.Add(required.DeepClone());
            changed = true;
        }
        return changed;
    }

    /// <summary>
    /// 切换法术实体时移除旧实体独占的必需词条，并补入新实体的必需词条。
    /// 两个实体共同要求的词条会保留用户已经编辑的参数。
    /// </summary>
    public static bool RebindEntity(
        SkillBlueprint blueprint,
        SkillEntityAsset previousEntity,
        SkillEntityAsset nextEntity)
    {
        if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
        if (nextEntity == null) throw new ArgumentNullException(nameof(nextEntity));

        bool changed = false;
        if (previousEntity != null && previousEntity != nextEntity)
        {
            int removed = blueprint.Modifiers.RemoveAll(spec =>
                spec != null &&
                previousEntity.IsRequiredModifier(spec.AssetId) &&
                !nextEntity.IsRequiredModifier(spec.AssetId));
            changed = removed > 0;
        }
        return EnsureRequiredModifiers(blueprint, nextEntity) || changed;
    }
}
