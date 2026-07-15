using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>
/// 将任意数量材料规范化为稳定的材料记录和可扩展语义 trait。
/// </summary>
public static class ArtifactMaterialSemantics
{
    public static ArtifactRecipeContext Build(IReadOnlyList<Entity> ingredients)
    {
        Dictionary<string, ArtifactMaterialRecord> grouped = new(StringComparer.Ordinal);
        for (int i = 0; i < ingredients.Count; i++)
        {
            ArtifactMaterialRecord record = Capture(ingredients[i]);
            string key = record.GetIdentityKey();
            if (grouped.TryGetValue(key, out ArtifactMaterialRecord existing))
            {
                existing.count++;
                grouped[key] = existing;
            }
            else
            {
                grouped.Add(key, record);
            }
        }

        ArtifactMaterialRecord[] records = grouped
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Value)
            .ToArray();
        return BuildContext(records, ingredients.Count);
    }

    /// <summary>
    /// 为百宝阁的显式设计创建不伪造具体材料来源的语义上下文。
    /// </summary>
    public static ArtifactRecipeContext BuildDesign(ItemLevel level, int selectedAtomCount)
    {
        int ingredientCount = Math.Max(1, selectedAtomCount);
        float stability = 1f;
        ArtifactMaterialData materialData = new()
        {
            materials = [],
            traits =
            [
                Trait(ArtifactMaterialTraits.Quality, (int)level / 9f),
                Trait(ArtifactMaterialTraits.Quantity, Log2(1f + ingredientCount)),
                Trait(ArtifactMaterialTraits.Stability, stability),
            ],
            ingredient_count = ingredientCount,
            quality_budget = ingredientCount * (1f + (int)level / 9f),
            stability = stability,
            complexity = Log2(1f + ingredientCount) * 0.06f,
        };
        return new ArtifactRecipeContext
        {
            quality_stage = level.Stage,
            quality_level = level.Level,
            material_data = materialData,
        };
    }

    /// <summary>
    /// 将已选 atom 的物理条件和功能倾向固化进成品材料语义。
    /// </summary>
    public static void ApplyAtoms(ArtifactRecipeContext context, IReadOnlyList<ArtifactAtomSelection> atoms)
    {
        ArtifactMaterialData data = context.material_data;
        Dictionary<string, float> traits = ToTraitMap(data.traits);
        float stabilityContribution = 0f;
        float volatilityContribution = 0f;
        float atomComplexity = 0f;

        for (int i = 0; i < atoms.Count; i++)
        {
            ArtifactAtomSelection selection = atoms[i];
            atomComplexity += selection.Strength * 0.035f;
            ArtifactMaterialTrait[] contributions = selection.Atom.semantic_traits ?? [];
            for (int j = 0; j < contributions.Length; j++)
            {
                ArtifactMaterialTrait contribution = contributions[j];
                float value = contribution.value * selection.Strength;
                if (contribution.key == ArtifactMaterialTraits.Stability)
                {
                    stabilityContribution += value;
                    continue;
                }
                if (contribution.key == ArtifactMaterialTraits.Volatility)
                {
                    volatilityContribution += value;
                }
                AddTrait(traits, contribution.key, value);
            }
        }

        data.stability = Mathf.Clamp01(
            data.stability + stabilityContribution * 0.06f - volatilityContribution * 0.04f);
        traits[ArtifactMaterialTraits.Stability] = data.stability;
        data.complexity += atomComplexity + Math.Max(0, atoms.Count - 1) * 0.025f;
        data.traits = ToTraitArray(traits);
        context.material_data = data;
    }

    public static ArtifactMaterialRecord Capture(Entity ingredient)
    {
        ItemShape shape = ingredient.GetComponent<ItemShape>();
        ItemLevel level = ingredient.GetComponent<ItemLevel>();
        ItemCreation creation = ingredient.GetComponent<ItemCreation>();
        ArtifactMaterialRecord record = new()
        {
            shape_id = shape.shape_id,
            source_asset_id = creation.creator_asset_id ?? string.Empty,
            jindan_id = string.Empty,
            quality = level,
            count = 1,
        };

        if (ingredient.TryGetComponent(out ElementRoot root))
        {
            record.iron += Mathf.Max(0f, root.Iron);
            record.wood += Mathf.Max(0f, root.Wood);
            record.water += Mathf.Max(0f, root.Water);
            record.fire += Mathf.Max(0f, root.Fire);
            record.earth += Mathf.Max(0f, root.Earth);
            record.neg += Mathf.Max(0f, root.Neg);
            record.pos += Mathf.Max(0f, root.Pos);
            record.entropy += Mathf.Max(0f, root.Entropy);
        }
        if (ingredient.TryGetComponent(out XianBase xianBase))
        {
            record.iron += Mathf.Max(0f, xianBase.iron);
            record.wood += Mathf.Max(0f, xianBase.wood);
            record.water += Mathf.Max(0f, xianBase.water);
            record.fire += Mathf.Max(0f, xianBase.fire);
            record.earth += Mathf.Max(0f, xianBase.earth);
            record.jing = Mathf.Max(0f, xianBase.jing);
            record.qi = Mathf.Max(0f, xianBase.qi);
            record.shen = Mathf.Max(0f, xianBase.shen);
        }
        if (ingredient.TryGetComponent(out Jindan jindan))
        {
            record.jindan_id = jindan.jindan_type ?? string.Empty;
            record.jindan_strength = Mathf.Max(0f, jindan.strength);
        }
        return record;
    }

    private static ArtifactRecipeContext BuildContext(ArtifactMaterialRecord[] records, int ingredientCount)
    {
        Dictionary<string, float> traits = new(StringComparer.Ordinal);
        HashSet<string> sources = new(StringComparer.Ordinal);
        float qualityBudget = 0f;
        float qualitySum = 0f;
        float semanticWeight = 0f;
        float iron = 0f;
        float wood = 0f;
        float water = 0f;
        float fire = 0f;
        float earth = 0f;
        float neg = 0f;
        float pos = 0f;
        float entropy = 0f;
        float vitality = 0f;
        float spirituality = 0f;

        for (int i = 0; i < records.Length; i++)
        {
            ArtifactMaterialRecord record = records[i];
            float count = record.count;
            float weight = (1f + record.quality / 9f) * count;
            qualityBudget += weight;
            qualitySum += record.quality * count;
            semanticWeight += weight;
            iron += record.iron * weight;
            wood += record.wood * weight;
            water += record.water * weight;
            fire += record.fire * weight;
            earth += record.earth * weight;
            neg += record.neg * weight;
            pos += record.pos * weight;
            entropy += record.entropy * weight;
            vitality += (record.jing + record.qi) * 0.5f * weight;
            spirituality += (record.shen + record.jindan_strength + record.quality / 35f) * weight;
            sources.Add(record.source_asset_id ?? string.Empty);
        }

        float divisor = Mathf.Max(semanticWeight, 0.0001f);
        iron /= divisor;
        wood /= divisor;
        water /= divisor;
        fire /= divisor;
        earth /= divisor;
        neg /= divisor;
        pos /= divisor;
        entropy /= divisor;
        vitality /= divisor;
        spirituality /= divisor;

        float qualityAverage = ingredientCount > 0 ? qualitySum / ingredientCount : 0f;
        float stability = ResolveStability(
            records,
            qualityAverage,
            sources.Count,
            iron,
            wood,
            water,
            fire,
            earth,
            neg,
            pos,
            entropy);
        int quality = Mathf.Clamp(
            Mathf.RoundToInt(
                qualityAverage + Log2(Mathf.Max(1, ingredientCount)) * 0.75f - (1f - stability) * 2.5f),
            0,
            35);

        SetPositive(traits, ArtifactMaterialTraits.Iron, iron);
        SetPositive(traits, ArtifactMaterialTraits.Wood, wood);
        SetPositive(traits, ArtifactMaterialTraits.Water, water);
        SetPositive(traits, ArtifactMaterialTraits.Fire, fire);
        SetPositive(traits, ArtifactMaterialTraits.Earth, earth);
        SetPositive(traits, ArtifactMaterialTraits.Neg, neg);
        SetPositive(traits, ArtifactMaterialTraits.Pos, pos);
        SetPositive(traits, ArtifactMaterialTraits.Entropy, entropy);
        SetPositive(traits, ArtifactMaterialTraits.Vitality, vitality);
        SetPositive(traits, ArtifactMaterialTraits.Spirituality, spirituality);
        traits[ArtifactMaterialTraits.Quality] = quality / 9f;
        traits[ArtifactMaterialTraits.Quantity] = Log2(1f + ingredientCount);
        traits[ArtifactMaterialTraits.SourceDiversity] = Log2(1f + sources.Count);
        traits[ArtifactMaterialTraits.Stability] = stability;

        ArtifactMaterialData materialData = new()
        {
            materials = records,
            traits = ToTraitArray(traits),
            ingredient_count = ingredientCount,
            quality_budget = qualityBudget,
            stability = stability,
            complexity = Log2(1f + ingredientCount) * 0.06f +
                         Math.Max(0, records.Length - 1) * 0.025f +
                         (1f - stability) * 0.25f,
        };
        return new ArtifactRecipeContext
        {
            dominant_shape_id = ResolveDominantShape(records),
            main_material_shape_id = ResolveMainMaterialShape(records),
            quality_stage = quality / 9,
            quality_level = quality % 9,
            material_data = materialData,
        };
    }

    private static float ResolveStability(
        IReadOnlyList<ArtifactMaterialRecord> records,
        float qualityAverage,
        int sourceCount,
        float iron,
        float wood,
        float water,
        float fire,
        float earth,
        float neg,
        float pos,
        float entropy)
    {
        float fiveTotal = iron + wood + water + fire + earth;
        float cycleConflict = Mathf.Min(iron, wood) + Mathf.Min(wood, earth) +
                              Mathf.Min(earth, water) + Mathf.Min(water, fire) + Mathf.Min(fire, iron);
        float polarityConflict = Mathf.Min(neg, pos);
        float elementTotal = fiveTotal + neg + pos + entropy;
        float elementPenalty = elementTotal > 0.0001f
            ? cycleConflict / Mathf.Max(fiveTotal, 0.0001f) * 0.34f +
              polarityConflict / elementTotal * 0.28f +
              entropy / elementTotal * 0.18f
            : 0f;

        float variance = 0f;
        int count = 0;
        for (int i = 0; i < records.Count; i++)
        {
            float delta = records[i].quality - qualityAverage;
            variance += delta * delta * records[i].count;
            count += records[i].count;
        }
        float qualityPenalty = count > 0 ? Mathf.Sqrt(variance / count) / 35f * 0.35f : 0f;
        float sourcePenalty = Mathf.Min(0.15f, Math.Max(0, sourceCount - 1) * 0.015f);
        return Mathf.Clamp01(1f - elementPenalty - qualityPenalty - sourcePenalty);
    }

    private static string ResolveDominantShape(IReadOnlyList<ArtifactMaterialRecord> records)
    {
        return records
            .GroupBy(record => record.shape_id, StringComparer.Ordinal)
            .Select(group => new
            {
                Shape = group.Key,
                Count = group.Sum(record => record.count),
                Quality = group.Sum(record => (record.quality + 1) * record.count),
            })
            .OrderByDescending(group => group.Count)
            .ThenByDescending(group => group.Quality)
            .ThenBy(group => group.Shape, StringComparer.Ordinal)
            .Select(group => group.Shape)
            .FirstOrDefault();
    }

    private static string ResolveMainMaterialShape(IReadOnlyList<ArtifactMaterialRecord> records)
    {
        return records
            .GroupBy(record => record.shape_id, StringComparer.Ordinal)
            .Select(group => new
            {
                Shape = group.Key,
                Quality = group.Sum(record => (record.quality + 1) * record.count),
            })
            .OrderByDescending(group => group.Quality)
            .ThenBy(group => group.Shape, StringComparer.Ordinal)
            .Select(group => group.Shape)
            .FirstOrDefault();
    }

    private static Dictionary<string, float> ToTraitMap(ArtifactMaterialTrait[] values)
    {
        Dictionary<string, float> result = new(StringComparer.Ordinal);
        ArtifactMaterialTrait[] source = values ?? [];
        for (int i = 0; i < source.Length; i++) result[source[i].key] = source[i].value;
        return result;
    }

    private static ArtifactMaterialTrait[] ToTraitArray(Dictionary<string, float> traits)
    {
        return traits
            .Where(pair => Mathf.Abs(pair.Value) > 0.0001f)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => Trait(pair.Key, pair.Value))
            .ToArray();
    }

    private static void AddTrait(Dictionary<string, float> traits, string key, float value)
    {
        if (string.IsNullOrEmpty(key) || Mathf.Abs(value) <= 0.0001f) return;
        traits.TryGetValue(key, out float current);
        traits[key] = current + value;
    }

    private static void SetPositive(Dictionary<string, float> traits, string key, float value)
    {
        if (value > 0.0001f) traits[key] = value;
    }

    private static ArtifactMaterialTrait Trait(string key, float value)
    {
        return new ArtifactMaterialTrait { key = key, value = value };
    }

    private static float Log2(float value)
    {
        return Mathf.Log(Mathf.Max(value, 1f), 2f);
    }
}
