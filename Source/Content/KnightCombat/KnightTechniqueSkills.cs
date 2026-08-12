using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Components.AnimOverwrite;
using Cultiway.Core.Progression;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Patch;
using Friflo.Engine.ECS;

namespace Cultiway.Content.KnightCombat;

/// <summary>建立九个可学习战技原型，并注册骑士战技运行时入口。</summary>
[Dependency(
    typeof(SkillCastResources),
    typeof(SkillMotionProfiles),
    typeof(SkillVfxElements),
    typeof(StatusEffects),
    typeof(EquippedWeaponTrajectories),
    typeof(KnightTechniqueCatalog))]
public sealed class KnightTechniqueSkills : ICanInit
{
    private static readonly Dictionary<string, SkillEntityAsset> Assets = new();
    private static readonly Dictionary<string, Entity> Containers = new();

    public static Entity GetContainer(Libraries.KnightTechniqueAsset technique)
    {
        if (Containers.TryGetValue(technique.id, out Entity container) && !container.IsNull) return container;

        container = BuildContainer(technique, Assets[technique.id]);
        Containers[technique.id] = container;
        return container;
    }

    /// <summary>补齐指定流派在当前骑士等级已经解锁的战技，并返回本次新增数量。</summary>
    public static int LearnStyle(ActorExtend actor, Libraries.KnightStyleAsset style)
    {
        var learnedCount = 0;
        IReadOnlyList<Libraries.KnightTechniqueAsset> techniques = KnightTechniqueCatalog.GetByStyle(style);
        for (var i = 0; i < techniques.Count; i++)
        {
            Libraries.KnightTechniqueAsset technique = techniques[i];
            if (TryLearnTechnique(actor, technique)) learnedCount++;
        }
        return learnedCount;
    }

    private static bool TryLearnTechnique(ActorExtend actor, Libraries.KnightTechniqueAsset technique)
    {
        if (actor.GetCultisys<Knight>().CurrLevel < technique.MinimumKnightLevel ||
            HasLearnedTechnique(actor, technique.id)) return false;
        return SkillOwnershipService.Learn(actor, GetContainer(technique), true) == SkillOwnershipResult.Added;
    }

    private static bool HasLearnedTechnique(ActorExtend actor, string techniqueId)
    {
        IReadOnlyList<Entity> learnedSkills = actor.GetLearnedSkillsInOrder();
        for (var i = 0; i < learnedSkills.Count; i++)
        {
            Entity skill = learnedSkills[i];
            if (skill.IsNull || !skill.HasComponent<SpecializedActiveAbility>()) continue;
            SpecializedActiveAbility specialized = skill.GetComponent<SpecializedActiveAbility>();
            if (specialized.ProviderId == KnightTechniqueAbilityProvider.ProviderId &&
                specialized.EntryId == techniqueId) return true;
        }
        return false;
    }

    public void Init()
    {
        Assets.Clear();
        Containers.Clear();
        KnightTechniqueStatuses.Init();
        for (var i = 0; i < KnightTechniqueCatalog.All.Count; i++)
        {
            Libraries.KnightTechniqueAsset technique = KnightTechniqueCatalog.All[i];
            Assets.Add(technique.id, BuildAsset(technique));
        }

        KnightWeaponStrikeResolver.Init();
        KnightTechniqueStatusBridge.Init();
        KnightTechniqueVisuals.Init();
        ActiveAbilityService.Register(new KnightTechniqueAbilityProvider());
        ProgressionLifecycle.RegisterCommitted(OnProgressionCommitted);
        PatchMapBox.RegisterActionOnClearWorld(ClearWorldState);
    }

    private static void OnProgressionCommitted(ProgressionCommittedEvent evt)
    {
        if (evt.Cultisys != Cultisyses.Knight) return;

        IReadOnlyList<Libraries.KnightTechniqueAsset> techniques = KnightTechniqueCatalog.All;
        for (var i = 0; i < techniques.Count; i++)
        {
            Libraries.KnightTechniqueAsset technique = techniques[i];
            if (KnightStyleMasteryService.IsMastered(evt.Actor, technique.Style))
                TryLearnTechnique(evt.Actor, technique);
        }
    }

    private static SkillEntityAsset BuildAsset(Libraries.KnightTechniqueAsset technique)
    {
        string assetId = technique.id + ".Execution";
        var impact = new SkillImpactProfileAsset
        {
            id = assetId + ".Impact",
            Kind = technique.Style == KnightStyles.Guardian ? SkillImpactKind.GroundWave : SkillImpactKind.Wave,
            CollisionRadius = 0.01f,
            DamageMultiplier = 0f,
            ContinueAfterHit = true,
            HitOncePerTarget = true,
            Lifetime = 0.8f,
            CostMultiplier = 1f,
            ExpectedTargets = technique.ActiveUse.ResolveEffectRadius == null ? 1f : 2f,
        };
        var asset = new SkillEntityAsset
        {
            id = assetId,
            Element = new ElementComposition(),
            Type = technique.Style == KnightStyles.Guardian
                ? SkillEntityType.Defense
                : SkillEntityType.Attack,
            EditorSelectable = false,
            EditorCategoryKey = "Cultiway.SkillEntity.Category.Attack",
            EditorDescriptionKey = technique.DescriptionKey,
        };
        asset.AddSemantics(
                SkillSemantics.Element.Generic,
                technique.Style == KnightStyles.Guardian
                    ? SkillSemantics.Role.Defensive
                    : SkillSemantics.Role.Offensive)
            .RequireCastResource(SkillCastResources.Vigor)
            .SetBaseCastDemand(technique.VigorCost)
            .SetDealsBaseDamage(false)
            .SetIcon(technique.IconPath)
            .SetupCommonPrefab("cultiway/effect/flying_sword/1/runtime", 1f, true)
            .SetupImpactProfile(impact, new ColliderConfig
            {
                Enabled = false,
                Actor = false,
                Building = false,
                Enemy = false,
            })
            .SetupDefaultTraj(EquippedWeaponTrajectories.Motion)
             .SetupUseProfile(technique.ActiveUse.TargetMode == ActiveAbilityTargetMode.Self
                 ? SkillUseProfileLibrary.CasterSelf
                 : SkillUseProfileLibrary.EnemyObjectOrPoint)
             .AcceptTrajectoryDomains(SkillTrajectoryDomain.Melee)
            .SetupVisualRotation(VisualRotation.FollowRotation())
            .AllowLearning();
        asset.PrefabEntity.AddComponent(new AnimRuntimeFrames());
        asset.PrefabEntity.AddComponent(new AnimAfterimageOverride());
        asset.PrefabEntity.AddComponent(new MotionRibbonTrail());
        asset.PrefabEntity.AddComponent(new MotionRibbonTrailBinder());
        ModClass.I.SkillV3.SkillLib.add(asset);
        return asset;
    }

    private static Entity BuildContainer(
        Libraries.KnightTechniqueAsset technique,
        SkillEntityAsset asset)
    {
        Entity container = new SkillContainerBuilder(asset).Build(SkillContainerBuildMode.Preview);
        container.AddComponent(new SpecializedActiveAbility
        {
            ProviderId = KnightTechniqueAbilityProvider.ProviderId,
            EntryId = technique.id,
        });
        container.GetComponent<EntityName>().value = technique.ResolveName();
        return container;
    }

    private static void ClearWorldState()
    {
        Containers.Clear();
        KnightWeaponStrikeResolver.ClearWorldState();
        KnightTechniqueStatusBridge.ClearWorldState();
        KnightTechniqueVisuals.ClearWorldState();
        KnightTechniqueRuntimeService.ClearWorldState();
    }
}
