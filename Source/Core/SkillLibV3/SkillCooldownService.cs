using System;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

/// <summary>按角色和具体技能容器管理通用冷却。</summary>
public static class SkillCooldownService
{
    /// <summary>判断指定技能当前是否已经结束冷却。</summary>
    public static bool IsReady(ActorExtend actor, Entity skillContainer)
    {
        return GetRemaining(actor, skillContainer) <= 0f;
    }

    /// <summary>取得指定技能的剩余冷却秒数。</summary>
    public static float GetRemaining(ActorExtend actor, Entity skillContainer)
    {
        long skillContainerPid = ResolveSkillContainerPid(skillContainer);
        if (actor == null || skillContainerPid == 0 ||
            !actor.E.TryGetComponent(out SkillCooldownRuntime runtime)) return 0f;
        int index = runtime.FindIndex(skillContainerPid);
        return index < 0 ? 0f : Mathf.Max(0f, runtime.Entries[index].Remaining);
    }

    /// <summary>从当前值和新值中取较长者，启动或延长指定技能冷却。</summary>
    public static void Start(ActorExtend actor, Entity skillContainer, float duration)
    {
        long skillContainerPid = ResolveSkillContainerPid(skillContainer);
        duration = Mathf.Max(0f, duration);
        if (actor == null || skillContainerPid == 0 || duration <= 0f) return;

        SkillCooldownRuntime runtime = actor.E.TryGetComponent(out SkillCooldownRuntime existing)
            ? existing
            : new SkillCooldownRuntime { Entries = Array.Empty<SkillCooldownEntry>() };
        int index = runtime.FindIndex(skillContainerPid);
        if (index >= 0)
        {
            runtime.Entries[index].Remaining = Mathf.Max(runtime.Entries[index].Remaining, duration);
        }
        else
        {
            int length = runtime.Entries?.Length ?? 0;
            Array.Resize(ref runtime.Entries, length + 1);
            runtime.Entries[length] = new SkillCooldownEntry
            {
                SkillContainerPid = skillContainerPid,
                Remaining = duration
            };
        }

        if (actor.E.HasComponent<SkillCooldownRuntime>())
            actor.E.GetComponent<SkillCooldownRuntime>() = runtime;
        else
            actor.E.AddComponent(runtime);
    }

    /// <summary>清除指定技能的冷却；冷却表保持原有容量，避免频繁结构变更。</summary>
    public static void Clear(ActorExtend actor, Entity skillContainer)
    {
        long skillContainerPid = ResolveSkillContainerPid(skillContainer);
        if (actor == null || skillContainerPid == 0 ||
            !actor.E.TryGetComponent(out SkillCooldownRuntime runtime)) return;
        int index = runtime.FindIndex(skillContainerPid);
        if (index < 0) return;
        runtime.Entries[index].Remaining = 0f;
        actor.E.GetComponent<SkillCooldownRuntime>() = runtime;
    }

    /// <summary>清除角色当前全部通用技能冷却。</summary>
    public static void ClearAll(ActorExtend actor)
    {
        if (actor == null || !actor.E.TryGetComponent(out SkillCooldownRuntime runtime) ||
            runtime.Entries == null) return;
        for (var i = 0; i < runtime.Entries.Length; i++) runtime.Entries[i].Remaining = 0f;
        actor.E.GetComponent<SkillCooldownRuntime>() = runtime;
    }

    /// <summary>从技能容器解析不会因普通实体 ID 复用而冲突的持久实体 ID。</summary>
    private static long ResolveSkillContainerPid(Entity skillContainer)
    {
        return skillContainer.IsNull || !skillContainer.HasComponent<Components.SkillContainer>()
            ? 0
            : skillContainer.Pid;
    }
}
