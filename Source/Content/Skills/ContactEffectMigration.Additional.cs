using System.Collections.Generic;
using Cultiway.Content.Components.Skill;
using Cultiway.Core;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using UnityEngine;
using strings;

namespace Cultiway.Content.Skills;

internal static partial class ContactEffectMigration
{
    /// <summary>原随机附加展开为六份普通结果，概率不乘效果倍率，运行只抽取候选。</summary>
    /// <param name="skill">真实旧容器。</param><param name="effectScale">本次效果倍率。</param>
    /// <param name="forceScale">本体量换回旧附加作用强度的比例。</param>
    private static ConstructionRandomContact RandomContact(Entity skill, float effectScale, float forceScale)
    {
        if (!skill.TryGetComponent(out RandomAffixModifier random) || random.Chance <= 0f) return null;
        float power = Mathf.Clamp(random.EffectPower, .1f, 20f);
        var daze = new List<ConstructionStatus>();
        float duration = (.25f + power * .15f) * effectScale;
        if (duration > 0f) daze.Add(new ConstructionStatus
        {
            Id = StatusEffects.Daze.id, Duration = duration,
            Application = ConstructionStatusApplication.Refresh, WaitForDuration = true
        });
        var burn = new List<ConstructionStatus>();
        AddTick(burn, StatusEffects.Burn.id, 2f + power, (.08f + power * .03f) * forceScale, ElementComposition.Static.Fire);
        var poison = new List<ConstructionStatus>();
        AddTick(poison, StatusEffects.Poison.id, 3f + power, (.06f + power * .025f) * forceScale, ElementComposition.Static.Poison);
        var armor = new List<ConstructionStatus>();
        AddReduction(armor, StatusEffects.ArmorBreak.id, 2f + power * .4f, S.armor, (.08f + power * .03f) * effectScale);
        return new ConstructionRandomContact
        {
            Chance = Mathf.Clamp01(random.Chance),
            Outcomes = new[]
            {
                new ConstructionContactOutcome { Statuses = daze.ToArray() },
                new ConstructionContactOutcome { Statuses = burn.ToArray() },
                new ConstructionContactOutcome { Statuses = poison.ToArray() },
                new ConstructionContactOutcome { Damage = new[] { new ConstructionContactDamage
                {
                    PerForce = (.12f + power * .04f) * forceScale,
                    Element = skill.GetComponent<SkillContainer>().Asset.Element
                } } },
                new ConstructionContactOutcome { Statuses = armor.ToArray() },
                new ConstructionContactOutcome { Knockback = new ConstructionKnockback
                {
                    Distance = Mathf.Clamp((1f + power * .4f) * effectScale, 0f, 10f),
                    Height = Mathf.Clamp((.4f + power * .2f) * effectScale, 0f, 5f)
                } }
            }
        };
    }

    /// <summary>原燃尽展开为同次追加火伤和已有共享燃烧，不创建额外爆炸作用区。</summary>
    /// <param name="skill">原容器。</param><param name="forceScale">本体量换回旧附加强度的比例。</param>
    /// <param name="statuses">本次普通状态声明。</param><param name="damage">本次追加伤害声明。</param>
    private static void AddBurnout(Entity skill, float forceScale, List<ConstructionStatus> statuses,
        List<ConstructionContactDamage> damage)
    {
        if (!skill.TryGetComponent(out BurnoutModifier burnout)) return;
        float direct = Mathf.Clamp(burnout.DamageRatio, 0f, 10f) * forceScale;
        if (direct > 0f) damage.Add(new ConstructionContactDamage { PerForce = direct, Element = ElementComposition.Static.Fire });
        AddTick(statuses, StatusEffects.Burn.id, burnout.BurnDuration,
            Mathf.Clamp(burnout.BurnDamageRatio, 0f, 10f) * forceScale, ElementComposition.Static.Fire);
    }
}
