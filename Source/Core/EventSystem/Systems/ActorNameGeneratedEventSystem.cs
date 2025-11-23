using Cultiway.Core.EventSystem.Events;

namespace Cultiway.Core.EventSystem.Systems;

/// <summary>
/// 处理名称生成完成后的同步逻辑。
/// </summary>
public class ActorNameGeneratedEventSystem : GenericEventSystem<ActorNameGeneratedEvent>
{
    protected override void HandleEvent(ActorNameGeneratedEvent evt)
    {
        if (evt.ID == 0 || string.IsNullOrEmpty(evt.Name))
        {
            return;
        }
        var actor = World.world.units.get(evt.ID);
        if (actor == null || actor.isRekt()) return;
        actor.setName(evt.Name);
    }
}
