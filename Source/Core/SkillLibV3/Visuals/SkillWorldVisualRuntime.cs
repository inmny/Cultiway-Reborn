using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Visuals;

/// <summary>消费真实结算事件，生成可在原版和空间视角中共用的反馈。</summary>
internal static class SkillWorldVisualRuntime
{
    /// <summary>从已经确认发生变化的位置发射，不补画整个作用范围。</summary>
    /// <param name="profile">实际结果对应的材质表现。</param><param name="position">发生变化的位置。</param>
    internal static void ShowLocal(SkillWorldVisualProfile profile, Vector3 position)
    {
        SkillLocalEffectVisualKind kind = profile.LocalEffect;
        SpatialParticleMaterial material = kind switch
        {
            SkillLocalEffectVisualKind.Healing or SkillLocalEffectVisualKind.Rejuvenation or
                SkillLocalEffectVisualKind.NatureGrowth or SkillLocalEffectVisualKind.Fertilize => SpatialParticleMaterial.Leaf,
            SkillLocalEffectVisualKind.RaiseTerrain or SkillLocalEffectVisualKind.LowerTerrain or
                SkillLocalEffectVisualKind.GuardBlessing or SkillLocalEffectVisualKind.CleanLand => SpatialParticleMaterial.Fragment,
            SkillLocalEffectVisualKind.FillWater or SkillLocalEffectVisualKind.DrainWater => SpatialParticleMaterial.Water,
            _ => SpatialParticleMaterial.Energy,
        };
        bool down = kind is SkillLocalEffectVisualKind.LowerTerrain or SkillLocalEffectVisualKind.DrainWater;
        bool inward = down || kind is SkillLocalEffectVisualKind.Healing or SkillLocalEffectVisualKind.Purification;
        float lift = down ? -.35f : material == SpatialParticleMaterial.Leaf ? .7f : .5f;
        float gravity = material is SpatialParticleMaterial.Fragment or SpatialParticleMaterial.Water ? -1.4f : 0f;
        int count = material is SpatialParticleMaterial.Fragment or SpatialParticleMaterial.Water ? 2 : 4;
        for (int i = 0; i < count; i++)
        {
            float angle = i * 2.3999632f + position.x * .8f + position.y * 1.3f;
            Vector3 side = new(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 start = position + side * (inward ? .21f : .04f) + Vector3.forward * (down ? .22f : .04f);
            Vector3 velocity = side * (inward ? -.45f : .45f) + Vector3.forward * lift;
            SpatialVisualEffects.Particle(start, velocity, i == 0 && material == SpatialParticleMaterial.Energy
                    ? profile.GlowColor : profile.PrimaryColor,
                material == SpatialParticleMaterial.Energy ? .06f : .14f, down ? .29f : .45f, material, gravity);
        }
    }
}
