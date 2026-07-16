using System;
using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>
/// 通用生命周期会发布的基础视觉信号。具体能力仍可使用任意自定义字符串通道。
/// </summary>
public static class ArtifactVisualChannels
{
    public const string Trigger = "trigger";
    public const string Tick = "tick";
    public const string End = "end";
    public const string Hit = "hit";
    public const string CraftStep = "craft_step";
    public const string CraftResult = "craft_result";
    public const string Guard = "guard";
    public const string Counter = "counter";
    public const string Impact = "impact";
    public const string Cleanse = "cleanse";
    public const string Reflect = "reflect";
    public const string Drain = "drain";
}

/// <summary>
/// 法器能力向表现层发送短时语义信号的唯一入口。信号先排队，由视觉系统在 ECS 查询结束后创建实体。
/// </summary>
public static class ArtifactAbilityVisuals
{
    private static readonly List<ArtifactVisualSignalRequest> PendingSignals = new();
    private static readonly Dictionary<string, ArtifactVisualTheme> AppearanceThemeCache =
        new(StringComparer.Ordinal);

    public static void Emit(
        ArtifactAbilityExecutionContext execution,
        ArtifactAbilityInstance ability,
        ArtifactAbilityRuntimeEntry runtime,
        string channel,
        Vector3? position = null,
        Vector3 direction = default,
        BaseSimObject target = null,
        float intensity = 1f,
        float duration = 0f,
        ArtifactAbilityEndReason? endReason = null)
    {
        ArtifactAbilityAsset asset = Libraries.Manager.ArtifactAbilityLibrary.get(ability.ability_id);
        ArtifactAbilityVisualProfile profile = asset?.visual;
        if (profile == null || !profile.TryGetSignal(channel, out ArtifactAbilityVisualSignal signal)) return;

        Vector3 resolvedPosition = position ?? ResolveSnapshotPosition(execution.controller, execution.artifact);
        if (direction.sqrMagnitude < 0.0001f && target != null && !target.isRekt())
        {
            direction = target.GetSimPos() - resolvedPosition;
        }

        ArtifactAbilityVisualContext context = new(
            execution.controller,
            execution.artifact,
            asset,
            ability,
            runtime,
            execution.control_state,
            ResolveTheme(execution.artifact, profile),
            channel,
            resolvedPosition,
            direction,
            target,
            Mathf.Max(0f, intensity),
            endReason);
        PendingSignals.Add(new ArtifactVisualSignalRequest(
            context,
            signal,
            duration > 0f ? duration : signal.duration));
    }

    internal static ArtifactAbilityVisualContext CreateLoopContext(
        Entity artifact,
        ArtifactAbilityAsset asset,
        ArtifactAbilityInstance ability,
        ArtifactAbilityRuntimeEntry runtime,
        ArtifactAbilityRuntime ownerRuntime,
        string channel)
    {
        Vector3 position = runtime.has_activity_position
            ? runtime.activity_position
            : ResolveSnapshotPosition(ownerRuntime.controller, artifact);
        Vector3 direction = runtime.has_activity_direction
            ? runtime.activity_direction
            : default;
        return new ArtifactAbilityVisualContext(
            ownerRuntime.controller,
            artifact,
            asset,
            ability,
            runtime,
            ownerRuntime.control_state,
            ResolveTheme(artifact, asset.visual),
            channel,
            position,
            direction);
    }

    internal static void DrainSignals(List<ArtifactVisualSignalRequest> output)
    {
        output.Clear();
        output.AddRange(PendingSignals);
        PendingSignals.Clear();
    }

    internal static bool TryResolveAnchorPosition(
        ArtifactAbilityVisualContext context,
        ArtifactVisualAnchorRef anchor,
        out Vector3 position)
    {
        if (anchor.use_body_anchor && !context.artifact.IsNull && context.artifact.HasComponent<Position>())
        {
            position = ArtifactManifestationTools.ResolveWorldAnchor(context.artifact, anchor.body_anchor);
            return true;
        }

        switch (anchor.kind)
        {
            case ArtifactVisualAnchorKind.Controller:
                if (TryResolveControllerPosition(context.controller, out position)) return true;
                break;
            case ArtifactVisualAnchorKind.Artifact:
                if (!context.artifact.IsNull && context.artifact.HasComponent<Position>())
                {
                    position = context.artifact.GetComponent<Position>().value;
                    return true;
                }
                break;
            case ArtifactVisualAnchorKind.DeploymentOrigin:
                if (!context.artifact.IsNull &&
                    context.artifact.TryGetComponent(out ArtifactDeployment deployment))
                {
                    position = ArtifactManifestationTools.ResolveWorldAnchor(
                        context.artifact,
                        deployment.ResolveBodyAnchor());
                    return true;
                }
                break;
            case ArtifactVisualAnchorKind.ActiveExecution:
                if (!context.runtime.active_execution.IsNull &&
                    context.runtime.active_execution.HasComponent<Position>())
                {
                    position = context.runtime.active_execution.GetComponent<Position>().value;
                    return true;
                }
                if (!context.artifact.IsNull && context.artifact.HasComponent<Position>())
                {
                    position = context.artifact.GetComponent<Position>().value;
                    return true;
                }
                break;
            case ArtifactVisualAnchorKind.Target:
                if (context.target != null && !context.target.isRekt())
                {
                    position = context.target.GetSimPos();
                    return true;
                }
                break;
            case ArtifactVisualAnchorKind.Point:
                position = context.position;
                return true;
            default:
                throw new ArgumentOutOfRangeException(nameof(anchor), anchor.kind, null);
        }

        position = context.position;
        return true;
    }

    internal static float ResolveActorScale(ArtifactAbilityVisualContext context)
    {
        if (context.controller.IsNull || !context.controller.HasComponent<ActorBinder>()) return 1f;
        Actor actor = context.controller.GetComponent<ActorBinder>().Actor;
        return Mathf.Max(actor.stats[S.scale], 0.1f) * 10f;
    }

    internal static bool IsVisible(ArtifactAbilityVisualContext context)
    {
        if (context.controller.IsNull || !context.controller.HasComponent<ActorBinder>()) return false;
        Actor actor = context.controller.GetComponent<ActorBinder>().Actor;
        if (actor == null || !actor.isAlive() || !actor.is_visible) return false;
        return !context.artifact.TryGetComponent(out ArtifactManifestation manifestation) || manifestation.visible;
    }

    internal static void ClearThemeCache()
    {
        AppearanceThemeCache.Clear();
    }

    internal static ArtifactVisualTheme ResolveTheme(Entity artifact, ArtifactAbilityVisualProfile profile)
    {
        if (profile.explicit_theme.HasValue) return profile.explicit_theme.Value;
        if (!artifact.IsNull && artifact.TryGetComponent(out ArtifactAppearance appearance))
        {
            string cacheKey = appearance.GetCacheKey();
            if (AppearanceThemeCache.TryGetValue(cacheKey, out ArtifactVisualTheme cached)) return cached;
            if (TryResolveAppearanceTheme(appearance, out ArtifactVisualTheme resolved))
            {
                AppearanceThemeCache[cacheKey] = resolved;
                return resolved;
            }
        }
        return profile.fallback_theme ?? ArtifactVisualTheme.FromPrimary(new Color(0.45f, 0.82f, 1f));
    }

    private static bool TryResolveAppearanceTheme(
        ArtifactAppearance appearance,
        out ArtifactVisualTheme theme)
    {
        ArtifactAppearancePart[] parts = appearance.parts ?? [];
        Dictionary<string, int> schemeCounts = new(StringComparer.Ordinal);
        for (int i = 0; i < parts.Length; i++)
        {
            string schemeKey = parts[i].color_scheme;
            if (string.IsNullOrEmpty(schemeKey)) continue;
            if (schemeCounts.TryGetValue(schemeKey, out int count))
            {
                schemeCounts[schemeKey] = count + 1;
                continue;
            }
            schemeCounts.Add(schemeKey, 1);
        }

        string dominantScheme = null;
        int dominantCount = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            string schemeKey = parts[i].color_scheme;
            if (string.IsNullOrEmpty(schemeKey) || schemeCounts[schemeKey] <= dominantCount) continue;
            dominantScheme = schemeKey;
            dominantCount = schemeCounts[schemeKey];
        }
        if (!string.IsNullOrEmpty(dominantScheme) &&
            ArtifactAppearanceCatalogLoader.Current.ColorSchemes.TryGetValue(
                dominantScheme,
                out ArtifactAppearanceColorSchemeDef scheme) &&
            TryParseTheme(scheme.VisualTheme, out theme))
        {
            return true;
        }

        theme = default;
        return false;
    }

    private static bool TryParseTheme(
        ArtifactAppearanceVisualThemeDef definition,
        out ArtifactVisualTheme theme)
    {
        if (definition == null ||
            !ColorUtility.TryParseHtmlString(definition.Primary, out Color primary))
        {
            theme = default;
            return false;
        }

        Color secondary = ColorUtility.TryParseHtmlString(definition.Secondary, out Color parsedSecondary)
            ? parsedSecondary
            : Color.Lerp(primary, Color.black, 0.28f);
        Color glow = ColorUtility.TryParseHtmlString(definition.Glow, out Color parsedGlow)
            ? parsedGlow
            : Color.Lerp(primary, Color.white, 0.58f);
        theme = new ArtifactVisualTheme(primary, secondary, glow);
        return true;
    }

    private static Vector3 ResolveSnapshotPosition(Entity controller, Entity artifact)
    {
        if (!artifact.IsNull && artifact.HasComponent<Position>())
        {
            return artifact.GetComponent<Position>().value;
        }
        return TryResolveControllerPosition(controller, out Vector3 position) ? position : Vector3.zero;
    }

    private static bool TryResolveControllerPosition(Entity controller, out Vector3 position)
    {
        if (!controller.IsNull && controller.HasComponent<ActorBinder>())
        {
            Actor actor = controller.GetComponent<ActorBinder>().Actor;
            if (actor != null && !actor.isRekt())
            {
                position = actor.GetSimPos();
                return true;
            }
        }
        position = default;
        return false;
    }
}

internal readonly struct ArtifactVisualSignalRequest
{
    public readonly ArtifactAbilityVisualContext context;
    public readonly ArtifactAbilityVisualSignal signal;
    public readonly float duration;

    public ArtifactVisualSignalRequest(
        ArtifactAbilityVisualContext context,
        ArtifactAbilityVisualSignal signal,
        float duration)
    {
        this.context = context;
        this.signal = signal;
        this.duration = duration;
    }
}
