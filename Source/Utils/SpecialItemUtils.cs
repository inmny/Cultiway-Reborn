using Cultiway.Const;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Utils;

public static class SpecialItemUtils
{
    public static Builder StartBuild(string shape_id, double creation_time, string creator = "", float year_limit = 99,
        string creator_asset_id = "")
    {
        return new Builder(shape_id, creator, creator_asset_id, creation_time, year_limit);
    }

    public class Builder
    {
        private readonly Entity entity;

        internal Builder(string shape_id, string creator, string creator_asset_id, double creation_time, float year_limit)
        {
            if (year_limit < 1e6)
            {
                entity = ModClass.I.ActorExtendManager.World.CreateEntity(new SpecialItem(), new ItemShape(shape_id),
                    new ItemIconData()
                    {
                    },
                    new ItemCreation
                    {
                        created_time = creation_time,
                        creator = creator,
                        creator_asset_id = creator_asset_id
                    }, new AliveTimer(), new AliveTimeLimit()
                    {
                        value = year_limit * TimeScales.SecPerYear
                    });
            }
            else
            {
                entity = ModClass.I.ActorExtendManager.World.CreateEntity(new SpecialItem(), new ItemShape(shape_id),
                    new ItemIconData()
                    {
                    },
                    new ItemCreation
                    {
                        created_time = creation_time,
                        creator = creator,
                        creator_asset_id = creator_asset_id
                    });
            }
            entity.GetComponent<SpecialItem>().self = entity;
        }

        public Builder AddComponent<T>(T component) where T : struct, IComponent
        {
            if (entity.HasComponent<T>())
            {
                entity.Set(component);
                return this;
            }
            entity.AddComponent(component);
            return this;
        }

        public Builder AddTag<T>() where T : struct, ITag
        {
            entity.AddTag<T>();
            return this;
        }

        public Entity Build()
        {
            return entity;
        }
    }
}
