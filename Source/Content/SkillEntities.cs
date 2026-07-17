using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Editor;
using DeliveryTag = Cultiway.Core.Semantics.SkillSemantics.Delivery;
using ElementTag = Cultiway.Core.Semantics.SkillSemantics.Element;
using FormTag = Cultiway.Core.Semantics.SkillSemantics.Form;
using MotionTag = Cultiway.Core.Semantics.SkillSemantics.Motion;
using SeriesTag = Cultiway.Core.Semantics.SkillSemantics.Theme;

namespace Cultiway.Content;

[Dependency(typeof(SkillVfxElements), typeof(SkillTrajectories), typeof(SkillModifiers),
    typeof(SkillCastResources))]
public class SkillEntities : ExtendLibrary<SkillEntityAsset, SkillEntities>
{
    private const float UpFacingSpriteToRightOffset = -90f;

    public static SkillEntityAsset GoldSword { get; private set; }
    public static SkillEntityAsset GoldBlade { get; private set; }
    public static SkillEntityAsset WoodThorn { get; private set; }
    public static SkillEntityAsset FallWood { get; private set; }
    public static SkillEntityAsset WaterArrow { get; private set; }
    public static SkillEntityAsset WaterBall { get; private set; }
    public static SkillEntityAsset WaterBlade { get; private set; }
    public static SkillEntityAsset Fireball { get; private set; }
    public static SkillEntityAsset FireBlade { get; private set; }
    public static SkillEntityAsset FallStone { get; private set; }
    public static SkillEntityAsset StoneThorn { get; private set; }
    public static SkillEntityAsset WindBlade { get; private set; }
    public static SkillEntityAsset WindPolo { get; private set; }
    public static SkillEntityAsset Tornado { get; private set; }
    public static SkillEntityAsset FallLightning { get; private set; }
    public static SkillEntityAsset LightningPolo { get; private set; }

    protected override bool AutoRegisterAssets() => true;

    protected override void OnInit()
    {
        var metal = new ElementComposition(iron: 1f);
        var wood = new ElementComposition(wood: 1f);
        var water = new ElementComposition(water: 1f);
        var fire = new ElementComposition(fire: 1f);
        var earth = new ElementComposition(earth: 1f);
        var wind = new ElementComposition(water: 0.5f, wood: 0.5f);
        var lightning = new ElementComposition(water: 0.5f, fire: 0.5f);

        Configure(GoldSword, metal, "cultiway/effect/gold_sword", SkillTrajectories.TowardsDirection, 1f, true,
            SkillHitResolver.Single(GoldSword, recycleOnHit: true, continueAfterHit: false),
            SeriesTag.Metal, FormTag.Slash, FormTag.Single, DeliveryTag.Projectile)
            .AcceptOrientations(TrajectoryOrientation.Horizontal | TrajectoryOrientation.Vertical);
        Configure(GoldBlade, metal, "cultiway/effect/gold_blade", SkillTrajectories.TowardsDirection, 1.5f, false,
            SkillHitResolver.Single(GoldBlade, recycleOnHit: false, continueAfterHit: true),
            SeriesTag.Metal, FormTag.Slash, FormTag.Sustain, DeliveryTag.Projectile)
            .AcceptOrientations(TrajectoryOrientation.Horizontal | TrajectoryOrientation.Vertical);
        Configure(WoodThorn, wood, "cultiway/effect/wood_thorn", SkillTrajectories.TowardsDirection, 1.2f, false,
            VisualRotation.FollowRotation(UpFacingSpriteToRightOffset),
            SkillHitResolver.Single(WoodThorn, recycleOnHit: false, continueAfterHit: true),
            ElementTag.Wood, FormTag.Pierce, FormTag.Single, DeliveryTag.Projectile)
            .AcceptOrientations(TrajectoryOrientation.Horizontal | TrajectoryOrientation.Vertical);
        Configure(FallWood, wood, "cultiway/effect/fall_wood", SkillTrajectories.FallingStrike, 1.2f, true,
            SkillHitResolver.Single(FallWood, recycleOnHit: true, continueAfterHit: true),
            ElementTag.Wood, FormTag.Pierce, FormTag.Single, MotionTag.Falling, DeliveryTag.Projectile)
            .SetModifierWeightMultiplier(SkillModifiers.Huge, 4f)
            .AcceptOrientations(TrajectoryOrientation.Horizontal | TrajectoryOrientation.Vertical);
        Configure(WaterArrow, water, "cultiway/effect/single_water_sword", SkillTrajectories.TowardsDirection, 1f,
            true, SkillHitResolver.Single(WaterArrow, recycleOnHit: true, continueAfterHit: false),
            ElementTag.Water, FormTag.Pierce, FormTag.Single, DeliveryTag.Projectile)
            .AcceptOrientations(TrajectoryOrientation.Horizontal | TrajectoryOrientation.Vertical);
        Configure(WaterBall, water, "cultiway/effect/water_polo", SkillTrajectories.TowardsDirection, 1f, true,
            SkillHitResolver.Single(WaterBall, recycleOnHit: true, continueAfterHit: false),
            ElementTag.Water, FormTag.Ball, FormTag.Single, DeliveryTag.Projectile)
            .AcceptOrientations(TrajectoryOrientation.Horizontal | TrajectoryOrientation.Vertical);
        Configure(WaterBlade, water, "cultiway/effect/water_blade", SkillTrajectories.TowardsDirection, 1.5f, false,
            SkillHitResolver.Single(WaterBlade, recycleOnHit: false, continueAfterHit: true),
            ElementTag.Water, FormTag.Slash, FormTag.Sustain, DeliveryTag.Projectile);
        Configure(Fireball, fire, "cultiway/effect/fire_polo", SkillTrajectories.TowardsDirection, 1f, true,
            SkillHitResolver.Area(Fireball, radius: 2f, recycleOnHit: true),
            ElementTag.Fire, FormTag.Ball, FormTag.Aoe, DeliveryTag.Projectile)
            .AddAnimation("cultiway/effect/flying_fireball", 0.025f)
            .AddAnimation("cultiway/effect/flying_missile1", 0.025f)
            .AddAnimation("cultiway/effect/flying_missile2", 0.025f)
            .AddAnimation("cultiway/effect/flying_missile3", 0.025f)
            .AddAnimation("cultiway/effect/flying_missile4", 0.025f)
            .AddAnimation("cultiway/effect/flying_missile5", 0.025f)
            .AddAnimation("cultiway/effect/flying_missile6", 0.025f)
            .AcceptOrientations(TrajectoryOrientation.Horizontal | TrajectoryOrientation.Vertical);
        Configure(FireBlade, fire, "cultiway/effect/fire_blade", SkillTrajectories.TowardsDirection, 1.5f, false,
            SkillHitResolver.Single(FireBlade, recycleOnHit: false, continueAfterHit: true),
            ElementTag.Fire, FormTag.Slash, FormTag.Sustain, DeliveryTag.Projectile)
            .AddAnimation("cultiway/effect/fire_blade2", 0.025f);
        Configure(FallStone, earth, "cultiway/effect/fall_rock", SkillTrajectories.FallingStrike, 1.2f, true,
            SkillHitResolver.Single(FallStone, recycleOnHit: true, continueAfterHit: true),
            ElementTag.Earth, FormTag.Pierce, FormTag.Single, MotionTag.Falling, DeliveryTag.Projectile)
            .SetModifierWeightMultiplier(SkillModifiers.Huge, 4f)
            .AcceptOrientations(TrajectoryOrientation.Horizontal | TrajectoryOrientation.Vertical);
        Configure(StoneThorn, earth, "cultiway/effect/ground_thorn", SkillTrajectories.GroundCrawl, 1.2f, false,
            VisualRotation.FollowRotation(UpFacingSpriteToRightOffset),
            SkillHitResolver.Single(StoneThorn, recycleOnHit: false, continueAfterHit: true),
            ElementTag.Earth, FormTag.Pierce, FormTag.Sustain, DeliveryTag.Projectile)
            .AcceptOrientations(TrajectoryOrientation.Ground);
        Configure(WindBlade, wind, "cultiway/effect/wind_blade", SkillTrajectories.TowardsDirection, 1.5f, false,
            SkillHitResolver.Single(WindBlade, recycleOnHit: false, continueAfterHit: true),
            ElementTag.Wind, FormTag.Slash, FormTag.Sustain, DeliveryTag.Projectile);
        Configure(WindPolo, wind, "cultiway/effect/wind_polo", SkillTrajectories.TowardsDirection, 1f, true,
            SkillHitResolver.Single(WindPolo, recycleOnHit: true, continueAfterHit: false),
            ElementTag.Wind, FormTag.Ball, FormTag.Single, DeliveryTag.Projectile)
            .AcceptOrientations(TrajectoryOrientation.Horizontal | TrajectoryOrientation.Vertical);
        Configure(Tornado, wind, "cultiway/effect/simple_tornado", SkillTrajectories.SlowVortex, 1.5f, true,
            VisualRotation.FixedUpright(),
            SkillHitResolver.Single(Tornado, recycleOnHit: false, continueAfterHit: true),
            ElementTag.Wind, FormTag.Aoe, FormTag.Sustain, DeliveryTag.Field)
            .SetModifierWeightMultiplier(SkillModifiers.Huge, 4f)
            .AcceptOrientations(TrajectoryOrientation.Horizontal | TrajectoryOrientation.Appear)
            .AddAnimation("cultiway/effect/simple_tornado2", 0.025f);
        Configure(FallLightning, lightning, "cultiway/effect/default_lightning", SkillTrajectories.AppearAtTarget, 0.5f,
            false, VisualRotation.FixedUpright(),
            SkillHitResolver.Single(FallLightning, recycleOnHit: true, continueAfterHit: true),
            ElementTag.Lightning, FormTag.Single, MotionTag.Falling, DeliveryTag.Instant)
            // 落雷动画本身从上到下竖直播放，只允许原地显现，排除任何带水平位移的轨迹。
            .AcceptOrientations(TrajectoryOrientation.Appear)
            .AddAnimation("cultiway/effect/default_lightning2", 0.025f)
            .AddAnimation("cultiway/effect/default_lightning3", 0.025f)
            .AddAnimation("cultiway/effect/default_lightning4", 0.025f)
            .AddAnimation("cultiway/effect/default_lightning5", 0.025f)
            .AddAnimation("cultiway/effect/default_lightning6", 0.025f)
            .AddAnimation("cultiway/effect/default_lightning7", 0.025f)
            .AddAnimation("cultiway/effect/default_lightning8", 0.025f)
            .AddAnimation("cultiway/effect/default_lightning9", 0.025f)
            .AddAnimation("cultiway/effect/default_lightning10", 0.025f);
        Configure(LightningPolo, lightning, "cultiway/effect/lightning_polo", SkillTrajectories.LightningSnap, 1f,
            true, SkillHitResolver.Single(LightningPolo, recycleOnHit: true, continueAfterHit: false),
            ElementTag.Lightning, FormTag.Ball, FormTag.Single, DeliveryTag.Projectile)
            .AcceptOrientations(TrajectoryOrientation.Horizontal | TrajectoryOrientation.Vertical)
            .AddAnimation("cultiway/effect/lightning_polo2", 0.025f);
    }

    private static SkillEntityAsset Configure(SkillEntityAsset asset, ElementComposition element, string effectPath,
        TrajectoryAsset trajectory, float colliderRadius, bool animLoop, OnObjCollision onCollision,
        params SemanticAsset[] semantics)
    {
        asset.Element = element;
        asset.AddSemantics(semantics);
        asset.RequireCastResource(SkillCastResources.Wakan);
        asset.SetupCommonPrefab(effectPath, anim_loop: animLoop)
            .SetupColliderSphere(colliderRadius, EnemyActorCollider())
            .SetupDefaultTraj(trajectory)
            .OnObjCollision = onCollision;
        ConfigureLearnableAsset(asset);
        return asset;
    }

    private static SkillEntityAsset Configure(SkillEntityAsset asset, ElementComposition element, string effectPath,
        TrajectoryAsset trajectory, float colliderRadius, bool animLoop, VisualRotation visualRotation,
        OnObjCollision onCollision, params SemanticAsset[] semantics)
    {
        asset.Element = element;
        asset.AddSemantics(semantics);
        asset.RequireCastResource(SkillCastResources.Wakan);
        asset.SetupCommonPrefab(effectPath, anim_loop: animLoop)
            .SetupVisualRotation(visualRotation)
            .SetupColliderSphere(colliderRadius, EnemyActorCollider())
            .SetupDefaultTraj(trajectory)
            .OnObjCollision = onCollision;
        ConfigureLearnableAsset(asset);
        return asset;
    }

    private static void ConfigureLearnableAsset(SkillEntityAsset asset)
    {
        asset.AllowLearning();
        asset.EditorCategoryKey = "Cultiway.SkillEntity.Category.Attack";
        asset.EditorSortOrder = ModClass.I.SkillV3.SkillLib.list.IndexOf(asset);
        asset.EditorSelectable = true;
    }

    private static ColliderConfig EnemyActorCollider()
    {
        return new ColliderConfig
        {
            Enabled = true,
            Enemy = true,
            Actor = true
        };
    }
}
