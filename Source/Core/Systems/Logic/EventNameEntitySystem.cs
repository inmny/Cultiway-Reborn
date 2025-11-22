using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.Systems.Logic;

public class EventNameEntitySystem : QuerySystem<EventNameEntity>
{
    protected override void OnUpdate()
    {
        Query.ForEachEntity(((ref EventNameEntity eventNameEntity, Entity entity) =>
        {
            if (eventNameEntity.Target.IsNull)
            {
                CommandBuffer.DeleteEntity(entity.Id);
                return;
            }

            if (string.IsNullOrEmpty(eventNameEntity.Name))
            {
                return;
            }
            if (eventNameEntity.Target.HasName)
            {
                eventNameEntity.Target.GetComponent<EntityName>().value = eventNameEntity.Name;
            }
            else
            {
                eventNameEntity.Target.AddComponent(new EntityName(eventNameEntity.Name));
            }
            CommandBuffer.DeleteEntity(entity.Id);
        }));
    }
}