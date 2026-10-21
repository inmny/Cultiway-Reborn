using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>为每个形成效果建立保存机制数据和空间标记的普通共享状态。</summary>
internal static class CoreFormationStatusFactory
{

    /// <summary>按效果定义建立独立状态资产，并按需绑定空间标记和运行时属性。</summary>
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
            Color color = definition.family_id switch
            {
                CoreFormationEffectFamilies.Earth or CoreFormationEffectFamilies.Body => new Color(.74f, .55f, .3f, .28f),
                CoreFormationEffectFamilies.Sword => new Color(.76f, .85f, 1f, .3f),
                CoreFormationEffectFamilies.Reservoir => new Color(.3f, .8f, .65f, .25f),
                _ => new Color(.68f, .72f, 1f, .25f),
            };
            builder.SetSpatialVisual(new StatusSpatialSettings
            {
                Color = color,
                RadiusRatio = animationScale * 6f,
                Form = definition.family_id == CoreFormationEffectFamilies.Reservoir
                    ? StatusSpatialForm.Reserve : StatusSpatialForm.Guard,
            });
        }
        return builder.Build();
    }
}
