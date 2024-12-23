using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Utils;

public static class SpecialItemUtils
{
    public static Builder StartBuild(string shape_id, double creation_time, string creator = "")
    {
        return new Builder(shape_id, creator, creation_time);
    }

    public class Builder
    {
        private readonly Entity entity;

        internal Builder(string shape_id, string creator, double creation_time)
        {
            entity = ModClass.I.ActorExtendManager.World.CreateEntity(new SpecialItem(), new ItemShape(shape_id),
                new ItemCreation
                {
                    created_time = creation_time,
                    creator = creator
                });
            entity.GetComponent<SpecialItem>().self = entity;
        }

        public Builder AddComponent<T>(T component) where T : struct, IComponent
        {
            entity.AddComponent(component);
            return this;
        }

        public Entity Build()
        {
            return entity;
        }
    }
}