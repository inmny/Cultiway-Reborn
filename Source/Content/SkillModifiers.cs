using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Utils;

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

        Setup(Placeholder, SkillModifierRarity.Common);

        Setup(Slow, SkillModifierRarity.Common);
        Setup(Burn, SkillModifierRarity.Common);
        Setup(Freeze, SkillModifierRarity.Common);
        Setup(Poison, SkillModifierRarity.Common);
        Setup(Explosion, SkillModifierRarity.Common);
        Setup(Haste, SkillModifierRarity.Common);
        Setup(Proficiency, SkillModifierRarity.Common);
        Setup(Empower, SkillModifierRarity.Common);
        Setup(Knockback, SkillModifierRarity.Common);
        Setup(Volley, SkillModifierRarity.Common);

        Setup(LockOn, SkillModifierRarity.Rare);
        Setup(Huge, SkillModifierRarity.Rare);
        Setup(Weaken, SkillModifierRarity.Rare);
        Setup(ArmorBreak, SkillModifierRarity.Rare);
        Setup(Gravity, SkillModifierRarity.Rare);
        Setup(Daze, SkillModifierRarity.Rare);

        Setup(Mercy, SkillModifierRarity.Epic, KillOverrideTag);
        Setup(Chaos, SkillModifierRarity.Epic);
        Setup(Swap, SkillModifierRarity.Epic);
        Setup(RandomAffix, SkillModifierRarity.Epic);
        Setup(Burnout, SkillModifierRarity.Epic);
        Setup(Combo, SkillModifierRarity.Epic);

        Setup(Silence, SkillModifierRarity.Legendary);
        Setup(DeathSentence, SkillModifierRarity.Legendary, KillOverrideTag);
        Setup(ReincarnationTrial, SkillModifierRarity.Legendary);
        Setup(EternalCurse, SkillModifierRarity.Legendary);
    }

    private void Setup(SkillModifierAsset asset, SkillModifierRarity rarity, params string[] conflictTags)
    {
        foreach (var tag in conflictTags)
        {
            asset.ConflictTags.Add(tag);
        }

        asset.Rarity = rarity;
        asset.OnAddOrUpgrade = builder => AddPlaceholder(builder, asset.id);
    }

    private static bool AddPlaceholder(SkillContainerBuilder builder, string assetId)
    {
        if (builder.HasModifier<PlaceholderModifier>())
        {
            var modifier = builder.GetModifier<PlaceholderModifier>();
            modifier.ModifierAssetIds ??= new HashSet<string>();
            var added = modifier.ModifierAssetIds.Add(assetId);
            builder.SetModifier(modifier);
            return added;
        }

        builder.AddModifier(new PlaceholderModifier
        {
            ModifierAssetIds = new HashSet<string>
            {
                assetId
            }
        });
        return true;
    }
}
