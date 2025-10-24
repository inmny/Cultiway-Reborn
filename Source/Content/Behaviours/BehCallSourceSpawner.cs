using ai.behaviours;
using Cultiway.Core.BuildingComponents;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours;

public class BehCallSourceSpawner : BehaviourActionActor
{
    public override BehResult execute(Actor pObject)
    {
        var source_spawner_asset_id = pObject.GetSourceSpawnerAssetId();
        if (string.IsNullOrEmpty(source_spawner_asset_id)) return BehResult.Continue;
        var spawner = World.world.buildings.addBuilding(source_spawner_asset_id, pObject.beh_tile_target);
        foreach (var component in spawner.components_list)
        {
            if (component is AdvancedUnitSpawner advanced_unit_spawner)
            {
                advanced_unit_spawner.InsertUnit(pObject);
                break;
            }
        }
        pObject.SetSourceSpawnerId(spawner.id);
        return base.execute(pObject);
    }
}