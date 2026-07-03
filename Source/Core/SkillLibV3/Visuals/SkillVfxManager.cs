using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.Pathfinding;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Visuals;

public sealed class SkillVfxManager
{
    private const int MaxQueuedVfxPerFrame = 180;
    private const float BaseCastScale = 0.078f;
    private const float BaseMuzzleScale = 0.052f;
    private const float BaseTrailScale = 0.046f;
    private const float BaseImpactScale = 0.076f;
    private const float BaseResidualScale = 0.058f;

    private readonly List<VfxSpawnRequest> _requests = new(128);

    public SkillVfxProfileAsset GetProfile(SkillEntityAsset asset)
    {
        return GetProfile(SkillVfxProfileAsset.ResolveStyle(asset));
    }

    private SkillVfxProfileAsset GetProfile(SkillVfxElementStyle style)
    {
        return ModClass.I.SkillV3.VfxProfileLib.GetByStyle(style);
    }

    public float CalculateIntensity(float powerLevel, float strength)
    {
        var strengthBonus = Mathf.Log10(Mathf.Max(1f, strength) + 1f) * 0.18f;
        var powerBonus = Mathf.Clamp(powerLevel, 0f, 24f) * 0.075f;
        return Mathf.Clamp(1f + strengthBonus + powerBonus, 1f, 3.35f);
    }

    public float CalculateIntensity(Entity skillContainer, float powerLevel, float strength)
    {
        var intensity = CalculateIntensity(powerLevel, strength);
        if (skillContainer.IsNull) return intensity;

        var modifierCount = 0;
        foreach (var componentType in skillContainer.GetComponentTypes())
        {
            if (typeof(IModifier).IsAssignableFrom(componentType))
            {
                modifierCount++;
            }
        }

        return Mathf.Clamp(intensity + Mathf.Min(modifierCount, 8) * 0.045f, 1f, 3.35f);
    }

    public SkillVfxWeight GetWeight(float intensity)
    {
        if (intensity >= 2.75f) return SkillVfxWeight.Extreme;
        if (intensity >= 2.1f) return SkillVfxWeight.Heavy;
        if (intensity >= 1.42f) return SkillVfxWeight.Medium;
        return SkillVfxWeight.Light;
    }

    public void AttachRuntime(Entity skillEntity, Entity skillContainer, SkillEntityAsset asset, float powerLevel,
        float strength)
    {
        var style = SkillVfxProfileAsset.ResolveStyle(asset);
        var color = SkillVfxProfileAsset.GetElementColor(asset.Element);
        skillEntity.AddComponent(new SkillVfxRuntime
        {
            Style = style,
            Color = color,
            AccentColor = SkillVfxProfileAsset.GetAccentColor(style, color),
            Intensity = CalculateIntensity(skillContainer, powerLevel, strength),
            NextTrailTime = 0f,
            TrailWidth = CalculateTrailWidth(skillEntity, asset)
        });
    }

    public void QueueCastStart(BaseSimObject source, Entity skillContainer, SkillEntityAsset asset, Vector3 direction,
        float powerLevel, float strength)
    {
        if (source == null || source.isRekt()) return;

        var style = SkillVfxProfileAsset.ResolveStyle(asset);
        var profile = GetProfile(style);
        var color = SkillVfxProfileAsset.GetElementColor(asset.Element);
        var accentColor = SkillVfxProfileAsset.GetAccentColor(style, color);
        var intensity = CalculateIntensity(skillContainer, powerLevel, strength);
        var weight = GetWeight(intensity);
        var tier = GetTierConfig(weight);
        var pos = source.GetSimPos();
        var dir = SafeDirection(direction);
        var side = Perpendicular(dir);

        Queue(new VfxSpawnRequest
        {
            Path = profile.CastPath,
            Position = Lift(pos, 0.04f),
            Rotation = dir,
            Scale = BaseCastScale * profile.CastScale * intensity * tier.Scale,
            Tint = WithAlpha(color, 0.72f),
            FrameInterval = 0.052f,
            LifeTime = Mathf.Lerp(0.2f, 0.38f, Mathf.InverseLerp(1f, 3.35f, intensity)),
            VisualRotation = FixedUpright(profile.CastFixedUpright),
            ShakeObject = source,
            ObjectShakeDuration = tier.CastShakeDuration,
            ObjectShakeVolume = tier.CastShakeVolume
        });

        Queue(new VfxSpawnRequest
        {
            Path = profile.MuzzlePath,
            Position = Lift(pos + dir * 0.36f, 0.07f),
            Rotation = dir,
            Scale = BaseMuzzleScale * profile.MuzzleScale * intensity * tier.Scale,
            Tint = WithAlpha(accentColor, 0.76f),
            FrameInterval = 0.05f,
            LifeTime = 0.16f + tier.ExtraLifeTime
        });

        for (var i = 0; i < tier.CastAccentCount; i++)
        {
            var sign = i % 2 == 0 ? 1f : -1f;
            var spread = (0.14f + i * 0.045f) * sign;
            var forward = 0.08f + i * 0.06f;
            Queue(new VfxSpawnRequest
            {
                Path = profile.CastAccentPath,
                Position = Lift(pos + dir * forward + side * spread, 0.055f + i * 0.005f),
                Rotation = Rotate(dir, sign * (18f + i * 7f)),
                Scale = BaseMuzzleScale * profile.MuzzleScale * intensity * (0.72f + i * 0.08f),
                Tint = WithAlpha(accentColor, Mathf.Clamp01(0.58f - i * 0.045f)),
                FrameInterval = 0.06f,
                LifeTime = 0.15f + tier.ExtraLifeTime,
                VisualRotation = profile.Style == SkillVfxElementStyle.Wind
                    ? VisualRotation.Spin(sign * (90f + i * 20f))
                    : null
            });
        }
    }

    public void QueueTrail(ref Position position, ref Rotation rotation, ref SkillVfxRuntime runtime, float now)
    {
        if (now < runtime.NextTrailTime) return;

        var weight = GetWeight(runtime.Intensity);
        var tier = GetTierConfig(weight);
        var profile = GetProfile(runtime.Style);
        runtime.NextTrailTime = now + profile.TrailInterval * tier.TrailIntervalScale;

        var dir = SafeDirection(rotation.value);
        var pos = position.value - dir * 0.18f;
        if (UseParticleTrail(weight))
        {
            SkillTrailParticleManager.Emit(profile, runtime.Color, runtime.AccentColor, weight, Lift(pos, 0.03f),
                dir, runtime.Intensity, runtime.TrailWidth, tier.TrailAlpha);
            return;
        }

        QueueAnimatedTrail(profile, tier, pos, dir, rotation.value, runtime.Intensity, runtime.TrailWidth,
            runtime.Color);

        var side = Perpendicular(dir);
        for (var i = 0; i < tier.TrailAccentCount; i++)
        {
            var sign = i % 2 == 0 ? 1f : -1f;
            var offset = side * Random.Range(0.08f, 0.24f + i * 0.06f) * sign - dir * Random.Range(0.05f, 0.2f);
            Queue(new VfxSpawnRequest
            {
                Path = profile.TrailAccentPath,
                Position = Lift(pos + offset, 0.035f + i * 0.01f),
                Rotation = Rotate(dir, Random.Range(-32f, 32f)),
                Scale = BaseTrailScale * profile.TrailScale * runtime.Intensity * (0.72f + i * 0.08f),
                Tint = WithAlpha(runtime.AccentColor, tier.TrailAlpha * 0.85f),
                FrameInterval = 0.07f,
                LifeTime = tier.TrailLifeTime * 0.85f,
                VisualRotation = profile.Style == SkillVfxElementStyle.Lightning && Random.value < 0.45f
                    ? VisualRotation.FixedUpright()
                    : null
            });
        }
    }

    private static bool UseParticleTrail(SkillVfxWeight weight)
    {
        return weight is SkillVfxWeight.Light or SkillVfxWeight.Medium;
    }

    private static float CalculateTrailWidth(Entity skillEntity, SkillEntityAsset asset)
    {
        var width = 0.75f;
        if (skillEntity.TryGetComponent(out AnimData animData) && animData.frames != null)
        {
            foreach (var frame in animData.frames)
            {
                if (frame == null) continue;
                width = Mathf.Max(width, frame.bounds.size.x, frame.bounds.size.y);
            }
        }

        var scale = 0.1f;
        if (skillEntity.TryGetComponent(out Scale scaleComponent))
        {
            scale = Mathf.Max(Mathf.Abs(scaleComponent.x), Mathf.Abs(scaleComponent.y), 0.01f);
        }

        width *= scale;
        if (asset.SeriesTags.Contains("slash")) width *= 1.35f;
        if (asset.SeriesTags.Contains("aoe")) width *= 1.2f;
        return Mathf.Clamp(width, 0.55f, 4.8f);
    }

    private static int GetTrailLaneCount(float trailWidth, bool heavy)
    {
        var laneSize = heavy ? 1.1f : 0.85f;
        var maxLanes = heavy ? 4 : 5;
        return Mathf.Clamp(Mathf.CeilToInt(trailWidth / laneSize), 1, maxLanes);
    }

    private static float GetTrailLaneOffset(int index, int count, float trailWidth)
    {
        if (count <= 1) return 0f;
        var normalized = index / (count - 1f) - 0.5f;
        return normalized * trailWidth * 0.72f;
    }

    private static float GetTrailLaneScale(int index, int count)
    {
        if (count <= 1) return 1f;
        var centerDistance = Mathf.Abs(index / (count - 1f) - 0.5f) * 2f;
        return Mathf.Lerp(1f, 0.72f, centerDistance);
    }

    private void QueueAnimatedTrail(SkillVfxProfileAsset profile, SkillVfxTierConfig tier, Vector3 pos, Vector3 dir,
        Vector3 rotation, float intensity, float trailWidth, Color color)
    {
        var side = Perpendicular(dir);
        var laneCount = GetTrailLaneCount(trailWidth, heavy: true);
        for (var i = 0; i < laneCount; i++)
        {
            var laneOffset = GetTrailLaneOffset(i, laneCount, trailWidth);
            Queue(new VfxSpawnRequest
            {
                Path = profile.TrailPath,
                Position = Lift(pos + side * laneOffset, 0.03f + i * 0.002f),
                Rotation = rotation,
                Scale = BaseTrailScale * profile.TrailScale * intensity * tier.Scale * GetTrailLaneScale(i, laneCount),
                Tint = WithAlpha(color, tier.TrailAlpha),
                FrameInterval = 0.085f,
                LifeTime = tier.TrailLifeTime
            });
        }
    }

    public void QueueImpact(ref SkillContext context, SkillEntityAsset asset, Entity skillEntity,
        BaseSimObject target)
    {
        if (target == null || target.isRekt()) return;

        ResolveRuntime(context, asset, skillEntity, out var profile, out var color, out var accentColor,
            out var intensity);
        var targetPos = target.GetSimPos();
        var dir = DirectionFromSource(context.SourceObj, targetPos, context.TargetDir);
        QueueImpact(profile, color, accentColor, intensity, targetPos, dir, target);
    }

    public void QueueElementImpact(SkillContext context, ElementComposition element, float strength,
        BaseSimObject target)
    {
        if (target == null || target.isRekt()) return;

        var style = SkillVfxProfileAsset.ResolveStyle(element);
        var profile = GetProfile(style);
        var color = SkillVfxProfileAsset.GetElementColor(element);
        var accentColor = SkillVfxProfileAsset.GetAccentColor(style, color);
        var intensity = CalculateIntensity(context.PowerLevel, strength);
        var targetPos = target.GetSimPos();
        var dir = DirectionFromSource(context.SourceObj, targetPos, context.TargetDir);
        QueueImpact(profile, color, accentColor, intensity, targetPos, dir, target);
    }

    private void QueueImpact(SkillVfxProfileAsset profile, Color color, Color accentColor, float intensity,
        Vector3 targetPos, Vector3 dir, BaseSimObject target)
    {
        var weight = GetWeight(intensity);
        var tier = GetTierConfig(weight);

        Queue(new VfxSpawnRequest
        {
            Path = profile.ImpactPath,
            Position = Lift(targetPos, 0.04f),
            Rotation = dir,
            Scale = BaseImpactScale * profile.ImpactScale * intensity * tier.Scale,
            Tint = WithAlpha(color, 0.88f),
            FrameInterval = tier.ImpactFrameInterval,
            LifeTime = tier.ImpactLifeTime,
            VisualRotation = FixedUpright(profile.ImpactFixedUpright),
            ShakeObject = target,
            ObjectShakeDuration = tier.ImpactShakeDuration,
            ObjectShakeVolume = tier.ImpactShakeVolume,
            WorldShakeDuration = tier.WorldShakeDuration,
            WorldShakeIntensity = tier.WorldShakeIntensity
        });

        QueueImpactAccents(profile, tier, targetPos, dir, intensity, accentColor);
        QueueImpactRings(profile, tier, targetPos, dir, intensity, accentColor);
        QueueResiduals(profile, tier, targetPos, dir, intensity, color);
    }

    public void QueueAreaImpact(ref SkillContext context, SkillEntityAsset asset, Entity skillEntity,
        Vector3 position, float radius, BaseSimObject shakeObject = null)
    {
        ResolveRuntime(context, asset, skillEntity, out var profile, out var color, out var accentColor,
            out var intensity);
        QueueAreaImpact(profile, color, accentColor, intensity, position, Mathf.Max(0.5f, radius), shakeObject);
    }

    public void QueueExplosion(Vector3 position, ElementComposition element, float powerLevel, float strength,
        float radius, BaseSimObject shakeObject = null)
    {
        var style = SkillVfxProfileAsset.ResolveStyle(element);
        var profile = GetProfile(style);
        var color = SkillVfxProfileAsset.GetElementColor(element);
        var accentColor = SkillVfxProfileAsset.GetAccentColor(style, color);
        var intensity = CalculateIntensity(powerLevel, strength);
        QueueAreaImpact(profile, color, accentColor, intensity, position, Mathf.Max(0.5f, radius), shakeObject);
    }

    private void QueueAreaImpact(SkillVfxProfileAsset profile, Color color, Color accentColor, float intensity,
        Vector3 position, float radius, BaseSimObject shakeObject)
    {
        var weight = GetWeight(intensity * Mathf.Clamp(radius / 2f, 0.85f, 1.8f));
        var tier = GetTierConfig(weight);
        var dir = RandomDirection();
        var areaScale = Mathf.Clamp(radius / 2f, 0.85f, 2.4f);

        Queue(new VfxSpawnRequest
        {
            Path = profile.ImpactPath,
            Position = Lift(position, 0.04f),
            Rotation = dir,
            Scale = BaseImpactScale * profile.ImpactScale * intensity * tier.Scale * areaScale,
            Tint = WithAlpha(color, 0.82f),
            FrameInterval = tier.ImpactFrameInterval,
            LifeTime = tier.ImpactLifeTime + radius * 0.025f,
            VisualRotation = FixedUpright(profile.ImpactFixedUpright),
            ShakeObject = shakeObject,
            ObjectShakeDuration = tier.ImpactShakeDuration,
            ObjectShakeVolume = tier.ImpactShakeVolume,
            WorldShakeDuration = tier.WorldShakeDuration,
            WorldShakeIntensity = tier.WorldShakeIntensity
        });

        var count = Mathf.Clamp(tier.ImpactAccentCount + Mathf.CeilToInt(radius), 2, 9);
        for (var i = 0; i < count; i++)
        {
            var angle = i * (360f / count) + Random.Range(-12f, 12f);
            var branchDir = Rotate(Vector3.right, angle);
            var distance = Random.Range(radius * 0.18f, radius * 0.48f);
            Queue(new VfxSpawnRequest
            {
                Path = profile.ImpactAccentPath,
                Position = Lift(position + branchDir * distance, 0.035f + i * 0.003f),
                Rotation = branchDir,
                Scale = BaseImpactScale * profile.ImpactScale * intensity * (0.42f + areaScale * 0.18f),
                Tint = WithAlpha(accentColor, 0.62f),
                FrameInterval = 0.06f,
                LifeTime = tier.ImpactLifeTime * 0.8f,
                VisualRotation = FixedUpright(profile.ImpactFixedUpright)
            });
        }

        QueueImpactRings(profile, tier, position, dir, intensity * areaScale, accentColor);
        QueueResiduals(profile, tier, position, dir, intensity * areaScale, color);
    }

    private void QueueImpactAccents(SkillVfxProfileAsset profile, SkillVfxTierConfig tier, Vector3 targetPos,
        Vector3 dir, float intensity, Color accentColor)
    {
        if (tier.ImpactAccentCount <= 0) return;

        var side = Perpendicular(dir);
        for (var i = 0; i < tier.ImpactAccentCount; i++)
        {
            var sign = i % 2 == 0 ? 1f : -1f;
            var branchDir = Rotate(dir, sign * (24f + i * 17f) + Random.Range(-8f, 8f));
            var offset = GetImpactAccentOffset(profile.Style, dir, side, branchDir, sign, i, intensity);

            Queue(new VfxSpawnRequest
            {
                Path = profile.ImpactAccentPath,
                Position = Lift(targetPos + offset, 0.04f + i * 0.006f),
                Rotation = branchDir,
                Scale = BaseImpactScale * profile.ImpactScale * intensity * (0.55f + i * 0.08f),
                Tint = WithAlpha(accentColor, Mathf.Clamp01(0.68f - i * 0.05f)),
                FrameInterval = 0.055f,
                LifeTime = tier.ImpactLifeTime * 0.78f,
                VisualRotation = FixedUpright(profile.ImpactFixedUpright)
            });
        }
    }

    private void QueueResiduals(SkillVfxProfileAsset profile, SkillVfxTierConfig tier, Vector3 position, Vector3 dir,
        float intensity, Color color)
    {
        for (var i = 0; i < tier.ResidualCount; i++)
        {
            var residualDir = Rotate(dir, Random.Range(-45f, 45f));
            var offset = residualDir * Random.Range(0.03f, 0.18f + i * 0.04f);
            Queue(new VfxSpawnRequest
            {
                Path = profile.ResidualPath,
                Position = Lift(position + offset, 0.025f + i * 0.006f),
                Rotation = residualDir,
                Scale = BaseResidualScale * profile.ResidualScale * intensity * (0.78f + i * 0.1f),
                Tint = WithAlpha(color, Mathf.Clamp01(0.38f - i * 0.035f)),
                FrameInterval = 0.09f,
                LifeTime = tier.ResidualLifeTime + i * 0.035f,
                VisualRotation = FixedUpright(profile.ResidualFixedUpright)
            });
        }
    }

    private void QueueImpactRings(SkillVfxProfileAsset profile, SkillVfxTierConfig tier, Vector3 position,
        Vector3 dir, float intensity, Color accentColor)
    {
        for (var i = 0; i < tier.ImpactRingCount; i++)
        {
            Queue(new VfxSpawnRequest
            {
                Path = profile.CastPath,
                Position = Lift(position, 0.052f + i * 0.008f),
                Rotation = Rotate(dir, i * 17f),
                Scale = BaseImpactScale * profile.ImpactScale * intensity * tier.ImpactRingScale * (1f + i * 0.24f),
                Tint = WithAlpha(accentColor, Mathf.Clamp01(tier.ImpactRingAlpha - i * 0.08f)),
                FrameInterval = tier.ImpactRingFrameInterval,
                LifeTime = tier.ImpactRingLifeTime + i * 0.04f,
                VisualRotation = FixedUpright(profile.CastFixedUpright)
            });
        }
    }

    public void Flush()
    {
        var count = Mathf.Min(_requests.Count, MaxQueuedVfxPerFrame);
        for (var i = 0; i < count; i++)
        {
            var request = _requests[i];
            ModClass.I.SkillV3.SpawnAnim(request.Path, request.Position, request.Rotation, request.Scale,
                request.Tint, request.FrameInterval, lifeTime: request.LifeTime,
                visualRotation: request.VisualRotation);

            ApplyObjectShake(request);
            ApplyWorldShake(request);
        }

        _requests.Clear();
    }

    private void Queue(VfxSpawnRequest request)
    {
        if (_requests.Count >= MaxQueuedVfxPerFrame) return;
        if (string.IsNullOrEmpty(request.Path)) return;
        _requests.Add(request);
    }

    private void ResolveRuntime(SkillContext context, SkillEntityAsset asset, Entity skillEntity,
        out SkillVfxProfileAsset profile, out Color color, out Color accentColor, out float intensity)
    {
        var style = SkillVfxProfileAsset.ResolveStyle(asset);
        profile = GetProfile(style);
        color = SkillVfxProfileAsset.GetElementColor(asset.Element);
        accentColor = SkillVfxProfileAsset.GetAccentColor(style, color);
        intensity = CalculateIntensity(context.PowerLevel, context.Strength);
        if (skillEntity.IsNull || !skillEntity.TryGetComponent(out SkillVfxRuntime runtime)) return;

        profile = GetProfile(runtime.Style);
        color = runtime.Color;
        accentColor = runtime.AccentColor;
        intensity = runtime.Intensity;
    }

    private static void ApplyObjectShake(VfxSpawnRequest request)
    {
        if (request.ObjectShakeDuration <= 0f || request.ShakeObject == null || request.ShakeObject.isRekt()) return;

        if (request.ShakeObject.isActor())
        {
            request.ShakeObject.a.startShake(request.ObjectShakeDuration, request.ObjectShakeVolume);
        }
        else
        {
            request.ShakeObject.b.startShake(request.ObjectShakeDuration, request.ObjectShakeVolume,
                request.ObjectShakeVolume);
        }
    }

    private static void ApplyWorldShake(VfxSpawnRequest request)
    {
        if (request.WorldShakeDuration <= 0f || request.WorldShakeIntensity <= 0f) return;
        if (!IsVisible(request.Position)) return;

        World.world.startShake(request.WorldShakeDuration, 0.02f, request.WorldShakeIntensity, true, true);
    }

    private static bool IsVisible(Vector3 position)
    {
        return World.world?.move_camera == null ||
               World.world.move_camera.isWithinCameraViewNotPowerBar(position);
    }

    private static Vector3 DirectionFromSource(BaseSimObject source, Vector3 targetPos, Vector3 fallback)
    {
        if (source != null && !source.isRekt())
        {
            var dir = targetPos - source.GetSimPos();
            if (dir.sqrMagnitude > 0.0001f) return dir.normalized;
        }

        return SafeDirection(fallback);
    }

    private static Vector3 SafeDirection(Vector3 direction)
    {
        direction.z = 0f;
        return direction.sqrMagnitude < 0.0001f ? Vector3.right : direction.normalized;
    }

    private static Vector3 Perpendicular(Vector3 direction)
    {
        direction = SafeDirection(direction);
        return new Vector3(-direction.y, direction.x, 0f);
    }

    private static Vector3 Rotate(Vector3 direction, float angle)
    {
        return SafeDirection(Quaternion.AngleAxis(angle, Vector3.forward) * SafeDirection(direction));
    }

    private static Vector3 RandomDirection()
    {
        return Rotate(Vector3.right, Random.Range(0f, 360f));
    }

    private static Vector3 Lift(Vector3 position, float height)
    {
        position.z += height;
        return position;
    }

    private static Vector3 GetImpactAccentOffset(SkillVfxElementStyle style, Vector3 dir, Vector3 side,
        Vector3 branchDir, float sign, int index, float intensity)
    {
        var scale = Mathf.Clamp(intensity * 0.08f, 0.08f, 0.28f);
        return style switch
        {
            SkillVfxElementStyle.Metal => side * sign * (0.08f + index * 0.05f) + dir * 0.08f,
            SkillVfxElementStyle.Wood => branchDir * (0.08f + index * 0.07f + scale),
            SkillVfxElementStyle.Water => side * sign * (0.12f + index * 0.06f),
            SkillVfxElementStyle.Fire => branchDir * (0.12f + index * 0.08f + scale),
            SkillVfxElementStyle.Earth => branchDir * (0.06f + index * 0.05f),
            SkillVfxElementStyle.Neg => side * sign * (0.16f + index * 0.07f) - dir * (0.03f + scale),
            SkillVfxElementStyle.Pos => branchDir * (0.14f + index * 0.08f + scale),
            SkillVfxElementStyle.Entropy => branchDir * (0.2f + index * 0.11f + scale),
            SkillVfxElementStyle.Wind => branchDir * (0.16f + index * 0.08f),
            SkillVfxElementStyle.Lightning => branchDir * (0.18f + index * 0.09f),
            _ => branchDir * (0.1f + index * 0.06f)
        };
    }

    private static VisualRotation? FixedUpright(bool enabled)
    {
        if (!enabled) return null;
        return VisualRotation.FixedUpright();
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static SkillVfxTierConfig GetTierConfig(SkillVfxWeight weight)
    {
        return weight switch
        {
            SkillVfxWeight.Extreme => new SkillVfxTierConfig(
                1.62f, 0.52f, 4, 3, 5, 5, 2, 1.25f, 0.52f, 0.11f, 0.6f, 0.48f, 0.34f, 0.068f,
                0.62f, 0.9f, 0.042f, 0.52f, 0.2f, 0.09f, 0.1f, 0.09f, 0.07f),
            SkillVfxWeight.Heavy => new SkillVfxTierConfig(
                1.34f, 0.72f, 2, 2, 3, 3, 0, 1f, 0f, 0.08f, 0.42f, 0.42f, 0.26f, 0.048f,
                0.42f, 0.64f, 0.05f, 0.38f, 0.16f, 0.065f, 0.065f, 0.05f, 0.045f),
            SkillVfxWeight.Medium => new SkillVfxTierConfig(
                1.14f, 0.92f, 1, 1, 1, 1, 0, 1f, 0f, 0.07f, 0.32f, 0.34f, 0.2f, 0.052f,
                0.3f, 0.42f, 0f, 0f, 0f, 0f, 0f, 0f, 0.025f),
            _ => new SkillVfxTierConfig(
                1f, 1.12f, 0, 0, 0, 0, 0, 1f, 0f, 0.06f, 0.24f, 0.26f, 0.16f, 0.058f,
                0.22f, 0.28f, 0f, 0f, 0f, 0f, 0f, 0f, 0f)
        };
    }

    private struct VfxSpawnRequest
    {
        public string Path;
        public Vector3 Position;
        public Vector3 Rotation;
        public float Scale;
        public Color Tint;
        public float FrameInterval;
        public float LifeTime;
        public VisualRotation? VisualRotation;
        public BaseSimObject ShakeObject;
        public float ObjectShakeDuration;
        public float ObjectShakeVolume;
        public float WorldShakeDuration;
        public float WorldShakeIntensity;
    }

    private readonly struct SkillVfxTierConfig
    {
        public readonly float Scale;
        public readonly float TrailIntervalScale;
        public readonly int CastAccentCount;
        public readonly int TrailAccentCount;
        public readonly int ImpactAccentCount;
        public readonly int ResidualCount;
        public readonly int ImpactRingCount;
        public readonly float ImpactRingScale;
        public readonly float ImpactRingAlpha;
        public readonly float ImpactRingFrameInterval;
        public readonly float ImpactRingLifeTime;
        public readonly float TrailAlpha;
        public readonly float TrailLifeTime;
        public readonly float ImpactFrameInterval;
        public readonly float ImpactLifeTime;
        public readonly float ResidualLifeTime;
        public readonly float CastShakeDuration;
        public readonly float ImpactShakeDuration;
        public readonly float CastShakeVolume;
        public readonly float ImpactShakeVolume;
        public readonly float WorldShakeDuration;
        public readonly float WorldShakeIntensity;
        public readonly float ExtraLifeTime;

        public SkillVfxTierConfig(float scale, float trailIntervalScale, int castAccentCount, int trailAccentCount,
            int impactAccentCount, int residualCount, int impactRingCount, float impactRingScale,
            float impactRingAlpha, float impactRingFrameInterval, float impactRingLifeTime, float trailAlpha,
            float trailLifeTime, float impactFrameInterval, float impactLifeTime, float residualLifeTime,
            float castShakeDuration, float impactShakeDuration, float castShakeVolume, float impactShakeVolume,
            float worldShakeDuration, float worldShakeIntensity, float extraLifeTime)
        {
            Scale = scale;
            TrailIntervalScale = trailIntervalScale;
            CastAccentCount = castAccentCount;
            TrailAccentCount = trailAccentCount;
            ImpactAccentCount = impactAccentCount;
            ResidualCount = residualCount;
            ImpactRingCount = impactRingCount;
            ImpactRingScale = impactRingScale;
            ImpactRingAlpha = impactRingAlpha;
            ImpactRingFrameInterval = impactRingFrameInterval;
            ImpactRingLifeTime = impactRingLifeTime;
            TrailAlpha = trailAlpha;
            TrailLifeTime = trailLifeTime;
            ImpactFrameInterval = impactFrameInterval;
            ImpactLifeTime = impactLifeTime;
            ResidualLifeTime = residualLifeTime;
            CastShakeDuration = castShakeDuration;
            ImpactShakeDuration = impactShakeDuration;
            CastShakeVolume = castShakeVolume;
            ImpactShakeVolume = impactShakeVolume;
            WorldShakeDuration = worldShakeDuration;
            WorldShakeIntensity = worldShakeIntensity;
            ExtraLifeTime = extraLifeTime;
        }
    }
}
