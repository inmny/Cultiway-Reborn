using Cultiway.Core.Components;
using Cultiway.Utils;
using Friflo.Engine.ECS;

namespace Cultiway.Core.Libraries;

public class ForceTypeAsset : Asset
{
    private Entity _prefab;
    public Builder StartBuild()
    {
        return new Builder(this);
    }
    public class Builder
    {
        private ForceTypeAsset _asset;
        internal Builder(ForceTypeAsset asset)
        {
            _asset = asset;
            _asset._prefab = ModClass.I.W.CreateEntity(new ForceComponent()
            {

            }, Tags.Get<TagPrefab>());
        }

        public Builder WithZones<TLayer>() where TLayer : struct, IForceZoneLayer
        {
            _asset._prefab.AddComponent(new ForceZones()
            {
                Zones = new ()
            });
            _asset._prefab.AddTag<TLayer>();
            return this;
        }
    }
}