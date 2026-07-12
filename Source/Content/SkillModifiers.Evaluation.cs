using Cultiway.Content.Components.Skill;
using Cultiway.Core.SkillLibV3;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content;

public partial class SkillModifiers
{
    private static void ConfigureEvaluations()
    {
        Placeholder.EvaluateLevel = SkillEvaluationActions.None;
        Slow.EvaluateLevel = EvaluateSlow;
        Burn.EvaluateLevel = EvaluateBurn;
        Freeze.EvaluateLevel = EvaluateFreeze;
        Poison.EvaluateLevel = EvaluatePoison;
        Explosion.EvaluateLevel = EvaluateExplosion;
        Haste.EvaluateLevel = EvaluateHaste;
        Proficiency.EvaluateLevel = SkillEvaluationActions.None;
        Empower.EvaluateLevel = EvaluateEmpower;
        Knockback.EvaluateLevel = EvaluateKnockback;
        Volley.EvaluateLevel = EvaluateVolley;
        Huge.EvaluateLevel = EvaluateHuge;
        Weaken.EvaluateLevel = EvaluateWeaken;
        ArmorBreak.EvaluateLevel = EvaluateArmorBreak;
        Gravity.EvaluateLevel = EvaluateGravity;
        Daze.EvaluateLevel = EvaluateDaze;
        Mercy.EvaluateLevel = EvaluateMercy;
        Chaos.EvaluateLevel = EvaluateChaos;
        Swap.EvaluateLevel = EvaluateSwap;
        RandomAffix.EvaluateLevel = EvaluateRandomAffix;
        Burnout.EvaluateLevel = EvaluateBurnout;
        Combo.EvaluateLevel = EvaluateCombo;
        Silence.EvaluateLevel = EvaluateSilence;
        DeathSentence.EvaluateLevel = EvaluateDeathSentence;
        ReincarnationTrial.EvaluateLevel = EvaluateReincarnationTrial;
        EternalCurse.EvaluateLevel = EvaluateEternalCurse;
    }

    private static void EvaluateSlow(Entity container, ref SkillEvaluationContext context)
    {
        var modifier = container.GetComponent<SlowModifier>();
        context.AddControl(modifier.Duration * modifier.Strength * 0.2f);
    }

    private static void EvaluateBurn(Entity container, ref SkillEvaluationContext context)
    {
        context.AddAdditionalPower(container.GetComponent<BurnModifier>().DamageRatio);
    }

    private static void EvaluateFreeze(Entity container, ref SkillEvaluationContext context)
    {
        context.AddControl(container.GetComponent<FreezeModifier>().Duration * 0.35f);
    }

    private static void EvaluatePoison(Entity container, ref SkillEvaluationContext context)
    {
        var modifier = container.GetComponent<PoisonModifier>();
        context.AddAdditionalPower(modifier.DamageRatio * Mathf.Sqrt(Mathf.Max(1, modifier.MaxStacks)));
    }

    private static void EvaluateExplosion(Entity container, ref SkillEvaluationContext context)
    {
        var modifier = container.GetComponent<ExplosionModifier>();
        context.AddAdditionalPower(modifier.DamageRatio);
        context.AtLeastExpectedTargets(1f + modifier.Radius);
    }

    private static void EvaluateHaste(Entity container, ref SkillEvaluationContext context)
    {
        var modifier = container.GetComponent<HasteModifier>();
        context.AddUtility((modifier.SpeedMultiplier - 1f) * 0.2f);
    }

    private static void EvaluateEmpower(Entity container, ref SkillEvaluationContext context)
    {
        var modifier = container.GetComponent<EmpowerModifier>();
        context.MultiplyDirectPower(1f + modifier.SetupBonus);
    }

    private static void EvaluateKnockback(Entity container, ref SkillEvaluationContext context)
    {
        var modifier = container.GetComponent<KnockbackModifier>();
        context.AddControl((modifier.Distance + modifier.Height) * 0.1f);
    }

    private static void EvaluateVolley(Entity container, ref SkillEvaluationContext context)
    {
        context.MultiplyDirectPower(container.GetComponent<VolleyModifier>().DamageMultiplier);
    }

    private static void EvaluateHuge(Entity container, ref SkillEvaluationContext context)
    {
        var modifier = container.GetComponent<HugeModifier>();
        context.MultiplyExpectedTargets(Mathf.Sqrt(Mathf.Max(1f, modifier.Value)));
    }

    private static void EvaluateWeaken(Entity container, ref SkillEvaluationContext context)
    {
        var modifier = container.GetComponent<WeakenModifier>();
        context.AddControl(modifier.Duration * modifier.AttackReduction * 0.25f);
    }

    private static void EvaluateArmorBreak(Entity container, ref SkillEvaluationContext context)
    {
        var modifier = container.GetComponent<ArmorBreakModifier>();
        context.AddControl(modifier.Duration * modifier.ArmorReduction * 0.25f);
    }

    private static void EvaluateGravity(Entity container, ref SkillEvaluationContext context)
    {
        var modifier = container.GetComponent<GravityModifier>();
        context.AddControl(Mathf.Sqrt(Mathf.Max(0f, modifier.Radius * modifier.Strength)) * 0.3f);
    }

    private static void EvaluateDaze(Entity container, ref SkillEvaluationContext context)
    {
        context.AddControl(container.GetComponent<DazeModifier>().Duration * 0.45f);
    }

    private static void EvaluateMercy(Entity container, ref SkillEvaluationContext context)
    {
        var modifier = container.GetComponent<MercyModifier>();
        context.MultiplyDirectPower(modifier.DamageMultiplier);
        context.AddUtility(modifier.HealRatio);
    }

    private static void EvaluateChaos(Entity container, ref SkillEvaluationContext context)
    {
        var modifier = container.GetComponent<ChaosModifier>();
        context.AddUtility((modifier.DamageVariance + modifier.AngleVariance / 180f + modifier.SpeedVariance) *
                           0.1f);
    }

    private static void EvaluateSwap(Entity container, ref SkillEvaluationContext context)
    {
        context.AddUtility(container.GetComponent<SwapModifier>().Chance);
    }

    private static void EvaluateRandomAffix(Entity container, ref SkillEvaluationContext context)
    {
        var modifier = container.GetComponent<RandomAffixModifier>();
        context.AddUtility(modifier.Chance * modifier.EffectPower * 0.3f);
    }

    private static void EvaluateBurnout(Entity container, ref SkillEvaluationContext context)
    {
        var modifier = container.GetComponent<BurnoutModifier>();
        context.AddAdditionalPower(modifier.DamageRatio + modifier.BurnDamageRatio);
    }

    private static void EvaluateCombo(Entity container, ref SkillEvaluationContext context)
    {
        context.MultiplyDirectPower(container.GetComponent<ComboModifier>().DamageMultiplier);
    }

    private static void EvaluateSilence(Entity container, ref SkillEvaluationContext context)
    {
        var modifier = container.GetComponent<SilenceModifier>();
        context.AddControl(modifier.Duration * (1f + modifier.DamageReduction) * 0.35f);
    }

    private static void EvaluateDeathSentence(Entity container, ref SkillEvaluationContext context)
    {
        var modifier = container.GetComponent<DeathSentenceModifier>();
        context.AddAdditionalPower(modifier.BonusDamageRatio + modifier.ExecuteHealthRatio * 2f);
    }

    private static void EvaluateReincarnationTrial(Entity container, ref SkillEvaluationContext context)
    {
        var modifier = container.GetComponent<ReincarnationTrialModifier>();
        context.AddAdditionalPower(modifier.DamageRatio - modifier.BacklashRatio * 0.5f);
        context.AddUtility(modifier.HealRatio);
    }

    private static void EvaluateEternalCurse(Entity container, ref SkillEvaluationContext context)
    {
        var modifier = container.GetComponent<EternalCurseModifier>();
        context.AddAdditionalPower(modifier.DamageRatio);
        context.AddControl(modifier.DebuffRatio * modifier.Duration * 0.25f);
    }
}
