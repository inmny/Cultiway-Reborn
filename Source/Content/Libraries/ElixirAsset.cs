using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Events;
using Cultiway.Content.Semantics;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Semantics;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.Content.Libraries;

public delegate void ElixirCraftDelegate(ActorExtend ae, Entity elixirEntity, Entity[] ingredients);

public delegate void ElixirEffectDelegate(ActorExtend ae, Entity elixirEntity, ref Elixir elixir);

public delegate bool ElixirCheckDelegate(ActorExtend ae, Entity elixirEntity, ref Elixir elixir);

public class ElixirAsset : Asset, IDeleteWhenUnknown
{
    public ElixirCheckDelegate consumable_check_action;
    public ElixirCraftDelegate craft_action;
    public ElixirEffectDelegate effect_action;
    public ElixirEffectType effect_type;
    public ElixirIngredientRequirement[] ingredients = [];
    public ElixirRecipeContext recipe_context;
    public int composition_seed;
    public string name_key;
    public string description_key;

    public string GetName()
    {
        if (string.IsNullOrEmpty(name_key)) name_key = ElixirEffectComposer.Compose(this).Name;
        return LM.Has(name_key) ? LM.Get(name_key) : name_key;
    }

    public void SetupDataGain(ElixirEffectDelegate action)
    {
        effect_action = action;
        effect_type = ElixirEffectType.DataGain;
    }

    public void SetupRestore(ElixirEffectDelegate action)
    {
        effect_action = action;
        effect_type = ElixirEffectType.Restore;
    }

    public void SetupStatusGain(ElixirEffectDelegate action)
    {
        effect_action = action;
        effect_type = ElixirEffectType.StatusGain;
    }

    [Hotfixable]
    public void Craft(ActorExtend ae, Entity craftingElixir, IHasInventory receiver, Entity[] batch)
    {
        var batchLevel = CalculateBatchLevel(batch);
        var elixirComponent = new Elixir
        {
            elixir_id = id,
            value = CalculateBatchPotency(batch)
        };
        switch (effect_type)
        {
            case ElixirEffectType.Restore:
                craftingElixir.Add(elixirComponent, Tags.Get<TagElixirRestore>());
                break;
            case ElixirEffectType.StatusGain:
                craftingElixir.Add(elixirComponent, Tags.Get<TagElixirStatusGain>());
                break;
            case ElixirEffectType.DataGain:
                craftingElixir.Add(elixirComponent, Tags.Get<TagElixirDataGain>());
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        craft_action?.Invoke(ae, craftingElixir, batch);
        var value = craftingElixir.GetComponent<Elixir>().value;
        var potencyLevel = Mathf.FloorToInt(Mathf.Log10(Mathf.Max(0f, value) + 1f));
        var level = ItemLevel.FromValue(batchLevel + potencyLevel);

        for (var i = 0; i < batch.Length; i++) batch[i].DeleteEntity();

        craftingElixir.RemoveComponent<CraftingElixir>();
        craftingElixir.RemoveTag<TagUncompleted>();
        craftingElixir.AddComponent(new EntityName(GetName()));

        var productionResult = ArtifactProductionService.DispatchResult(
            ae,
            ArtifactProductionProcesses.Alchemy,
            this,
            craftingElixir);
        var result = new ElixirCraftResultEvent(this, craftingElixir)
        {
            QualityBonus = productionResult.QualityBonus
        };
        ArtifactAbilityDispatcher.Dispatch(ae.E, result);
        level = ItemLevel.FromValue(level + result.QualityBonus);
        if (craftingElixir.HasComponent<ItemLevel>())
            craftingElixir.GetComponent<ItemLevel>() = level;
        else
            craftingElixir.AddComponent(level);

        var outputCount = ArtifactProductionService.ResolveOutputCount(productionResult.YieldMultiplier);
        ArtifactProductionService.AddOutputs(receiver, craftingElixir, outputCount);
    }

    public bool QueryInventoryForIngredients(IHasInventory inventory, out Entity[] matchingIngredients)
    {
        matchingIngredients = null;
        if (ingredients == null || ingredients.Length == 0) return false;

        var candidates = inventory.GetItems()
            .Where(item => !item.IsNull && item.Tags.Has<TagIngredient>() &&
                           !item.Tags.HasAny(Tags.Get<TagConsumed, TagOccupied, TagRecycle>()))
            .OrderBy(item => item.Id)
            .Select(item => new Candidate(item, IngredientSemanticService.Build(item)))
            .ToArray();
        if (candidates.Length < ingredients.Length) return false;

        var slots = new SlotOptions[ingredients.Length];
        for (var i = 0; i < ingredients.Length; i++)
        {
            var requirement = ingredients[i];
            var options = candidates
                .Select(candidate => new CandidateOption(
                    candidate.Entity,
                    requirement.Match(candidate.Entity, candidate.Profile)))
                .Where(option => option.Score >= 0f)
                .OrderByDescending(option => option.Score)
                .ThenBy(option => option.Entity.Id)
                .ToArray();
            if (options.Length == 0) return false;
            slots[i] = new SlotOptions(i, options);
        }

        Array.Sort(slots, (left, right) =>
        {
            var countComparison = left.Options.Length.CompareTo(right.Options.Length);
            return countComparison != 0 ? countComparison : left.OriginalIndex.CompareTo(right.OriginalIndex);
        });
        var result = new Entity[ingredients.Length];
        var used = new HashSet<int>();
        if (!TryAssign(slots, 0, used, result)) return false;
        matchingIngredients = result;
        return true;
    }

    private static bool TryAssign(
        SlotOptions[] slots,
        int slotIndex,
        ISet<int> used,
        Entity[] result)
    {
        if (slotIndex >= slots.Length) return true;
        var slot = slots[slotIndex];
        for (var i = 0; i < slot.Options.Length; i++)
        {
            var option = slot.Options[i];
            if (!used.Add(option.Entity.Id)) continue;
            result[slot.OriginalIndex] = option.Entity;
            if (TryAssign(slots, slotIndex + 1, used, result)) return true;
            result[slot.OriginalIndex] = default;
            used.Remove(option.Entity.Id);
        }
        return false;
    }

    private static int CalculateBatchLevel(Entity[] ingredients)
    {
        if (ingredients == null || ingredients.Length == 0) return 0;
        var total = 0;
        for (var i = 0; i < ingredients.Length; i++)
        {
            if (ingredients[i].TryGetComponent(out ItemLevel level)) total += Mathf.Clamp((int)level, 0, 35);
        }
        return Mathf.FloorToInt((float)total / ingredients.Length);
    }

    private static float CalculateBatchPotency(Entity[] ingredients)
    {
        if (ingredients == null || ingredients.Length == 0) return 0f;
        var rootStrength = 0f;
        var jindanStrength = 0f;
        for (var i = 0; i < ingredients.Length; i++)
        {
            var ingredient = ingredients[i];
            if (ingredient.TryGetComponent(out ElementRoot root))
                rootStrength += Mathf.Log(1f + Mathf.Max(0f, root.GetStrength()), 2f);
            if (ingredient.TryGetComponent(out Jindan jindan))
                jindanStrength += Mathf.Log(1f + Mathf.Max(0f, jindan.strength), 2f);
        }

        var averageLevel = CalculateBatchLevel(ingredients);
        return ingredients.Length + averageLevel / 18f + rootStrength * 0.5f + jindanStrength * 0.5f;
    }

    public void OnDelete()
    {
    }

    public int Current { get; set; }

    private readonly struct Candidate
    {
        public readonly Entity Entity;
        public readonly SemanticProfile Profile;

        public Candidate(Entity entity, SemanticProfile profile)
        {
            Entity = entity;
            Profile = profile;
        }
    }

    private readonly struct CandidateOption
    {
        public readonly Entity Entity;
        public readonly float Score;

        public CandidateOption(Entity entity, float score)
        {
            Entity = entity;
            Score = score;
        }
    }

    private readonly struct SlotOptions
    {
        public readonly int OriginalIndex;
        public readonly CandidateOption[] Options;

        public SlotOptions(int originalIndex, CandidateOption[] options)
        {
            OriginalIndex = originalIndex;
            Options = options;
        }
    }
}
