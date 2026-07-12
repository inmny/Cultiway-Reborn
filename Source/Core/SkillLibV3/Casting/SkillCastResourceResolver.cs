using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3;

/// <summary>
/// 解析并缓存施法者到法术资源通道的绑定。失败结果不缓存，使新获得的体系能立即生效。
/// </summary>
public static class SkillCastResourceResolver
{
    private const int CacheCapacity = 4096;
    private static readonly Dictionary<BindingCacheKey, BindingCacheEntry> Cache = new();

    public static SkillCastResourceBinding Resolve(ActorExtend caster, Entity skill)
    {
        if (caster == null || caster.Base.isRekt()) return null;
        if (skill.IsNull || !skill.HasComponent<SkillContainer>()) return null;

        var requirement = skill.GetComponent<SkillContainer>().CastResourceRequirement;

        var key = new BindingCacheKey(caster.E.Id, skill.Id);
        var signature = BuildSignature(requirement);
        if (Cache.TryGetValue(key, out var cached) && cached.RequirementSignature == signature &&
            IsBindingAvailable(caster, cached.Binding))
        {
            return cached.Binding;
        }

        var binding = ResolveUncached(caster, requirement);
        if (binding == null) return null;

        if (Cache.Count >= CacheCapacity) Cache.Clear();
        Cache[key] = new BindingCacheEntry(signature, binding);
        return binding;
    }

    /// <summary>
    /// 判断技能是否声明使用指定施法资源，不要求施法者当前能够提供该资源。
    /// </summary>
    public static bool UsesResource(Entity skill, SkillCastResourceAsset resource)
    {
        if (resource == null || skill.IsNull || !skill.HasComponent<SkillContainer>()) return false;

        var requirement = skill.GetComponent<SkillContainer>().CastResourceRequirement;
        return requirement?.ResourceAssetIds?.Contains(resource.id, StringComparer.Ordinal) ?? false;
    }

    public static void Invalidate(ActorExtend caster)
    {
        if (caster == null) return;
        var actorId = caster.E.Id;
        foreach (var key in Cache.Keys.Where(key => key.ActorEntityId == actorId).ToArray())
        {
            Cache.Remove(key);
        }
    }

    public static void Invalidate(Entity skill)
    {
        if (skill.IsNull) return;
        var skillId = skill.Id;
        foreach (var key in Cache.Keys.Where(key => key.SkillEntityId == skillId).ToArray())
        {
            Cache.Remove(key);
        }
    }

    private static SkillCastResourceBinding ResolveUncached(ActorExtend caster,
        SkillCastResourceRequirement requirement)
    {
        var resources = new List<SkillCastResourceAsset>();
        foreach (var resourceId in requirement.ResourceAssetIds)
        {
            var resource = ModClass.I.SkillV3.CastResourceLib.get(resourceId);
            if (resource == null || !resource.IsAvailable(caster))
            {
                if (requirement.Mode == SkillCastResourceRequirementMode.AllOf) return null;
                continue;
            }

            resources.Add(resource);
            if (requirement.Mode == SkillCastResourceRequirementMode.Single) break;
        }

        if (resources.Count == 0) return null;
        return new SkillCastResourceBinding(requirement.Mode, resources);
    }

    private static bool IsBindingAvailable(ActorExtend caster, SkillCastResourceBinding binding)
    {
        return binding.Resources.All(resource => resource.IsAvailable(caster));
    }

    private static string BuildSignature(SkillCastResourceRequirement requirement)
    {
        return $"{(int)requirement.Mode}:{string.Join("|", requirement.ResourceAssetIds)}";
    }

    private readonly struct BindingCacheKey : IEquatable<BindingCacheKey>
    {
        public readonly int ActorEntityId;
        public readonly int SkillEntityId;

        public BindingCacheKey(int actorEntityId, int skillEntityId)
        {
            ActorEntityId = actorEntityId;
            SkillEntityId = skillEntityId;
        }

        public bool Equals(BindingCacheKey other)
        {
            return ActorEntityId == other.ActorEntityId && SkillEntityId == other.SkillEntityId;
        }

        public override bool Equals(object obj)
        {
            return obj is BindingCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (ActorEntityId * 397) ^ SkillEntityId;
            }
        }
    }

    private sealed class BindingCacheEntry
    {
        public readonly string RequirementSignature;
        public readonly SkillCastResourceBinding Binding;

        public BindingCacheEntry(string requirementSignature, SkillCastResourceBinding binding)
        {
            RequirementSignature = requirementSignature;
            Binding = binding;
        }
    }
}
