using Cultiway.Core;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Events;
/// <summary>
/// 任意生产行为推进一次工序前发布的可变上下文。
/// Recipe 由具体生产系统解释，法器能力应优先依据 Process 和语义标签判断。
/// </summary>
public sealed class ArtifactProductionStepEvent
{
    public ActorExtend Producer { get; }
    public string Process { get; }
    public object Recipe { get; }
    public Entity Product { get; }
    public int ProgressGain { get; set; } = 1;
    public float Duration { get; set; }

    public ArtifactProductionStepEvent(
        ActorExtend producer,
        string process,
        object recipe,
        Entity product,
        float duration)
    {
        Producer = producer;
        Process = process;
        Recipe = recipe;
        Product = product;
        Duration = duration;
    }
}
