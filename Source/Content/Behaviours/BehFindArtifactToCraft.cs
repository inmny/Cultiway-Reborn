using System.Collections.Generic;
using System.Linq;
using ai.behaviours;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Behaviours;

public class BehFindArtifactToCraft : BehCityActor
{
    /// <summary>
    /// 第一阶段每次炼器需要的材料数。
    /// </summary>
    private const int IngredientCount = 3;

    public override BehResult execute(Actor pObject)
    {
        // 已经在炼制则不再选器形
        ActorExtend ae = pObject.GetExtend();
        if (ae.HasItem<CraftingArtifact>()) return BehResult.Continue;

        // 扫描背包未占用材料，按倾向归类到候选器形
        var available = new List<Entity>();
        var shape_scores = new Dictionary<ArtifactShapeAsset, int>();
        foreach (Entity item in ae.GetItems())
        {
            if (item.Tags.HasAny(Tags.Get<TagConsumed, TagOccupied>())) continue;
            available.Add(item);

            ArtifactShapeAsset affinity = ResolveAffinity(item);
            if (affinity == null) continue;
            shape_scores[affinity] = (shape_scores.TryGetValue(affinity, out var s) ? s : 0) + 1;
        }

        if (available.Count < IngredientCount) return BehResult.Stop;

        // 取主导器形；无明确倾向时默认剑
        ArtifactShapeAsset shape = ItemShapes.Sword;
        int best = 0;
        foreach (var kv in shape_scores)
        {
            if (kv.Value > best)
            {
                best = kv.Value;
                shape = kv.Key;
            }
        }

        // 占用前 IngredientCount 个可用材料
        var ingredients = available.Take(IngredientCount).ToArray();
        Entity crafting_artifact = SpecialItemUtils
            .StartBuild(shape, World.world.getCurWorldTime(), pObject.getName())
            .AddComponent(new CraftingArtifact
            {
                shape = shape,
            })
            .AddComponent(new ItemIconData())
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

    /// <summary>
    /// 按材料的 ItemShape 后缀查表，返回该材料倾向的器形。
    /// </summary>
    private static ArtifactShapeAsset ResolveAffinity(Entity item)
    {
        if (!item.TryGetComponent<ItemShape>(out var ishape)) return null;
        // shape_id 形如 "Cultiway.ItemShape.Bone"，取最后一段
        var id = ishape.shape_id;
        var suffix = id.Substring(id.LastIndexOf('.') + 1);
        return suffix switch
        {
            "Bone" or "Claw" or "Tooth" or "Horn" or "Feather" or "Wing"     => ItemShapes.Sword,
            "Crystal" or "Stone" or "Shell"                                   => ItemShapes.Seal,
            "Fur" or "Silk" or "Bamboo" or "Herb" or "Flower"                 => ItemShapes.Robe,
            "Eye" or "Blood" or "Liquid"                                      => ItemShapes.Mirror,
            "Wood" or "Root" or "Mushroom" or "Fruit" or "Lotus"             => ItemShapes.Ding,
            _ => null,
        };
    }
}
