using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Cultiway.Content.CreatureCompositions.Libraries;
using Cultiway.Content.CreatureCompositions.Models;
using Cultiway.Core.Semantics;
using Cultiway.Patch;
using ContentLibraries = Cultiway.Content.Libraries.Manager;

namespace Cultiway.Content.CreatureCompositions.Services;

/// <summary>把身体方案整理为可由所有玩法共用的固定结果。</summary>
public static class CreaturePhenotypeCompiler
{
    public const int MaximumCompiledPhenotypes = 8192;
    public const int MaximumBodySlots = 12;
    public const int MaximumSlotCapacity = 8;
    public const int MaximumOverlayLayers = 3;

    private static readonly Dictionary<string, int> indexBySignature = new(StringComparer.Ordinal);
    private static readonly List<CompiledCreaturePhenotype> compiledByIndex = [null];
    private static bool initialized;

    public static int CachedCount => compiledByIndex.Count - 1;

    /// <summary>登记一次世界清理回调。</summary>
    internal static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        PatchMapBox.RegisterActionOnClearWorld(ClearWorldState);
    }

    /// <summary>整理身体方案；相同组合在当前世界共用同一个结果。</summary>
    public static bool TryGetOrCompile(
        CreaturePhenotypePlan plan,
        out CompiledCreaturePhenotype compiled)
    {
        Initialize();
        compiled = null;
        if (!TryBuild(plan, out CompilationDraft draft, out string signature)) return false;

        if (indexBySignature.TryGetValue(signature, out int oldIndex))
        {
            compiled = compiledByIndex[oldIndex];
            return true;
        }

        if (CachedCount >= MaximumCompiledPhenotypes) return false;

        int newIndex = compiledByIndex.Count;
        compiled = draft.Create(newIndex, signature);
        compiledByIndex.Add(compiled);
        indexBySignature.Add(signature, newIndex);
        return true;
    }

    /// <summary>用角色组件中的编号和指纹读取已经整理好的结果。</summary>
    public static bool TryGetCompiled(
        int compiledIndex,
        string signature,
        out CompiledCreaturePhenotype compiled)
    {
        compiled = null;
        if (compiledIndex <= 0 || compiledIndex >= compiledByIndex.Count || string.IsNullOrEmpty(signature))
            return false;

        CompiledCreaturePhenotype candidate = compiledByIndex[compiledIndex];
        if (!string.Equals(candidate.Signature, signature, StringComparison.Ordinal)) return false;
        compiled = candidate;
        return true;
    }

    /// <summary>清掉只属于当前世界的组合缓存；静态资源定义保持不变。</summary>
    internal static void ClearWorldState()
    {
        indexBySignature.Clear();
        compiledByIndex.Clear();
        compiledByIndex.Add(null);
    }

    private static bool TryBuild(
        CreaturePhenotypePlan plan,
        out CompilationDraft draft,
        out string signature)
    {
        draft = null;
        signature = null;
        if (plan == null || plan.Version != CreaturePhenotypePlan.CurrentVersion ||
            string.IsNullOrWhiteSpace(plan.BodyPlanId) || string.IsNullOrWhiteSpace(plan.MorphId))
            return false;

        CreatureBodyPlanAsset bodyPlan = ContentLibraries.CreatureBodyPlanLibrary.get(plan.BodyPlanId);
        CreatureMorphAsset morph = ContentLibraries.CreatureMorphLibrary.get(plan.MorphId);
        if (bodyPlan == null || morph == null ||
            !string.Equals(morph.BodyPlanId, bodyPlan.id, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(morph.ActorAssetId) ||
            AssetManager.actor_library == null || AssetManager.actor_library.get(morph.ActorAssetId) == null ||
            !ContainsOrAllowsAll(bodyPlan.AllowedMorphIds, morph.id))
            return false;

        if (bodyPlan.BaseComplexityCapacity < 0 ||
            bodyPlan.MaximumOverlayLayers < 0 ||
            bodyPlan.MaximumOverlayLayers > MaximumOverlayLayers)
            return false;

        long complexityCapacity = (long)bodyPlan.BaseComplexityCapacity + morph.BaseComplexityModifier;
        if (complexityCapacity < 0 || complexityCapacity > int.MaxValue) return false;

        if (!TryCreateSlots(bodyPlan, morph, out Dictionary<string, SlotUse> slots)) return false;
        if (plan.Organs.Count > MaximumBodySlots * MaximumSlotCapacity) return false;

        var resolvedOrgans = new List<ResolvedOrgan>(plan.Organs.Count);
        long complexityUsed = 0;
        for (int i = 0; i < plan.Organs.Count; i++)
        {
            CreatureOrganEntry entry = plan.Organs[i];
            if (!TryResolveOrgan(entry, bodyPlan, morph, slots, out ResolvedOrgan resolved)) return false;

            complexityUsed += resolved.Rank.ComplexityCost;
            if (complexityUsed > complexityCapacity) return false;
            resolvedOrgans.Add(resolved);
        }

        foreach (SlotUse slot in slots.Values)
        {
            if (slot.UsedCapacity > slot.TotalCapacity || slot.Slot.Required && slot.UsedCapacity == 0)
                return false;
        }

        if (!CheckOrganRelations(resolvedOrgans)) return false;
        resolvedOrgans.Sort(CompareResolvedOrgans);

        if (!TryCompileOutputs(
                bodyPlan,
                resolvedOrgans,
                out CompiledCreatureOrgan[] orderedOrgans,
                out CreatureStatValue[] stats,
                out SemanticContribution[] semantics,
                out string[] activeAbilityIds,
                out CreatureEffectRank[] passiveEffects,
                out CompiledCreatureVisualLayer[] visualLayers))
            return false;

        signature = CreaturePhenotypeSignature.Build(plan.Version, bodyPlan, morph.id, orderedOrgans);
        draft = new CompilationDraft(
            bodyPlan,
            morph,
            orderedOrgans,
            stats,
            semantics,
            activeAbilityIds,
            passiveEffects,
            visualLayers,
            (int)complexityUsed);
        return true;
    }

    private static bool TryCreateSlots(
        CreatureBodyPlanAsset bodyPlan,
        CreatureMorphAsset morph,
        out Dictionary<string, SlotUse> slots)
    {
        slots = new Dictionary<string, SlotUse>(StringComparer.Ordinal);
        string[] slotIds = bodyPlan.SlotIds ?? Array.Empty<string>();
        if (slotIds.Length == 0 || slotIds.Length > MaximumBodySlots) return false;

        for (int i = 0; i < slotIds.Length; i++)
        {
            string slotId = slotIds[i];
            if (string.IsNullOrWhiteSpace(slotId) || slots.ContainsKey(slotId)) return false;
            CreatureBodySlotAsset slot = ContentLibraries.CreatureBodySlotLibrary.get(slotId);
            if (slot == null || slot.Capacity <= 0 || slot.Capacity > MaximumSlotCapacity ||
                slot.AcceptedCategoryMask == CreatureOrganCategoryMask.None)
                return false;
            slots.Add(slotId, new SlotUse(slot, i));
        }

        CreatureSlotCapacityChange[] capacityChanges =
            morph.AddedSlotCapacity ?? Array.Empty<CreatureSlotCapacityChange>();
        for (int i = 0; i < capacityChanges.Length; i++)
        {
            CreatureSlotCapacityChange change = capacityChanges[i];
            if (string.IsNullOrWhiteSpace(change.SlotId) ||
                !slots.TryGetValue(change.SlotId, out SlotUse slot) || change.AddedCapacity < 0 ||
                slot.TotalCapacity > MaximumSlotCapacity - change.AddedCapacity)
                return false;
            slot.TotalCapacity += change.AddedCapacity;
        }

        string[] lockedSlotIds = morph.LockedSlots ?? Array.Empty<string>();
        for (int i = 0; i < lockedSlotIds.Length; i++)
        {
            string lockedSlotId = lockedSlotIds[i];
            if (string.IsNullOrWhiteSpace(lockedSlotId) ||
                !slots.TryGetValue(lockedSlotId, out SlotUse slot) || slot.Locked)
                return false;
            slot.Locked = true;
        }
        return true;
    }

    private static bool TryResolveOrgan(
        CreatureOrganEntry entry,
        CreatureBodyPlanAsset bodyPlan,
        CreatureMorphAsset morph,
        Dictionary<string, SlotUse> slots,
        out ResolvedOrgan resolved)
    {
        resolved = default;
        if (string.IsNullOrWhiteSpace(entry.SlotId) || string.IsNullOrWhiteSpace(entry.OrganId) ||
            entry.Rank <= 0 || !slots.TryGetValue(entry.SlotId, out SlotUse primarySlot) || primarySlot.Locked)
            return false;

        CreatureOrganAsset organ = ContentLibraries.CreatureOrganLibrary.get(entry.OrganId);
        if (organ == null || organ.Category == CreatureOrganCategoryMask.None ||
            !MatchesTags(organ.AllowedBodyPlanTags, bodyPlan.Tags) ||
            !MatchesTags(organ.AllowedMorphTags, morph.Tags) ||
            !TryResolveRank(organ, entry.Rank, out CreatureOrganRankAsset rank) ||
            rank.ComplexityCost < 0)
            return false;

        CreatureSlotRequirement[] requirements = organ.SlotRequirements ?? Array.Empty<CreatureSlotRequirement>();
        if (requirements.Length == 0)
        {
            if (!TryUseSlot(primarySlot, organ.Category, 1)) return false;
        }
        else
        {
            var requiredCapacity = new Dictionary<string, int>(StringComparer.Ordinal);
            bool containsPrimary = false;
            for (int i = 0; i < requirements.Length; i++)
            {
                CreatureSlotRequirement requirement = requirements[i];
                if (string.IsNullOrWhiteSpace(requirement.SlotId) || requirement.Capacity <= 0 ||
                    !slots.TryGetValue(requirement.SlotId, out SlotUse slot) || slot.Locked ||
                    (slot.Slot.AcceptedCategoryMask & organ.Category) != organ.Category)
                    return false;

                containsPrimary |= string.Equals(requirement.SlotId, entry.SlotId, StringComparison.Ordinal);
                int previous = requiredCapacity.TryGetValue(requirement.SlotId, out int value) ? value : 0;
                if (previous > int.MaxValue - requirement.Capacity) return false;
                requiredCapacity[requirement.SlotId] = previous + requirement.Capacity;
            }
            if (!containsPrimary) return false;

            foreach (KeyValuePair<string, int> requirement in requiredCapacity)
            {
                if (!TryUseSlot(slots[requirement.Key], organ.Category, requirement.Value)) return false;
            }
        }

        resolved = new ResolvedOrgan(entry, primarySlot, organ, rank);
        return true;
    }

    private static bool TryUseSlot(SlotUse slot, CreatureOrganCategoryMask category, int capacity)
    {
        if ((slot.Slot.AcceptedCategoryMask & category) != category ||
            slot.UsedCapacity > slot.TotalCapacity - capacity)
            return false;
        slot.UsedCapacity += capacity;
        return true;
    }

    private static bool TryResolveRank(
        CreatureOrganAsset organ,
        int requestedRank,
        out CreatureOrganRankAsset rank)
    {
        rank = null;
        string[] rankIds = organ.RankIds ?? Array.Empty<string>();
        for (int i = 0; i < rankIds.Length; i++)
        {
            string rankId = rankIds[i];
            if (string.IsNullOrWhiteSpace(rankId)) return false;
            CreatureOrganRankAsset candidate = ContentLibraries.CreatureOrganRankLibrary.get(rankId);
            if (candidate == null || candidate.Rank != requestedRank) continue;
            if (rank != null) return false;
            rank = candidate;
        }
        return rank != null;
    }

    private static bool CheckOrganRelations(List<ResolvedOrgan> resolvedOrgans)
    {
        var presentOrganIds = new HashSet<string>(resolvedOrgans.Select(x => x.Organ.id), StringComparer.Ordinal);
        for (int i = 0; i < resolvedOrgans.Count; i++)
        {
            CreatureOrganAsset organ = resolvedOrgans[i].Organ;
            string[] prerequisites = organ.PrerequisiteOrganIds ?? Array.Empty<string>();
            for (int j = 0; j < prerequisites.Length; j++)
            {
                if (string.IsNullOrWhiteSpace(prerequisites[j]) || !presentOrganIds.Contains(prerequisites[j]))
                    return false;
            }

            string[] conflicts = organ.ConflictOrganIds ?? Array.Empty<string>();
            for (int j = 0; j < conflicts.Length; j++)
            {
                if (string.IsNullOrWhiteSpace(conflicts[j]) || presentOrganIds.Contains(conflicts[j]))
                    return false;
            }
        }
        return true;
    }

    private static bool TryCompileOutputs(
        CreatureBodyPlanAsset bodyPlan,
        List<ResolvedOrgan> resolvedOrgans,
        out CompiledCreatureOrgan[] orderedOrgans,
        out CreatureStatValue[] stats,
        out SemanticContribution[] semantics,
        out string[] activeAbilityIds,
        out CreatureEffectRank[] passiveEffects,
        out CompiledCreatureVisualLayer[] visualLayers)
    {
        orderedOrgans = new CompiledCreatureOrgan[resolvedOrgans.Count];
        var statTotals = new Dictionary<string, float>(StringComparer.Ordinal);
        var semanticBuilder = new SemanticDescriptorBuilder();
        var abilityIds = new HashSet<string>(StringComparer.Ordinal);
        var effectRanks = new Dictionary<string, int>(StringComparer.Ordinal);
        var visualsByChannel = new Dictionary<string, VisualCandidate>(StringComparer.Ordinal);

        for (int i = 0; i < resolvedOrgans.Count; i++)
        {
            ResolvedOrgan resolved = resolvedOrgans[i];
            orderedOrgans[i] = new CompiledCreatureOrgan(
                resolved.Entry,
                resolved.PrimarySlot.Slot,
                resolved.Organ,
                resolved.Rank);

            CreatureStatValue[] organStats = resolved.Rank.StatValues ?? Array.Empty<CreatureStatValue>();
            for (int j = 0; j < organStats.Length; j++)
            {
                CreatureStatValue stat = organStats[j];
                if (string.IsNullOrWhiteSpace(stat.StatId) || float.IsNaN(stat.Value) || float.IsInfinity(stat.Value))
                    return FailOutputs(
                        out orderedOrgans,
                        out stats,
                        out semantics,
                        out activeAbilityIds,
                        out passiveEffects,
                        out visualLayers);
                float previous = statTotals.TryGetValue(stat.StatId, out float value) ? value : 0f;
                float total = previous + stat.Value;
                if (float.IsNaN(total) || float.IsInfinity(total))
                    return FailOutputs(
                        out orderedOrgans,
                        out stats,
                        out semantics,
                        out activeAbilityIds,
                        out passiveEffects,
                        out visualLayers);
                statTotals[stat.StatId] = total;
            }

            SemanticContribution[] contributions = resolved.Organ.Semantics?.contributions;
            if (contributions != null)
            {
                for (int j = 0; j < contributions.Length; j++) semanticBuilder.Add(contributions[j]);
            }

            string[] organAbilityIds = resolved.Rank.SkillContainerIds ?? Array.Empty<string>();
            for (int j = 0; j < organAbilityIds.Length; j++)
            {
                if (string.IsNullOrWhiteSpace(organAbilityIds[j]))
                    return FailOutputs(
                        out orderedOrgans,
                        out stats,
                        out semantics,
                        out activeAbilityIds,
                        out passiveEffects,
                        out visualLayers);
                abilityIds.Add(organAbilityIds[j]);
            }

            CreatureEffectRank[] organEffects = resolved.Rank.EffectRanks ?? Array.Empty<CreatureEffectRank>();
            for (int j = 0; j < organEffects.Length; j++)
            {
                CreatureEffectRank effect = organEffects[j];
                if (string.IsNullOrWhiteSpace(effect.EffectFamilyId) || effect.Rank <= 0 ||
                    !Contains(resolved.Organ.EffectFamilyIds, effect.EffectFamilyId))
                    return FailOutputs(
                        out orderedOrgans,
                        out stats,
                        out semantics,
                        out activeAbilityIds,
                        out passiveEffects,
                        out visualLayers);
                if (!effectRanks.TryGetValue(effect.EffectFamilyId, out int oldRank) || effect.Rank > oldRank)
                    effectRanks[effect.EffectFamilyId] = effect.Rank;
            }

            string[] organLayers = resolved.Rank.VisualLayerIds ?? Array.Empty<string>();
            if (organLayers.Length > 0)
            {
                string channel = resolved.PrimarySlot.Slot.VisualChannel;
                if (string.IsNullOrWhiteSpace(channel) || !TryCopyUnique(organLayers, out string[] copiedLayers))
                    return FailOutputs(
                        out orderedOrgans,
                        out stats,
                        out semantics,
                        out activeAbilityIds,
                        out passiveEffects,
                        out visualLayers);
                if (!visualsByChannel.TryGetValue(channel, out VisualCandidate oldVisual) ||
                    resolved.Rank.Rank > oldVisual.Rank)
                {
                    visualsByChannel[channel] = new VisualCandidate(
                        resolved.PrimarySlot.Order,
                        resolved.Rank.Rank,
                        new CompiledCreatureVisualLayer(
                            channel,
                            resolved.Entry.SlotId,
                            resolved.Organ.id,
                            copiedLayers));
                }
            }
        }

        if (visualsByChannel.Count > bodyPlan.MaximumOverlayLayers)
            return FailOutputs(
                out orderedOrgans,
                out stats,
                out semantics,
                out activeAbilityIds,
                out passiveEffects,
                out visualLayers);

        stats = statTotals
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new CreatureStatValue(x.Key, x.Value))
            .ToArray();
        semantics = semanticBuilder.Build().contributions;
        activeAbilityIds = abilityIds.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        passiveEffects = effectRanks
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new CreatureEffectRank(x.Key, x.Value))
            .ToArray();
        visualLayers = visualsByChannel.Values
            .OrderBy(x => x.SlotOrder)
            .ThenBy(x => x.Layer.Channel, StringComparer.Ordinal)
            .Select(x => x.Layer)
            .ToArray();
        return true;
    }

    private static bool FailOutputs(
        out CompiledCreatureOrgan[] orderedOrgans,
        out CreatureStatValue[] stats,
        out SemanticContribution[] semantics,
        out string[] activeAbilityIds,
        out CreatureEffectRank[] passiveEffects,
        out CompiledCreatureVisualLayer[] visualLayers)
    {
        orderedOrgans = null;
        stats = null;
        semantics = null;
        activeAbilityIds = null;
        passiveEffects = null;
        visualLayers = null;
        return false;
    }

    private static bool TryCopyUnique(string[] source, out string[] result)
    {
        var unique = new HashSet<string>(StringComparer.Ordinal);
        var copied = new List<string>(source.Length);
        for (int i = 0; i < source.Length; i++)
        {
            string value = source[i];
            if (string.IsNullOrWhiteSpace(value))
            {
                result = null;
                return false;
            }
            if (unique.Add(value)) copied.Add(value);
        }
        result = copied.ToArray();
        return true;
    }

    private static bool MatchesTags(string[] allowedTags, string[] actualTags)
    {
        if (allowedTags == null || allowedTags.Length == 0) return true;
        if (actualTags == null || actualTags.Length == 0) return false;
        for (int i = 0; i < allowedTags.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(allowedTags[i])) return false;
            if (Contains(actualTags, allowedTags[i])) return true;
        }
        return false;
    }

    private static bool ContainsOrAllowsAll(string[] values, string value)
    {
        return values == null || values.Length == 0 || Contains(values, value);
    }

    private static bool Contains(string[] values, string value)
    {
        if (values == null || string.IsNullOrEmpty(value)) return false;
        for (int i = 0; i < values.Length; i++)
        {
            if (string.Equals(values[i], value, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private static int CompareResolvedOrgans(ResolvedOrgan left, ResolvedOrgan right)
    {
        int bySlot = left.PrimarySlot.Order.CompareTo(right.PrimarySlot.Order);
        if (bySlot != 0) return bySlot;
        int byOrgan = string.CompareOrdinal(left.Organ.id, right.Organ.id);
        return byOrgan != 0 ? byOrgan : left.Rank.Rank.CompareTo(right.Rank.Rank);
    }

    private sealed class SlotUse
    {
        internal readonly CreatureBodySlotAsset Slot;
        internal readonly int Order;
        internal int TotalCapacity;
        internal int UsedCapacity;
        internal bool Locked;

        internal SlotUse(CreatureBodySlotAsset slot, int order)
        {
            Slot = slot;
            Order = order;
            TotalCapacity = slot.Capacity;
        }
    }

    private readonly struct ResolvedOrgan
    {
        internal readonly CreatureOrganEntry Entry;
        internal readonly SlotUse PrimarySlot;
        internal readonly CreatureOrganAsset Organ;
        internal readonly CreatureOrganRankAsset Rank;

        internal ResolvedOrgan(
            CreatureOrganEntry entry,
            SlotUse primarySlot,
            CreatureOrganAsset organ,
            CreatureOrganRankAsset rank)
        {
            Entry = entry;
            PrimarySlot = primarySlot;
            Organ = organ;
            Rank = rank;
        }
    }

    private readonly struct VisualCandidate
    {
        internal readonly int SlotOrder;
        internal readonly int Rank;
        internal readonly CompiledCreatureVisualLayer Layer;

        internal VisualCandidate(int slotOrder, int rank, CompiledCreatureVisualLayer layer)
        {
            SlotOrder = slotOrder;
            Rank = rank;
            Layer = layer;
        }
    }

    private sealed class CompilationDraft
    {
        private readonly CreatureBodyPlanAsset bodyPlan;
        private readonly CreatureMorphAsset morph;
        private readonly CompiledCreatureOrgan[] orderedOrgans;
        private readonly CreatureStatValue[] stats;
        private readonly SemanticContribution[] semantics;
        private readonly string[] activeAbilityIds;
        private readonly CreatureEffectRank[] passiveEffects;
        private readonly CompiledCreatureVisualLayer[] visualLayers;
        private readonly int complexityUsed;

        internal CompilationDraft(
            CreatureBodyPlanAsset bodyPlan,
            CreatureMorphAsset morph,
            CompiledCreatureOrgan[] orderedOrgans,
            CreatureStatValue[] stats,
            SemanticContribution[] semantics,
            string[] activeAbilityIds,
            CreatureEffectRank[] passiveEffects,
            CompiledCreatureVisualLayer[] visualLayers,
            int complexityUsed)
        {
            this.bodyPlan = bodyPlan;
            this.morph = morph;
            this.orderedOrgans = orderedOrgans;
            this.stats = stats;
            this.semantics = semantics;
            this.activeAbilityIds = activeAbilityIds;
            this.passiveEffects = passiveEffects;
            this.visualLayers = visualLayers;
            this.complexityUsed = complexityUsed;
        }

        internal CompiledCreaturePhenotype Create(int index, string signature)
        {
            return new CompiledCreaturePhenotype(
                index,
                signature,
                bodyPlan,
                morph,
                orderedOrgans,
                stats,
                semantics,
                activeAbilityIds,
                passiveEffects,
                visualLayers,
                complexityUsed);
        }
    }
}

/// <summary>按固定字段顺序生成与运行平台无关的组合指纹。</summary>
internal static class CreaturePhenotypeSignature
{
    internal static string Build(
        int version,
        CreatureBodyPlanAsset bodyPlan,
        string morphId,
        CompiledCreatureOrgan[] orderedOrgans)
    {
        var canonical = new StringBuilder();
        Append(canonical, version.ToString(CultureInfo.InvariantCulture));
        Append(canonical, bodyPlan.id);
        Append(canonical, morphId);
        Append(canonical, bodyPlan.SlotIds.Length.ToString(CultureInfo.InvariantCulture));

        int organIndex = 0;
        for (int slotIndex = 0; slotIndex < bodyPlan.SlotIds.Length; slotIndex++)
        {
            string slotId = bodyPlan.SlotIds[slotIndex];
            Append(canonical, slotId);

            int slotStart = organIndex;
            while (organIndex < orderedOrgans.Length &&
                   string.Equals(orderedOrgans[organIndex].Entry.SlotId, slotId, StringComparison.Ordinal))
                organIndex++;

            Append(canonical, (organIndex - slotStart).ToString(CultureInfo.InvariantCulture));
            for (int i = slotStart; i < organIndex; i++)
            {
                CompiledCreatureOrgan organ = orderedOrgans[i];
                Append(canonical, organ.Organ.id);
                Append(canonical, organ.Rank.Rank.ToString(CultureInfo.InvariantCulture));
            }
        }

        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
        var result = new StringBuilder(hash.Length * 2);
        for (int i = 0; i < hash.Length; i++) result.Append(hash[i].ToString("x2"));
        return result.ToString();
    }

    private static void Append(StringBuilder builder, string value)
    {
        value ??= string.Empty;
        builder.Append(value.Length);
        builder.Append(':');
        builder.Append(value);
        builder.Append('|');
    }
}
