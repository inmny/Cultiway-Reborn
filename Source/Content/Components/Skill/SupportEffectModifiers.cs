using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;

namespace Cultiway.Content.Components.Skill;

/// <summary>即时治疗法术的基础治疗量。</summary>
public struct HealingModifier : IModifier
{
    public float Amount;
    public SkillModifierAsset ModifierAsset => SkillModifiers.Healing;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"治疗 {Amount:F1}";
}

/// <summary>持续恢复区域的持续时间与每秒治疗量。</summary>
public struct RejuvenationModifier : IModifier
{
    public float Duration;
    public float HealPerSecond;
    public SkillModifierAsset ModifierAsset => SkillModifiers.Rejuvenation;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"{Duration:F1} 秒，每秒治疗 {HealPerSecond:F1}";
}

/// <summary>净化波每次为每个目标移除的负面状态数量。</summary>
public struct PurificationModifier : IModifier
{
    public int MaxStatuses;
    public SkillModifierAsset ModifierAsset => SkillModifiers.Purification;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"净化 {MaxStatuses} 个负面状态";
}

/// <summary>战意祝福的持续时间与伤害倍率加成。</summary>
public struct BattleBlessingModifier : IModifier
{
    public float Duration;
    public float DamageBonus;
    public SkillModifierAsset ModifierAsset => SkillModifiers.BattleBlessing;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"{Duration:F1} 秒，伤害 +{DamageBonus:P0}";
}

/// <summary>守护祝福的持续时间与固定护甲加成。</summary>
public struct GuardBlessingModifier : IModifier
{
    public float Duration;
    public float ArmorBonus;
    public SkillModifierAsset ModifierAsset => SkillModifiers.GuardBlessing;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"{Duration:F1} 秒，护甲 +{ArmorBonus:F1}";
}

/// <summary>迅捷祝福的持续时间、移动速度与攻击速度加成。</summary>
public struct HasteBlessingModifier : IModifier
{
    public float Duration;
    public float MoveSpeedBonus;
    public float AttackSpeedBonus;
    public SkillModifierAsset ModifierAsset => SkillModifiers.HasteBlessing;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() =>
        $"{Duration:F1} 秒，移动 +{MoveSpeedBonus:P0}，攻速 +{AttackSpeedBonus:P0}";
}
