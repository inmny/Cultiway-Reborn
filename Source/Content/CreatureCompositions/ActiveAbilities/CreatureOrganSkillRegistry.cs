using System;
using System.Collections.Generic;
using Cultiway.Content.YaoBeasts;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Utils;
using Friflo.Engine.ECS;

namespace Cultiway.Content.CreatureCompositions.ActiveAbilities;

/// <summary>
///     器官和妖丹共用的技能容器登记处。每个世界只为每个技能建立一个容器。
/// </summary>
public static class CreatureOrganSkillRegistry
{
    private static readonly Dictionary<string, Entity> containersBySkillId = new(StringComparer.Ordinal);
    private static bool built;

    /// <summary>按技能资产编号读取长期持有的来源技能容器。</summary>
    public static bool TryGetContainer(string skillEntityAssetId, out Entity container)
    {
        EnsureBuilt();
        return containersBySkillId.TryGetValue(skillEntityAssetId, out container);
    }

    /// <summary>为单个技能资产补建容器；已登记时直接返回。</summary>
    public static void RegisterSkill(string skillEntityAssetId)
    {
        if (string.IsNullOrWhiteSpace(skillEntityAssetId) || containersBySkillId.ContainsKey(skillEntityAssetId))
            return;

        SkillEntityAsset asset = ModClass.I.SkillV3.SkillLib.get(skillEntityAssetId);
        if (asset == null)
        {
            ModClass.LogWarning($"器官或妖丹引用了不存在的技能资产 {skillEntityAssetId}");
            return;
        }

        containersBySkillId[skillEntityAssetId] = new SkillContainerBuilder(asset)
            .UseCastResources(SkillCastResourceRequirement.Single(Content.SkillCastResources.YaoPower))
            .Build(SkillContainerBuildMode.SourceGranted);
    }

    /// <summary>在首次使用或恢复世界时登记器官和妖丹引用的技能。</summary>
    public static void EnsureBuilt()
    {
        if (built) return;

        foreach (var rank in Content.Libraries.Manager.CreatureOrganRankLibrary.getArray())
            foreach (string skillId in rank.SkillContainerIds ?? Array.Empty<string>())
                RegisterSkill(skillId);
        foreach (YaoCorePatternAsset pattern in YaoCorePatterns.All)
            foreach (string skillId in pattern.SkillIds)
                RegisterSkill(skillId);

        built = true;
    }

    /// <summary>清理世界时释放全部来源容器；静态技能资产保持不变。</summary>
    public static void ClearWorldState()
    {
        foreach (Entity container in containersBySkillId.Values)
        {
            if (!container.IsNull) ModClass.I.CommandBuffer.AddTag<TagRecycle>(container.Id);
        }

        containersBySkillId.Clear();
        built = false;
    }
}
