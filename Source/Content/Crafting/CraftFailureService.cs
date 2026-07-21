using System;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Crafting;

public static class CraftFailureService
{
    private const string ElixirNameKey = "Cultiway.CraftWaste.Name.Elixir";
    private const string ElixirNameFormatKey = "Cultiway.CraftWaste.Name.Elixir.Format";
    private const string ArtifactNameKey = "Cultiway.CraftWaste.Name.Artifact";
    private const string ArtifactNameFormatKey = "Cultiway.CraftWaste.Name.Artifact.Format";

    public static bool Fail(Entity craftingItem, CraftFailureReason reason)
    {
        if (craftingItem.IsNull || craftingItem.HasComponent<CraftWaste>()) return false;
        if (!TryResolveProcess(craftingItem, out CraftProcessType process, out string originalName)) return false;

        var ingredients = craftingItem.GetRelations<CraftOccupyingRelation>();
        var ingredientEntities = new Entity[ingredients.Length];
        for (int i = 0; i < ingredients.Length; i++)
        {
            ingredientEntities[i] = ingredients[i].item;
        }
        for (int i = 0; i < ingredientEntities.Length; i++)
        {
            if (!ingredientEntities[i].IsNull) ingredientEntities[i].DeleteEntity();
        }

        if (craftingItem.HasComponent<CraftingElixir>()) craftingItem.RemoveComponent<CraftingElixir>();
        if (craftingItem.HasComponent<CraftingArtifact>()) craftingItem.RemoveComponent<CraftingArtifact>();
        if (!craftingItem.Tags.Has<TagUncompleted>()) craftingItem.AddTag<TagUncompleted>();
        craftingItem.AddComponent(new CraftWaste
        {
            process = process,
            reason = reason,
        });

        string wasteName = ComposeWasteName(process, originalName);
        if (craftingItem.HasName)
            craftingItem.GetComponent<EntityName>().value = wasteName;
        else
            craftingItem.AddComponent(new EntityName(wasteName));

        ModClass.LogWarning($"炼制品 {craftingItem.Id} 转为 {wasteName}，原因: {reason}");
        return true;
    }

    private static bool TryResolveProcess(
        Entity craftingItem,
        out CraftProcessType process,
        out string originalName)
    {
        if (craftingItem.TryGetComponent(out CraftingElixir craftingElixir))
        {
            process = CraftProcessType.Alchemy;
            ElixirAsset asset = string.IsNullOrEmpty(craftingElixir.elixir_id)
                ? null
                : Libraries.Manager.ElixirLibrary.get(craftingElixir.elixir_id);
            originalName = asset?.GetName();
            return true;
        }

        if (craftingItem.HasComponent<CraftingArtifact>())
        {
            process = CraftProcessType.ArtifactRefining;
            originalName = craftingItem.HasName ? craftingItem.Name.value : null;
            return true;
        }

        process = default;
        originalName = null;
        return false;
    }

    private static string ComposeWasteName(CraftProcessType process, string originalName)
    {
        string nameKey = process == CraftProcessType.Alchemy ? ElixirNameKey : ArtifactNameKey;
        if (string.IsNullOrWhiteSpace(originalName)) return nameKey.Localize();

        string formatKey = process == CraftProcessType.Alchemy ? ElixirNameFormatKey : ArtifactNameFormatKey;
        return string.Format(formatKey.Localize(), originalName);
    }
}
