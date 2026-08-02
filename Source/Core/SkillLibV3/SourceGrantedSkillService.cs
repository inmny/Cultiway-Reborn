using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3;

/// <summary>来源授予技能在人物技能页中的只读展示数据。</summary>
public readonly struct SourceGrantedSkillPresentation
{
    /// <summary>由来源长期持有的只读技能容器。</summary>
    public readonly Entity SkillContainer;

    /// <summary>显示在技能名称下方的本地化详情键。</summary>
    public readonly string DetailLocaleKey;

    /// <summary>创建一项来源授予技能展示。</summary>
    public SourceGrantedSkillPresentation(Entity skillContainer, string detailLocaleKey)
    {
        SkillContainer = skillContainer;
        DetailLocaleKey = detailLocaleKey ?? string.Empty;
    }
}

/// <summary>按角色实时公开体系、装备或其他来源授予技能的提供器。</summary>
public interface ISourceGrantedSkillProvider
{
    /// <summary>提供器的稳定唯一 ID。</summary>
    string Id { get; }

    /// <summary>向结果集合追加角色当前可用的来源授予技能。</summary>
    void Collect(ActorExtend actor, ICollection<SourceGrantedSkillPresentation> output);
}

/// <summary>
/// 汇总角色当前由外部来源直接授予的技能。服务只负责发现和展示，不接管技能容器生命周期。
/// </summary>
public static class SourceGrantedSkillService
{
    private static readonly List<ISourceGrantedSkillProvider> Providers = new();
    private static readonly HashSet<string> ProviderIds = new(StringComparer.Ordinal);

    /// <summary>注册一个来源授予技能提供器；重复 ID 视为配置错误。</summary>
    public static void Register(ISourceGrantedSkillProvider provider)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        if (string.IsNullOrWhiteSpace(provider.Id))
            throw new ArgumentException("来源授予技能 Provider 缺少 ID", nameof(provider));
        if (!ProviderIds.Add(provider.Id))
            throw new InvalidOperationException($"来源授予技能 Provider 重复注册: {provider.Id}");
        Providers.Add(provider);
    }

    /// <summary>枚举角色当前全部来源授予技能。</summary>
    public static void Collect(ActorExtend actor, ICollection<SourceGrantedSkillPresentation> output)
    {
        output.Clear();
        if (actor == null || actor.Base.isRekt()) return;
        for (var i = 0; i < Providers.Count; i++) Providers[i].Collect(actor, output);
    }

    /// <summary>判断角色当前是否至少有一项来源授予技能。</summary>
    public static bool HasAny(ActorExtend actor)
    {
        if (actor == null || actor.Base.isRekt()) return false;
        var entries = new List<SourceGrantedSkillPresentation>();
        for (var i = 0; i < Providers.Count; i++)
        {
            Providers[i].Collect(actor, entries);
            if (entries.Count > 0) return true;
        }
        return false;
    }
}
