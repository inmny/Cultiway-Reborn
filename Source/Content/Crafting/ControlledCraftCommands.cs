using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.ControlledTasks;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Crafting;

internal sealed class ControlledElixirCraftContext : IControlledTaskExecutionContext
{
    public string RecipeId { get; }

    public ControlledElixirCraftContext(string recipeId)
    {
        RecipeId = recipeId;
    }

    public void OnOrderFinished(ControlledTaskOrderState state, string reasonLocaleKey)
    {
        // 配方选择本身不持有材料；行为开工后所有权转移到 CraftSession。
    }
}

internal sealed class ControlledArtifactCraftContext : IControlledTaskExecutionContext
{
    public Entity[] Materials { get; }

    public ControlledArtifactCraftContext(Entity[] materials)
    {
        Materials = materials ?? Array.Empty<Entity>();
    }

    public void OnOrderFinished(ControlledTaskOrderState state, string reasonLocaleKey)
    {
        // 材料在真正开工前不占用，未消费上下文无需回滚。
    }
}

internal sealed class ElixirCraftCommandConfigurator : IControlledTaskCommandConfigurator
{
    internal const string RecipeParameter = "recipe";

    private static readonly IReadOnlyList<ControlledTaskParameterDefinition> ParameterDefinitions =
        new[]
        {
            new ControlledTaskParameterDefinition(
                RecipeParameter,
                ControlledTaskParameterMode.SingleChoice,
                true,
                1,
                1,
                "Cultiway.ControlledTask.Parameter.Recipe",
                "Cultiway.ControlledTask.Parameter.Recipe.Description",
                ControlledTaskParameterLayout.CompactList),
        };

    public IReadOnlyList<ControlledTaskParameterDefinition> Parameters => ParameterDefinitions;

    public IReadOnlyList<ControlledTaskOption> GetOptions(
        Actor actor,
        string parameterKey,
        ControlledTaskInvocation invocation)
    {
        if (actor == null || actor.isRekt() || parameterKey != RecipeParameter)
            return Array.Empty<ControlledTaskOption>();
        var result = new List<ControlledTaskOption>();
        ActorExtend extend = actor.GetExtend();
        foreach ((ElixirAsset recipe, float mastery) in extend.GetAllMaster<ElixirAsset>()
                     .OrderBy(item => item.Item1?.id, StringComparer.Ordinal))
        {
            if (recipe == null || !recipe.QueryInventoryForIngredients(extend, out Entity[] ingredients)) continue;
            result.Add(new ControlledTaskOption(
                "asset:" + recipe.id,
                recipe.GetName(),
                $"{"Cultiway.ControlledTask.Parameter.Mastery".Localize()}: {mastery:F0}% · " +
                string.Format("Cultiway.ControlledTask.Parameter.MaterialCount".Localize(), ingredients.Length),
                "cultiway/icons/iconElixirCauldron"));
        }
        return result;
    }

    public ControlledTaskAvailability Validate(Actor actor, ControlledTaskInvocation invocation)
    {
        string key = invocation.GetSelections(RecipeParameter).FirstOrDefault();
        return TryResolveRecipe(actor, key, out _, out _)
            ? ControlledTaskAvailability.Available
            : ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.RecipeUnavailable");
    }

    public IControlledTaskExecutionContext Prepare(Actor actor, ControlledTaskInvocation invocation)
    {
        ControlledTaskAvailability availability = Validate(actor, invocation);
        if (!availability.Enabled) throw new InvalidOperationException(availability.ReasonLocaleKey);
        string key = invocation.GetSelections(RecipeParameter)[0];
        return new ControlledElixirCraftContext(key.Substring("asset:".Length));
    }

    internal static bool TryResolveRecipe(Actor actor, string key, out ElixirAsset recipe, out Entity[] ingredients)
    {
        recipe = null;
        ingredients = null;
        if (actor == null || actor.isRekt() || string.IsNullOrEmpty(key) ||
            !key.StartsWith("asset:", StringComparison.Ordinal)) return false;
        recipe = Libraries.Manager.ElixirLibrary.get(key.Substring("asset:".Length));
        ActorExtend extend = actor.GetExtend();
        return recipe != null && extend.GetMaster(recipe) > 0f &&
               recipe.QueryInventoryForIngredients(extend, out ingredients);
    }
}

internal sealed class ArtifactCraftCommandConfigurator : IControlledTaskCommandConfigurator
{
    internal const string MaterialsParameter = "materials";

    private static readonly IReadOnlyList<ControlledTaskParameterDefinition> ParameterDefinitions =
        new[]
        {
            new ControlledTaskParameterDefinition(
                MaterialsParameter,
                ControlledTaskParameterMode.MultipleChoice,
                true,
                1,
                int.MaxValue,
                "Cultiway.ControlledTask.Parameter.Materials",
                "Cultiway.ControlledTask.Parameter.Materials.Description",
                ControlledTaskParameterLayout.ItemGrid),
        };

    public IReadOnlyList<ControlledTaskParameterDefinition> Parameters => ParameterDefinitions;

    public IReadOnlyList<ControlledTaskOption> GetOptions(
        Actor actor,
        string parameterKey,
        ControlledTaskInvocation invocation)
    {
        if (actor == null || actor.isRekt() || parameterKey != MaterialsParameter)
            return Array.Empty<ControlledTaskOption>();
        var result = new List<ControlledTaskOption>();
        foreach (Entity item in actor.GetExtend().GetItems().OrderBy(entity => entity.Id))
        {
            if (!IsValidMaterial(item)) continue;
            string label = item.HasName ? item.Name.value : $"#{item.Id}";
            string summary = item.TryGetComponent(out ItemLevel level)
                ? string.Format("Cultiway.ControlledTask.Parameter.ItemLevel".Localize(), level.Stage, level.Level)
                : string.Empty;
            Sprite icon = item.TryGetComponent(out SpecialItem specialItem)
                ? specialItem.GetSprite()
                : null;
            result.Add(new ControlledTaskOption(
                "entity:" + item.Id,
                label,
                summary,
                iconSprite: icon,
                specialItemId: item.Id));
        }
        return result;
    }

    public ControlledTaskAvailability Validate(Actor actor, ControlledTaskInvocation invocation)
    {
        if (!TryResolveMaterials(actor, invocation.GetSelections(MaterialsParameter), out Entity[] materials))
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.MaterialsUnavailable");
        try
        {
            ArtifactComposeResult result = ArtifactComposer.Compose(materials);
            return result?.Shape != null
                ? ControlledTaskAvailability.Available
                : ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.MaterialsInvalid");
        }
        catch
        {
            return ControlledTaskAvailability.Unavailable("Cultiway.ControlledTask.Reason.MaterialsInvalid");
        }
    }

    public IControlledTaskExecutionContext Prepare(Actor actor, ControlledTaskInvocation invocation)
    {
        ControlledTaskAvailability availability = Validate(actor, invocation);
        if (!availability.Enabled) throw new InvalidOperationException(availability.ReasonLocaleKey);
        if (!TryResolveMaterials(actor, invocation.GetSelections(MaterialsParameter), out Entity[] materials))
            throw new InvalidOperationException("Selected artifact materials disappeared.");
        return new ControlledArtifactCraftContext(materials);
    }

    internal static bool TryResolveMaterials(Actor actor, IReadOnlyList<string> values, out Entity[] materials)
    {
        materials = null;
        if (actor == null || actor.isRekt() || values == null || values.Count == 0) return false;
        Dictionary<int, Entity> inventory = actor.GetExtend().GetItems()
            .Where(IsValidMaterial)
            .ToDictionary(entity => entity.Id);
        var selected = new Entity[values.Count];
        var unique = new HashSet<int>();
        for (int i = 0; i < values.Count; i++)
        {
            string value = values[i];
            if (string.IsNullOrEmpty(value) || !value.StartsWith("entity:", StringComparison.Ordinal) ||
                !int.TryParse(value.Substring("entity:".Length), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int id) || !unique.Add(id) ||
                !inventory.TryGetValue(id, out selected[i])) return false;
        }
        materials = selected;
        return true;
    }

    internal static bool TryValidateMaterials(Actor actor, IReadOnlyList<Entity> materials)
    {
        if (actor == null || actor.isRekt() || materials == null || materials.Count == 0) return false;
        HashSet<Entity> inventory = new(actor.GetExtend().GetItems());
        var unique = new HashSet<Entity>();
        for (int i = 0; i < materials.Count; i++)
        {
            Entity material = materials[i];
            if (!unique.Add(material) || !inventory.Contains(material) || !IsValidMaterial(material) ||
                material.GetIncomingLinks<CraftOccupyingRelation>().Entities.Count > 0) return false;
        }
        return true;
    }

    internal static bool IsValidMaterial(Entity item)
    {
        return !item.IsNull && item.Tags.Has<TagIngredient>() &&
               !item.Tags.HasAny(Tags.Get<TagConsumed, TagOccupied, TagRecycle, TagUncompleted>()) &&
               item.HasComponent<ItemShape>() && item.HasComponent<ItemLevel>() &&
               item.HasComponent<ItemCreation>();
    }
}

internal static class CraftSessionService
{
    internal static bool ValidateSession(Actor actor, Entity craftingItem, CraftProcessType process,
        out string reasonLocaleKey)
    {
        reasonLocaleKey = string.Empty;
        if (actor == null || actor.isRekt() || craftingItem.IsNull ||
            !craftingItem.TryGetComponent(out CraftSession session) ||
            session.actor_id != actor.getID() || session.process != process)
        {
            reasonLocaleKey = "Cultiway.ControlledTask.Reason.CraftingSessionInvalid";
            return false;
        }
        if (session.order_id <= 0) return true;
        if (ControlledTaskOrderService.TryGetActiveOrderId(actor.getID(), out long orderId) &&
            orderId == session.order_id) return true;
        reasonLocaleKey = "Cultiway.ControlledTask.Reason.CraftingSessionInvalid";
        return false;
    }

    internal static bool HasActiveCraft(Actor actor)
    {
        if (actor == null || actor.isRekt()) return false;
        ActorExtend extend = actor.GetExtend();
        return extend.HasItem<CraftingElixir>() || extend.HasItem<CraftingArtifact>();
    }

    internal static bool TryBeginElixir(Actor actor, string recipeId, long orderId,
        out Entity craftingItem, out string reasonLocaleKey)
    {
        craftingItem = default;
        reasonLocaleKey = string.Empty;
        if (HasActiveCraft(actor))
        {
            reasonLocaleKey = "Cultiway.ControlledTask.Reason.CraftingAlreadyActive";
            return false;
        }

        ElixirAsset recipe = Libraries.Manager.ElixirLibrary.get(recipeId);
        ActorExtend extend = actor.GetExtend();
        if (recipe == null || extend.GetMaster(recipe) <= 0f ||
            !recipe.QueryInventoryForIngredients(extend, out Entity[] ingredients))
        {
            reasonLocaleKey = "Cultiway.ControlledTask.Reason.RecipeUnavailable";
            return false;
        }

        return TryCreateSession(actor, orderId, CraftProcessType.Alchemy, ingredients,
            () => SpecialItemUtils
                .StartBuild(ItemShapes.Ball, World.world.getCurWorldTime(), actor.getName())
                .AddComponent(new CraftingElixir { elixir_id = recipe.id })
                .AddTag<TagUncompleted>()
                .Build(), out craftingItem, out reasonLocaleKey);
    }

    internal static bool TryBeginArtifact(Actor actor, IReadOnlyList<Entity> ingredients, long orderId,
        out Entity craftingItem, out string reasonLocaleKey)
    {
        craftingItem = default;
        reasonLocaleKey = string.Empty;
        if (HasActiveCraft(actor))
        {
            reasonLocaleKey = "Cultiway.ControlledTask.Reason.CraftingAlreadyActive";
            return false;
        }
        if (!ArtifactCraftCommandConfigurator.TryValidateMaterials(actor, ingredients))
        {
            reasonLocaleKey = "Cultiway.ControlledTask.Reason.MaterialsUnavailable";
            return false;
        }

        ArtifactComposeResult result;
        try
        {
            result = ArtifactComposer.Compose(ingredients);
        }
        catch (Exception exception)
        {
            ModClass.LogError($"[CraftSession] artifact composition failed actor={actor.getID()}: {exception}");
            reasonLocaleKey = "Cultiway.ControlledTask.Reason.MaterialsInvalid";
            return false;
        }
        if (result?.Shape == null)
        {
            reasonLocaleKey = "Cultiway.ControlledTask.Reason.MaterialsInvalid";
            return false;
        }

        return TryCreateSession(actor, orderId, CraftProcessType.ArtifactRefining, ingredients,
            () => SpecialItemUtils
                .StartBuild(result.Shape, World.world.getCurWorldTime(), actor.getName())
                .AddComponent(new CraftingArtifact())
                .AddComponent(result.Level)
                .AddComponent(new EntityName(result.Name))
                .AddComponent(result.ToAtomData())
                .AddComponent(result.MaterialData)
                .AddComponent(result.ToControlProfile())
                .AddComponent(result.AbilitySet)
                .AddComponent(result.AbilityRuntime)
                .AddComponent(new ArtifactStorageState())
                .AddComponent(new ArtifactSpiritState())
                .AddComponent(result.Appearance)
                .AddTag<TagUncompleted>()
                .Build(), out craftingItem, out reasonLocaleKey);
    }

    private static bool TryCreateSession(Actor actor, long orderId, CraftProcessType process,
        IReadOnlyList<Entity> ingredients, Func<Entity> createItem,
        out Entity craftingItem, out string reasonLocaleKey)
    {
        craftingItem = default;
        reasonLocaleKey = string.Empty;
        var tagged = new List<Entity>(ingredients.Count);
        try
        {
            for (int i = 0; i < ingredients.Count; i++)
            {
                Entity ingredient = ingredients[i];
                if (ingredient.IsNull || ingredient.Tags.HasAny(
                        Tags.Get<TagConsumed, TagOccupied, TagRecycle, TagUncompleted>()) ||
                    ingredient.GetIncomingLinks<CraftOccupyingRelation>().Entities.Count > 0)
                {
                    reasonLocaleKey = "Cultiway.ControlledTask.Reason.MaterialsUnavailable";
                    return false;
                }
            }

            craftingItem = createItem();
            if (craftingItem.IsNull)
            {
                reasonLocaleKey = "Cultiway.ControlledTask.Reason.CraftingStartFailed";
                return false;
            }
            craftingItem.AddComponent(new CraftSession
            {
                session_id = Guid.NewGuid().ToString("N"),
                actor_id = actor.getID(),
                order_id = orderId,
                process = process,
            });
            actor.GetExtend().AddSpecialItem(craftingItem);
            for (int i = 0; i < ingredients.Count; i++)
            {
                Entity ingredient = ingredients[i];
                craftingItem.AddRelation(new CraftOccupyingRelation { item = ingredient });
                ingredient.AddTag<TagConsumed>();
                tagged.Add(ingredient);
            }
            return true;
        }
        catch (Exception exception)
        {
            ModClass.LogError($"[CraftSession] startup failed actor={actor.getID()} process={process}: {exception}");
            for (int i = 0; i < tagged.Count; i++)
                if (!tagged[i].IsNull) tagged[i].RemoveTag<TagConsumed>();
            if (!craftingItem.IsNull) craftingItem.DeleteEntity();
            craftingItem = default;
            reasonLocaleKey = "Cultiway.ControlledTask.Reason.CraftingStartFailed";
            return false;
        }
    }
}
