using Cultiway.Core.Components;
using Cultiway.Core.EventSystem.Events;
using Friflo.Engine.ECS;

namespace Cultiway.Core.EventSystem.Systems;

/// <summary>
/// 处理名称生成完成后的同步逻辑。
/// </summary>
public class EntityNameGeneratedEventSystem : GenericEventSystem<EntityNameGeneratedEvent>
{
    protected override void HandleEvent(EntityNameGeneratedEvent evt)
    {
        if (evt.Target.IsNull || string.IsNullOrEmpty(evt.Name))
        {
            return;
        }

        if (evt.Target.HasName)
        {
            evt.Target.GetComponent<EntityName>().value = evt.Name;
        }
        else
        {
            evt.Target.AddComponent(new EntityName(evt.Name));
        }
    }
}
