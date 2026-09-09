using Cultiway.Content.CreatureCompositions.Combat;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.Systems.Logic;

/// <summary>每帧在主循环安全时机消费器官后果队列。</summary>
public sealed class CreatureConsequenceSystem : BaseSystem
{
    /// <summary>队列本身已经是数量受限的批处理，这里直接逐帧清空。</summary>
    protected override void OnUpdateGroup()
    {
        CreatureConsequenceQueue.Flush();
    }
}
