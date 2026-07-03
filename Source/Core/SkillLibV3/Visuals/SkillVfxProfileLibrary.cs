namespace Cultiway.Core.SkillLibV3.Visuals;

public class SkillVfxProfileLibrary : AssetLibrary<SkillVfxProfileAsset>
{
    public static SkillVfxProfileAsset Generic { get; private set; }
    public static SkillVfxProfileAsset Metal { get; private set; }
    public static SkillVfxProfileAsset Wood { get; private set; }
    public static SkillVfxProfileAsset Water { get; private set; }
    public static SkillVfxProfileAsset Fire { get; private set; }
    public static SkillVfxProfileAsset Earth { get; private set; }
    public static SkillVfxProfileAsset Neg { get; private set; }
    public static SkillVfxProfileAsset Pos { get; private set; }
    public static SkillVfxProfileAsset Entropy { get; private set; }
    public static SkillVfxProfileAsset Wind { get; private set; }
    public static SkillVfxProfileAsset Lightning { get; private set; }

    public override void init()
    {
        base.init();
        Generic = Add(SkillVfxElementStyle.Generic, 0.06f, 1.08f, 1f, 0.7f, 1.3f, 1.55f);
        Metal = Add(SkillVfxElementStyle.Metal, 0.055f, 1.05f, 0.95f, 0.58f, 1.18f, 1.3f);
        Wood = Add(SkillVfxElementStyle.Wood, 0.06f, 1.05f, 0.92f, 0.56f, 1.16f, 1.24f);
        Water = Add(SkillVfxElementStyle.Water, 0.058f, 1.08f, 1f, 0.68f, 1.28f, 1.5f);
        Fire = Add(SkillVfxElementStyle.Fire, 0.052f, 1.18f, 1.1f, 0.76f, 1.55f, 1.85f);
        Earth = Add(SkillVfxElementStyle.Earth, 0.066f, 1.12f, 1f, 0.62f, 1.32f, 1.45f);
        Neg = Add(SkillVfxElementStyle.Neg, 0.052f, 1.1f, 1.04f, 0.64f, 1.42f, 1.58f,
            impactFixedUpright: true, residualFixedUpright: true);
        Pos = Add(SkillVfxElementStyle.Pos, 0.05f, 1.14f, 1.06f, 0.68f, 1.48f, 1.7f);
        Entropy = Add(SkillVfxElementStyle.Entropy, 0.046f, 1.2f, 1.1f, 0.76f, 1.62f, 1.9f,
            impactFixedUpright: true, residualFixedUpright: true);
        Wind = Add(SkillVfxElementStyle.Wind, 0.048f, 1.05f, 1f, 0.66f, 1.35f, 1.65f,
            impactFixedUpright: true, residualFixedUpright: true);
        Lightning = Add(SkillVfxElementStyle.Lightning, 0.04f, 1.12f, 1.05f, 0.58f, 1.45f, 1.55f,
            impactFixedUpright: true, residualFixedUpright: true);
    }

    public SkillVfxProfileAsset GetByStyle(SkillVfxElementStyle style)
    {
        return get(GetId(style)) ?? Generic;
    }

    private SkillVfxProfileAsset Add(SkillVfxElementStyle style, float trailInterval, float castScale,
        float muzzleScale, float trailScale, float impactScale, float residualScale, bool castFixedUpright = false,
        bool impactFixedUpright = false, bool residualFixedUpright = false)
    {
        return add(SkillVfxProfileAsset.Create(GetId(style), style, trailInterval, castScale, muzzleScale,
            trailScale, impactScale, residualScale, castFixedUpright, impactFixedUpright, residualFixedUpright));
    }

    private static string GetId(SkillVfxElementStyle style)
    {
        return style switch
        {
            SkillVfxElementStyle.Metal => "cultiway.vfx.metal",
            SkillVfxElementStyle.Wood => "cultiway.vfx.wood",
            SkillVfxElementStyle.Water => "cultiway.vfx.water",
            SkillVfxElementStyle.Fire => "cultiway.vfx.fire",
            SkillVfxElementStyle.Earth => "cultiway.vfx.earth",
            SkillVfxElementStyle.Neg => "cultiway.vfx.neg",
            SkillVfxElementStyle.Pos => "cultiway.vfx.pos",
            SkillVfxElementStyle.Entropy => "cultiway.vfx.entropy",
            SkillVfxElementStyle.Wind => "cultiway.vfx.wind",
            SkillVfxElementStyle.Lightning => "cultiway.vfx.lightning",
            _ => "cultiway.vfx.generic"
        };
    }
}
