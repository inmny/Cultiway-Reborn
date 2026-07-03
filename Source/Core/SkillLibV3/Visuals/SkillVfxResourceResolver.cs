using System.Collections.Generic;

namespace Cultiway.Core.SkillLibV3.Visuals;

public enum SkillVfxPhase
{
    Cast,
    Muzzle,
    Trail,
    Impact,
    Residual
}

internal static class SkillVfxResourceResolver
{
    private const string RootPath = "cultiway/effect_v2";

    private static readonly Dictionary<string, bool> _framePresenceCache = new();
    private static readonly Dictionary<string, string> _resolvedPathCache = new();
    private static readonly HashSet<string> _loggedMissingPaths = new();

    public static string ResolvePhase(SkillVfxElementStyle style, SkillVfxPhase phase)
    {
        var cacheKey = $"{style}:{phase}";
        if (_resolvedPathCache.TryGetValue(cacheKey, out var cached)) return cached;

        var stylePath = $"{RootPath}/{GetStyleId(style)}/{GetPhaseId(phase)}";
        if (HasFrames(stylePath))
        {
            _resolvedPathCache[cacheKey] = stylePath;
            return stylePath;
        }

        var fallbackPath = $"{RootPath}/generic/{GetPhaseId(phase)}";
        if (style != SkillVfxElementStyle.Generic)
        {
            LogMissing(stylePath, fallbackPath);
        }

        if (HasFrames(fallbackPath))
        {
            _resolvedPathCache[cacheKey] = fallbackPath;
            return fallbackPath;
        }

        LogMissing(fallbackPath, null);
        _resolvedPathCache[cacheKey] = stylePath;
        return stylePath;
    }

    private static bool HasFrames(string path)
    {
        if (!_framePresenceCache.TryGetValue(path, out var present))
        {
            present = SkillEntityAsset.LoadOrderedFrames(path).Length > 0;
            _framePresenceCache[path] = present;
        }

        return present;
    }

    private static void LogMissing(string path, string fallbackPath)
    {
        if (!_loggedMissingPaths.Add(path)) return;

        if (string.IsNullOrEmpty(fallbackPath))
        {
            ModClass.LogWarning($"[SkillVfx] 缺少特效资源: {path}");
            return;
        }

        ModClass.LogWarning($"[SkillVfx] 缺少特效资源: {path}，降级到 {fallbackPath}");
    }

    private static string GetStyleId(SkillVfxElementStyle style)
    {
        return style switch
        {
            SkillVfxElementStyle.Metal => "metal",
            SkillVfxElementStyle.Wood => "wood",
            SkillVfxElementStyle.Water => "water",
            SkillVfxElementStyle.Fire => "fire",
            SkillVfxElementStyle.Earth => "earth",
            SkillVfxElementStyle.Neg => "neg",
            SkillVfxElementStyle.Pos => "pos",
            SkillVfxElementStyle.Entropy => "entropy",
            SkillVfxElementStyle.Wind => "wind",
            SkillVfxElementStyle.Lightning => "lightning",
            _ => "generic"
        };
    }

    private static string GetPhaseId(SkillVfxPhase phase)
    {
        return phase switch
        {
            SkillVfxPhase.Cast => "cast",
            SkillVfxPhase.Muzzle => "muzzle",
            SkillVfxPhase.Trail => "trail",
            SkillVfxPhase.Impact => "impact",
            SkillVfxPhase.Residual => "residual",
            _ => "impact"
        };
    }
}
