using Cultiway.Content.Components;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.Systems.Logic;

/// <summary>
/// 在模拟时钟上维护突破表现寿命，避免渲染系统修改逻辑 ECS 状态。
/// </summary>
public sealed class BreakthroughVisualLifetimeSystem :
    QuerySystem<RealmVisual, XianBreakthroughState>
{
    public BreakthroughVisualLifetimeSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());
    }

    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;
        Query.ForEachEntity((
            ref RealmVisual visual,
            ref XianBreakthroughState state,
            Entity entity) =>
        {
            state.visual_timer -= delta;
            if (state.visual_timer > 0f)
            {
                return;
            }

            state.visual_timer = 0f;
            if (visual.visual_state == RealmVisual.VisualStateBreakthrough)
            {
                visual.visual_state = RealmVisual.VisualStateDefault;
            }

            CommandBuffer.RemoveComponent<XianBreakthroughState>(entity.Id);
        });
        CommandBuffer.Playback();
    }
}
