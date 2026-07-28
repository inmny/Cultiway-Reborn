using System;
using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.Semantics;
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
    private const int WeightedSelectionLimit = 8;

    [ThreadStatic]
    private static StudyScratch studyScratch;

    private static readonly SemanticAsset[] ElementSemantics =
    {
        SkillSemantics.Element.Iron, SkillSemantics.Element.Wood, SkillSemantics.Element.Water,
        SkillSemantics.Element.Fire, SkillSemantics.Element.Earth, SkillSemantics.Element.Neg,
        SkillSemantics.Element.Pos, SkillSemantics.Element.Entropy
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

        StudyScratch scratch = studyScratch ??= new StudyScratch();
        scratch.Reset();
        ref var magic = ref actor.GetCultisys<Magic>();
        var maxRing = Cultisyses.GetMaxSpellRing(magic.CurrLevel);
        var capacity = Cultisyses.GetKnownSpellCapacity(magic.CurrLevel);
        var root = actor.GetElementRoot();
        GetKnownSpellEntries(actor, scratch.KnownSpells);
        for (int i = 0; i < scratch.KnownSpells.Count; i++)
        {
            KnownSpellEntry known = scratch.KnownSpells[i];
            scratch.KnownFamilies.Add(
                known.Profile.FamilySignature);
            scratch.KnownPrimaryElements.Add(
                known.Profile.PrimaryElement);
        }

        MagicWebQuery query = scratch.Query;
        query.MaxRing = maxRing;
        query.MaxResults = MagicSetting.MagicStudyQueryLimit;
        query.SelectionSeed = unchecked(
            actor.E.Id * 397 ^
            (int)(GetWorldTime() /
                  (TimeScales.SecPerYear * 5f)));
        query.AnySemantics.Add(SkillSemantics.Element.Generic);
        var strongestElementIndex = 0;
        var strongestElementAffinity = float.MinValue;
        for (var i = 0; i < ElementSemantics.Length; i++)
        {
            var elementalAffinity = ElementRequirement.GetElementAffinity(root[i]);
            if (elementalAffinity > strongestElementAffinity)
            {
                strongestElementAffinity = elementalAffinity;
                strongestElementIndex = i;
            }
            if (elementalAffinity >= MagicSetting.MagicStudyAffinityThreshold)
                query.AnySemantics.Add(ElementSemantics[i]);
        }
        query.AnySemantics.Add(ElementSemantics[strongestElementIndex]);

        manager.QueryStudyEntries(
            query,
            scratch.QueryEntries);
        for (int i = 0; i < scratch.QueryEntries.Count; i++)
        {
            MagicWebStudyEntryView entry =
                scratch.QueryEntries[i];
            var profile = entry.Profile;
            if (scratch.KnownFamilies.Contains(
                    profile.FamilySignature))
            {
                continue;
            }

            var affinity = profile.ElementRequirement.GetWeightedAffinity(root);
            if (affinity < MagicSetting.MagicStudyAffinityThreshold) continue;

            var novelty = scratch.KnownPrimaryElements.Contains(
                profile.PrimaryElement)
                ? 0f
                : 1f;
            var score = Score(profile, affinity, maxRing, novelty, entry.IsDefault);
            var replacement = default(Entity);
            if (scratch.KnownSpells.Count >= capacity)
            {
                if (!TryFindReplacement(
                        scratch.KnownSpells,
                        profile,
                        score,
                        root,
                        maxRing,
                        out replacement))
                {
                    continue;
                }
            }

            scratch.Candidates.Add(
                new MagicWebStudyCandidate(
                    entry.Container,
                    replacement,
                    profile,
                    affinity,
                    score,
                    ResolveDifficulty(profile)));
        }

        if (scratch.Candidates.Count == 0) return false;
        selected = WeightedSelect(
            scratch.Candidates,
            scratch.TopCandidates);
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

    private static void GetKnownSpellEntries(
        ActorExtend actor,
        List<KnownSpellEntry> result)
    {
        result.Clear();
        foreach (var container in actor.GetLearnedSkillsInOrder())
        {
            if (container.IsNull ||
                !SkillCastResourceResolver.UsesResource(container, SkillCastResources.Mana)) continue;
            var profile = MagicSpellProfile.Resolve(container);
            if (profile != null) result.Add(new KnownSpellEntry(container, profile));
        }
    }

    private static bool TryFindReplacement(IReadOnlyList<KnownSpellEntry> known, MagicSpellProfile candidate,
        float candidateScore, Core.Components.ElementRoot root, int maxRing, out Entity replacement)
    {
        replacement = default;
        var dominantElement = ResolveDominantElement(root);
        int dominantCount = 0;
        for (int i = 0; i < known.Count; i++)
        {
            if (known[i].Profile.PrimaryElement ==
                dominantElement)
            {
                dominantCount++;
            }
        }

        var weakestScore = float.MaxValue;
        for (int i = 0; i < known.Count; i++)
        {
            KnownSpellEntry item = known[i];
            if (item.Profile.PrimaryElement == dominantElement && dominantCount <= 1) continue;
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

    private static MagicWebStudyCandidate WeightedSelect(
        List<MagicWebStudyCandidate> candidates,
        List<MagicWebStudyCandidate> top)
    {
        top.Clear();
        for (int i = 0; i < candidates.Count; i++)
        {
            MagicWebStudyCandidate candidate = candidates[i];
            int insertIndex = 0;
            while (insertIndex < top.Count &&
                   top[insertIndex].Score >= candidate.Score)
            {
                insertIndex++;
            }

            if (insertIndex >= WeightedSelectionLimit)
            {
                continue;
            }

            top.Insert(insertIndex, candidate);
            if (top.Count > WeightedSelectionLimit)
            {
                top.RemoveAt(WeightedSelectionLimit);
            }
        }

        float total = 0f;
        for (int i = 0; i < top.Count; i++)
        {
            total += Mathf.Max(0.01f, top[i].Score);
        }

        var roll = Randy.randomFloat(0f, total);
        for (int i = 0; i < top.Count; i++)
        {
            MagicWebStudyCandidate candidate = top[i];
            roll -= Mathf.Max(0.01f, candidate.Score);
            if (roll <= 0f) return candidate;
        }

        return top[top.Count - 1];
    }

    private static SemanticAsset ResolveDominantElement(ElementRoot root)
    {
        var bestIndex = 0;
        for (var i = 1; i < ElementSemantics.Length; i++)
            if (root[i] > root[bestIndex]) bestIndex = i;
        return ElementSemantics[bestIndex];
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

    private sealed class StudyScratch
    {
        internal readonly MagicWebQuery Query = new();
        internal readonly List<KnownSpellEntry> KnownSpells = new();
        internal readonly HashSet<string> KnownFamilies =
            new(StringComparer.Ordinal);
        internal readonly HashSet<SemanticAsset>
            KnownPrimaryElements = new();
        internal readonly List<MagicWebStudyEntryView>
            QueryEntries = new();
        internal readonly List<MagicWebStudyCandidate>
            Candidates = new();
        internal readonly List<MagicWebStudyCandidate>
            TopCandidates = new(WeightedSelectionLimit);

        internal void Reset()
        {
            Query.RequiredSemantics.Clear();
            Query.AnySemantics.Clear();
            Query.ExcludedSemantics.Clear();
            KnownSpells.Clear();
            KnownFamilies.Clear();
            KnownPrimaryElements.Clear();
            QueryEntries.Clear();
            Candidates.Clear();
            TopCandidates.Clear();
        }
    }
}
