using System;
using Cultiway.Const;
using Cultiway.Utils;
using UnityEngine;

namespace Cultiway.Core.Libraries;

public class ElementRootLibrary : AssetLibrary<ElementRootAsset>
{
    private const float MinimumGeneratedPurity = 0.15f;
    private const int DirectionSearchIterations = 16;

    public ElementRootAsset Common { get; private set; }
    public ElementRootAsset Entropy { get; private set; }

    public override void init()
    {
        Common = add(new ElementRootAsset(
            id: nameof(Common),
            new ElementComposition([0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.5f, 0.5f, 1f])
        ));
        Common.IconPath = "cultiway/icons/element_root/common";
        Common.Archetype.NaturalWeight = 27f;
        Entropy = add(new ElementRootAsset(
            id: nameof(Entropy),
            new ElementComposition([0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f])
        ));
        Entropy.IconPath = "cultiway/icons/element_root/entropy";
        Entropy.Archetype.NaturalWeight = 0.5f;
        Entropy.Archetype.NaturalSimilarityFloor = 0.95f;
        Entropy.Archetype.PuritySimilarityBaseline = 0.95f;
    }

    /// <summary>按完整八维组成解析灵根类型，并返回命中原型的余弦相似度。</summary>
    public ElementRootAsset GetRootType(float[] composition, out float final_sim)
    {
        if (composition == null || composition.Length < ElementIndex.Count)
            throw new ArgumentException($"Element root composition requires {ElementIndex.Count} values.",
                nameof(composition));

        ElementRootAsset asset = Common;
        var best_sim = MathUtils.CosineSimilarity(composition, Common.composition.AsArray(), ElementIndex.Count);
        foreach (var type in list)
        {
            if (type == Common) continue;
            var sim = MathUtils.CosineSimilarity(composition, type.composition.AsArray(), ElementIndex.Count);
            if (sim < best_sim ||
                sim.Equals(best_sim) &&
                string.CompareOrdinal(type.id, asset.id) >= 0) continue;
            best_sim = sim;
            asset = type;
        }

        final_sim = best_sim;
        return asset;
    }

    /// <summary>
    /// 按资产权重选择自然原型，再生成具有连续纯度的组成；最终缩放保持旧灵根强度分布不变。
    /// </summary>
    public float[] RollComposition()
    {
        var target = SelectNaturalArchetype();
        var raw = new float[ElementIndex.Count];
        for (var i = 0; i < raw.Length; i++) raw[i] = Mathf.Abs(RdUtils.NextStdNormal());

        var targetLogStrength = CalculateLogStrength(raw);
        Normalize(raw);
        var prototype = target.composition.AsArray();
        Normalize(prototype);

        float[] direction;
        if (target == Common)
        {
            direction = Blend(raw, prototype, 0.62f);
            var blend = 0.62f;
            while (GetRootType(direction, out _) != target)
            {
                blend = (blend + 1f) * 0.5f;
                direction = Blend(raw, prototype, blend);
            }
        }
        else
        {
            var quality = (Randy.randomFloat(0f, 1f) + Randy.randomFloat(0f, 1f)) * 0.5f;
            var desiredSimilarity = Mathf.Lerp(
                target.Archetype.NaturalSimilarityFloor,
                1f,
                Mathf.Lerp(MinimumGeneratedPurity, 1f, quality));
            var blend = ResolveBlend(raw, prototype, desiredSimilarity);
            direction = Blend(raw, prototype, blend);
            while (GetRootType(direction, out _) != target)
            {
                blend = (blend + 1f) * 0.5f;
                direction = Blend(raw, prototype, blend);
            }
        }

        var directionLogStrength = CalculateLogStrength(direction);
        var scale = directionLogStrength > 0f ? targetLogStrength / directionLogStrength : 0f;
        for (var i = 0; i < direction.Length; i++) direction[i] *= scale;
        return direction;
    }

    /// <summary>计算与 <see cref="Core.Components.ElementRoot.GetStrength"/> 对应的指数输入值。</summary>
    internal static float CalculateLogStrength(float[] composition)
    {
        return CalculateLogStrength(
            composition[ElementIndex.Iron],
            composition[ElementIndex.Wood],
            composition[ElementIndex.Water],
            composition[ElementIndex.Fire],
            composition[ElementIndex.Earth],
            composition[ElementIndex.Neg],
            composition[ElementIndex.Pos],
            composition[ElementIndex.Entropy]);
    }

    /// <summary>用八项元素值直接计算强度指数，避免高频调用创建临时数组。</summary>
    internal static float CalculateLogStrength(
        float iron,
        float wood,
        float water,
        float fire,
        float earth,
        float neg,
        float pos,
        float entropy)
    {
        return ((iron + wood + water + fire + earth) / 5f +
                (neg + pos) / 2f +
                entropy) / 3f;
    }

    /// <summary>按所有已注册原型的非负自然权重随机选择目标。</summary>
    private ElementRootAsset SelectNaturalArchetype()
    {
        var total = 0f;
        for (var i = 0; i < list.Count; i++) total += Mathf.Max(0f, list[i].Archetype.NaturalWeight);
        if (total <= 0f) return Common;

        var roll = Randy.randomFloat(0f, total);
        for (var i = 0; i < list.Count; i++)
        {
            roll -= Mathf.Max(0f, list[i].Archetype.NaturalWeight);
            if (roll <= 0f) return list[i];
        }
        return list[list.Count - 1];
    }

    /// <summary>二分求出使组成达到目标余弦相似度的最小原型混合量。</summary>
    private static float ResolveBlend(float[] noise, float[] prototype, float desiredSimilarity)
    {
        var low = 0f;
        var high = 1f;
        for (var i = 0; i < DirectionSearchIterations; i++)
        {
            var middle = (low + high) * 0.5f;
            var candidate = Blend(noise, prototype, middle);
            var similarity = MathUtils.CosineSimilarity(candidate, prototype, ElementIndex.Count);
            if (similarity < desiredSimilarity)
                low = middle;
            else
                high = middle;
        }
        return high;
    }

    /// <summary>在线性空间中混合随机方向和灵根原型方向。</summary>
    private static float[] Blend(float[] source, float[] target, float amount)
    {
        var result = new float[ElementIndex.Count];
        for (var i = 0; i < result.Length; i++) result[i] = Mathf.Lerp(source[i], target[i], amount);
        return result;
    }

    /// <summary>将非负向量归一化为总和一；零向量回退为等权方向。</summary>
    private static void Normalize(float[] values)
    {
        var total = 0f;
        for (var i = 0; i < values.Length; i++) total += Mathf.Max(0f, values[i]);
        if (total <= 0f)
        {
            var equal = 1f / values.Length;
            for (var i = 0; i < values.Length; i++) values[i] = equal;
            return;
        }
        for (var i = 0; i < values.Length; i++) values[i] = Mathf.Max(0f, values[i]) / total;
    }
}
