using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>维护器灵的持久成长、显化属性继承和战死复苏。</summary>
public static class ArtifactSpiritService
{
    private const int MaxLevel = 100;

    public static void Init()
    {
        ActorExtend.RegisterCachedStatsBuilder(ContributeAvatarStats);
        ActorExtend.RegisterActionOnDamageResolved(OnDamageResolved);
        ActorExtend.RegisterActionOnKill(OnKill);
        ActorExtend.RegisterActionOnDeath(OnDeath);
    }

    public static bool Awaken(Entity artifact)
    {
        ref ArtifactSpiritState state = ref artifact.GetComponent<ArtifactSpiritState>();
        if (state.awakened) return false;
        state.awakened = true;
        state.level = 1;
        state.bond = 1f;
        return true;
    }

    public static void AddExperience(Entity artifact, float experience, float bond)
    {
        if (!artifact.IsAvailable()) return;
        ref ArtifactSpiritState state = ref artifact.GetComponent<ArtifactSpiritState>();
        if (!state.awakened) return;

        state.experience += Mathf.Max(0f, experience);
        state.bond = Mathf.Clamp(state.bond + Mathf.Max(0f, bond), 0f, 100f);
        int previousLevel = state.level;
        while (state.level < MaxLevel && state.experience >= ExperienceForLevel(state.level + 1))
        {
            state.level++;
        }
        if (state.level != previousLevel) MarkAvatarStatsDirty(artifact);
    }

    public static float ResolveLevelScale(in ArtifactSpiritState state)
    {
        return 1f + Mathf.Max(0, state.level - 1) * 0.035f + state.bond * 0.0025f;
    }

    private static float ExperienceForLevel(int level)
    {
        return 8f * level * (level - 1);
    }

    private static void ContributeAvatarStats(ActorExtend extend, BaseStats stats)
    {
        if (!extend.TryGetComponent(out ArtifactSpiritAvatar avatar) ||
            !avatar.controller.IsAvailable()) return;
        Actor controller = avatar.controller.GetComponent<ActorBinder>().Actor;
        if (controller == null || controller.isRekt()) return;
        stats[S.damage] += controller.stats[S.damage] * avatar.damage_ratio;
        stats[S.health] += controller.stats[S.health] * avatar.health_ratio;
        stats[S.armor] += avatar.armor_bonus;
    }

    private static void OnDamageResolved(
        ActorExtend _,
        BaseSimObject attacker,
        float damage,
        ElementComposition __,
        AttackType ___)
    {
        if (attacker == null || attacker.isRekt() || !attacker.isActor() ||
            !attacker.a.GetExtend().TryGetComponent(out ArtifactSpiritAvatar avatar)) return;
        AddExperience(avatar.artifact, Mathf.Sqrt(Mathf.Max(0f, damage)) * 0.035f, 0.002f);
    }

    private static void OnKill(ActorExtend killer, Actor _, Kingdom __)
    {
        if (!killer.TryGetComponent(out ArtifactSpiritAvatar avatar)) return;
        AddExperience(avatar.artifact, 2f, 0.08f);
    }

    private static void OnDeath(ActorExtend extend)
    {
        if (!extend.TryGetComponent(out ArtifactSpiritAvatar avatar) ||
            !avatar.recover_on_death || !avatar.artifact.IsAvailable()) return;
        ref ArtifactSpiritState state = ref avatar.artifact.GetComponent<ArtifactSpiritState>();
        state.recovery_until = System.Math.Max(
            state.recovery_until,
            World.world.getCurWorldTime() + avatar.recovery_duration);
    }

    private static void MarkAvatarStatsDirty(Entity artifact)
    {
        var relations = artifact.GetRelations<ArtifactSpiritAvatarRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            Entity avatar = relations[i].avatar;
            if (!avatar.IsAvailable() || !avatar.TryGetComponent(out ActorBinder binder)) continue;
            if (binder.Actor == null || binder.Actor.isRekt()) continue;
            binder.Actor.GetExtend().MarkCultiwayStatsDirty();
        }
    }
}
