using System;
using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Utils;
using Friflo.Engine.ECS;

namespace Cultiway.Content.CreatureCompositions.ActiveAbilities;

/// <summary>
///     器官引用的技能容器登记处。每个被器官等级引用的技能资产只在内容初始化后建立一次容器，
///     全部生物单位长期共用，不随出生或检查重复创建。
/// </summary>
public static class CreatureOrganSkillRegistry
{
    private static readonly Dictionary<string, Entity> containersBySkillId = new(StringComparer.Ordinal);
    private static bool built;

    /// <summary>按技能资产编号读取长期持有的来源技能容器。</summary>
    public static bool TryGetContainer(string skillEntityAssetId, out Entity container)
    {
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
            .Build(SkillContainerBuildMode.SourceGranted);
    }

    /// <summary>扫描全部器官等级引用并补建缺失的容器；重复调用只补差额。</summary>
    public static void EnsureBuilt()
    {
        if (built && containersBySkillId.Count > 0) return;
        built = true;

        foreach (Content.CreatureCompositions.Libraries.CreatureOrganRankAsset rank in Content.Libraries.Manager.CreatureOrganRankLibrary.getArray())
        {
            string[] skillIds = rank.SkillContainerIds ?? Array.Empty<string>();
            for (int i = 0; i < skillIds.Length; i++)
            {
                string skillId = skillIds[i];
                if (string.IsNullOrWhiteSpace(skillId) || containersBySkillId.ContainsKey(skillId)) continue;

                SkillEntityAsset asset = ModClass.I.SkillV3.SkillLib.get(skillId);
                if (asset == null)
                {
                    ModClass.LogWarning($"器官等级 {rank.id} 引用了不存在的技能资产 {skillId}");
                    continue;
                }

                containersBySkillId[skillId] = new SkillContainerBuilder(asset)
                    .Build(SkillContainerBuildMode.SourceGranted);
            }
        }
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
