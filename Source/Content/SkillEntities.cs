using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.Semantics;

namespace Cultiway.Content;

[Dependency(typeof(SkillVfxElements), typeof(SkillTrajectories), typeof(SkillModifiers),
    typeof(SkillCastResources))]
public partial class SkillEntities : ExtendLibrary<SkillEntityAsset, SkillEntities>
{
    private static int nextEditorSortOrder;

    protected override bool AutoRegisterAssets() => true;

    protected override void OnInit()
    {
        nextEditorSortOrder = 0;
        ConfigureMetal();
        ConfigureWood();
        ConfigureWater();
        ConfigureIce();
        ConfigurePoison();
        ConfigureFire();
        ConfigureEarth();
        ConfigureYinYangEntropy();
        ConfigureWind();
        ConfigureLightning();
        ConfigureGeneric();
    }

    private static SkillEntityAsset Configure(
        SkillEntityAsset asset,
        ElementComposition element,
        SkillEntityAnimation animation,
        TrajectoryAsset trajectory,
        SkillImpactProfileAsset impactProfile,
        SkillTrajectoryDomain domains,
        SkillUseProfileAsset useProfile,
        SkillEntityType type,
        bool animationLoop,
        VisualRotation? visualRotation,
        params SemanticAsset[] semantics)
    {
        asset.Element = element;
        asset.Type = type;
        asset.AddSemantics(semantics);
        asset.RequireCastResource(SkillCastResources.Wakan);
        asset.SetupCommonPrefab(animation, animationLoop);
        if (visualRotation.HasValue)
        {
            asset.SetupVisualRotation(visualRotation.Value);
        }

        bool contactCollision = !impactProfile.IsBarrier ||
                                asset.ImpactTuning.ContactDamage ||
                                asset.ImpactTuning.ContactForce > 0f;
        asset.SetupImpactProfile(impactProfile, EnemyCollider(contactCollision))
            .SetupDefaultTraj(trajectory)
            .SetupUseProfile(useProfile)
            .AcceptTrajectoryDomains(domains);
        if (!SkillTrajectoryCompatibility.IsCompatible(asset, trajectory))
        {
            throw new System.InvalidOperationException(
                $"{asset.id} 的默认轨迹 {trajectory.id} 不属于本体声明的运行形态");
        }
        asset.AllowLearning();
        asset.EditorCategoryKey = type == SkillEntityType.Defense
            ? "Cultiway.SkillEntity.Category.Defense"
            : "Cultiway.SkillEntity.Category.Attack";
        asset.EditorDescriptionKey = $"{asset.id}.Description";
        asset.EditorSortOrder = nextEditorSortOrder++;
        asset.EditorSelectable = true;
        return asset;
    }

    private static SkillEntityAnimation Anim(
        SkillEntityAsset asset,
        int variantIndex,
        float scale = 0.025f,
        float? frameRate = null,
        SkillAnimationGameplayFlags appearanceGameplay = SkillAnimationGameplayFlags.None,
        float appearanceFrameRate = 18f,
        float dissipationFrameRate = 18f)
    {
        string root = GetAnimationResourceRoot(asset, variantIndex);
        SkillEntityAnimationSettings runtimeSettings = frameRate.HasValue
            ? SkillEntityAnimationSettings.Inherit.WithFrameRate(frameRate.Value)
            : SkillEntityAnimationSettings.Inherit;
        SkillEntityAnimationSettings appearanceSettings =
            SkillEntityAnimationSettings.Inherit.WithFrameRate(appearanceFrameRate);
        SkillEntityAnimationSettings dissipationSettings =
            SkillEntityAnimationSettings.Inherit.WithFrameRate(dissipationFrameRate);

        return SkillEntityAnimation
            .Create($"{root}/runtime", scale, runtimeSettings)
            .WithAppearance($"{root}/appearance", appearanceSettings, appearanceGameplay)
            .WithDissipation($"{root}/dissipation", dissipationSettings);
    }

    private static string GetAnimationResourceRoot(SkillEntityAsset asset, int variantIndex)
    {
        int localIdStart = asset.id.LastIndexOf('.') + 1;
        string localId = asset.id.Substring(localIdStart);
        var folder = new System.Text.StringBuilder(localId.Length + 8);
        for (int i = 0; i < localId.Length; i++)
        {
            char character = localId[i];
            if (char.IsUpper(character) && i > 0)
            {
                folder.Append('_');
            }
            folder.Append(char.ToLowerInvariant(character));
        }

        return $"cultiway/effect/{folder}/{variantIndex}";
    }

    private static string RuntimeAnimPath(SkillEntityAsset asset, int variantIndex)
    {
        return $"{GetAnimationResourceRoot(asset, variantIndex)}/runtime";
    }

    private static ColliderConfig EnemyCollider(bool enabled)
    {
        return new ColliderConfig
        {
            Enabled = enabled,
            Enemy = true,
            Actor = enabled,
            Building = enabled
        };
    }
}
