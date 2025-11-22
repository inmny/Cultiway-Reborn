using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Content.Components.Skill;

namespace Cultiway.Content;

public class SkillModifiers : ExtendLibrary<SkillModifierAsset, SkillModifiers>
{
    private const string KillOverrideTag = "kill_override";

    [AssetId(PlaceholderModifier.PlaceholderAssetId)]
    public static SkillModifierAsset Placeholder { get; private set; }

    public static SkillModifierAsset Slow { get; private set; }
    public static SkillModifierAsset Burn { get; private set; }
    public static SkillModifierAsset Freeze { get; private set; }
    public static SkillModifierAsset Poison { get; private set; }
    public static SkillModifierAsset Explosion { get; private set; }
    public static SkillModifierAsset Haste { get; private set; }
    public static SkillModifierAsset Proficiency { get; private set; }
    public static SkillModifierAsset Empower { get; private set; }
    public static SkillModifierAsset Knockback { get; private set; }
    public static SkillModifierAsset Volley { get; private set; }

    public static SkillModifierAsset LockOn { get; private set; }
    public static SkillModifierAsset Huge { get; private set; }
    public static SkillModifierAsset Weaken { get; private set; }
    public static SkillModifierAsset ArmorBreak { get; private set; }
    public static SkillModifierAsset Gravity { get; private set; }
    public static SkillModifierAsset Daze { get; private set; }

    public static SkillModifierAsset Mercy { get; private set; }
    public static SkillModifierAsset Chaos { get; private set; }
    public static SkillModifierAsset Swap { get; private set; }
    public static SkillModifierAsset RandomAffix { get; private set; }
    public static SkillModifierAsset Burnout { get; private set; }
    public static SkillModifierAsset Combo { get; private set; }

    public static SkillModifierAsset Silence { get; private set; }
    public static SkillModifierAsset DeathSentence { get; private set; }
    public static SkillModifierAsset ReincarnationTrial { get; private set; }
    public static SkillModifierAsset EternalCurse { get; private set; }

    protected override void OnInit()
    {
        RegisterAssets();

        Setup<PlaceholderModifier>(Placeholder, SkillModifierRarity.Common);

        Setup<SlowModifier>(Slow, SkillModifierRarity.Common);
        Setup<BurnModifier>(Burn, SkillModifierRarity.Common);
        Setup<FreezeModifier>(Freeze, SkillModifierRarity.Common);
        Setup<PoisonModifier>(Poison, SkillModifierRarity.Common);
        Setup<ExplosionModifier>(Explosion, SkillModifierRarity.Common);
        Setup<HasteModifier>(Haste, SkillModifierRarity.Common);
        Setup<ProficiencyModifier>(Proficiency, SkillModifierRarity.Common);
        Setup<EmpowerModifier>(Empower, SkillModifierRarity.Common);
        Setup<KnockbackModifier>(Knockback, SkillModifierRarity.Common);

        Setup<LockOnModifier>(LockOn, SkillModifierRarity.Rare);
        Setup<HugeModifier>(Huge, SkillModifierRarity.Rare);
        Setup<WeakenModifier>(Weaken, SkillModifierRarity.Rare);
        Setup<ArmorBreakModifier>(ArmorBreak, SkillModifierRarity.Rare);
        Setup<GravityModifier>(Gravity, SkillModifierRarity.Rare);
        Setup<DazeModifier>(Daze, SkillModifierRarity.Rare);

        Setup<MercyModifier>(Mercy, SkillModifierRarity.Epic, KillOverrideTag);
        Setup<ChaosModifier>(Chaos, SkillModifierRarity.Epic);
        Setup<SwapModifier>(Swap, SkillModifierRarity.Epic);
        Setup<RandomAffixModifier>(RandomAffix, SkillModifierRarity.Epic);
        Setup<BurnoutModifier>(Burnout, SkillModifierRarity.Epic);
        Setup<ComboModifier>(Combo, SkillModifierRarity.Epic);

        Setup<SilenceModifier>(Silence, SkillModifierRarity.Legendary);
        Setup<DeathSentenceModifier>(DeathSentence, SkillModifierRarity.Legendary, KillOverrideTag);
        Setup<ReincarnationTrialModifier>(ReincarnationTrial, SkillModifierRarity.Legendary);
        Setup<EternalCurseModifier>(EternalCurse, SkillModifierRarity.Legendary);
    }

    private void Setup<TModifier>(SkillModifierAsset asset, SkillModifierRarity rarity, params string[] conflictTags)
        where TModifier : struct, IModifier
    {
        foreach (var tag in conflictTags)
        {
            asset.ConflictTags.Add(tag);
        }

        asset.Rarity = rarity;
        asset.OnAddOrUpgrade = builder => AddModifier<TModifier>(builder);
        asset.GetDescription = entity =>
        {
            if (entity.HasComponent<TModifier>())
            {
                var modifier = entity.GetComponent<TModifier>();
                var value = modifier.GetValue();
                if (string.IsNullOrEmpty(value)) return modifier.GetKey();
                return $"{modifier.GetKey()}: {modifier.GetValue()}";
            }
            return null;
        };
    }

    private static bool AddModifier<TModifier>(SkillContainerBuilder builder) where TModifier : struct, IModifier
    {
        if (builder.HasModifier<TModifier>()) return false;
        builder.AddModifier(new TModifier());
        return true;
    }
}
