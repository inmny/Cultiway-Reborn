using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.UI.CreatureInfoPages;

/// <summary>人物功法页使用的只读展示快照。</summary>
internal sealed class CultibookPageModel
{
    public ActorExtend Actor;
    public CultibookEntryModel Main;
    public string CurrentMethodName;
    public float TotalPracticeMonths;
    public readonly List<CultivationPracticeEntryModel> Practices = new();
    public readonly float[] ElementExposure = new float[ElementIndex.Count];
    public bool HasElementExposure;
    public readonly List<CultivationResourceEntryModel> Resources = new();
    public readonly List<CultibookEntryModel> KnownCultibooks = new();
}

/// <summary>一部人物已掌握或了解的功法摘要。</summary>
internal sealed class CultibookEntryModel
{
    public CultibookAsset Asset;
    public string CoverPath;
    public float Mastery;
    public bool HasAffinity;
    public float Affinity;
}

/// <summary>一种修炼方式的累计实践摘要。</summary>
internal sealed class CultivationPracticeEntryModel
{
    public string MethodName;
    public float EffectiveMonths;
    public float Share;
}

/// <summary>功法页中一项当前可见的修炼资源。</summary>
internal sealed class CultivationResourceEntryModel
{
    public string Name;
    public string IconPath;
    public float Value;
    public float Capacity;
    public bool HasCapacity;
}

/// <summary>集中解析功法、实践和资源数据，避免页面直接遍历 ECS 状态。</summary>
internal static class CultibookPagePresentation
{
    private const string WakanNameKey = "Cultiway.Cultivation.Resource.ActorWakan";
    private const string WakanIconPath = "cultiway/icons/iconWakan";
    private static readonly string[] CultibookCoverPaths =
    [
        "books/custom_book_covers/cultibook/01",
        "books/custom_book_covers/cultibook/11",
        "books/custom_book_covers/cultibook/31",
        "books/custom_book_covers/cultibook/41",
        "books/custom_book_covers/cultibook/51"
    ];

    /// <summary>为指定人物创建一次稳定的功法页展示快照。</summary>
    public static CultibookPageModel Build(ActorExtend actor)
    {
        var model = new CultibookPageModel { Actor = actor };
        if (actor?.Base == null) return model;

        CultibookAsset main = actor.GetMainCultibook();
        if (main != null)
        {
            model.Main = BuildCultibookEntry(actor, main, actor.GetMainCultibookMastery());
            CultivateMethodAsset method = main.GetCultivateMethod();
            model.CurrentMethodName = method == null ? string.Empty : method.id.Localize();
            AppendContextualResources(model, actor, method);
        }

        AppendPractice(model, actor);
        AppendActorWakan(model, actor);
        AppendKnownCultibooks(model, actor, main);
        return model;
    }

    /// <summary>解析人物对指定功法的八维灵根契合。</summary>
    public static bool TryResolveAffinity(ActorExtend actor, CultibookAsset asset, out float affinity)
    {
        affinity = 0f;
        if (actor == null || asset == null || !actor.HasElementRoot()) return false;
        ref ElementRoot root = ref actor.GetElementRoot();
        affinity = ElementRootAffinityResolver.Resolve(root, asset).Combined;
        return true;
    }

    /// <summary>以紧凑格式显示非负数值。</summary>
    public static string FormatNumber(float value)
    {
        value = Mathf.Max(0f, value);
        return value.ToString(value >= 1000f ? "0" : "0.#", CultureInfo.InvariantCulture);
    }

    /// <summary>以整数百分比显示归一化比例。</summary>
    public static string FormatPercent(float value)
    {
        return Mathf.Clamp01(value).ToString("P0", CultureInfo.CurrentCulture);
    }

    private static CultibookEntryModel BuildCultibookEntry(
        ActorExtend actor,
        CultibookAsset asset,
        float mastery)
    {
        bool hasAffinity = TryResolveAffinity(actor, asset, out float affinity);
        return new CultibookEntryModel
        {
            Asset = asset,
            CoverPath = ResolveCultibookCoverPath(asset),
            Mastery = Mathf.Clamp(mastery, 0f, 100f),
            HasAffinity = hasAffinity,
            Affinity = affinity
        };
    }

    /// <summary>按功法 ID 稳定选择现有书封，使同一功法在所有刷新中保持相同图标。</summary>
    private static string ResolveCultibookCoverPath(CultibookAsset asset)
    {
        string key = string.IsNullOrEmpty(asset.id) ? asset.Name ?? string.Empty : asset.id;
        unchecked
        {
            uint hash = 2166136261;
            for (var i = 0; i < key.Length; i++)
            {
                hash ^= key[i];
                hash *= 16777619;
            }
            return CultibookCoverPaths[(int)(hash % (uint)CultibookCoverPaths.Length)];
        }
    }

    private static void AppendPractice(CultibookPageModel model, ActorExtend actor)
    {
        if (!actor.TryGetComponent(out CultivationPracticeState state)) return;

        var practices = new List<(string MethodId, string MethodName, float Months)>();
        if (state.methods != null)
        {
            for (var i = 0; i < state.methods.Length; i++)
            {
                CultivationMethodPracticeEntry entry = state.methods[i];
                if (entry.effective_months <= 0f || string.IsNullOrEmpty(entry.method_id)) continue;
                string methodName = Libraries.Manager.CultivateMethodLibrary.dict.TryGetValue(
                    entry.method_id,
                    out CultivateMethodAsset method)
                    ? method.id.Localize()
                    : entry.method_id;
                practices.Add((entry.method_id, methodName, entry.effective_months));
            }
        }

        model.TotalPracticeMonths = practices.Sum(entry => entry.Months);
        foreach ((string _, string methodName, float months) in practices
                 .OrderByDescending(entry => entry.Months)
                 .ThenBy(entry => entry.MethodId, StringComparer.Ordinal))
        {
            model.Practices.Add(new CultivationPracticeEntryModel
            {
                MethodName = methodName,
                EffectiveMonths = months,
                Share = model.TotalPracticeMonths > 0f ? months / model.TotalPracticeMonths : 0f
            });
        }

        if (!state.TryResolveElementExposure(out ElementComposition exposure)) return;
        model.HasElementExposure = true;
        for (var i = 0; i < ElementIndex.Count; i++) model.ElementExposure[i] = exposure[i];
    }

    private static void AppendActorWakan(CultibookPageModel model, ActorExtend actor)
    {
        if (!actor.HasCultisys<Xian>()) return;
        ref Xian xian = ref actor.GetCultisys<Xian>();
        model.Resources.Insert(0, new CultivationResourceEntryModel
        {
            Name = WakanNameKey.Localize(),
            IconPath = WakanIconPath,
            Value = Mathf.Max(0f, xian.wakan),
            Capacity = Mathf.Max(0f, actor.Base.stats[BaseStatses.MaxWakan.id]),
            HasCapacity = true
        });
    }

    private static void AppendContextualResources(
        CultibookPageModel model,
        ActorExtend actor,
        CultivateMethodAsset method)
    {
        var addedIds = new HashSet<string>(StringComparer.Ordinal);
        CultivationResourceAsset[] inputs = method?.ResolveResourceInputs(actor) ??
                                            Array.Empty<CultivationResourceAsset>();
        for (var i = 0; i < inputs.Length; i++)
        {
            CultivationResourceAsset resource = inputs[i];
            if (resource == null || resource.GetAvailable == null || !addedIds.Add(resource.id)) continue;
            AppendResource(model, actor, resource);
        }

        float personalDirtyWakan = CultivationResources.GetPersonalDirtyWakan(actor);
        CultivationResourceAsset personal = CultivationResources.PersonalDirtyWakan;
        if (personalDirtyWakan > 0f && personal != null && addedIds.Add(personal.id))
            AppendResource(model, actor, personal);
    }

    private static void AppendResource(
        CultibookPageModel model,
        ActorExtend actor,
        CultivationResourceAsset resource)
    {
        if ((resource == CultivationResources.WorldWakan || resource == CultivationResources.TileDirtyWakan) &&
            actor.Base.current_tile == null)
            return;

        var context = new CultivationResourceContext(actor);
        float value = Mathf.Max(0f, resource.GetAvailable(in context));
        float capacity = resource.GetCapacity == null
            ? 0f
            : Mathf.Max(0f, resource.GetCapacity(in context));
        model.Resources.Add(new CultivationResourceEntryModel
        {
            Name = string.IsNullOrEmpty(resource.DisplayNameKey)
                ? resource.id
                : resource.DisplayNameKey.Localize(),
            IconPath = resource.IconPath,
            Value = value,
            Capacity = capacity,
            HasCapacity = resource.GetCapacity != null
        });
    }

    private static void AppendKnownCultibooks(
        CultibookPageModel model,
        ActorExtend actor,
        CultibookAsset main)
    {
        IEnumerable<(CultibookAsset Asset, float Mastery)> known = actor.GetAllMaster<CultibookAsset>()
            .Where(entry => entry.Item1 != null && entry.Item2 > 0f && entry.Item1 != main)
            .Select(entry => (entry.Item1, entry.Item2))
            .OrderByDescending(entry => entry.Item2)
            .ThenByDescending(entry => (int)entry.Item1.Level)
            .ThenBy(entry => entry.Item1.Name, StringComparer.CurrentCulture);
        foreach ((CultibookAsset asset, float mastery) in known)
            model.KnownCultibooks.Add(BuildCultibookEntry(actor, asset, mastery));
    }
}
