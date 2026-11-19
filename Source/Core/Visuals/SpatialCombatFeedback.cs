using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Visuals;

/// <summary>真实作用的声音和关键震动共用限频；切换显示方式不重播事件。</summary>
internal static class SpatialCombatFeedback
{
    /// <summary>可选空间视角将事件位置转换为游戏声音监听器的位置。</summary>
    internal static Func<Vector3, Vector3> AudioPosition;
    /// <summary>每种声音最近实际发声的世界时间。</summary>
    private static readonly Dictionary<string, double> sounds = new(StringComparer.Ordinal);
    /// <summary>不同声音之间也保留少量间隔。</summary>
    private static double lastSound = double.MinValue, lastShake = double.MinValue;

    /// <summary>声音事件由现有游戏素材库播放，先省略不可见和过密的反馈。</summary>
    /// <param name="path">已经登记的游戏声音。</param><param name="position">实际发生位置。</param>
    internal static void Sound(string path, Vector3 position)
    {
        double now = World.world.getCurWorldTime();
        if (string.IsNullOrEmpty(path) || !MusicBox.sounds_on || !SpatialVisualEffects.IsRelevant(position, 2f) ||
            now - lastSound < .025d || sounds.TryGetValue(path, out double previous) && now - previous < .08d) return;
        Vector3 audio = AudioPosition?.Invoke(position) ?? position;
        MusicBox.playSound(path, audio.x, audio.y);
        sounds[path] = lastSound = now;
    }

    /// <summary>只有较强的交锋保留短促震动，普通弹丸不会连续震屏。</summary>
    /// <param name="position">真实作用点。</param><param name="intensity">原有定义的强度。</param>
    internal static bool Shake(Vector3 position, float intensity)
    {
        double now = World.world.getCurWorldTime();
        if (intensity < .12f || now - lastShake < .4d || !SpatialVisualEffects.IsRelevant(position, 3f)) return false;
        World.world.startShake(.06f, .02f, Mathf.Min(.16f, intensity));
        lastShake = now;
        return true;
    }

    /// <summary>世界切换后清除事件时间，避免新世界时间较小时被错误静音。</summary>
    internal static void Clear() { sounds.Clear(); lastSound = lastShake = double.MinValue; }
}
