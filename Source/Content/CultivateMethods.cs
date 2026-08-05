using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Content.Semantics;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Semantics;
using Cultiway.Patch;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>修炼方式集合。</summary>
[Dependency(typeof(CultivationSemantics), typeof(CultivationResources))]
public class CultivateMethods : ExtendLibrary<CultivateMethodAsset, CultivateMethods>
{
    private const float BattleOutgoingWakanRatio = 0.10f;
    private const float BattleIncomingWakanRatio = 0.05f;
    private const float KillMonthlyWakanRatio = 0.20f;
    private const float FortuneMonthlyWakanRatio = 0.15f;
    private const float EarthFireMonthlyDamageRatio = 0.08f;
    private const float ColdMonthlyDamageRatio = 0.05f;
    private const float DesolateDirtyWakanResidueRatio = 0.15f;
    private const float ThunderWakanRewardRatio = 0.30f;
    private const float ThunderBaseRate = 0.16f;
    private const float ThunderMaximumRate = 1f / 15f;

    private static readonly ElementComposition EarthFireTarget =
        new(fire: 0.5f, earth: 0.5f, normalize: true);
    private static readonly ElementComposition DesolateTarget =
        new(neg: 0.5f, entropy: 0.5f, normalize: true);
    private static readonly ElementComposition SolarTarget =
        new(fire: 0.35f, pos: 0.65f, normalize: true);
    private static readonly ElementComposition LunarTarget =
        new(water: 0.35f, neg: 0.65f, normalize: true);
    private static readonly HashSet<string> DesolateBiomeIds = new(StringComparer.Ordinal)
    {
        "biome_corrupted",
        "biome_infernal",
        "biome_wasteland",
        "biome_dark",
        "biome_fleshblood"
    };

    /// <summary>标准闭关修炼方式。</summary>
    public static CultivateMethodAsset Standard { get; private set; }

    /// <summary>水中修炼方式。</summary>
    public static CultivateMethodAsset WaterMeditation { get; private set; }

    /// <summary>借草木生机温养自身的环境修炼方式。</summary>
    public static CultivateMethodAsset GrassWoodNourishing { get; private set; }

    /// <summary>借山岳厚重之势稳定灵气的环境修炼方式。</summary>
    public static CultivateMethodAsset MountainStabilization { get; private set; }

    /// <summary>在地火附近承受火力淬炼的危险修炼方式。</summary>
    public static CultivateMethodAsset EarthFireTempering { get; private set; }

    /// <summary>在寒冷环境中凝练神意的危险修炼方式。</summary>
    public static CultivateMethodAsset ColdMeditation { get; private set; }

    /// <summary>吞纳荒芜地带浊气并留下个人浊气的修炼方式。</summary>
    public static CultivateMethodAsset DesolateDevouring { get; private set; }

    /// <summary>在太阳时代吸收日精的露天修炼方式。</summary>
    public static CultivateMethodAsset SolarEssence { get; private set; }

    /// <summary>在月亮时代吸收月华的露天修炼方式。</summary>
    public static CultivateMethodAsset LunarEssence { get; private set; }

    /// <summary>修炼时主动引来真实天雷淬体的危险修炼方式。</summary>
    public static CultivateMethodAsset ThunderTempering { get; private set; }

    /// <summary>按实际伤害磨炼战意的战斗修炼方式。</summary>
    public static CultivateMethodAsset BattleCultivate { get; private set; }

    /// <summary>夺取死亡浊气并缓慢炼化的杀戮修炼方式。</summary>
    public static CultivateMethodAsset KillAbsorb { get; private set; }

    /// <summary>消耗城市或国家国运的帝道修炼方式。</summary>
    public static CultivateMethodAsset KingdomFortune { get; private set; }

    /// <summary>解析角色当前修炼方式，并在其支持触发且满足前置条件时执行对应规则。</summary>
    public static bool TryDispatch(in CultivationTriggerContext context)
    {
        ActorExtend actor = context.Practitioner;
        if (actor?.Base == null || !actor.Base.isAlive()) return false;
        CultibookAsset cultibook = actor.GetMainCultibook();
        CultivateMethodAsset method = cultibook?.GetCultivateMethod() ?? Standard;
        if (method == null || !method.Handles(context.Trigger) || method.Execute == null) return false;
        if (method.CanCultivate != null && !method.CanCultivate(actor)) return false;
        method.Execute(in context);
        return true;
    }

    protected override bool AutoRegisterAssets() => true;

    protected override void OnInit()
    {
        ConfigureStandard();
        ConfigureWaterMeditation();
        ConfigureGrassWoodNourishing();
        ConfigureMountainStabilization();
        ConfigureEarthFireTempering();
        ConfigureColdMeditation();
        ConfigureDesolateDevouring();
        ConfigureSolarEssence();
        ConfigureLunarEssence();
        ConfigureThunderTempering();
        ConfigureBattleCultivation();
        ConfigureKillAbsorption();
        ConfigureKingdomFortune();
    }

    private static void ConfigureStandard()
    {
        Standard.Semantics = SemanticDescriptor.Of(
            CultivationSemantics.Role.Cultivation,
            CultivationSemantics.Path.Meditation);
        Standard.TriggerKinds = CultivationTriggerKind.ActiveTick;
        Standard.CanCultivate = HasXianCultisys;
        Standard.ResourceInputs = [CultivationResources.WorldWakan, CultivationResources.TileDirtyWakan];
        Standard.GetContextualResourceInputs = actor => actor.Base.hasHouse()
            ? [CultivationResources.WorldWakan]
            : [CultivationResources.TileDirtyWakan];
        Standard.GetMethodMultiplier = _ => 1f;
        Standard.GetSelectionScore = _ => 5f;
        Standard.GetBehaviourJobId = actor => actor.Base.hasHouse()
            ? ActorJobs.XianCultivator.id
            : ActorJobs.PlantXianCultivator.id;
        Standard.Execute = ExecuteActiveMeditation;
    }

    private static void ConfigureWaterMeditation()
    {
        ConfigureEnvironmentalMethod(
            WaterMeditation,
            SemanticDescriptor.Of(
                CultivationSemantics.Role.Cultivation,
                CultivationSemantics.Path.NaturalCultivation,
                SkillSemantics.Element.Water),
            new CultivationEnvironmentRule
            {
                TargetComposition = ElementComposition.Static.Water,
                GetTileQuality = (_, tile) => tile.IsWater() ? 1f : 0f,
                GetMultiplier = (_, quality) => 0.5f + quality,
                Resource = CultivationResources.WorldWakan,
                WalkOnWater = true
            });
    }

    /// <summary>配置草木养息的植被环境规则。</summary>
    private static void ConfigureGrassWoodNourishing()
    {
        ConfigureEnvironmentalMethod(
            GrassWoodNourishing,
            SemanticDescriptor.Of(
                CultivationSemantics.Role.Cultivation,
                CultivationSemantics.Path.NaturalCultivation,
                CultivationSemantics.Effect.Recovery,
                SkillSemantics.Element.Wood),
            new CultivationEnvironmentRule
            {
                TargetComposition = ElementComposition.Static.Wood,
                GetTileQuality = ResolveVegetationQuality,
                GetMultiplier = (_, quality) => 0.6f + 0.9f * quality,
                Resource = CultivationResources.WorldWakan
            });
    }

    /// <summary>配置山岳镇元的山地层级与阻挡地块通行规则。</summary>
    private static void ConfigureMountainStabilization()
    {
        ConfigureEnvironmentalMethod(
            MountainStabilization,
            SemanticDescriptor.Of(
                CultivationSemantics.Role.Cultivation,
                CultivationSemantics.Path.NaturalCultivation,
                CultivationSemantics.Material.Stability,
                SkillSemantics.Element.Earth),
            new CultivationEnvironmentRule
            {
                TargetComposition = ElementComposition.Static.Earth,
                GetTileQuality = ResolveMountainQuality,
                GetMultiplier = (_, quality) => 0.6f + quality,
                Resource = CultivationResources.WorldWakan,
                WalkOnBlocks = true,
                AllowDamagingTerrain = true
            });
    }

    /// <summary>配置地火淬体的地火环境、通行方式和逐月火焰伤害。</summary>
    private static void ConfigureEarthFireTempering()
    {
        ConfigureEnvironmentalMethod(
            EarthFireTempering,
            SemanticDescriptor.Of(
                CultivationSemantics.Role.Cultivation,
                CultivationSemantics.Path.NaturalCultivation,
                CultivationSemantics.Form.Body,
                SkillSemantics.Element.Fire,
                SkillSemantics.Element.Earth),
            new CultivationEnvironmentRule
            {
                TargetComposition = EarthFireTarget,
                GetTileQuality = ResolveEarthFireQuality,
                GetMultiplier = (_, quality) => 0.5f + 1.2f * quality,
                Resource = CultivationResources.WorldWakan,
                WalkOnLava = true,
                AllowDamagingTerrain = true,
                AfterSettlement = ApplyEarthFireRisk
            });
    }

    /// <summary>配置寒域凝神的冰雪环境和逐月寒冷伤害。</summary>
    private static void ConfigureColdMeditation()
    {
        ConfigureEnvironmentalMethod(
            ColdMeditation,
            SemanticDescriptor.Of(
                CultivationSemantics.Role.Cultivation,
                CultivationSemantics.Path.NaturalCultivation,
                CultivationSemantics.Theme.Spirit,
                SkillSemantics.Element.Ice),
            new CultivationEnvironmentRule
            {
                TargetComposition = ElementComposition.Static.Ice,
                GetTileQuality = ResolveColdQuality,
                GetMultiplier = (_, quality) => 0.6f + 0.9f * quality,
                Resource = CultivationResources.WorldWakan,
                AllowDamagingTerrain = true,
                AfterSettlement = ApplyColdRisk
            });
    }

    /// <summary>配置荒煞吞元对荒芜生物群系和地块浊气的连续评分。</summary>
    private static void ConfigureDesolateDevouring()
    {
        ConfigureEnvironmentalMethod(
            DesolateDevouring,
            SemanticDescriptor.Of(
                CultivationSemantics.Role.Cultivation,
                CultivationSemantics.Path.NaturalCultivation,
                CultivationSemantics.Effect.Devouring,
                CultivationSemantics.Resource.DirtyWakan,
                SkillSemantics.Element.Neg,
                SkillSemantics.Element.Entropy),
            new CultivationEnvironmentRule
            {
                TargetComposition = DesolateTarget,
                GetTileQuality = ResolveDesolateQuality,
                GetMultiplier = (_, quality) => 0.5f + quality,
                Resource = CultivationResources.TileDirtyWakan,
                ResourcePerWakan = ContentSetting.DirtyWakanToWakanRatio,
                AfterSettlement = ApplyDesolateResidue
            });
    }

    /// <summary>配置日精炼形只在太阳时代高效的露天规则。</summary>
    private static void ConfigureSolarEssence()
    {
        ConfigureEnvironmentalMethod(
            SolarEssence,
            SemanticDescriptor.Of(
                CultivationSemantics.Role.Cultivation,
                CultivationSemantics.Path.NaturalCultivation,
                CultivationSemantics.Form.Body,
                SkillSemantics.Element.Fire,
                SkillSemantics.Element.Pos),
            new CultivationEnvironmentRule
            {
                TargetComposition = SolarTarget,
                GetTileQuality = ResolveOutdoorQuality,
                GetMultiplier = (_, quality) => ResolveEraMultiplier("age_sun") * ResolveOutdoorFallback(quality),
                GetEraMatch = _ => IsCurrentEra("age_sun") ? 1f : 0f,
                Resource = CultivationResources.WorldWakan,
                PreferOutdoors = true
            });
    }

    /// <summary>配置月华养神只在月亮时代高效的露天规则。</summary>
    private static void ConfigureLunarEssence()
    {
        ConfigureEnvironmentalMethod(
            LunarEssence,
            SemanticDescriptor.Of(
                CultivationSemantics.Role.Cultivation,
                CultivationSemantics.Path.NaturalCultivation,
                CultivationSemantics.Theme.Spirit,
                SkillSemantics.Element.Water,
                SkillSemantics.Element.Neg),
            new CultivationEnvironmentRule
            {
                TargetComposition = LunarTarget,
                GetTileQuality = ResolveOutdoorQuality,
                GetMultiplier = (_, quality) => ResolveEraMultiplier("age_moon") * ResolveOutdoorFallback(quality),
                GetEraMatch = _ => IsCurrentEra("age_moon") ? 1f : 0f,
                Resource = CultivationResources.WorldWakan,
                PreferOutdoors = true
            });
    }

    /// <summary>配置雷霆淬体恒定一倍的普通修炼部分；引雷与额外奖励由独立流程处理。</summary>
    private static void ConfigureThunderTempering()
    {
        ConfigureEnvironmentalMethod(
            ThunderTempering,
            SemanticDescriptor.Of(
                CultivationSemantics.Role.Cultivation,
                CultivationSemantics.Path.NaturalCultivation,
                CultivationSemantics.Form.Body,
                SkillSemantics.Element.Lightning),
            new CultivationEnvironmentRule
            {
                TargetComposition = ElementComposition.Static.Lightning,
                GetTileQuality = ResolveOutdoorQuality,
                GetMultiplier = (_, _) => 1f,
                Resource = CultivationResources.WorldWakan,
                PreferOutdoors = true
            });
        ThunderTempering.GetSelectionScore = ResolveThunderSelectionScore;
        ThunderTempering.TriggerKinds = CultivationTriggerKind.ActiveTick |
                                        CultivationTriggerKind.HeavenlyLightningDamage;
        ThunderTempering.Execute = ExecuteThunderTempering;
    }

    private static void ConfigureBattleCultivation()
    {
        BattleCultivate.Semantics = SemanticDescriptor.Of(
            CultivationSemantics.Role.Cultivation,
            CultivationSemantics.Path.BattleCultivation,
            CultivationSemantics.Resource.BattleIntent,
            SkillSemantics.Role.Offensive);
        BattleCultivate.TriggerKinds = CultivationTriggerKind.DamageDealt | CultivationTriggerKind.DamageTaken;
        BattleCultivate.CanCultivate = HasXianCultisys;
        BattleCultivate.ResourceInputs = Array.Empty<CultivationResourceAsset>();
        BattleCultivate.GetMethodMultiplier = _ => 1f;
        BattleCultivate.GetSelectionScore = actor => Mathf.Min(actor.all_skills.Count, 5) * 0.7f;
        BattleCultivate.Execute = ExecuteBattleCultivation;
    }

    private static void ConfigureKillAbsorption()
    {
        KillAbsorb.Semantics = SemanticDescriptor.Of(
            CultivationSemantics.Role.Cultivation,
            CultivationSemantics.Path.SlaughterCultivation,
            CultivationSemantics.Effect.Devouring,
            CultivationSemantics.Resource.DirtyWakan,
            SkillSemantics.Element.Neg,
            SkillSemantics.Element.Entropy);
        KillAbsorb.TriggerKinds = CultivationTriggerKind.Kill | CultivationTriggerKind.TimedTick;
        KillAbsorb.CanCultivate = HasXianCultisys;
        KillAbsorb.ResourceInputs =
            [CultivationResources.PersonalDirtyWakan, CultivationResources.TileDirtyWakan];
        KillAbsorb.GetMethodMultiplier = _ => 1f;
        KillAbsorb.GetSelectionScore = actor =>
        {
            float score = Mathf.Log(actor.Base.data.kills + 1f) * 0.8f;
            if (actor.HasElementRoot())
            {
                ref ElementRoot root = ref actor.GetElementRoot();
                score += root.Neg + root.Entropy;
            }
            return score;
        };
        KillAbsorb.Execute = ExecuteKillAbsorption;
    }

    private static void ConfigureKingdomFortune()
    {
        KingdomFortune.Semantics = SemanticDescriptor.Of(
            CultivationSemantics.Role.Cultivation,
            CultivationSemantics.Path.FortuneCultivation,
            CultivationSemantics.Resource.Fortune,
            SkillSemantics.Element.Pos);
        KingdomFortune.TriggerKinds = CultivationTriggerKind.TimedTick;
        KingdomFortune.ResourceInputs = [CultivationResources.RoleFortune];
        KingdomFortune.CanCultivate = actor =>
        {
            if (!actor.HasCultisys<Xian>()) return false;
            Actor unit = actor.Base;
            return unit.kingdom != null && unit.kingdom.king == unit ||
                   unit.city != null && unit.city.leader == unit;
        };
        KingdomFortune.GetMethodMultiplier = actor =>
        {
            Actor unit = actor.Base;
            if (unit.kingdom != null && unit.kingdom.king == unit) return 1f;
            return unit.city != null && unit.city.leader == unit ? 0.5f : 0f;
        };
        KingdomFortune.GetSelectionScore = _ => 8f;
        KingdomFortune.Execute = ExecuteKingdomFortune;
    }

    /// <summary>把共同的主动触发、选址 Job、倍率和结算入口写入一个环境修炼资产。</summary>
    private static void ConfigureEnvironmentalMethod(
        CultivateMethodAsset method,
        SemanticDescriptor semantics,
        CultivationEnvironmentRule rule)
    {
        method.Semantics = semantics;
        method.TriggerKinds = CultivationTriggerKind.ActiveTick;
        method.CanCultivate = HasXianCultisys;
        method.EnvironmentRule = rule;
        method.ResourceInputs = rule.Resource == null
            ? Array.Empty<CultivationResourceAsset>()
            : [rule.Resource];
        method.GetMethodMultiplier = rule.ResolveMultiplier;
        method.GetSelectionScore = actor => ResolveEnvironmentalSelectionScore(actor, rule);
        method.GetBehaviourJobId = _ => ActorJobs.EnvironmentalCultivator.id;
        method.Execute = ExecuteEnvironmentalCultivation;
    }

    /// <summary>按灵根组成、附近最佳地点和时代匹配计算自然修炼方式的生成分数。</summary>
    private static float ResolveEnvironmentalSelectionScore(
        ActorExtend actor,
        CultivationEnvironmentRule rule)
    {
        float similarity = 0f;
        if (actor.HasElementRoot())
        {
            ref ElementRoot root = ref actor.GetElementRoot();
            similarity = ElementRootAffinityResolver.ResolveCompositionSimilarity(root, rule.TargetComposition);
        }

        float quality = CultivationEnvironmentService.ResolveBestNearbyQuality(actor, rule);
        float eraMatch = Mathf.Clamp01(rule.GetEraMatch?.Invoke(actor) ?? 0f);
        return 0.5f + similarity * 4f + quality * 5f + eraMatch * 2f;
    }

    /// <summary>雷霆淬体只按雷灵根组成相似度和综合强度选择，不受附近环境或时代影响。</summary>
    private static float ResolveThunderSelectionScore(ActorExtend actor)
    {
        if (!actor.HasElementRoot()) return 0.5f;
        ref ElementRoot root = ref actor.GetElementRoot();
        float similarity = ElementRootAffinityResolver.ResolveCompositionSimilarity(
            root,
            ElementComposition.Static.Lightning);
        float strength = ElementRootAffinityResolver.ResolveStrengthFactor(root);
        return 0.5f + similarity * 4f + strength * 2f;
    }

    /// <summary>有自然植被生长能力的生物群系提供完整草木环境质量。</summary>
    private static float ResolveVegetationQuality(ActorExtend _, WorldTile tile)
    {
        return tile.getBiome()?.grow_vegetation_auto == true ? 1f : 0f;
    }

    /// <summary>按山峰、山地和丘陵岩地层级计算山岳环境质量。</summary>
    private static float ResolveMountainQuality(ActorExtend _, WorldTile tile)
    {
        TileTypeBase type = tile.Type;
        if (type.summit) return 1f;
        if (type.mountains) return 0.85f;
        return type.edge_mountains || type.edge_hills || type.rocks ? 0.65f : 0f;
    }

    /// <summary>按熔岩、当前燃烧及邻接地火计算地火环境质量。</summary>
    private static float ResolveEarthFireQuality(ActorExtend _, WorldTile tile)
    {
        if (tile.Type.lava) return 1f;
        if (tile.isOnFire()) return 0.8f;
        foreach (WorldTile neighbour in tile.neighbours)
            if (neighbour?.Type?.lava == true || neighbour?.isOnFire() == true)
                return 0.65f;
        return 0f;
    }

    /// <summary>按冰封山峰、冻结或永冻地块及雪地计算寒域环境质量。</summary>
    private static float ResolveColdQuality(ActorExtend _, WorldTile tile)
    {
        if (tile.Type.summit && tile.isFrozen()) return 1f;
        if (tile.isFrozen() || tile.getBiome()?.id == "biome_permafrost") return 0.8f;
        return tile.Type.id.StartsWith("snow_", StringComparison.Ordinal) ? 0.65f : 0f;
    }

    /// <summary>按荒芜生物群系和当前地块实际浊气浓度计算荒煞质量。</summary>
    private static float ResolveDesolateQuality(ActorExtend actor, WorldTile tile)
    {
        float biomeQuality = DesolateBiomeIds.Contains(tile.getBiome()?.id ?? string.Empty) ? 0.8f : 0f;
        var resourceContext = new CultivationResourceContext(actor, tile.x, tile.y);
        float dirty = CultivationResources.TileDirtyWakan.GetAvailable(in resourceContext);
        float dirtyQuality = dirty <= 0f ? 0f : dirty / (dirty + 100f);
        return Mathf.Max(biomeQuality, dirtyQuality);
    }

    /// <summary>无建筑地块视为露天地点。</summary>
    private static float ResolveOutdoorQuality(ActorExtend _, WorldTile tile)
    {
        return tile.hasBuilding() ? 0f : 1f;
    }

    /// <summary>返回目标时代 1.3 倍、其他时代 0.35 倍的动态倍率。</summary>
    private static float ResolveEraMultiplier(string eraId)
    {
        return IsCurrentEra(eraId) ? 1.3f : 0.35f;
    }

    /// <summary>角色无法抵达露天地点时再施加一次 0.35 的室内回退系数。</summary>
    private static float ResolveOutdoorFallback(float quality)
    {
        return quality > 0f ? 1f : 0.35f;
    }

    /// <summary>判断当前 WorldBox 时代是否与指定资产 ID 相同。</summary>
    private static bool IsCurrentEra(string eraId)
    {
        return World.world?.era_manager?.getCurrentAge()?.id == eraId;
    }

    private static bool HasXianCultisys(ActorExtend actor)
    {
        return actor.HasCultisys<Xian>();
    }

    /// <summary>按环境规则的修炼资源、倍率和当前地块完成一次环境修炼。</summary>
    [Hotfixable]
    private static void ExecuteEnvironmentalCultivation(in CultivationTriggerContext context)
    {
        ActorExtend actor = context.Practitioner;
        CultibookAsset cultibook = actor.GetMainCultibook();
        CultivateMethodAsset method = cultibook.GetCultivateMethod();
        CultivationEnvironmentRule rule = method.EnvironmentRule;
        CultivationEfficiencyResult efficiency = CultivationEfficiencyResolver.Resolve(actor, cultibook, method);
        Vector2Int position = actor.Base.current_tile.pos;
        var resourceContext = new CultivationResourceContext(actor, position.x, position.y);
        float available = rule.Resource.GetAvailable(in resourceContext);
        float cleanEquivalent = available / rule.ResourcePerWakan;
        float requested = Mathf.Log10(cleanEquivalent + 1f) * efficiency.FinalMultiplier;
        CultivationSettlementResult result = CultivationSettlementService.ConvertToWakan(
            actor,
            rule.Resource,
            requested,
            rule.ResourcePerWakan,
            position.x,
            position.y);

        float quality = rule.ResolveQuality(actor, actor.Base.current_tile);
        RecordSettledPractice(in context, method, efficiency, requested, result);
        rule.AfterSettlement?.Invoke(in context, quality, result);
    }

    /// <summary>普通吐纳固定按一倍方式倍率结算，并在主动修炼时按灵根资质尝试引来天雷。</summary>
    [Hotfixable]
    private static void ExecuteThunderTempering(in CultivationTriggerContext context)
    {
        if (context.Trigger == CultivationTriggerKind.HeavenlyLightningDamage)
        {
            ApplyHeavenlyLightningReward(in context);
            return;
        }

        ExecuteEnvironmentalCultivation(in context);
        TrySummonHeavenlyLightning(in context);
    }

    /// <summary>按灵根综合强度和雷元素八维相似度计算泊松引雷概率，并生成真实原版天雷。</summary>
    private static void TrySummonHeavenlyLightning(in CultivationTriggerContext context)
    {
        ActorExtend actor = context.Practitioner;
        if (context.ElapsedSeconds <= 0f || actor?.Base?.current_tile == null || !actor.HasElementRoot()) return;

        ref ElementRoot root = ref actor.GetElementRoot();
        float strengthFactor = ElementRootAffinityResolver.ResolveStrengthFactor(root);
        float similarity = ElementRootAffinityResolver.ResolveCompositionSimilarity(
            root,
            ElementComposition.Static.Lightning);
        float rate = Mathf.Min(
            ThunderMaximumRate,
            ThunderBaseRate * strengthFactor * Mathf.Pow(similarity, 4f));
        float probability = 1f - Mathf.Exp(-rate * context.ElapsedSeconds);
        if (probability <= 0f || !Randy.randomChance(probability)) return;

        PatchLightning.ExecuteTrackedSkyLightning(
            actor.Base.data.id,
            () => MapBox.spawnLightningSmall(actor.Base.current_tile, 0.25f, null));
    }

    /// <summary>只按实际雷击伤害占生命上限的比例奖励灵气，并始终记录幸存淬体实践。</summary>
    private static void ApplyHeavenlyLightningReward(in CultivationTriggerContext context)
    {
        ActorExtend actor = context.Practitioner;
        if (actor?.Base == null || !actor.Base.isAlive() || context.ActualDamage <= 0f ||
            context.ReferenceMaxHealth <= 0f) return;

        CultibookAsset cultibook = actor.GetMainCultibook();
        CultivationEfficiencyResult efficiency =
            CultivationEfficiencyResolver.Resolve(actor, cultibook, ThunderTempering);
        float damageRatio = Mathf.Clamp01(context.ActualDamage / context.ReferenceMaxHealth);
        float maximum = Mathf.Max(0f, actor.Base.stats[BaseStatses.MaxWakan.id]);
        float requested = maximum * ThunderWakanRewardRatio * damageRatio * efficiency.AptitudeMultiplier;
        CultivationSettlementService.GainWakan(actor, requested);

        // 天雷淬体的事实不依赖当前灵气是否已满；一次等生命上限的有效雷伤折算为一月实践。
        RecordPractice(actor, ThunderTempering, damageRatio * efficiency.AptitudeMultiplier);
    }

    /// <summary>按当前环境质量施加地火淬体的真实火焰伤害。</summary>
    private static void ApplyEarthFireRisk(
        in CultivationTriggerContext context,
        float quality,
        CultivationSettlementResult _)
    {
        ApplyEnvironmentalDamage(in context, quality, EarthFireMonthlyDamageRatio,
            ElementComposition.Static.Fire);
    }

    /// <summary>按当前环境质量施加寒域凝神的真实寒冷伤害。</summary>
    private static void ApplyColdRisk(
        in CultivationTriggerContext context,
        float quality,
        CultivationSettlementResult _)
    {
        ApplyEnvironmentalDamage(in context, quality, ColdMonthlyDamageRatio,
            ElementComposition.Static.Ice);
    }

    /// <summary>把荒煞吞元实际消耗浊气的固定比例留存在角色体内。</summary>
    private static void ApplyDesolateResidue(
        in CultivationTriggerContext context,
        float _,
        CultivationSettlementResult settlement)
    {
        CultivationResources.AddPersonalDirtyWakan(
            context.Practitioner,
            settlement.ResourceSpent * DesolateDirtyWakanResidueRatio);
    }

    /// <summary>把逐月危险按本次经过时间折算后送入统一元素伤害结算。</summary>
    private static void ApplyEnvironmentalDamage(
        in CultivationTriggerContext context,
        float quality,
        float monthlyRatio,
        ElementComposition composition)
    {
        if (quality <= 0f || context.ElapsedSeconds <= 0f || !context.Practitioner.Base.isAlive()) return;
        float maximum = Mathf.Max(0f, context.Practitioner.Base.getMaxHealth());
        float damage = maximum * monthlyRatio * quality * context.ElapsedSeconds / TimeScales.SecPerMonth;
        if (damage <= 0f) return;
        context.Practitioner.GetHit(damage, ref composition, null, AttackType.Other);
    }

    [Hotfixable]
    private static void ExecuteActiveMeditation(in CultivationTriggerContext context)
    {
        ActorExtend actor = context.Practitioner;
        CultibookAsset cultibook = actor.GetMainCultibook();
        CultivateMethodAsset method = cultibook?.GetCultivateMethod() ?? Standard;
        CultivationEfficiencyResult efficiency = CultivationEfficiencyResolver.Resolve(actor, cultibook, method);
        Vector2Int position = actor.Base.current_tile.pos;
        var resourceContext = new CultivationResourceContext(actor, position.x, position.y);
        CultivationSettlementResult result;
        float requested;
        if (context.Activity == CultivationActivityKind.PlantPurification)
        {
            float dirty = CultivationResources.TileDirtyWakan.GetAvailable(in resourceContext);
            float cleanEquivalent = dirty / ContentSetting.DirtyWakanToWakanRatio;
            requested = Mathf.Log10(cleanEquivalent + 1f) * efficiency.FinalMultiplier;
            result = CultivationSettlementService.ConvertToWakan(
                actor,
                CultivationResources.TileDirtyWakan,
                requested,
                ContentSetting.DirtyWakanToWakanRatio,
                position.x,
                position.y);
        }
        else
        {
            float worldWakan = CultivationResources.WorldWakan.GetAvailable(in resourceContext);
            requested = Mathf.Log10(worldWakan + 1f) * efficiency.FinalMultiplier;
            result = CultivationSettlementService.ConvertToWakan(
                actor,
                CultivationResources.WorldWakan,
                requested,
                1f,
                position.x,
                position.y);
        }

        RecordSettledPractice(in context, method, efficiency, requested, result);
    }

    [Hotfixable]
    private static void ExecuteBattleCultivation(in CultivationTriggerContext context)
    {
        if (context.ActualDamage <= 0f || context.ReferenceMaxHealth <= 0f) return;
        ActorExtend actor = context.Practitioner;
        CultibookAsset cultibook = actor.GetMainCultibook();
        CultivationEfficiencyResult efficiency =
            CultivationEfficiencyResolver.Resolve(actor, cultibook, BattleCultivate);
        float threat = ResolveThreat(context.PractitionerPower, context.OpponentPower);
        float damageRatio = Mathf.Max(0f, context.ActualDamage / context.ReferenceMaxHealth);
        float gainRatio = context.Trigger == CultivationTriggerKind.DamageDealt
            ? BattleOutgoingWakanRatio
            : BattleIncomingWakanRatio;
        float maximum = Mathf.Max(0f, actor.Base.stats[BaseStatses.MaxWakan.id]);
        float requested = maximum * gainRatio * damageRatio * threat * efficiency.FinalMultiplier;
        CultivationSettlementService.GainWakan(actor, requested);

        // 一次对同战力目标造成其生命上限总量的实际伤害，折算为一月有效战斗修炼。
        float effectiveMonths = damageRatio * threat * efficiency.FinalMultiplier;
        RecordPractice(actor, BattleCultivate, effectiveMonths);
    }

    [Hotfixable]
    private static void ExecuteKillAbsorption(in CultivationTriggerContext context)
    {
        if (context.Trigger == CultivationTriggerKind.Kill)
        {
            ExecuteKillClaim(in context);
            return;
        }

        ActorExtend actor = context.Practitioner;
        CultibookAsset cultibook = actor.GetMainCultibook();
        CultivationEfficiencyResult efficiency = CultivationEfficiencyResolver.Resolve(actor, cultibook, KillAbsorb);
        float maximum = Mathf.Max(0f, actor.Base.stats[BaseStatses.MaxWakan.id]);
        float monthlyLimit = maximum * KillMonthlyWakanRatio * efficiency.FinalMultiplier;
        float requested = monthlyLimit * context.ElapsedSeconds / TimeScales.SecPerMonth;
        CultivationSettlementResult result = CultivationSettlementService.ConvertToWakan(
            actor,
            CultivationResources.PersonalDirtyWakan,
            requested,
            ContentSetting.DirtyWakanToWakanRatio);
        RecordSettledPractice(in context, KillAbsorb, efficiency, requested, result);
    }

    private static void ExecuteKillClaim(in CultivationTriggerContext context)
    {
        ActorExtend actor = context.Practitioner;
        float claimFactor = Mathf.Min(1f, ResolveThreat(context.PractitionerPower, context.OpponentPower));
        float effectiveMonths = claimFactor;
        float personalCapacity = CultivationResources.GetPersonalDirtyWakanCapacity(actor);
        float room = Mathf.Max(0f, personalCapacity - CultivationResources.GetPersonalDirtyWakan(actor));
        float requested = Mathf.Min(CultivationResources.DeathDirtyWakanYield * claimFactor, room);
        if (requested > 0f)
        {
            var resourceContext = new CultivationResourceContext(actor, context.TileX, context.TileY);
            float claimed = CultivationResources.TileDirtyWakan.WithdrawUpTo(in resourceContext, requested);
            float absorbed = CultivationResources.AddPersonalDirtyWakan(actor, claimed);
            effectiveMonths += absorbed / CultivationResources.DeathDirtyWakanYield;
        }

        RecordPractice(actor, KillAbsorb, effectiveMonths);
    }

    [Hotfixable]
    private static void ExecuteKingdomFortune(in CultivationTriggerContext context)
    {
        ActorExtend actor = context.Practitioner;
        CultibookAsset cultibook = actor.GetMainCultibook();
        CultivationEfficiencyResult efficiency =
            CultivationEfficiencyResolver.Resolve(actor, cultibook, KingdomFortune);
        float maximum = Mathf.Max(0f, actor.Base.stats[BaseStatses.MaxWakan.id]);
        float monthlyLimit = maximum * FortuneMonthlyWakanRatio * efficiency.FinalMultiplier;
        float requested = monthlyLimit * context.ElapsedSeconds / TimeScales.SecPerMonth;
        CultivationSettlementResult result = CultivationSettlementService.ConvertToWakan(
            actor,
            CultivationResources.RoleFortune,
            requested,
            1f);
        RecordSettledPractice(in context, KingdomFortune, efficiency, requested, result);
    }

    private static float ResolveThreat(float practitionerPower, float opponentPower)
    {
        return Mathf.Clamp(Mathf.Pow(2f, (opponentPower - practitionerPower) * 0.5f), 0.25f, 4f);
    }

    /// <summary>按经过时间、修炼效率和实际灵气结算完成率换算标准有效修炼月数。</summary>
    private static void RecordSettledPractice(
        in CultivationTriggerContext context,
        CultivateMethodAsset method,
        CultivationEfficiencyResult efficiency,
        float requestedWakan,
        CultivationSettlementResult settlement)
    {
        if (context.ElapsedSeconds <= 0f || requestedWakan <= 0f || settlement.WakanGained <= 0f) return;
        float completion = Mathf.Clamp01(settlement.WakanGained / requestedWakan);
        float effectiveMonths = context.ElapsedSeconds / TimeScales.SecPerMonth *
                                efficiency.FinalMultiplier * completion;
        RecordPractice(context.Practitioner, method, effectiveMonths);
    }

    /// <summary>把统一量纲的实践归入修炼方式，并冻结本次实践对应的元素暴露。</summary>
    private static void RecordPractice(
        ActorExtend actor,
        CultivateMethodAsset method,
        float effectiveMonths)
    {
        if (actor == null || method == null || effectiveMonths <= 0f) return;
        ElementComposition? exposure = TryResolvePracticeExposure(method, out ElementComposition composition)
            ? composition
            : null;
        ref CultivationPracticeState state = ref actor.GetComponent<CultivationPracticeState>();
        state.Record(method.id, effectiveMonths, exposure);
        actor.MarkSemanticProfileDirty();
    }

    /// <summary>优先使用环境规则的精确组成，否则从修炼方式语义解析八维元素暴露。</summary>
    private static bool TryResolvePracticeExposure(
        CultivateMethodAsset method,
        out ElementComposition composition)
    {
        composition = method.EnvironmentRule?.TargetComposition ?? default;
        float total = 0f;
        for (var i = 0; i < ElementIndex.Count; i++) total += Mathf.Max(0f, composition[i]);
        if (total > 0.0001f) return true;
        return ElementSemanticProfileService.TryResolveComposition(method.Semantics, out composition);
    }
}
