using System;
using System.IO;
using System.Runtime.InteropServices;
using Cultiway.Content.Compatibility.Worldfall;
using FMOD;
using FMODUnity;
using UnityEngine;

namespace Cultiway.Content.Combat;

/// <summary>通过游戏正在使用的声音系统播放自制钟音，按当前视角定位并限制密集重叠。</summary>
internal static class CombatVisualAudio
{
    /// <summary>游戏持有的声音系统，本模组不负责关闭。</summary>
    private static FMOD.System system;
    /// <summary>当前世界共用的钟音采样，退出世界时释放。</summary>
    private static Sound bell;
    /// <summary>独立音效组，跟随游戏总音量与音效音量。</summary>
    private static ChannelGroup group;
    /// <summary>最近一次实际播放的声道，供实机核对。</summary>
    private static Channel lastChannel;
    /// <summary>当前世界是否已成功建立声音资源。</summary>
    private static bool initialized;
    /// <summary>外部声音接口报错后只报告一次，切换世界可重新建立。</summary>
    private static bool disabled;
    /// <summary>上次发声的世界时间。</summary>
    private static double lastBellAt = double.MinValue;
    /// <summary>实际成功启动的声音次数。</summary>
    internal static int PlayedCount { get; private set; }
    /// <summary>最近的声道是否正在播放，不用创建对象来冒充发声成功。</summary>
    internal static bool IsPlaying => initialized && lastChannel.isPlaying(out bool playing) == RESULT.OK && playing;

    /// <summary>在真实作用发生时播放一次，两个视角共用声像与距离衰减。</summary>
    /// <param name="position">作用位置。</param>
    /// <param name="strength">声音强度。</param>
    internal static void PlayBell(Vector3 position, float strength)
    {
        double now = World.world.getCurWorldTime();
        if (!MusicBox.sounds_on || disabled || now - lastBellAt < 0.055d) return;
        Vector3 listener = World.world.camera.transform.position;
        Vector3 right = Vector3.right;
        WorldfallVisualBridge.TryGetListener(ref listener, ref right);
        Vector3 offset = position - listener;
        offset.z = 0f;
        float distance = offset.magnitude;
        if (distance > 48f || Volume() <= 0f || !Initialize()) return;
        if (!Check(system.playSound(bell, group, true, out lastChannel))) return;
        UpdateVolume();
        lastChannel.setPan(Mathf.Clamp(Vector3.Dot(offset, right) / 12f, -0.8f, 0.8f));
        lastChannel.setVolume(Mathf.Clamp01(strength) * 0.55f / (1f + distance * 0.08f));
        lastChannel.setPaused(false);
        lastBellAt = now;
        PlayedCount++;
    }

    /// <summary>声音正在响时也响应音量变化，避免只在起音时读取设置。</summary>
    internal static void UpdateVolume()
    {
        if (initialized) group.setVolume(MusicBox.sounds_on ? Volume() : 0f);
    }

    /// <summary>读取游戏总音量和音效音量。</summary>
    private static float Volume() => PlayerConfig.getIntValue("volume_master_sound") * 0.01f *
                                     PlayerConfig.getIntValue("volume_sound_effects") * 0.01f;

    /// <summary>在已启动的游戏声音系统中建立一次采样及音效组。</summary>
    private static bool Initialize()
    {
        if (initialized) return true;
        if (!Check(RuntimeManager.StudioSystem.getCoreSystem(out system))) return false;
        byte[] wav = BuildBell();
        CREATESOUNDEXINFO info = new() { cbsize = Marshal.SizeOf(typeof(CREATESOUNDEXINFO)), length = (uint)wav.Length };
        if (!Check(system.createSound(wav, MODE.OPENMEMORY | MODE.LOOP_OFF | MODE._2D, ref info, out bell))) return false;
        if (!Check(system.createChannelGroup("Cultiway_Combat", out group)))
        {
            bell.release();
            return false;
        }
        initialized = true;
        return true;
    }

    /// <summary>声音驱动是外部边界；失败时明确报告并停止本世界的自制声音，不中断战斗。</summary>
    /// <param name="result">声音系统实际返回的结果。</param>
    private static bool Check(RESULT result)
    {
        if (result == RESULT.OK) return true;
        disabled = true;
        ModClass.LogWarning("战斗自制声音未能播放：" + result);
        return false;
    }

    /// <summary>生成单声道十六位钟音，采用自己编排的衰减泛音，不依赖外部参考素材。</summary>
    private static byte[] BuildBell()
    {
        const int rate = 22050, count = rate * 2;
        using MemoryStream stream = new(44 + count * 2);
        using BinaryWriter writer = new(stream);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
        writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
        writer.Write((short)2); writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            float sample = Mathf.Min(1f, t / 0.006f) *
                (Mathf.Sin(t * 440f * Mathf.PI * 2f) * Mathf.Exp(-t * 3.2f) * 0.55f +
                 Mathf.Sin(t * 905f * Mathf.PI * 2f) * Mathf.Exp(-t * 5f) * 0.22f +
                 Mathf.Sin(t * 1724f * Mathf.PI * 2f) * Mathf.Exp(-t * 7f) * 0.13f +
                 Mathf.Sin(t * 2920f * Mathf.PI * 2f) * Mathf.Exp(-t * 12f) * 0.08f);
            writer.Write((short)(sample * short.MaxValue));
        }
        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>退出世界时停止本模组声道，释放采样与音效组，保留游戏自身的播放器。</summary>
    internal static void ClearWorldState()
    {
        if (initialized) { group.stop(); bell.release(); group.release(); }
        initialized = false;
        disabled = false;
        lastBellAt = double.MinValue;
        PlayedCount = 0;
    }
}
