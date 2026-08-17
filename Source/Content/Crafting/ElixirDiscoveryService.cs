using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.ControlledTasks;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Crafting;

internal sealed class ControlledElixirDiscoveryContext : IControlledTaskExecutionContext
{
    public Entity[] Materials { get; }

    internal ControlledElixirDiscoveryContext(Entity[] materials)
    {
        Materials = materials ?? Array.Empty<Entity>();
    }

    public void OnOrderFinished(ControlledTaskOrderState state, string reasonLocaleKey)
    {
        // 推演行为提交前不占用材料，取消未消费上下文没有资源副作用。
    }
}

internal sealed class ElixirDiscoveryCommandConfigurator : IControlledTaskCommandConfigurator,
    IControlledTaskInvocationSummaryProvider
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
                "Cultiway.ControlledTask.Parameter.DiscoveryMaterials.Description"),
        };

    public IReadOnlyList<ControlledTaskParameterDefinition> Parameters => ParameterDefinitions;

    public IReadOnlyList<ControlledTaskOption> GetOptions(Actor actor, string parameterKey,
        ControlledTaskInvocation invocation)
    {
        if (actor == null || actor.isRekt() || parameterKey != MaterialsParameter)
            return Array.Empty<ControlledTaskOption>();
        var result = new List<ControlledTaskOption>();
        foreach (Entity item in actor.GetExtend().GetItems().OrderBy(entity => entity.Id))
        {
            if (!IsValidMaterial(item)) continue;
            string label = item.HasName ? item.Name.value : $"#{item.Id}";
            string detail = item.TryGetComponent(out ItemLevel level)
                ? string.Format("Cultiway.ControlledTask.Parameter.ItemLevel".Localize(), level.Stage, level.Level)
                : string.Empty;
            result.Add(new ControlledTaskOption(
                "entity:" + item.Id,
                label,
                detail,
                "cultiway/icons/iconElixirCauldron"));
        }
        return result;
    }

    public ControlledTaskAvailability Validate(Actor actor, ControlledTaskInvocation invocation)
    {
        if (CraftSessionService.HasActiveCraft(actor))
            return ControlledTaskAvailability.Unavailable(
                "Cultiway.ControlledTask.Reason.CraftingAlreadyActive");
        if (!TryResolveMaterials(actor, invocation.GetSelections(MaterialsParameter), out Entity[] materials))
            return ControlledTaskAvailability.Unavailable(
                "Cultiway.ControlledTask.Reason.MaterialsUnavailable");
        try
        {
            ElixirRecipeDefinition definition = ElixirRecipeBuilder.Build(materials);
            return definition?.Ingredients?.Length > 0
                ? ControlledTaskAvailability.Available
                : ControlledTaskAvailability.Unavailable(
                    "Cultiway.ControlledTask.Reason.ElixirDraftInvalid");
        }
        catch
        {
            return ControlledTaskAvailability.Unavailable(
                "Cultiway.ControlledTask.Reason.ElixirDraftInvalid");
        }
    }

    public IControlledTaskExecutionContext Prepare(Actor actor, ControlledTaskInvocation invocation)
    {
        ControlledTaskAvailability availability = Validate(actor, invocation);
        if (!availability.Enabled) throw new InvalidOperationException(availability.ReasonLocaleKey);
        if (!TryResolveMaterials(actor, invocation.GetSelections(MaterialsParameter), out Entity[] materials))
            throw new InvalidOperationException("Selected discovery materials disappeared.");
        return new ControlledElixirDiscoveryContext(materials);
    }

    public string GetInvocationSummary(Actor actor, ControlledTaskInvocation invocation)
    {
        if (!TryResolveMaterials(actor, invocation.GetSelections(MaterialsParameter), out Entity[] materials))
            return string.Empty;
        try
        {
            ElixirRecipeDefinition definition = ElixirRecipeBuilder.Build(materials);
            var preview = new ElixirAsset
            {
                id = definition.AssetId,
                ingredients = definition.Ingredients,
                recipe_context = definition.Context,
                composition_seed = definition.Seed,
            };
            ElixirEffectComposition composition = ElixirEffectComposer.Compose(preview);
            return string.Format("Cultiway.ControlledTask.UI.ElixirDiscoveryPreview".Localize(),
                composition.Name, definition.Ingredients.Length);
        }
        catch
        {
            return string.Empty;
        }
    }

    internal static bool TryResolveMaterials(Actor actor, IReadOnlyList<string> values,
        out Entity[] materials)
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

    internal static bool IsValidMaterial(Entity item)
    {
        if (!ArtifactCraftCommandConfigurator.IsValidMaterial(item)) return false;
        foreach (Entity _ in item.GetIncomingLinks<CraftOccupyingRelation>().Entities) return false;
        return true;
    }
}

internal static class ElixirDiscoveryService
{
    internal static bool TryDiscover(Actor actor, Entity[] materials,
        out ElixirAsset asset, out string reasonLocaleKey)
    {
        asset = null;
        reasonLocaleKey = string.Empty;
        if (CraftSessionService.HasActiveCraft(actor))
        {
            reasonLocaleKey = "Cultiway.ControlledTask.Reason.CraftingAlreadyActive";
            return false;
        }
        if (!TryValidateMaterials(actor, materials, out Entity[] resolvedMaterials))
        {
            reasonLocaleKey = "Cultiway.ControlledTask.Reason.MaterialsUnavailable";
            return false;
        }
        materials = resolvedMaterials;

        ElixirRecipeDefinition definition;
        try
        {
            definition = ElixirRecipeBuilder.Build(materials);
        }
        catch (Exception exception)
        {
            ModClass.LogError($"[ElixirDiscovery] draft failed actor={actor.getID()}: {exception}");
            reasonLocaleKey = "Cultiway.ControlledTask.Reason.ElixirDraftInvalid";
            return false;
        }

        ActorExtend extend = actor.GetExtend();
        var tagged = new List<Entity>(materials.Length);
        bool created = false;
        float previousMastery = 0f;
        try
        {
            for (int i = 0; i < materials.Length; i++)
            {
                Entity material = materials[i];
                if (!ElixirDiscoveryCommandConfigurator.IsValidMaterial(material))
                    throw new InvalidOperationException("Selected discovery material changed before commit.");
                material.AddTag<TagConsumed>();
                tagged.Add(material);
            }

            asset = Libraries.Manager.ElixirLibrary.GetOrAddDefinition(definition, out created);
            previousMastery = extend.GetMaster(asset);
            extend.Master(asset, Math.Max(1f, previousMastery));
            for (int i = 0; i < materials.Length; i++) materials[i].DeleteEntity();
            ModClass.LogInfo($"{extend} 推演出丹方 {asset.GetName()}");
            return true;
        }
        catch (Exception exception)
        {
            ModClass.LogError($"[ElixirDiscovery] commit failed actor={actor.getID()}: {exception}");
            if (asset != null)
            {
                if (previousMastery > 0f)
                    extend.Master(asset, previousMastery);
                else
                    extend.DeMaster(asset);
                if (created) Libraries.Manager.ElixirLibrary.RemoveAll(new[] { asset.id });
            }
            for (int i = 0; i < tagged.Count; i++)
                if (!tagged[i].IsNull) tagged[i].RemoveTag<TagConsumed>();
            asset = null;
            reasonLocaleKey = "Cultiway.ControlledTask.Reason.ElixirDiscoveryCommitFailed";
            return false;
        }
    }

    private static bool TryValidateMaterials(Actor actor, IReadOnlyList<Entity> materials,
        out Entity[] resolvedMaterials)
    {
        resolvedMaterials = null;
        if (actor == null || actor.isRekt() || materials == null || materials.Count == 0) return false;
        HashSet<Entity> inventory = new(actor.GetExtend().GetItems());
        var unique = new HashSet<Entity>();
        var result = new Entity[materials.Count];
        for (int i = 0; i < materials.Count; i++)
        {
            Entity material = materials[i];
            if (!unique.Add(material) || !inventory.Contains(material) ||
                !ElixirDiscoveryCommandConfigurator.IsValidMaterial(material)) return false;
            result[i] = material;
        }
        resolvedMaterials = result;
        return true;
    }
}
