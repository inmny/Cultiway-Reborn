using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Libraries;

public class ElixirLibrary : DynamicAssetLibrary<ElixirAsset>
{
    /// <summary>初始化空丹方库，并注册角色按需自动服用属性状态类丹药的钩子。</summary>
    public override void init()
    {
        base.init();
        ActorExtend.RegisterActionOnGetStats((actor, statId) =>
        {
            var items = actor.GetItems().Where(item =>
                item.HasComponent<Elixir>() && item.Tags.Has<TagElixirStatusGain>());
            Entity elixirEntity = default;
            foreach (var item in items)
            {
                if (item.HasComponent<StatusOverwriteStats>())
                {
                    if (item.GetComponent<StatusOverwriteStats>().stats[statId] > 0f)
                    {
                        elixirEntity = item;
                        break;
                    }
                }
                else if (item.HasComponent<StatusComponent>() &&
                         item.GetComponent<StatusComponent>().Type.stats[statId] > 0f)
                {
                    elixirEntity = item;
                    break;
                }
            }

            if (elixirEntity.IsNull || !actor.TryConsumeElixir(elixirEntity)) return;
            actor.Base.setStatsDirty();
            actor.Base.updateStats();
        });
    }

    /// <summary>按材料语义构造运行时丹方；规范签名相同的配方复用同一资产。</summary>
    public ElixirAsset NewElixir(Entity[] ingredients)
    {
        var definition = ElixirRecipeBuilder.Build(ingredients);
        var existing = get(definition.AssetId);
        if (existing != null) return existing;

        var asset = new ElixirAsset
        {
            id = definition.AssetId,
            ingredients = definition.Ingredients,
            recipe_context = definition.Context,
            composition_seed = definition.Seed
        };
        ElixirEffectGenerator.GenerateElixirActions(asset);
        AddDynamic(asset);
        return asset;
    }
}
