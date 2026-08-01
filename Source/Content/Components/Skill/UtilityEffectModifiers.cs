using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;

namespace Cultiway.Content.Components.Skill;

/// <summary>将范围内合法地块永久抬升一个原版高度层级。</summary>
public struct RaiseTerrainModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.RaiseTerrain;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => "抬升一个高度层级";
}

/// <summary>将范围内合法地块永久降低一个原版高度层级。</summary>
public struct LowerTerrainModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.LowerTerrain;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => "降低一个高度层级";
}

/// <summary>把范围内原版允许蓄水的地块转换为对应海水层。</summary>
public struct FillWaterModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.FillWater;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => "填充可蓄水地块";
}

/// <summary>把范围内普通海水转换为其同层干燥地形。</summary>
public struct DrainWaterModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.DrainWater;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => "排除普通海水";
}

/// <summary>自然生长区域的持续时间与尝试间隔。</summary>
public struct NatureGrowthModifier : IModifier
{
    public float Duration;
    public SkillModifierAsset ModifierAsset => SkillModifiers.NatureGrowth;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"持续 {Duration:F1} 秒，每秒催生植被";
}

/// <summary>净土区域的持续时间与清理间隔。</summary>
public struct CleanLandModifier : IModifier
{
    public float Duration;
    public SkillModifierAsset ModifierAsset => SkillModifiers.CleanLand;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"持续 {Duration:F1} 秒，每 0.5 秒净化地块";
}

/// <summary>召唤出的原版雨云在世界中持续存在的秒数。</summary>
public struct SummonRainCloudModifier : IModifier
{
    public float Duration;
    public SkillModifierAsset ModifierAsset => SkillModifiers.SummonRainCloud;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"雨云持续 {Duration:F1} 秒";
}

/// <summary>把范围内尚未成熟的原版麦田直接催熟。</summary>
public struct FertilizeModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.Fertilize;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => "催熟尚未成熟的麦田";
}
