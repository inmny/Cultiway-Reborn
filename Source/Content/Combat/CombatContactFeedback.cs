using Cultiway.Core.Skills;
using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Content.Combat;

/// <summary>依据已结算的材料和交锋结果选择声音与局部节奏。</summary>
internal static class CombatContactFeedback
{
    internal const string Wood = "event:/SFX/HIT/HitWood";
    internal const string Metal = "event:/SFX/HIT/HitMetal";
    internal const string MetalClash = "event:/SFX/HIT/HitSwordSword";
    internal const string Physical = "event:/SFX/HIT/HitGeneric";
    internal const string Stone = "event:/SFX/WEAPONS/WeaponRockLand";
    internal const string Water = "event:/SFX/DROPS/DropWaterBomb";
    internal const string Ice = "event:/SFX/WEAPONS/WeaponFreezeOrbLand";
    internal const string Fire = "event:/SFX/WEAPONS/WeaponFireballLand";
    internal const string Lightning = "event:/SFX/WEAPONS/WeaponPlasmaBallLand";

    /// <summary>依据实际物态，未配合适声音的材料留空。</summary>
    internal static string SoundFor(AbilityMatter matter) => matter?.DamageKind switch
    {
        "wood" => Wood,
        "iron" => Metal,
        "earth" => Stone,
        "water" => Water,
        "ice" => Ice,
        "fire" or "molten_iron" => Fire,
        "lightning" => Lightning,
        "physical" => Physical,
        _ => null,
    };

    /// <summary>只用实际消耗的抗力与存量判断是否强调一次交锋。</summary>
    internal static void Clash(Vector3 point, Vector3 normal, AbilityMatter surface, float spent, float remaining)
    {
        if (spent <= 0f) return;
        string sound = SoundFor(surface);
        SpatialCombatFeedback.Sound(sound == Metal ? MetalClash : sound, point);
        Vector3 tangent = new(-normal.y, normal.x, 0f);
        if (remaining > 0f)
        {
            if (sound == Wood)
            {
                // 还在承力的木面只掉两片靠近撞点的木屑，不震屏、不盖住墙面。
                Color splinter = new(.69f, .53f, .31f, .86f);
                for (int side = -1; side <= 1; side += 2)
                    SpatialVisualEffects.Particle(point + tangent * (.036f * side),
                        tangent * (.75f * side) + Vector3.forward * .40f, splinter, .09f, .16f,
                        SpatialParticleMaterial.Fragment, -2f);
            }
            return;
        }
        if (spent < 4f || !SpatialCombatFeedback.Shake(point, .14f)) return;

        Color color = sound switch
        {
            Wood => new Color(.70f, .51f, .33f, .9f),
            Stone => new Color(.67f, .57f, .48f, .9f),
            Water or Ice => new Color(.46f, .72f, .85f, .9f),
            Fire => new Color(.95f, .52f, .23f, .9f),
            _ => new Color(.73f, .79f, .85f, .9f),
        };
        for (int i = -1; i <= 1; i++)
        {
            Vector3 offset = tangent * (i * .105f);
            Vector3 velocity = normal * ((i == 0 ? -1f : 1f) * .42f) + tangent * (i * .66f) + Vector3.forward * .6f;
            SpatialVisualEffects.Particle(point + offset, velocity, color, i == 0 ? .235f : .17f,
                .22f, SpatialParticleMaterial.Fragment, -2f, hold: .055f);
        }
    }
}
