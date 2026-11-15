using System;
using System.Collections.Generic;
using Cultiway.Content.Components.Skill;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;
using UnityEngine;
using strings;

namespace Cultiway.Content.Skills;

/// <summary>原接触词条只展开为普通状态资料，不保留旧执行实体或运行回调。</summary>
internal static class ContactStatusMigration
{
    /// <summary>已经完整接管的原接触状态词条。</summary>
    /// <param name="type">原容器中的词条类型。</param>
    internal static bool Supports(Type type) => type == typeof(SlowModifier) || type == typeof(FreezeModifier) || type == typeof(DazeModifier) ||
        type == typeof(WeakenModifier) || type == typeof(ArmorBreakModifier) || type == typeof(SilenceModifier);

    /// <summary>核对真实接触回调全部来自已映射词条，额外回调继续原入口。</summary>
    /// <param name="skill">实际原容器。</param>
    internal static bool MatchesCallbacks(Entity skill)
    {
        var expected = new List<Delegate>();
        foreach (Type type in skill.GetComponentTypes())
        {
            if (!Supports(type)) continue;
            var modifier = (IModifier)skill.GetComponent(type);
            if (modifier.ModifierAsset.OnEffectObj != null) expected.AddRange(modifier.ModifierAsset.OnEffectObj.GetInvocationList());
        }
        foreach (Delegate callback in skill.GetComponent<SkillContainer>().OnEffectObj?.GetInvocationList() ?? Array.Empty<Delegate>())
            if (!expected.Remove(callback)) return false;
        return expected.Count == 0;
    }

    /// <summary>在真实攻击面附加状态；飞行中的零伤害爆发载体及纯防御面不提前施加。</summary>
    /// <param name="definition">已经转换的普通构造。</param><param name="skill">原容器。</param>
    /// <param name="effectScale">本次固定效果倍率。</param>
    /// <param name="includeRoot">本体是否已经是实际作用面，爆发载体在后段作用。</param>
    internal static AbilityDefinition Bind(AbilityDefinition definition, Entity skill, float effectScale, bool includeRoot = true)
    {
        var statuses = new List<ConstructionStatus>();
        if (skill.TryGetComponent(out SlowModifier slow))
        {
            float duration = Mathf.Clamp(slow.Duration, 0f, 999f), strength = Mathf.Clamp01(slow.Strength * effectScale);
            if (duration > 0f && strength > 0f) statuses.Add(new ConstructionStatus
            {
                Id = StatusEffects.Slow.id, Duration = duration, Application = ConstructionStatusApplication.Stack,
                Stats = new Dictionary<string, float> { [S.multiplier_speed] = -strength }
            });
        }
        if (skill.TryGetComponent(out FreezeModifier freeze))
        {
            float duration = Mathf.Clamp(freeze.Duration * effectScale, 0f, 999f);
            if (duration > 0f) statuses.Add(new ConstructionStatus
            {
                Id = StatusEffects.Freeze.id, Duration = duration, Application = ConstructionStatusApplication.Stack
            });
        }
        if (skill.TryGetComponent(out DazeModifier daze))
        {
            float duration = Mathf.Clamp(daze.Duration * effectScale, 0f, 10f);
            if (duration > 0f) statuses.Add(new ConstructionStatus
            {
                Id = StatusEffects.Daze.id, Duration = duration, Application = ConstructionStatusApplication.Refresh, WaitForDuration = true
            });
        }
        if (skill.TryGetComponent(out WeakenModifier weaken))
            AddReduction(statuses, StatusEffects.Weaken.id, weaken.Duration, S.damage, weaken.AttackReduction * effectScale);
        if (skill.TryGetComponent(out ArmorBreakModifier armorBreak))
            AddReduction(statuses, StatusEffects.ArmorBreak.id, armorBreak.Duration, S.armor, armorBreak.ArmorReduction * effectScale);
        if (skill.TryGetComponent(out SilenceModifier silence))
            AddReduction(statuses, StatusEffects.Silence.id, silence.Duration, S.damage, silence.DamageReduction * effectScale);
        if (statuses.Count > 0) BindNode(definition.Root, statuses.ToArray(), includeRoot);
        return definition;
    }

    /// <summary>比例削弱在命中时取目标属性，同类刷新并沿用状态模板中的控制标记。</summary>
    /// <param name="statuses">本次接触声明。</param><param name="id">共享状态编号。</param>
    /// <param name="duration">原持续世界秒。</param><param name="stat">受影响的同名属性。</param>
    /// <param name="reduction">已经包含本次效果倍率的削弱比例。</param>
    private static void AddReduction(List<ConstructionStatus> statuses, string id, float duration, string stat, float reduction)
    {
        duration = Mathf.Clamp(duration, 0f, 999f);
        reduction = Mathf.Clamp01(reduction);
        if (duration <= 0f || reduction <= 0f) return;
        statuses.Add(new ConstructionStatus
        {
            Id = id, Duration = duration, Application = ConstructionStatusApplication.Refresh,
            TargetStatRatios = new Dictionary<string, float> { [stat] = -reduction }
        });
    }

    /// <summary>复用原接触关系、窗口和次数，不另造扩大作用区。</summary>
    /// <param name="node">实际部位。</param><param name="statuses">本次待编译的状态资料。</param>
    /// <param name="include">是否在本部位的接触时点施加。</param>
    private static void BindNode(ConstructionNode node, ConstructionStatus[] statuses, bool include = true)
    {
        if (include && (node.ContactRole == ConstructionContactRole.Attack || node.ContactRole == ConstructionContactRole.Barrier && node.Impact != null))
        {
            if (node.Impact == null) node.Impact = new ConstructionImpact();
            node.Impact.Statuses = statuses;
        }
        if (node.Children == null) return;
        foreach (ConstructionNode child in node.Children) BindNode(child, statuses);
    }
}
