using System.Collections.Generic;
using ai.behaviours;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Behaviours;

public class BehFindArtifactToCraft : BehCityActor
{
    public override BehResult execute(Actor pObject)
    {
        // 已经在炼制则不再选器形
        ActorExtend ae = pObject.GetExtend();
        if (ae.HasItem<CraftingArtifact>()) return BehResult.Continue;

        // 扫描背包未占用材料，具体炼器结果交给 ArtifactComposer 处理。
        var available = new List<Entity>();
        foreach (Entity item in ae.GetItems())
        {
            if (item.Tags.HasAny(Tags.Get<TagConsumed, TagOccupied>())) continue;
            if (!item.Tags.Has<TagIngredient>()) continue;
            available.Add(item);
        }

        if (available.Count == 0) return BehResult.Stop;

        Entity[] ingredients = ArtifactIngredientPlanner.Select(pObject, available);
        var result = ArtifactComposer.Compose(ingredients);
        Entity crafting_artifact = SpecialItemUtils
            .StartBuild(result.Shape, World.world.getCurWorldTime(), pObject.getName())
            .AddComponent(new CraftingArtifact
            {
                progress = 0,
            })
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
            .Build();
        ae.AddSpecialItem(crafting_artifact);
        foreach (Entity ing in ingredients)
        {
            crafting_artifact.AddRelation(new CraftOccupyingRelation { item = ing });
            ing.AddTag<TagConsumed>();
        }

        return BehResult.Continue;
    }
}
