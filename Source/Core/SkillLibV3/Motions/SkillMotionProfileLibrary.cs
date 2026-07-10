using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Utils;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Motions;

public class SkillMotionProfileLibrary : AssetLibrary<SkillMotionProfileAsset>
{
    public static SkillMotionProfileAsset Generic { get; private set; }

    public override void init()
    {
        base.init();
        Generic = add(new SkillMotionProfileAsset
        {
            id = "Cultiway.SkillMotionProfile.Generic"
        });
        Generic.Configure(48f, 120f, 0.55f, 480f, 0.065f, 1.15f, 1f, 0.08f,
            new AnimAfterimage
            {
                SpacingRatio = 0.075f,
                MinSpacing = 0.45f,
                NewestAlpha = 0.3f,
                OldestAlpha = 0.025f,
                LocalDirection = Vector2.left,
                Tint = Color.white
            })
            .ConfigureAfterimageDensity(20f);
    }

    public SkillMotionProfileAsset Resolve(SkillContainer container, Entity containerEntity)
    {
        var tags = SkillSemanticTags.NewSet();
        SkillSemanticTags.CollectAssetTags(container.Asset, tags);
        SkillSemanticTags.CollectModifierTags(containerEntity, tags);
        SkillSemanticTags.CollectTrajectoryTags(container.Asset, containerEntity, tags);

        var bestScore = -1;
        SkillMotionProfileAsset best = null;
        foreach (var profile in list)
        {
            var score = profile.ScoreTags(tags);
            if (score <= bestScore) continue;

            bestScore = score;
            best = profile;
        }

        return bestScore < 0 ? get(Generic.id) : best;
    }
}
