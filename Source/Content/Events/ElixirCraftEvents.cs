using Cultiway.Content.Libraries;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Events;

/// <summary>
/// 炼丹行为推进一次工序前发布的可变上下文。
/// </summary>
public sealed class ElixirCraftStepEvent
{
    public ElixirAsset Recipe { get; }
    public Entity Product { get; }
    public int ProgressGain { get; set; } = 1;
    public float Duration { get; set; }

    public ElixirCraftStepEvent(ElixirAsset recipe, Entity product, float duration)
    {
        Recipe = recipe;
        Product = product;
        Duration = duration;
    }
}

/// <summary>
/// 丹药固有效果完成、最终品级写入前发布的可变上下文。
/// </summary>
public sealed class ElixirCraftResultEvent
{
    public ElixirAsset Recipe { get; }
    public Entity Product { get; }
    public int QualityBonus { get; set; }

    public ElixirCraftResultEvent(ElixirAsset recipe, Entity product)
    {
        Recipe = recipe;
        Product = product;
    }
}
