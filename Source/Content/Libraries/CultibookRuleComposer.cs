using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Libraries;

internal sealed class CultibookRuleContext
{
    public ActorExtend Creator;
    public int Seed;
    public int CreatorLevel;
    public string CultivateMethodId;
    public int PrimaryElement = NamingRuleUtils.NoElement;
    public int SecondaryElement = NamingRuleUtils.NoElement;
    public float PrimaryElementScore;
    public float SecondaryElementScore;
    public float MasteryBias = 0.5f;
    public float ArmorBias = 0.5f;
    public int AlignedSkillCount;
    public readonly float[] RootValues = new float[8];
    public readonly float[] ElementScores = new float[8];
    public readonly List<CultibookSkillCandidate> Skills = new();
}

internal sealed class CultibookSkillCandidate
{
    public Entity Skill;
    public string Signature;
    public float[] Elements;
    public int ModifierCount;
}

public static class CultibookRuleComposer
{
    private const int MaxCultibookNameLength = 8;
    private const int MaxCultivationLevel = 20;

    public static CultibookAsset CreateDraft(ActorExtend creator)
    {
        var draft = new CultibookAsset
        {
            id = Guid.NewGuid().ToString()
        };
        var context = CreateContext(creator, draft.id, null, null);
        var profile = SelectProfile(context);
        ComposeLevelRange(context, profile, out var minLevel, out var maxLevel);

        draft.FinalStats = ComposeStats(context, profile);
        draft.ElementReq = ComposeElementRequirement(context, profile);
        draft.ElementAffinityThreshold = profile.AffinityThreshold;
        draft.MinLevel = minLevel;
        draft.MaxLevel = maxLevel;
        draft.CultivateMethodId = context.CultivateMethodId;
        draft.SkillPool = ComposeSkillPool(context, profile, minLevel, maxLevel);
        draft.Level = CalculateLevel(draft.FinalStats, draft.SkillPool, draft.CultivateMethodId, creator);
        draft.Name = ComposeName(context, profile, draft.Level);
        draft.Description = ComposeDescription(context, profile, draft.SkillPool);
        draft.ConflictTags = Array.Empty<string>();
        draft.SynergyTags = Array.Empty<string>();
        return draft;
    }

    public static CultibookAsset CreateImprovedDraft(CultibookAsset original, ActorExtend creator)
    {
        if (original == null) return null;

        var draft = new CultibookAsset
        {
            id = Guid.NewGuid().ToString()
        };
        var context = CreateContext(creator, draft.id, original.CultivateMethodId, original);
        var profile = SelectProfile(context);
        var creatorLevel = context.CreatorLevel;

        draft.FinalStats = ComposeImprovedStats(original, context, profile);
        draft.ElementReq = ComposeImprovedRequirement(original, context, profile);
        draft.ElementAffinityThreshold = Mathf.Clamp(
            Mathf.Max(original.ElementAffinityThreshold, profile.AffinityThreshold), 0.15f, 0.75f);
        draft.MinLevel = Mathf.Clamp(Mathf.Min(original.MinLevel, creatorLevel), 0, MaxCultivationLevel);
        draft.MaxLevel = Mathf.Clamp(Mathf.Max(Mathf.Max(original.MaxLevel + 1, creatorLevel), draft.MinLevel),
            0, MaxCultivationLevel);
        draft.CultivateMethodId = context.CultivateMethodId;
        draft.SkillPool = ComposeImprovedSkillPool(original, context, profile, draft.MinLevel, draft.MaxLevel);
        var calculatedLevel = CalculateLevel(draft.FinalStats, draft.SkillPool, draft.CultivateMethodId, creator);
        draft.Level = MaxItemLevel(original.Level, calculatedLevel);
        draft.Name = ComposeImprovedName(original.Name, context, profile, draft.Level);
        draft.Description = ComposeImprovedDescription(original.Name, context, profile, draft.SkillPool);
        draft.ConflictTags = original.ConflictTags?.ToArray() ?? Array.Empty<string>();
        draft.SynergyTags = original.SynergyTags?.ToArray() ?? Array.Empty<string>();
        return draft;
    }

    public static CultibookAsset NormalizeDraft(CultibookAsset draft, ActorExtend creator,
        CultibookAsset original = null)
    {
        if (draft == null)
        {
            return original == null ? CreateDraft(creator) : CreateImprovedDraft(original, creator);
        }

        if (string.IsNullOrEmpty(draft.id)) draft.id = Guid.NewGuid().ToString();
        var context = CreateContext(creator, draft.id, draft.CultivateMethodId, original);
        var profile = SelectProfile(context);
        draft.CultivateMethodId = context.CultivateMethodId;

        if (!HasStats(draft.FinalStats))
        {
            draft.FinalStats = original == null
                ? ComposeStats(context, profile)
                : ComposeImprovedStats(original, context, profile);
        }

        if (!HasRequirement(draft.ElementReq))
        {
            draft.ElementReq = original == null
                ? ComposeElementRequirement(context, profile)
                : ComposeImprovedRequirement(original, context, profile);
        }
        else
        {
            draft.ElementReq = ClampElementRequirement(draft.ElementReq, creator);
        }

        if (draft.ElementAffinityThreshold <= 0f)
        {
            draft.ElementAffinityThreshold = profile.AffinityThreshold;
        }
        draft.ElementAffinityThreshold = Mathf.Clamp(draft.ElementAffinityThreshold, 0.15f, 0.75f);

        if (draft.MaxLevel <= 0)
        {
            ComposeLevelRange(context, profile, out draft.MinLevel, out draft.MaxLevel);
        }
        else
        {
            ClampLevelRange(draft.MinLevel, draft.MaxLevel, creator, out var minLevel, out var maxLevel);
            draft.MinLevel = minLevel;
            draft.MaxLevel = maxLevel;
        }

        if (draft.SkillPool == null || draft.SkillPool.Count == 0)
        {
            draft.SkillPool = original == null
                ? ComposeSkillPool(context, profile, draft.MinLevel, draft.MaxLevel)
                : ComposeImprovedSkillPool(original, context, profile, draft.MinLevel, draft.MaxLevel);
        }
        else
        {
            draft.SkillPool = NormalizeSkillPool(draft.SkillPool, draft.MinLevel, draft.MaxLevel);
        }

        var calculatedLevel = CalculateLevel(draft.FinalStats, draft.SkillPool, draft.CultivateMethodId, creator);
        draft.Level = MaxItemLevel(draft.Level, calculatedLevel);
        if (original != null) draft.Level = MaxItemLevel(draft.Level, original.Level);
        if (string.IsNullOrWhiteSpace(draft.Name))
        {
            draft.Name = original == null
                ? ComposeName(context, profile, draft.Level)
                : ComposeImprovedName(original.Name, context, profile, draft.Level);
        }
        if (string.IsNullOrWhiteSpace(draft.Description))
        {
            draft.Description = original == null
                ? ComposeDescription(context, profile, draft.SkillPool)
                : ComposeImprovedDescription(original.Name, context, profile, draft.SkillPool);
        }

        draft.ConflictTags ??= original?.ConflictTags?.ToArray() ?? Array.Empty<string>();
        draft.SynergyTags ??= original?.SynergyTags?.ToArray() ?? Array.Empty<string>();
        return draft;
    }

    public static ElementRequirement ClampElementRequirement(ElementRequirement requirement, ActorExtend creator)
    {
        if (creator == null || !creator.HasElementRoot()) return new ElementRequirement();
        var root = creator.GetElementRoot();
        requirement.MinIron = Mathf.Clamp(requirement.MinIron, 0f, root.Iron);
        requirement.MinWood = Mathf.Clamp(requirement.MinWood, 0f, root.Wood);
        requirement.MinWater = Mathf.Clamp(requirement.MinWater, 0f, root.Water);
        requirement.MinFire = Mathf.Clamp(requirement.MinFire, 0f, root.Fire);
        requirement.MinEarth = Mathf.Clamp(requirement.MinEarth, 0f, root.Earth);
        requirement.MinNeg = Mathf.Clamp(requirement.MinNeg, 0f, root.Neg);
        requirement.MinPos = Mathf.Clamp(requirement.MinPos, 0f, root.Pos);
        requirement.MinEntropy = Mathf.Clamp(requirement.MinEntropy, 0f, root.Entropy);
        return requirement;
    }

    public static void ClampLevelRange(int minLevel, int maxLevel, ActorExtend creator,
        out int clampedMinLevel, out int clampedMaxLevel)
    {
        var creatorLevel = GetCreatorLevel(creator);
        clampedMinLevel = Mathf.Clamp(Mathf.Min(minLevel, creatorLevel), 0, MaxCultivationLevel);
        clampedMaxLevel = Mathf.Clamp(Mathf.Max(Mathf.Max(maxLevel, creatorLevel), clampedMinLevel),
            0, MaxCultivationLevel);
    }

    public static ItemLevel CalculateLevel(BaseStats finalStats, IReadOnlyCollection<SkillPoolEntry> skillPool,
        string cultivateMethodId, ActorExtend creator)
    {
        var statsValue = 0f;
        if (finalStats?._stats_list is IList<BaseStatsContainer> statsList)
        {
            foreach (var stat in statsList)
            {
                statsValue += Mathf.Abs(stat.value);
            }
        }

        var modifierCount = 0;
        if (skillPool != null)
        {
            foreach (var entry in skillPool)
            {
                if (entry?.SkillContainer.IsNull != false) continue;
                modifierCount += entry.SkillContainer.GetComponentTypes()
                    .Count(type => typeof(IModifier).IsAssignableFrom(type));
            }
        }

        var creatorLevel = GetCreatorLevel(creator);
        var skillCount = skillPool?.Count ?? 0;
        var score = creatorLevel * 2.1f
                    + Mathf.Min(statsValue, 4f) * 3f
                    + skillCount * 1.7f
                    + Mathf.Min(modifierCount, 10) * 0.28f;
        if (!string.IsNullOrEmpty(cultivateMethodId) && CultivateMethods.Standard != null
                                                        && cultivateMethodId != CultivateMethods.Standard.id)
        {
            score += 1.25f;
        }

        var quality = Mathf.Clamp(Mathf.RoundToInt(score), 0, 35);
        return new ItemLevel
        {
            Stage = Mathf.Clamp(quality / 9, 0, 3),
            Level = Mathf.Clamp(quality % 9, 0, 8)
        };
    }

    private static CultibookRuleContext CreateContext(ActorExtend creator, string seedKey,
        string preferredMethodId, CultibookAsset original)
    {
        var actorId = creator?.Base?.data?.id ?? 0;
        var context = new CultibookRuleContext
        {
            Creator = creator,
            CreatorLevel = GetCreatorLevel(creator),
            Seed = NamingRuleUtils.StableHash($"{actorId}|{seedKey}|{original?.id}"),
        };

        ApplyRootEvidence(context);
        ApplySkillEvidence(context);
        ApplyOriginalEvidence(context, original);
        context.CultivateMethodId = DetermineCultivateMethodId(context, preferredMethodId);
        ApplyMethodElementBias(context);
        ResolveElements(context);
        ResolveStatBias(context);
        return context;
    }

    private static void ApplyRootEvidence(CultibookRuleContext context)
    {
        if (context.Creator == null || !context.Creator.HasElementRoot()) return;
        var values = NamingRuleUtils.GetElementValues(context.Creator.GetElementRoot());
        var sum = values.Sum(value => Mathf.Max(0f, value));
        for (var i = 0; i < values.Length; i++)
        {
            context.RootValues[i] = Mathf.Max(0f, values[i]);
            if (sum > 0f) context.ElementScores[i] += context.RootValues[i] / sum * 8f;
        }
    }

    private static void ApplySkillEvidence(CultibookRuleContext context)
    {
        if (context.Creator?.all_skills == null) return;
        foreach (var skill in context.Creator.all_skills)
        {
            if (skill.IsNull || !skill.HasComponent<SkillContainer>()) continue;
            var container = skill.GetComponent<SkillContainer>();
            var asset = container.Asset;
            if (asset == null || string.IsNullOrEmpty(container.SkillEntityAssetID)) continue;

            var elements = asset.Element.AsArray();
            var sum = elements.Sum(value => Mathf.Max(0f, value));
            if (sum > 0f)
            {
                for (var i = 0; i < elements.Length; i++)
                {
                    elements[i] = Mathf.Max(0f, elements[i]) / sum;
                    context.ElementScores[i] += elements[i] * 2.5f;
                }
            }

            context.Skills.Add(new CultibookSkillCandidate
            {
                Skill = skill,
                Signature = SkillContainerSignature.Build(skill),
                Elements = elements,
                ModifierCount = skill.GetComponentTypes().Count(type => typeof(IModifier).IsAssignableFrom(type))
            });
        }
    }

    private static void ApplyOriginalEvidence(CultibookRuleContext context, CultibookAsset original)
    {
        if (original == null) return;
        var requirements = RequirementValues(original.ElementReq);
        var requirementSum = requirements.Sum();
        if (requirementSum > 0f)
        {
            for (var i = 0; i < requirements.Length; i++)
            {
                context.ElementScores[i] += requirements[i] / requirementSum * 12f;
            }
        }

        if (original.FinalStats?._stats_list is not IList<BaseStatsContainer> stats) return;
        foreach (var stat in stats)
        {
            var elementIndex = ResolveStatElement(stat.id);
            if (elementIndex >= 0) context.ElementScores[elementIndex] += Mathf.Abs(stat.value) * 4f;
        }
    }

    private static void ApplyMethodElementBias(CultibookRuleContext context)
    {
        if (CultivateMethods.WaterMeditation != null
            && context.CultivateMethodId == CultivateMethods.WaterMeditation.id)
        {
            context.ElementScores[ElementIndex.Water] += 4f;
        }
        else if (CultivateMethods.KillAbsorb != null
                 && context.CultivateMethodId == CultivateMethods.KillAbsorb.id)
        {
            context.ElementScores[ElementIndex.Neg] += 2.5f;
            context.ElementScores[ElementIndex.Entropy] += 1.5f;
        }
        else if (CultivateMethods.KingdomFortune != null
                 && context.CultivateMethodId == CultivateMethods.KingdomFortune.id)
        {
            context.ElementScores[ElementIndex.Pos] += 2.5f;
            context.ElementScores[ElementIndex.Earth] += 1.5f;
        }
    }

    private static void ResolveElements(CultibookRuleContext context)
    {
        context.PrimaryElement = NamingRuleUtils.GetMaxIndex(context.ElementScores, out context.PrimaryElementScore);
        context.SecondaryElement = NamingRuleUtils.GetSecondMaxIndex(context.ElementScores, context.PrimaryElement,
            out context.SecondaryElementScore);
        if (context.SecondaryElementScore < context.PrimaryElementScore * 0.42f)
        {
            context.SecondaryElement = NamingRuleUtils.NoElement;
            context.SecondaryElementScore = 0f;
        }

        if (context.PrimaryElement < 0) return;
        context.AlignedSkillCount = context.Skills.Count(candidate =>
            candidate.Elements != null && candidate.Elements.Length > context.PrimaryElement
                                       && candidate.Elements[context.PrimaryElement] >= 0.35f);
    }

    private static void ResolveStatBias(CultibookRuleContext context)
    {
        if (context.Creator?.Base == null || context.PrimaryElement < 0) return;
        var actor = context.Creator.Base;
        var mastery = Mathf.Max(0f, actor.stats[WorldboxGame.BaseStats.MasterStats[context.PrimaryElement]]);
        var armor = Mathf.Max(0f, actor.stats[WorldboxGame.BaseStats.ArmorStats[context.PrimaryElement]]);
        var total = mastery + armor;
        if (total <= 0f) return;
        context.MasteryBias = mastery / total;
        context.ArmorBias = armor / total;
    }

    private static string DetermineCultivateMethodId(CultibookRuleContext context, string preferredMethodId)
    {
        var preferred = ResolveAvailableMethod(preferredMethodId, context.Creator);
        if (preferred != null) return preferred.id;

        var mainMethod = context.Creator?.GetMainCultibook()?.CultivateMethodId;
        preferred = ResolveAvailableMethod(mainMethod, context.Creator);
        if (preferred != null) return preferred.id;

        var candidates = Manager.CultivateMethodLibrary.list
            .Where(method => IsMethodAvailable(method, context.Creator))
            .Select(method => new
            {
                Method = method,
                Score = ScoreMethod(method, context) + TieBreak(context.Seed, method.id)
            })
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Method.id, StringComparer.Ordinal)
            .ToArray();
        return candidates.Length > 0 ? candidates[0].Method.id : CultivateMethods.Standard.id;
    }

    private static CultivateMethodAsset ResolveAvailableMethod(string methodId, ActorExtend creator)
    {
        if (string.IsNullOrEmpty(methodId)) return null;
        var method = Manager.CultivateMethodLibrary.get(methodId);
        return IsMethodAvailable(method, creator) ? method : null;
    }

    private static bool IsMethodAvailable(CultivateMethodAsset method, ActorExtend creator)
    {
        if (method == null || creator == null) return false;
        return method.CanCultivate == null || method.CanCultivate(creator);
    }

    private static float ScoreMethod(CultivateMethodAsset method, CultibookRuleContext context)
    {
        var efficiency = Mathf.Max(0f, method.GetEfficiency?.Invoke(context.Creator) ?? 1f);
        var score = efficiency;
        if (CultivateMethods.Standard != null && method.id == CultivateMethods.Standard.id) score += 5f;
        if (CultivateMethods.WaterMeditation != null && method.id == CultivateMethods.WaterMeditation.id)
        {
            score += context.RootValues[ElementIndex.Water] * 3f;
            if (context.Creator.Base.current_tile != null && context.Creator.Base.current_tile.IsWater()) score += 4f;
        }
        if (CultivateMethods.BattleCultivate != null && method.id == CultivateMethods.BattleCultivate.id)
        {
            score += Mathf.Min(context.Skills.Count, 5) * 0.7f;
        }
        if (CultivateMethods.KillAbsorb != null && method.id == CultivateMethods.KillAbsorb.id)
        {
            score += Mathf.Log(context.Creator.Base.data.kills + 1f) * 0.8f;
            score += context.RootValues[ElementIndex.Neg] + context.RootValues[ElementIndex.Entropy];
        }
        if (CultivateMethods.KingdomFortune != null && method.id == CultivateMethods.KingdomFortune.id)
        {
            score += 8f;
        }
        return score;
    }

    private static CultibookRuleProfileAsset SelectProfile(CultibookRuleContext context)
    {
        var selected = Manager.CultibookRuleProfileLibrary.list
            .Select(profile => new
            {
                Profile = profile,
                Score = profile.ScoreFor(context) + TieBreak(context.Seed, profile.id)
            })
            .Where(entry => entry.Score > 0f)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Profile.id, StringComparer.Ordinal)
            .FirstOrDefault();
        return selected?.Profile ?? CultibookRuleProfiles.Balanced;
    }

    private static BaseStats ComposeStats(CultibookRuleContext context, CultibookRuleProfileAsset profile)
    {
        var stats = new BaseStats();
        var rootStrength = context.Creator != null && context.Creator.HasElementRoot()
            ? context.Creator.GetElementRoot().GetStrength()
            : 1f;
        var budget = Mathf.Clamp(1.05f + context.CreatorLevel * 0.075f
                                       + Mathf.Log(rootStrength + 1f) * 0.12f, 1f, 2.8f);
        ApplyElementStatBudget(stats, context.PrimaryElement, budget, profile.MasteryWeight, profile.ArmorWeight);
        if (context.SecondaryElement >= 0)
        {
            ApplyElementStatBudget(stats, context.SecondaryElement, budget * profile.SecondaryElementWeight,
                profile.MasteryWeight, profile.ArmorWeight);
        }
        return stats;
    }

    private static BaseStats ComposeImprovedStats(CultibookAsset original, CultibookRuleContext context,
        CultibookRuleProfileAsset profile)
    {
        var stats = new BaseStats();
        if (original.FinalStats?._stats_list is IList<BaseStatsContainer> originalStats)
        {
            var factor = 1.08f + Mathf.Min(context.CreatorLevel, 12) * 0.004f;
            foreach (var stat in originalStats)
            {
                stats[stat.id] = stat.value * factor;
            }
        }

        if (!HasStats(stats)) return ComposeStats(context, profile);
        var focusedBonus = 0.08f + context.CreatorLevel * 0.005f;
        ApplyElementStatBudget(stats, context.PrimaryElement, focusedBonus,
            profile.MasteryWeight, profile.ArmorWeight, true);
        return stats;
    }

    private static void ApplyElementStatBudget(BaseStats stats, int elementIndex, float budget,
        float masteryWeight, float armorWeight, bool additive = false)
    {
        if (elementIndex < ElementIndex.Iron || elementIndex > ElementIndex.Entropy || budget <= 0f) return;
        var masteryId = ResolveModStat(WorldboxGame.BaseStats.MasterStats[elementIndex]);
        var armorId = ResolveModStat(WorldboxGame.BaseStats.ArmorStats[elementIndex]);
        if (!string.IsNullOrEmpty(masteryId))
        {
            stats[masteryId] = (additive ? stats[masteryId] : 0f) + budget * masteryWeight;
        }
        if (!string.IsNullOrEmpty(armorId))
        {
            stats[armorId] = (additive ? stats[armorId] : 0f) + budget * armorWeight;
        }
    }

    private static ElementRequirement ComposeElementRequirement(CultibookRuleContext context,
        CultibookRuleProfileAsset profile)
    {
        var requirement = new ElementRequirement();
        SetRequirement(ref requirement, context.PrimaryElement,
            RootValue(context, context.PrimaryElement) * profile.RequirementRatio);
        SetRequirement(ref requirement, context.SecondaryElement,
            RootValue(context, context.SecondaryElement) * profile.RequirementRatio * profile.SecondaryElementWeight);
        return requirement;
    }

    private static ElementRequirement ComposeImprovedRequirement(CultibookAsset original,
        CultibookRuleContext context, CultibookRuleProfileAsset profile)
    {
        var requirement = HasRequirement(original.ElementReq)
            ? original.ElementReq
            : ComposeElementRequirement(context, profile);
        var values = RequirementValues(requirement);
        if (context.PrimaryElement >= 0) values[context.PrimaryElement] *= 1.04f;
        if (context.SecondaryElement >= 0) values[context.SecondaryElement] *= 1.02f;
        requirement = RequirementFromValues(values);
        return ClampElementRequirement(requirement, context.Creator);
    }

    private static void ComposeLevelRange(CultibookRuleContext context, CultibookRuleProfileAsset profile,
        out int minLevel, out int maxLevel)
    {
        if (context.Creator == null || !context.Creator.HasCultisys<Xian>())
        {
            minLevel = 0;
            maxLevel = MaxCultivationLevel;
            return;
        }

        var lowerReach = profile.Tag == "fortune" || profile.Tag == "balanced" ? 3 : 2;
        var upperReach = 4 + Mathf.Min(3, context.CreatorLevel / 4);
        minLevel = Mathf.Max(0, context.CreatorLevel - lowerReach);
        maxLevel = Mathf.Min(MaxCultivationLevel, context.CreatorLevel + upperReach);
    }

    private static List<SkillPoolEntry> ComposeSkillPool(CultibookRuleContext context,
        CultibookRuleProfileAsset profile, int minLevel, int maxLevel)
    {
        var targetCount = context.CreatorLevel switch
        {
            >= 5 => 3,
            >= 2 => 2,
            _ => 1
        };
        targetCount = Mathf.Min(targetCount, profile.MaxSkillCount);
        return RankSkills(context, profile)
            .Take(targetCount)
            .Select((candidate, index) => CreateSkillPoolEntry(candidate.Skill, index, profile,
                minLevel, maxLevel))
            .Where(entry => entry != null)
            .ToList();
    }

    private static List<SkillPoolEntry> ComposeImprovedSkillPool(CultibookAsset original,
        CultibookRuleContext context, CultibookRuleProfileAsset profile, int minLevel, int maxLevel)
    {
        var result = new List<SkillPoolEntry>();
        var signatures = new HashSet<string>(StringComparer.Ordinal);
        if (original.SkillPool != null)
        {
            foreach (var entry in original.SkillPool)
            {
                if (entry?.SkillContainer.IsNull != false
                    || !entry.SkillContainer.HasComponent<SkillContainer>()) continue;
                var signature = SkillContainerSignature.Build(entry.SkillContainer);
                if (!signatures.Add(signature)) continue;
                var clone = entry.SkillContainer.Store.CloneEntity(entry.SkillContainer);
                clone.AddTag<TagOccupied>();
                result.Add(new SkillPoolEntry
                {
                    SkillContainer = clone,
                    BaseChance = Mathf.Clamp(entry.BaseChance * 1.15f, 0.02f, 0.25f),
                    MasteryThreshold = Mathf.Clamp(entry.MasteryThreshold * 0.9f, 0f, 100f),
                    LevelRequirement = Mathf.Clamp(entry.LevelRequirement - 1, minLevel, maxLevel)
                });
            }
        }

        if (result.Count == 0) return ComposeSkillPool(context, profile, minLevel, maxLevel);
        if (result.Count >= profile.MaxSkillCount) return result;

        var addition = RankSkills(context, profile).FirstOrDefault(candidate => signatures.Add(candidate.Signature));
        if (addition != null)
        {
            var entry = CreateSkillPoolEntry(addition.Skill, result.Count, profile, minLevel, maxLevel);
            if (entry != null) result.Add(entry);
        }
        return result;
    }

    private static IEnumerable<CultibookSkillCandidate> RankSkills(CultibookRuleContext context,
        CultibookRuleProfileAsset profile)
    {
        return context.Skills
            .Select(candidate => new
            {
                Candidate = candidate,
                Score = ScoreSkill(candidate, context, profile)
            })
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Candidate.Signature, StringComparer.Ordinal)
            .Select(entry => entry.Candidate);
    }

    private static float ScoreSkill(CultibookSkillCandidate candidate, CultibookRuleContext context,
        CultibookRuleProfileAsset profile)
    {
        var score = candidate.ModifierCount * 0.45f;
        if (candidate.Elements != null)
        {
            if (context.PrimaryElement >= 0)
            {
                score += candidate.Elements[context.PrimaryElement] * 12f;
            }
            if (context.SecondaryElement >= 0)
            {
                score += candidate.Elements[context.SecondaryElement] * 6f * profile.SecondaryElementWeight;
            }
        }
        return score + TieBreak(context.Seed, candidate.Signature);
    }

    private static SkillPoolEntry CreateSkillPoolEntry(Entity source, int index,
        CultibookRuleProfileAsset profile, int minLevel, int maxLevel)
    {
        if (source.IsNull || !source.HasComponent<SkillContainer>()) return null;
        var clone = source.Store.CloneEntity(source);
        clone.AddTag<TagOccupied>();
        var levelRequirement = Mathf.Clamp(minLevel + index * Mathf.Max(1, (maxLevel - minLevel) / 3),
            minLevel, maxLevel);
        return new SkillPoolEntry
        {
            SkillContainer = clone,
            BaseChance = Mathf.Clamp(0.09f - index * 0.02f + profile.SkillChanceBonus, 0.025f, 0.2f),
            MasteryThreshold = Mathf.Clamp(20f + index * 25f, 0f, 100f),
            LevelRequirement = levelRequirement
        };
    }

    private static List<SkillPoolEntry> NormalizeSkillPool(IEnumerable<SkillPoolEntry> skillPool,
        int minLevel, int maxLevel)
    {
        var result = new List<SkillPoolEntry>();
        var signatures = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in skillPool)
        {
            if (entry?.SkillContainer.IsNull != false
                || !entry.SkillContainer.HasComponent<SkillContainer>()) continue;
            var signature = SkillContainerSignature.Build(entry.SkillContainer);
            if (!signatures.Add(signature))
            {
                entry.SkillContainer.RemoveTag<TagOccupied>();
                continue;
            }

            entry.SkillContainer.AddTag<TagOccupied>();
            entry.BaseChance = Mathf.Clamp(entry.BaseChance <= 0f ? 0.05f : entry.BaseChance, 0.01f, 0.25f);
            entry.MasteryThreshold = Mathf.Clamp(entry.MasteryThreshold, 0f, 100f);
            entry.LevelRequirement = Mathf.Clamp(entry.LevelRequirement, minLevel, maxLevel);
            result.Add(entry);
        }
        return result;
    }

    private static string ComposeName(CultibookRuleContext context, CultibookRuleProfileAsset profile,
        ItemLevel level)
    {
        var elementStem = PickElementStem(context.PrimaryElement, context.Seed);
        var profileStem = profile.PickNameStem(context.Seed / 3 + 17);
        var suffix = profile.PickSuffix(context.Seed / 5 + 29);
        var qualityPrefix = PickQualityPrefix(level, context.Seed);
        var candidates = new List<string>();
        AddNameCandidate(candidates, $"{qualityPrefix}{elementStem}{profileStem}{suffix}");
        AddNameCandidate(candidates, $"{elementStem}{profileStem}{suffix}");
        AddNameCandidate(candidates, $"{profileStem}{elementStem}{suffix}");
        if (context.PrimaryElement < 0) AddNameCandidate(candidates, $"{profileStem}{suffix}");
        return candidates.Count > 0 ? candidates[context.Seed % candidates.Count] : "归元功";
    }

    private static string ComposeImprovedName(string originalName, CultibookRuleContext context,
        CultibookRuleProfileAsset profile, ItemLevel level)
    {
        var core = TrimCultibookSuffix(originalName);
        if (string.IsNullOrEmpty(core)) core = PickElementStem(context.PrimaryElement, context.Seed);
        if (core.Length > 4) core = core.Substring(0, 4);
        var prefix = level.Stage switch
        {
            >= 3 => "太上",
            2 => "九转",
            _ => "真"
        };
        var suffix = profile.PickSuffix(context.Seed / 7 + 31);
        var candidates = new List<string>();
        AddNameCandidate(candidates, $"{prefix}{core}{suffix}");
        AddNameCandidate(candidates, $"{core}真{suffix}");
        return candidates.Count > 0 ? candidates[context.Seed % candidates.Count] : "归元真诀";
    }

    private static string ComposeDescription(CultibookRuleContext context,
        CultibookRuleProfileAsset profile, IReadOnlyCollection<SkillPoolEntry> skillPool)
    {
        var primary = NamingRuleUtils.LocalizeElement(context.PrimaryElement);
        var secondary = NamingRuleUtils.LocalizeElement(context.SecondaryElement);
        var method = NamingRuleUtils.Localize(context.CultivateMethodId);
        var lead = string.IsNullOrEmpty(primary)
            ? profile.DescriptionFragment
            : $"以{primary}行为本，{profile.DescriptionFragment}";
        if (!string.IsNullOrEmpty(secondary)) lead += $"，兼纳{secondary}行";
        var description = $"{lead}；循{method}行功";
        var skills = ResolveSkillNames(skillPool);
        if (skills.Length > 0) description += $"，可悟{string.Join("、", skills)}";
        return description + "。";
    }

    private static string ComposeImprovedDescription(string originalName, CultibookRuleContext context,
        CultibookRuleProfileAsset profile, IReadOnlyCollection<SkillPoolEntry> skillPool)
    {
        var primary = NamingRuleUtils.LocalizeElement(context.PrimaryElement);
        var lead = string.IsNullOrEmpty(primary)
            ? $"承续{originalName}法理，{profile.DescriptionFragment}"
            : $"承续{originalName}法理，重炼{primary}行，{profile.DescriptionFragment}";
        var skills = ResolveSkillNames(skillPool);
        if (skills.Length > 0) lead += $"；法术传承以{string.Join("、", skills)}为要";
        return lead + "。";
    }

    private static string[] ResolveSkillNames(IEnumerable<SkillPoolEntry> skillPool)
    {
        if (skillPool == null) return Array.Empty<string>();
        return skillPool
            .Where(entry => entry?.SkillContainer.IsNull == false
                            && entry.SkillContainer.HasComponent<SkillContainer>())
            .Select(entry =>
            {
                var entity = entry.SkillContainer;
                if (entity.HasName && !string.IsNullOrEmpty(entity.Name.value)) return entity.Name.value;
                return NamingRuleUtils.Localize(entity.GetComponent<SkillContainer>().SkillEntityAssetID);
            })
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct()
            .Take(2)
            .ToArray();
    }

    private static string PickElementStem(int elementIndex, int seed)
    {
        return elementIndex switch
        {
            ElementIndex.Iron => NamingRuleUtils.Pick(seed, "庚金", "金阙", "玄锋"),
            ElementIndex.Wood => NamingRuleUtils.Pick(seed, "青木", "长青", "苍灵"),
            ElementIndex.Water => NamingRuleUtils.Pick(seed, "玄水", "沧浪", "寒泉"),
            ElementIndex.Fire => NamingRuleUtils.Pick(seed, "离火", "赤炎", "焚阳"),
            ElementIndex.Earth => NamingRuleUtils.Pick(seed, "厚土", "坤岳", "镇山"),
            ElementIndex.Neg => NamingRuleUtils.Pick(seed, "玄阴", "幽冥", "太阴"),
            ElementIndex.Pos => NamingRuleUtils.Pick(seed, "纯阳", "曜灵", "明光"),
            ElementIndex.Entropy => NamingRuleUtils.Pick(seed, "混沌", "归墟", "浊玄"),
            _ => string.Empty
        };
    }

    private static string PickQualityPrefix(ItemLevel level, int seed)
    {
        return level.Stage switch
        {
            >= 3 => NamingRuleUtils.Pick(seed, "太上", "无极", "太清"),
            2 => NamingRuleUtils.Pick(seed, "九转", "天元", "玄天"),
            _ => string.Empty
        };
    }

    private static void AddNameCandidate(List<string> candidates, string rawName)
    {
        var name = NormalizeCultibookName(rawName);
        if (string.IsNullOrEmpty(name) || name.Length > MaxCultibookNameLength || candidates.Contains(name)) return;
        candidates.Add(name);
    }

    private static string NormalizeCultibookName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var text = value.Trim().Replace(" ", string.Empty).Replace("（改进版）", string.Empty);
        while (text.Contains("玄玄")) text = text.Replace("玄玄", "玄");
        while (text.Contains("元元")) text = text.Replace("元元", "元");
        while (text.Contains("真真")) text = text.Replace("真真", "真");
        return text;
    }

    private static string TrimCultibookSuffix(string name)
    {
        var value = NormalizeCultibookName(name);
        foreach (var suffix in new[] { "真解", "秘典", "功", "诀", "经", "典", "法" })
        {
            if (value.EndsWith(suffix, StringComparison.Ordinal))
            {
                return value.Substring(0, value.Length - suffix.Length);
            }
        }
        return value;
    }

    private static bool HasStats(BaseStats stats)
    {
        return stats?._stats_list is IList<BaseStatsContainer> list && list.Count > 0;
    }

    private static bool HasRequirement(ElementRequirement requirement)
    {
        return RequirementValues(requirement).Any(value => value > 0f);
    }

    private static float RootValue(CultibookRuleContext context, int elementIndex)
    {
        return elementIndex >= 0 && elementIndex < context.RootValues.Length
            ? context.RootValues[elementIndex]
            : 0f;
    }

    private static string ResolveModStat(string baseStatId)
    {
        return WorldboxGame.BaseStats.StatsToModStats.TryGetValue(baseStatId, out var modStatId)
            ? modStatId
            : $"Mod{baseStatId}";
    }

    private static int ResolveStatElement(string statId)
    {
        for (var i = ElementIndex.Iron; i <= ElementIndex.Entropy; i++)
        {
            var mastery = WorldboxGame.BaseStats.MasterStats[i];
            var armor = WorldboxGame.BaseStats.ArmorStats[i];
            if (statId == mastery || statId == armor || statId == ResolveModStat(mastery)
                || statId == ResolveModStat(armor)) return i;
        }
        return NamingRuleUtils.NoElement;
    }

    private static float[] RequirementValues(ElementRequirement requirement)
    {
        return
        [
            requirement.MinIron,
            requirement.MinWood,
            requirement.MinWater,
            requirement.MinFire,
            requirement.MinEarth,
            requirement.MinNeg,
            requirement.MinPos,
            requirement.MinEntropy
        ];
    }

    private static ElementRequirement RequirementFromValues(float[] values)
    {
        return new ElementRequirement
        {
            MinIron = values[ElementIndex.Iron],
            MinWood = values[ElementIndex.Wood],
            MinWater = values[ElementIndex.Water],
            MinFire = values[ElementIndex.Fire],
            MinEarth = values[ElementIndex.Earth],
            MinNeg = values[ElementIndex.Neg],
            MinPos = values[ElementIndex.Pos],
            MinEntropy = values[ElementIndex.Entropy]
        };
    }

    private static void SetRequirement(ref ElementRequirement requirement, int elementIndex, float value)
    {
        value = Mathf.Max(0f, value);
        switch (elementIndex)
        {
            case ElementIndex.Iron: requirement.MinIron = value; break;
            case ElementIndex.Wood: requirement.MinWood = value; break;
            case ElementIndex.Water: requirement.MinWater = value; break;
            case ElementIndex.Fire: requirement.MinFire = value; break;
            case ElementIndex.Earth: requirement.MinEarth = value; break;
            case ElementIndex.Neg: requirement.MinNeg = value; break;
            case ElementIndex.Pos: requirement.MinPos = value; break;
            case ElementIndex.Entropy: requirement.MinEntropy = value; break;
        }
    }

    private static int GetCreatorLevel(ActorExtend creator)
    {
        return creator != null && creator.HasCultisys<Xian>()
            ? Mathf.Clamp(creator.GetCultisys<Xian>().CurrLevel, 0, MaxCultivationLevel)
            : 0;
    }

    private static ItemLevel MaxItemLevel(ItemLevel left, ItemLevel right)
    {
        return (int)left >= (int)right ? left : right;
    }

    private static float TieBreak(int seed, string id)
    {
        return (NamingRuleUtils.StableHash($"{seed}|{id}") % 1000) / 100000f;
    }
}
