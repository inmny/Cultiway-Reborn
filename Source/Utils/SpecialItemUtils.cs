using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Utils;

public static class SpecialItemUtils
{
    public static Builder StartBuild(string shape_id)
    {
        return new Builder(shape_id);
    }

    public class Builder
    {
        private readonly Entity entity;

        internal Builder(string shape_id)
        {
            entity = ModClass.I.ActorExtendManager.World.CreateEntity(new SpecialItem(), new ItemShape(shape_id));
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