using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content;

public readonly struct MagicWebStudyCandidate
{
    public readonly Entity Container;
    public readonly Entity Replacement;
    public readonly MagicSpellProfile Profile;
    public readonly float Affinity;
    public readonly float Score;
    public readonly float Difficulty;

    public MagicWebStudyCandidate(Entity container, Entity replacement, MagicSpellProfile profile, float affinity,
        float score, float difficulty)
    {
        Container = container;
        Replacement = replacement;
        Profile = profile;
        Affinity = affinity;
        Score = score;
        Difficulty = difficulty;
    }
}

/// <summary>
/// 负责查询魔网、选择研究对象并重新校验进行中的研究目标。
/// </summary>
public static class MagicWebStudyPlanner
{
    private static readonly string[] ElementTags =
    {
        SkillTags.Element.Iron, SkillTags.Element.Wood, SkillTags.Element.Water, SkillTags.Element.Fire,
        SkillTags.Element.Earth, SkillTags.Element.Neg, SkillTags.Element.Pos, SkillTags.Element.Entropy
    };

    /// <summary>
    /// 判断魔法师当前是否应该尝试从魔网研究法术。
    /// </summary>
    public static bool ShouldStudy(ActorExtend actor)
    {
        if (actor == null || !actor.HasCultisys<Magic>()) return false;
        if (MagicWebManager.Instance == null) return false;
        if (!actor.TryGetComponent(out MagicStudyState state)) return true;
        return GetWorldTime() >= state.NextStudyWorldTime;
    }

    /// <summary>
    /// 从有界魔网查询结果中为魔法师选出研究目标，并在容量已满时给出可替换法术。
    /// </summary>
    public static bool TrySelectCandidate(ActorExtend actor, out MagicWebStudyCandidate selected)
    {
        selected = default;
        if (actor == null || !actor.HasCultisys<Magic>() || !actor.HasElementRoot()) return false;
        var manager = MagicWebManager.Instance;
        if (manager == null) return false;

        ref var magic = ref actor.GetCultisys<Magic>();
        var maxRing = Cultisyses.GetMaxSpellRing(magic.CurrLevel);
        var capacity = Cultisyses.GetKnownSpellCapacity(magic.CurrLevel);
        var root = actor.GetElementRoot();
        var known = GetKnownSpellEntries(actor);
        var knownFamilies = new HashSet<string>(known.Select(item => item.Profile.FamilySignature),
            StringComparer.Ordinal);
        var knownPrimaryElements = new HashSet<string>(known.Select(item => item.Profile.PrimaryElementTag),
            StringComparer.Ordinal);

        var query = new MagicWebQuery
        {
            MaxRing = maxRing,
            MaxResults = MagicSetting.MagicStudyQueryLimit,
            SelectionSeed = unchecked(actor.E.Id * 397 ^
                                      (int)(GetWorldTime() / (TimeScales.SecPerYear * 5f)))
        };
        query.AnyTags.Add(SkillTags.Element.Generic);
        var strongestElementIndex = 0;
        var strongestElementAffinity = float.MinValue;
        for (var i = 0; i < ElementTags.Length; i++)
        {
            var elementalAffinity = ElementRequirement.GetElementAffinity(root[i]);
            if (elementalAffinity > strongestElementAffinity)
            {
                strongestElementAffinity = elementalAffinity;
                strongestElementIndex = i;
            }
            if (elementalAffinity >= MagicSetting.MagicStudyAffinityThreshold) query.AnyTags.Add(ElementTags[i]);
        }
        query.AnyTags.Add(ElementTags[strongestElementIndex]);

        var candidates = new List<MagicWebStudyCandidate>();
        foreach (var entry in manager.Query(query))
        {
            var profile = entry.Profile;
            if (knownFamilies.Contains(profile.FamilySignature)) continue;
            var affinity = profile.ElementRequirement.GetWeightedAffinity(root);
            if (affinity < MagicSetting.MagicStudyAffinityThreshold) continue;

            var novelty = knownPrimaryElements.Contains(profile.PrimaryElementTag) ? 0f : 1f;
            var score = Score(profile, affinity, maxRing, novelty, entry.IsDefault);
            var replacement = default(Entity);
            if (known.Count >= capacity)
            {
                if (!TryFindReplacement(known, profile, score, root, maxRing, out replacement)) continue;
            }

            candidates.Add(new MagicWebStudyCandidate(entry.Container, replacement, profile, affinity, score,
                ResolveDifficulty(profile)));
        }

        if (candidates.Count == 0) return false;
        selected = WeightedSelect(candidates);
        return true;
    }

    /// <summary>
    /// 校验正在研究的条目并重新取得其档案、亲和度和研究难度。
    /// </summary>
    public static bool TryResolve(ActorExtend actor, in MagicStudyState state, out MagicSpellProfile profile,
        out float affinity, out float difficulty)
    {
        profile = null;
        affinity = 0f;
        difficulty = 0f;
        if (actor == null || state.Candidate.IsNull || !actor.HasElementRoot()) return false;
        var manager = MagicWebManager.Instance;
        if (manager == null || !manager.Contains(state.Candidate) ||
            !manager.TryGetProfile(state.Candidate, out profile)) return false;
        if (!state.Replacement.IsNull && !actor.OwnsLearnedSkill(state.Replacement)) return false;

        affinity = profile.ElementRequirement.GetWeightedAffinity(actor.GetElementRoot());
        if (affinity < MagicSetting.MagicStudyAffinityThreshold) return false;
        difficulty = ResolveDifficulty(profile);
        return true;
    }

    private static List<KnownSpellEntry> GetKnownSpellEntries(ActorExtend actor)
    {
        var result = new List<KnownSpellEntry>();
        foreach (var container in actor.GetLearnedSkillsInOrder())
        {
            if (container.IsNull ||
                !SkillCastResourceResolver.UsesResource(container, SkillCastResources.Mana)) continue;
            var profile = MagicSpellProfile.Resolve(container);
            if (profile != null) result.Add(new KnownSpellEntry(container, profile));
        }
        return result;
    }

    private static bool TryFindReplacement(IReadOnlyList<KnownSpellEntry> known, MagicSpellProfile candidate,
        float candidateScore, Core.Components.ElementRoot root, int maxRing, out Entity replacement)
    {
        replacement = default;
        var dominantTag = ResolveDominantElementTag(root);
        var dominantCount = known.Count(item => item.Profile.PrimaryElementTag == dominantTag);
        var weakestScore = float.MaxValue;
        foreach (var item in known)
        {
            if (item.Profile.PrimaryElementTag == dominantTag && dominantCount <= 1) continue;
            var affinity = item.Profile.ElementRequirement.GetWeightedAffinity(root);
            var score = Score(item.Profile, affinity, maxRing, 0f, false);
            if (score >= weakestScore) continue;
            weakestScore = score;
            replacement = item.Container;
        }

        return !replacement.IsNull && candidateScore >= weakestScore * MagicSetting.MagicReplacementScoreRatio;
    }

    private static float Score(MagicSpellProfile profile, float affinity, int maxRing, float novelty,
        bool isDefault)
    {
        var ringFit = maxRing <= 0 ? 1f : 0.5f + 0.5f * profile.Ring / maxRing;
        return affinity * 55f + ringFit * 20f + novelty * 15f + (isDefault ? 10f : 0f);
    }

    private static float ResolveDifficulty(MagicSpellProfile profile)
    {
        return MagicSetting.MagicStudyBaseDifficulty * Mathf.Pow(profile.Ring + 1f, 2f);
    }

    private static MagicWebStudyCandidate WeightedSelect(List<MagicWebStudyCandidate> candidates)
    {
        var top = candidates.OrderByDescending(candidate => candidate.Score).Take(8).ToArray();
        var total = top.Sum(candidate => Mathf.Max(0.01f, candidate.Score));
        var roll = Randy.randomFloat(0f, total);
        foreach (var candidate in top)
        {
            roll -= Mathf.Max(0.01f, candidate.Score);
            if (roll <= 0f) return candidate;
        }
        return top[top.Length - 1];
    }

    private static string ResolveDominantElementTag(ElementRoot root)
    {
        var bestIndex = 0;
        for (var i = 1; i < ElementTags.Length; i++)
            if (root[i] > root[bestIndex]) bestIndex = i;
        return ElementTags[bestIndex];
    }

    private static double GetWorldTime()
    {
        return World.world?.map_stats?.world_time ?? 0d;
    }

    private readonly struct KnownSpellEntry
    {
        public readonly Entity Container;
        public readonly MagicSpellProfile Profile;

        public KnownSpellEntry(Entity container, MagicSpellProfile profile)
        {
            Container = container;
            Profile = profile;
        }
    }
}
