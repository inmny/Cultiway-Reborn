using System;
using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Artifacts;

/// <summary>解析宗门供奉能力、分组生效上限和藏宝阁世界位置。</summary>
public static class ArtifactSectService
{
    public static bool HasSectUse(Entity artifact)
    {
        ArtifactAbilitySet abilitySet = artifact.GetComponent<ArtifactAbilitySet>();
        for (int i = 0; i < abilitySet.abilities.Length; i++)
        {
            ArtifactAbilityAsset asset = Libraries.Manager.ArtifactAbilityLibrary.get(
                abilitySet.abilities[i].ability_id);
            if (asset.sect_use != null) return true;
        }
        return false;
    }

    public static void ContributeMemberStats(ActorExtend member, BaseStats stats)
    {
        Sect sect = member.sect;
        if (sect == null || sect.isRekt()) return;

        using ListPool<SectAbilityCandidate> candidates = new();
        foreach (Entity artifact in sect.Treasures.GetStoredItems())
        {
            if (!artifact.IsAvailable() || !IsInstalledFor(sect, artifact)) continue;
            ArtifactAbilitySet abilitySet = artifact.GetComponent<ArtifactAbilitySet>();
            for (int i = 0; i < abilitySet.abilities.Length; i++)
            {
                ArtifactAbilityInstance ability = abilitySet.abilities[i];
                ArtifactSectAbilityProfile profile = Libraries.Manager.ArtifactAbilityLibrary
                    .get(ability.ability_id).sect_use;
                if (profile?.ContributeMemberStats == null) continue;
                candidates.Add(new SectAbilityCandidate(
                    artifact,
                    ability,
                    profile,
                    profile.ResolvePriority?.Invoke(ability) ?? 1f));
            }
        }

        candidates.Sort(CompareCandidates);
        var baselineStats = (BaseStats)stats.Clone();
        Dictionary<string, int> groupCounts = new(StringComparer.Ordinal);
        for (int i = 0; i < candidates.Count; i++)
        {
            SectAbilityCandidate candidate = candidates[i];
            string group = candidate.profile.stacking_group;
            if (!string.IsNullOrEmpty(group))
            {
                groupCounts.TryGetValue(group, out int count);
                if (count >= Math.Max(1, candidate.profile.max_active)) continue;
                groupCounts[group] = count + 1;
            }

            candidate.profile.ContributeMemberStats(
                new ArtifactSectAbilityContext(sect, candidate.artifact, baselineStats),
                candidate.ability,
                stats);
        }
    }

    public static Building ResolveInstallationBuilding(Sect sect)
    {
        Building building = FindUsable(sect.GetBuildingListOfID(Buildings.SectTreasurePavilion.id));
        return building ?? FindUsable(sect.GetBuildingListOfID(Buildings.SectHall.id));
    }

    public static void MarkMemberStatsDirty(Sect sect)
    {
        List<Actor> members = sect.GetLivingMembers();
        for (int i = 0; i < members.Count; i++) members[i].GetExtend().MarkCultiwayStatsDirty();
    }

    private static bool IsInstalledFor(Sect sect, Entity artifact)
    {
        foreach (Entity owner in artifact.GetIncomingLinks<ArtifactSectInstallationRelation>().Entities)
        {
            if (owner.TryGetComponent(out SectInventoryBinder binder) && binder.Sect == sect) return true;
        }
        return false;
    }

    private static Building FindUsable(List<Building> buildings)
    {
        if (buildings == null) return null;
        for (int i = 0; i < buildings.Count; i++)
        {
            Building building = buildings[i];
            if (building.isUsable() && !building.isUnderConstruction()) return building;
        }
        return null;
    }

    private static int CompareCandidates(SectAbilityCandidate left, SectAbilityCandidate right)
    {
        int comparison = string.CompareOrdinal(left.profile.stacking_group, right.profile.stacking_group);
        if (comparison != 0) return comparison;
        comparison = right.priority.CompareTo(left.priority);
        if (comparison != 0) return comparison;
        comparison = left.artifact.Id.CompareTo(right.artifact.Id);
        if (comparison != 0) return comparison;
        return string.CompareOrdinal(left.ability.instance_id, right.ability.instance_id);
    }

    private readonly struct SectAbilityCandidate
    {
        public readonly Entity artifact;
        public readonly ArtifactAbilityInstance ability;
        public readonly ArtifactSectAbilityProfile profile;
        public readonly float priority;

        public SectAbilityCandidate(
            Entity artifact,
            ArtifactAbilityInstance ability,
            ArtifactSectAbilityProfile profile,
            float priority)
        {
            this.artifact = artifact;
            this.ability = ability;
            this.profile = profile;
            this.priority = priority;
        }
    }
}
