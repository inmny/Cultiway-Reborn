using System;
using Cultiway.Const;
using Cultiway.Core;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Components;

/// <summary>一种修炼方式已经积累的标准化有效修炼量。</summary>
public struct CultivationMethodPracticeEntry
{
    /// <summary>对应的 <see cref="Libraries.CultivateMethodAsset"/> 标识。</summary>
    public string method_id;

    /// <summary>折算后的有效修炼月数；一表示一标准月的有效修炼。</summary>
    public float effective_months;
}

/// <summary>
/// 保存角色按修炼方式归集的实践量，以及实践过程实际接触的八维元素组成。
/// 语义不在此处固化，而是在读取时通过修炼方式资产解析。
/// </summary>
public struct CultivationPracticeState : IComponent
{
    /// <summary>各修炼方式的累计有效修炼月数。</summary>
    public CultivationMethodPracticeEntry[] methods;

    /// <summary>按有效修炼月数加权累积、尚未归一化的八维元素暴露。</summary>
    public ElementComposition element_exposure;

    /// <summary>参与元素暴露累计的有效修炼月数总和。</summary>
    public float element_exposure_weight;

    /// <summary>把一次已经换算为标准月的实践结算归入对应修炼方式。</summary>
    public void Record(
        string methodId,
        float effectiveMonths,
        ElementComposition? exposure = null)
    {
        if (string.IsNullOrEmpty(methodId) || effectiveMonths <= 0f) return;

        bool found = false;
        if (methods != null)
        {
            for (var i = 0; i < methods.Length; i++)
            {
                if (!string.Equals(methods[i].method_id, methodId, StringComparison.Ordinal)) continue;
                methods[i].effective_months += effectiveMonths;
                found = true;
                break;
            }
        }

        if (!found)
        {
            int previousLength = methods?.Length ?? 0;
            Array.Resize(ref methods, previousLength + 1);
            methods[previousLength] = new CultivationMethodPracticeEntry
            {
                method_id = methodId,
                effective_months = effectiveMonths
            };
        }

        if (!exposure.HasValue) return;
        ElementComposition composition = exposure.Value;
        float total = 0f;
        for (var i = 0; i < ElementIndex.Count; i++) total += Mathf.Max(0f, composition[i]);
        if (total <= 0.0001f) return;

        for (var i = 0; i < ElementIndex.Count; i++)
            element_exposure[i] += Mathf.Max(0f, composition[i]) / total * effectiveMonths;
        element_exposure_weight += effectiveMonths;
    }

    /// <summary>尝试把累计元素暴露解析成归一化的八维组成。</summary>
    public readonly bool TryResolveElementExposure(out ElementComposition composition)
    {
        composition = default;
        if (element_exposure_weight <= 0.0001f) return false;

        for (var i = 0; i < ElementIndex.Count; i++)
            composition[i] = Mathf.Max(0f, element_exposure[i]) / element_exposure_weight;
        composition.Normalize();
        return true;
    }

    /// <summary>复制内部数组，避免角色传承或克隆后共享同一份实践条目。</summary>
    public readonly CultivationPracticeState DeepClone()
    {
        var clone = this;
        clone.methods = methods == null
            ? null
            : (CultivationMethodPracticeEntry[])methods.Clone();
        return clone;
    }
}
