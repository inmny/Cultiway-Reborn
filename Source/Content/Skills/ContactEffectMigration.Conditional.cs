using System.Collections.Generic;
using Cultiway.Content.Components.Skill;
using Cultiway.Core;
using Cultiway.Core.Skills;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Skills;

internal static partial class ContactEffectMigration
{
    /// <summary>原特殊命中展开为有限条件和普通作用，不保留按招式分派的执行器。</summary>
    /// <param name="skill">真实原容器。</param><param name="effectScale">本次固定效果倍率。</param>
    /// <param name="forceScale">本体量换回原附加强度的比例。</param><param name="statuses">原接触标记。</param>
    private static ConstructionConditionalContact[] ConditionalContacts(Entity skill, float effectScale, float forceScale, List<ConstructionStatus> statuses)
    {
        var rules = new List<ConstructionConditionalContact>();
        if (skill.TryGetComponent(out DeathSentenceModifier sentence))
        {
            if (effectScale > 0f) statuses.Add(new ConstructionStatus
            {
                Id = StatusEffects.DeathSentence.id, Duration = 3f * effectScale, Application = ConstructionStatusApplication.Refresh
            });
            rules.Add(new ConstructionConditionalContact
            {
                MaximumHealthRatio = Mathf.Clamp01(sentence.ExecuteHealthRatio),
                Outcome = new ConstructionContactOutcome { Damage = new[] { new ConstructionContactDamage
                {
                    PerHealth = effectScale, PerMaxHealth = .02f * effectScale, MinimumBonus = effectScale,
                    IgnoreDamageReduction = true, Element = new ElementComposition(entropy: 1f)
                } } },
                Otherwise = new ConstructionContactOutcome { Damage = new[] { new ConstructionContactDamage
                {
                    PerForce = Mathf.Clamp(sentence.BonusDamageRatio, 0f, 20f) * forceScale,
                    Element = new ElementComposition(entropy: 1f)
                } } }
            });
        }
        if (skill.TryGetComponent(out ReincarnationTrialModifier trial))
        {
            float damage = Mathf.Clamp(trial.DamageRatio, 0f, 20f) * forceScale;
            if (damage > 0f) rules.Add(new ConstructionConditionalContact
            {
                Outcome = new ConstructionContactOutcome { Damage = new[] { new ConstructionContactDamage
                {
                    PerForce = damage, Element = new ElementComposition(pos: .35f, neg: .35f, entropy: .3f)
                } } },
                OnKill = new ConstructionContactOutcome { Restoration = new ConstructionRestoration
                {
                    Target = ConstructionRestorationTarget.Controller,
                    HealthPerForce = damage * Mathf.Clamp(trial.HealRatio, 0f, 10f)
                } },
                OnSurvive = new ConstructionContactOutcome { Damage = new[] { new ConstructionContactDamage
                {
                    Target = ConstructionContactActor.Controller, Source = ConstructionContactActor.Contact,
                    PerForce = damage * Mathf.Clamp(trial.BacklashRatio, 0f, 10f),
                    Element = new ElementComposition(pos: .35f, neg: .35f, entropy: .3f)
                } } }
            });
        }
        return rules.ToArray();
    }
}
