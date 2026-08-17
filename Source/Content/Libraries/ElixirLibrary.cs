using System;
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

    /// <summary>提交已验证的丹方定义；同签名丹方复用现有动态资产。</summary>
    public ElixirAsset GetOrAddDefinition(ElixirRecipeDefinition definition, out bool created)
    {
        if (definition == null || string.IsNullOrEmpty(definition.AssetId) ||
            definition.Ingredients == null || definition.Ingredients.Length == 0)
            throw new ArgumentException("丹方定义无效", nameof(definition));
        ElixirAsset existing = get(definition.AssetId);
        if (existing != null)
        {
            created = false;
            return existing;
        }

        var asset = new ElixirAsset
        {
            id = definition.AssetId,
            ingredients = definition.Ingredients,
            recipe_context = definition.Context,
            composition_seed = definition.Seed
        };
        try
        {
            ElixirEffectGenerator.GenerateElixirActions(asset);
            AddDynamic(asset);
            created = true;
            return asset;
        }
        catch
        {
            ModClass.L.StatusEffectLibrary.RemoveAll(new[] { asset.id });
            throw;
        }
    }

    /// <summary>从材料构造并提交运行时丹方。</summary>
    public ElixirAsset NewElixir(Entity[] ingredients)
    {
        ElixirRecipeDefinition definition = ElixirRecipeBuilder.Build(ingredients);
        return GetOrAddDefinition(definition, out _);
    }

    protected override void OnRemoveDynamic(ElixirAsset asset)
    {
        ModClass.L.StatusEffectLibrary.RemoveAll(new[] { asset.id });
        base.OnRemoveDynamic(asset);
    }
}
