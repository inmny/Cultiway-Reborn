using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Systems;

/// <summary>按模拟时间推进所有角色的通用技能冷却。</summary>
public sealed class SkillCooldownSystem : QuerySystem<SkillCooldownRuntime>
{
    /// <summary>忽略预制体、失活和待回收实体。</summary>
    public SkillCooldownSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());
    }

    /// <summary>在原数组中递减非零冷却，不产生每帧分配或结构变更。</summary>
    protected override void OnUpdate()
    {
        float deltaTime = Tick.deltaTime;
        Query.ForEachEntity((ref SkillCooldownRuntime runtime, Entity _) =>
        {
            if (runtime.Entries == null) return;
            for (var i = 0; i < runtime.Entries.Length; i++)
            {
                runtime.Entries[i].Remaining =
                    Mathf.Max(0f, runtime.Entries[i].Remaining - deltaTime);
            }
        });
    }
}
