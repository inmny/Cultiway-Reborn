using System;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Libraries;

/// <summary>一次战技回调调用期间使用的临时上下文。</summary>
public readonly struct KnightTechniqueContext
{
    public readonly ActorExtend Caster;
    public readonly KnightTechniqueAsset Technique;
    public readonly Item Weapon;
    public readonly EquipmentAsset WeaponAsset;
    public readonly BaseSimObject Target;
    public readonly ActiveAbilityTarget ActiveTarget;

    public KnightTechniqueContext(
        ActorExtend caster,
        KnightTechniqueAsset technique,
        Item weapon,
        EquipmentAsset weaponAsset,
        BaseSimObject target,
        in ActiveAbilityTarget activeTarget)
    {
        Caster = caster;
        Technique = technique;
        Weapon = weapon;
        WeaponAsset = weaponAsset;
        Target = target;
        ActiveTarget = activeTarget;
    }
}

public delegate bool KnightTechniqueCondition(KnightTechniqueContext context);
public delegate int KnightTechniqueAiWeightResolver(KnightTechniqueContext context);
public delegate float KnightTechniqueFloatResolver(KnightTechniqueContext context);
public delegate ActiveAbilityTacticalProfile KnightTechniqueTacticalProfileResolver(
    KnightTechniqueContext context);
public delegate bool KnightTechniqueUseHandler(
    KnightTechniqueContext context,
    ActiveAbilityUseOrigin origin);

/// <summary>战技向统一主动能力系统声明的使用画像。</summary>
public sealed class KnightTechniqueActiveUseProfile
{
    public ActiveAbilityChannel Channels = ActiveAbilityChannel.Combat;
    public ActiveAbilityTargetMode TargetMode = ActiveAbilityTargetMode.Object;
    public ActiveAbilityActivationMode ActivationMode = ActiveAbilityActivationMode.Instant;
    public ActiveAbilityCastMobility CastMobility = ActiveAbilityCastMobility.Mobile;
    public SkillUseTargetRelation TargetRelation = SkillUseTargetRelation.Hostile;
    public KnightTechniqueCondition PrepareCondition;
    public KnightTechniqueCondition UseCondition;
    public KnightTechniqueAiWeightResolver ResolveAiWeight;
    public KnightTechniqueTacticalProfileResolver ResolveTacticalProfile;
    public KnightTechniqueFloatResolver ResolveRange;
    public KnightTechniqueFloatResolver ResolveEffectRadius;
    public KnightTechniqueUseHandler TryUse;
}

/// <summary>描述一个流派下可被训练和执行的战技。</summary>
public sealed class KnightTechniqueAsset : Asset
{
    /// <summary>该战技所属流派。</summary>
    public KnightStyleAsset Style;

    /// <summary>战技名称本地化键。</summary>
    public string NameKey;

    /// <summary>战技效果描述本地化键。</summary>
    public string DescriptionKey;

    /// <summary>流派和战技展示使用的图标路径。</summary>
    public string IconPath;

    /// <summary>战技开放所需的最低骑士等级。</summary>
    public int MinimumKnightLevel;

    /// <summary>一次完整动作消耗的斗气。</summary>
    public float VigorCost;

    /// <summary>后续执行层在施放成功后采用的独立冷却秒数。</summary>
    public float Cooldown;

    /// <summary>战技级额外装备条件。</summary>
    public KnightTechniqueCondition EquipmentCondition;

    /// <summary>主动能力画像；未配置时没有可执行入口。</summary>
    public KnightTechniqueActiveUseProfile ActiveUse;

    /// <summary>取得战技显示名称。</summary>
    public string ResolveName() => NameKey.Localize();

    /// <summary>取得战技描述。</summary>
    public string ResolveDescription() => DescriptionKey.Localize();

    /// <summary>取得战技图标。</summary>
    public Sprite ResolveIcon() => SpriteTextureLoader.getSprite(IconPath);

    /// <summary>判断战技是否满足资产层额外装备条件。</summary>
    public bool MeetsEquipmentCondition(KnightTechniqueContext context)
    {
        return EquipmentCondition?.Invoke(context) ?? true;
    }
}

/// <summary>骑士战技资产库。</summary>
public sealed class KnightTechniqueLibrary : AssetLibrary<KnightTechniqueAsset>
{
}
