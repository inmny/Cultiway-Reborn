using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core.Libraries;

public class StatusEffectAsset : Asset
{
    public BaseStats stats = new();
    private Entity _prefab;
    private EntityStore _world;
    public StatusEffectAsset()
    {
        
    }

    public static Builder StartBuild(string id)
    {
        return new Builder(id);
    }

    public class Builder
    {
        private StatusEffectAsset _under_build;
        public Builder(string id)
        {
            _under_build = new StatusEffectAsset()
            {
                id = id
            };
            _under_build._world = ModClass.I.W;
            _under_build._prefab = _under_build._world.CreateEntity(new StatusComponent()
            {
                id = id
            }, new AliveTimer(), Tags.Get<TagPrefab>());
        }

        public StatusEffectAsset Build()
        {
            ModClass.L.StatusEffectLibrary.add(_under_build);
            return _under_build;
        }
    }
}