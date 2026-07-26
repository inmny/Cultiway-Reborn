using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Core.SkillLibV3;

namespace Cultiway.Content;

/// <summary>为每个形成效果建立保存机制数据和循环动画的普通共享状态。</summary>
internal static class CoreFormationStatusFactory
{
    private const float FrameInterval = 0.05f;

    /// <summary>按效果定义建立独立状态资产，并按需绑定循环动画和运行时属性。</summary>
    internal static StatusEffectAsset Build(
        string key,
        CoreFormationEffectDefinition definition,
        CoreFormationActiveProfile active,
        string animationPath = null,
        float animationScale = 0.1f)
    {
        StatusEffectAsset.Builder builder = StatusEffectAsset
            .StartBuild($"CoreFormationState_{key}")
            .SetName(definition.GetName())
            .SetDescription(definition.GetDescription())
            .SetIconPath(active?.icon_path ?? StatusEffectAsset.DefaultIconPath)
            .SetDuration(CoreFormationStateService.PersistentDuration)
            .AddComponent(new CoreFormationEffectState());
        if (definition.family_id == CoreFormationEffectFamilies.Body && definition.rank >= 2)
        {
            builder.SetStats(new BaseStats
            {
                [BaseStatses.KnockbackReduction.id] = 8f
            });
        }
        if (!string.IsNullOrWhiteSpace(animationPath))
        {
            builder.EnableAnimation(
                SkillEntityAnimation.Create(
                    animationPath,
                    animationScale * CoreFormationSkills.AnimationScaleMultiplier,
                    SkillEntityAnimationSettings.Inherit
                        .WithFrameInterval(FrameInterval)
                        .WithLoop(true)),
                FrameInterval);
        }
        return builder.Build();
    }
}
