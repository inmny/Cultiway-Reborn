using System.Collections.Generic;
using Cultiway.Patch;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>把 Mod 成就写入原版本地进度，并串行复用原版解锁弹窗。</summary>
internal static class CultivationAchievementUnlockService
{
    private static readonly Queue<Achievement> pending = new();
    private static readonly HashSet<string> pendingIds = new();
    private static AchievementPopup popup;

    internal static bool CanProcessRuntimeEvent =>
        CultivationAchievements.Ready &&
        Config.game_loaded &&
        !SmoothLoader.isLoading() &&
        World.world != null;

    internal static void Initialize()
    {
        PatchMapBox.RegisterActionOnClearWorld(ClearWorldState);
    }

    internal static bool TryUnlock(Achievement achievement)
    {
        if (!CanProcessRuntimeEvent || achievement == null || GameProgress.instance == null) return false;
        if (WorldLawLibrary.world_law_cursed_world.isEnabled()) return false;
        if (GameProgress.isAchievementUnlocked(achievement.id)) return false;
        if (!GameProgress.unlockAchievement(achievement.id)) return false;

        if (pendingIds.Add(achievement.id)) pending.Enqueue(achievement);
        ModClass.LogInfo($"解锁修仙成就：{achievement.id}");
        return true;
    }

    /// <summary>在原版弹窗空闲时展示下一项；由 ModClass.Update 每帧调用。</summary>
    internal static void UpdatePresentation()
    {
        if (pending.Count == 0) return;
        AchievementPopup current = ResolvePopup();
        if (current == null || current.gameObject.activeSelf) return;

        Achievement achievement = pending.Dequeue();
        pendingIds.Remove(achievement.id);
        AchievementPopup.show(achievement);
        MapBox.aye();
    }

    private static AchievementPopup ResolvePopup()
    {
        if (popup != null && popup.gameObject.scene.IsValid()) return popup;

        AchievementPopup[] candidates = Resources.FindObjectsOfTypeAll<AchievementPopup>();
        for (var i = 0; i < candidates.Length; i++)
        {
            AchievementPopup candidate = candidates[i];
            if (candidate == null || !candidate.gameObject.scene.IsValid()) continue;
            popup = candidate;
            return popup;
        }
        return null;
    }

    private static void ClearWorldState()
    {
        pending.Clear();
        pendingIds.Clear();
        popup = null;
    }
}
