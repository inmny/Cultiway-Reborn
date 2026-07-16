using Cultiway.Core;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Events;
/// <summary>任意生产行为写入最终品级或产出数量前发布的可变上下文。</summary>
public sealed class ArtifactProductionResultEvent
{
    public ActorExtend Producer { get; }
    public string Process { get; }
    public object Recipe { get; }
    public Entity Product { get; }
    public int QualityBonus { get; set; }
    public float YieldMultiplier { get; set; } = 1f;

    public ArtifactProductionResultEvent(
        ActorExtend producer,
        string process,
        object recipe,
        Entity product)
    {
        Producer = producer;
        Process = process;
        Recipe = recipe;
        Product = product;
    }
}
