using Cultiway.Const;
using Cultiway.Core.Libraries;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Visuals;

internal static class SkillTrailParticleManager
{
    private const float BaseSize = 0.16f;
    private const float BaseLifetime = 0.42f;
    private const float BackwardSpeed = 0.62f;
    private const int MaxParticles = 4096;

    private static ParticleSystem _sharedEmitter;
    private static readonly Gradient FadeOutGradient = new()
    {
        colorKeys = new[]
        {
            new GradientColorKey(Color.white, 0f),
            new GradientColorKey(Color.white, 1f)
        },
        alphaKeys = new[]
        {
            new GradientAlphaKey(0.9f, 0f),
            new GradientAlphaKey(0.35f, 0.45f),
            new GradientAlphaKey(0f, 1f)
        }
    };

    public static void Emit(SkillVfxProfileAsset profile, Color color, Color accentColor, SkillVfxWeight weight,
        Vector3 position, Vector3 direction, float intensity, float trailWidth, float alpha)
    {
        if (!MapBox.isRenderGameplay()) return;

        var emitter = GetEmitter();
        if (emitter == null) return;

        var main = emitter.main;
        main.simulationSpeed = Mathf.Max(0.01f, Config.time_scale_asset.multiplier);

        direction = SafeDirection(direction);
        var side = new Vector3(-direction.y, direction.x, 0f);
        var settings = GetSettings(profile.Style, weight, intensity);
        var laneCount = GetLaneCount(trailWidth);
        var particlesPerLane = Mathf.Max(1, Mathf.CeilToInt(settings.Count / (float)laneCount));
        color.a = Mathf.Clamp01(alpha * settings.AlphaScale * GetWidthAlphaScale(trailWidth));
        accentColor.a = Mathf.Clamp01(alpha * settings.AccentAlphaScale * GetWidthAlphaScale(trailWidth));

        for (var lane = 0; lane < laneCount; lane++)
        {
            var laneOffset = GetLaneOffset(lane, laneCount, trailWidth);
            for (var i = 0; i < particlesPerLane; i++)
            {
                var sign = (i + lane) % 2 == 0 ? 1f : -1f;
                var spread = Random.Range(0.01f, settings.Spread) * sign;
                var back = Random.Range(0.02f, 0.12f + intensity * 0.02f);
                var jittered = position - direction * back + side * (laneOffset + spread);
                var velocity = -direction * settings.BackSpeed
                               + side * Random.Range(-settings.SideSpeed, settings.SideSpeed)
                               + RandomInside(settings.RandomSpeed);
                var emitParams = new ParticleSystem.EmitParams
                {
                    position = jittered,
                    velocity = velocity,
                    startColor = i == 0 || Random.value > 0.45f ? color : accentColor,
                    startSize = settings.Size * GetLaneSizeScale(lane, laneCount) * Random.Range(0.82f, 1.32f),
                    startLifetime = settings.Lifetime * Random.Range(0.9f, 1.28f)
                };
                emitter.Emit(emitParams, 1);
            }
        }
    }

    private static ParticleSystem GetEmitter()
    {
        if (_sharedEmitter != null) return _sharedEmitter;

        var parent = World.world?.transform;
        if (parent == null) return null;

        var emitterObject = new GameObject("Cultiway_SkillTrailParticles_Global");
        emitterObject.transform.SetParent(parent, false);
        emitterObject.transform.localPosition = Vector3.zero;

        var particleSystem = emitterObject.AddComponent<ParticleSystem>();
        var main = particleSystem.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = BaseLifetime;
        main.startSpeed = 0f;
        main.startSize = BaseSize;
        main.maxParticles = MaxParticles;

        var emission = particleSystem.emission;
        emission.enabled = false;

        var shape = particleSystem.shape;
        shape.enabled = false;

        var colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(FadeOutGradient);

        var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        renderer.sortingOrder = -2;
        if (LibraryMaterials.instance != null)
        {
            renderer.material = LibraryMaterials.instance.mat_world_object;
        }

        particleSystem.Play();
        _sharedEmitter = particleSystem;
        return _sharedEmitter;
    }

    private static TrailParticleSettings GetSettings(SkillVfxElementStyle style, SkillVfxWeight weight,
        float intensity)
    {
        var weightScale = weight == SkillVfxWeight.Medium ? 1.18f : 1f;
        var count = weight == SkillVfxWeight.Medium ? 6 : 4;
        var size = BaseSize * Mathf.Lerp(0.9f, 1.35f, Mathf.InverseLerp(1f, 2.1f, intensity)) * weightScale;
        var lifetime = BaseLifetime * weightScale;

        return style switch
        {
            SkillVfxElementStyle.Fire => new TrailParticleSettings(count + 1, size * 1.08f, lifetime * 0.86f,
                0.11f, BackwardSpeed * 1.15f, 0.16f, 0.08f, 1.05f, 1.2f),
            SkillVfxElementStyle.Water => new TrailParticleSettings(count, size * 0.9f, lifetime * 1.16f,
                0.08f, BackwardSpeed * 0.85f, 0.1f, 0.05f, 0.9f, 1.15f),
            SkillVfxElementStyle.Wood => new TrailParticleSettings(count, size * 0.96f, lifetime * 1.12f,
                0.12f, BackwardSpeed * 0.78f, 0.14f, 0.06f, 0.92f, 1.12f),
            SkillVfxElementStyle.Metal => new TrailParticleSettings(count, size * 0.78f, lifetime * 0.82f,
                0.07f, BackwardSpeed * 1.22f, 0.08f, 0.05f, 1f, 1.25f),
            SkillVfxElementStyle.Earth => new TrailParticleSettings(count + 1, size * 1.02f, lifetime * 1.25f,
                0.1f, BackwardSpeed * 0.62f, 0.08f, 0.04f, 0.82f, 1f),
            SkillVfxElementStyle.Lightning => new TrailParticleSettings(count, size * 0.72f, lifetime * 0.58f,
                0.12f, BackwardSpeed * 1.55f, 0.22f, 0.1f, 1.15f, 1.35f),
            SkillVfxElementStyle.Wind => new TrailParticleSettings(count, size * 0.82f, lifetime * 1.08f,
                0.16f, BackwardSpeed * 0.92f, 0.22f, 0.07f, 0.74f, 0.92f),
            SkillVfxElementStyle.Neg => new TrailParticleSettings(count + 1, size * 1.08f, lifetime * 1.18f,
                0.13f, BackwardSpeed * 0.72f, 0.12f, 0.06f, 0.95f, 1.14f),
            SkillVfxElementStyle.Pos => new TrailParticleSettings(count, size * 0.92f, lifetime * 0.94f,
                0.1f, BackwardSpeed * 1.05f, 0.12f, 0.05f, 1f, 1.22f),
            SkillVfxElementStyle.Entropy => new TrailParticleSettings(count + 1, size * 0.88f, lifetime * 0.72f,
                0.18f, BackwardSpeed * 1.28f, 0.2f, 0.12f, 1.05f, 1.32f),
            _ => new TrailParticleSettings(count, size, lifetime, 0.1f, BackwardSpeed, 0.12f, 0.06f, 0.9f, 1.1f)
        };
    }

    private static Vector3 SafeDirection(Vector3 direction)
    {
        direction.z = 0f;
        return direction.sqrMagnitude < 0.0001f ? Vector3.right : direction.normalized;
    }

    private static Vector3 RandomInside(float speed)
    {
        var angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        var magnitude = Random.Range(0f, speed);
        return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * magnitude;
    }

    private static int GetLaneCount(float trailWidth)
    {
        return Mathf.Clamp(Mathf.CeilToInt(trailWidth / 0.78f), 1, 5);
    }

    private static float GetLaneOffset(int index, int count, float trailWidth)
    {
        if (count <= 1) return 0f;
        var normalized = index / (count - 1f) - 0.5f;
        return normalized * trailWidth * 0.74f;
    }

    private static float GetLaneSizeScale(int index, int count)
    {
        if (count <= 1) return 1f;
        var centerDistance = Mathf.Abs(index / (count - 1f) - 0.5f) * 2f;
        return Mathf.Lerp(1.08f, 0.82f, centerDistance);
    }

    private static float GetWidthAlphaScale(float trailWidth)
    {
        return Mathf.Clamp(0.9f + trailWidth * 0.12f, 0.9f, 1.35f);
    }

    private readonly struct TrailParticleSettings
    {
        public readonly int Count;
        public readonly float Size;
        public readonly float Lifetime;
        public readonly float Spread;
        public readonly float BackSpeed;
        public readonly float SideSpeed;
        public readonly float RandomSpeed;
        public readonly float AlphaScale;
        public readonly float AccentAlphaScale;

        public TrailParticleSettings(int count, float size, float lifetime, float spread, float backSpeed,
            float sideSpeed, float randomSpeed, float alphaScale, float accentAlphaScale)
        {
            Count = count;
            Size = size;
            Lifetime = lifetime;
            Spread = spread;
            BackSpeed = backSpeed;
            SideSpeed = sideSpeed;
            RandomSpeed = randomSpeed;
            AlphaScale = alphaScale;
            AccentAlphaScale = accentAlphaScale;
        }
    }
}
